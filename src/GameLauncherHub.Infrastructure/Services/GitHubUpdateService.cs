using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameLauncherHub.Infrastructure.Services;

/// <summary>
/// Implementação profissional do serviço de atualização do GameLauncher Hub utilizando a API pública do GitHub Releases.
/// Suporta verificação assíncrona, download seguro do instalador oficial com barra de progresso
/// e inicialização do instalador com encerramento controlado da aplicação.
/// </summary>
public class GitHubUpdateService : IAppUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly IDownloadManager _downloadManager;
    private readonly AuthenticodeVerifier _authenticodeVerifier;
    private readonly ILogger<GitHubUpdateService>? _logger;

    public event Action? RequestApplicationShutdown;

    public GitHubUpdateService(
        HttpClient? httpClient = null,
        ISettingsService? settingsService = null,
        IDownloadManager? downloadManager = null,
        AuthenticodeVerifier? authenticodeVerifier = null,
        ILogger<GitHubUpdateService>? logger = null)
    {
        _logger = logger;
        _settingsService = settingsService ?? new SettingsService(NullLogger<SettingsService>.Instance);
        _downloadManager = downloadManager ?? new Downloads.HttpDownloadManager();
        _authenticodeVerifier = authenticodeVerifier ?? new AuthenticodeVerifier();

        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(15),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <inheritdoc />
    public Version GetCurrentVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            // 1. Tenta obter do InformationalVersion (ex: "0.11.10b3", "11.10b3")
            var infoVer = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                var clean = infoVer.Split('+')[0].Trim();
                if (TryParseSemanticVersion(clean, out var parsedInfo))
                {
                    return parsedInfo;
                }
            }

            // 2. Tenta obter do Assembly Name Version (ex: 0.11.10.3)
            var version = assembly.GetName().Version;
            if (version != null && (version.Major > 0 || version.Minor > 0 || version.Build > 0 || version.Revision > 0))
            {
                return NormalizeVersion(version);
            }

            // 3. Fallback no FileVersionInfo do executável
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(processPath);
                if (!string.IsNullOrEmpty(fvi.ProductVersion))
                {
                    var cleanVersion = fvi.ProductVersion.Split('+')[0].Trim();
                    if (TryParseSemanticVersion(cleanVersion, out var parsed))
                    {
                        return parsed;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao obter versão do assembly.");
        }

        // Versão de fallback alinhada ao projeto
        return new Version(0, 11, 11, 1);
    }

    /// <inheritdoc />
    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();
        var updateInfo = new AppUpdateInfo
        {
            CurrentVersion = currentVersion,
            LatestVersion = currentVersion,
            IsUpdateAvailable = false
        };

        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            var owner = string.IsNullOrWhiteSpace(settings.GitHubRepoOwner) ? "ademirj93" : settings.GitHubRepoOwner.Trim();
            var repo = string.IsNullOrWhiteSpace(settings.GitHubRepoName) ? "GameLauncherHub" : settings.GitHubRepoName.Trim();

            _logger?.LogInformation("Verificando atualizações no GitHub para {Owner}/{Repo}...", owner, repo);

            var releaseJson = await FetchLatestReleaseJsonAsync(owner, repo, cancellationToken);
            if (string.IsNullOrWhiteSpace(releaseJson) && owner.Equals("ademirj93", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback secundário caso o repositório esteja registrado sob ademirjunior
                _logger?.LogInformation("Tentando repositório fallback: ademirjunior/{Repo}...", repo);
                releaseJson = await FetchLatestReleaseJsonAsync("ademirjunior", repo, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(releaseJson))
            {
                _logger?.LogInformation("Nenhuma release pública localizada no repositório GitHub {Owner}/{Repo}.", owner, repo);
                return updateInfo;
            }

            using var doc = JsonDocument.Parse(releaseJson);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
            var releaseName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;
            var htmlUrl = root.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? string.Empty : string.Empty;
            var isPrerelease = root.TryGetProperty("prerelease", out var preProp) && preProp.GetBoolean();
            DateTimeOffset? publishedAt = null;
            if (root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTimeOffset(out var dt))
            {
                publishedAt = dt;
            }

            updateInfo.TagName = tagName;
            updateInfo.ReleaseName = releaseName;
            updateInfo.ReleaseNotes = body;
            updateInfo.HtmlUrl = htmlUrl;
            updateInfo.IsPrerelease = isPrerelease;
            updateInfo.PublishedAt = publishedAt;

            if (TryParseSemanticVersion(tagName, out var latestVersion))
            {
                updateInfo.LatestVersion = latestVersion;
                updateInfo.IsUpdateAvailable = latestVersion > currentVersion;
            }

            // Localizar o melhor asset de instalação (.exe / Setup)
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                var candidateAssets = new List<(string Name, string Url, long Size)>();

                foreach (var asset in assetsProp.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var aName) ? aName.GetString() : null;
                    var downloadUrl = asset.TryGetProperty("browser_download_url", out var aUrl) ? aUrl.GetString() : null;
                    var size = asset.TryGetProperty("size", out var aSize) && aSize.TryGetInt64(out var s) ? s : 0L;

                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        candidateAssets.Add((name, downloadUrl, size));
                    }
                }

                // Priorização: 1º setup/installer .exe, 2º qualquer .exe
                var bestAsset = candidateAssets
                    .Where(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(a => a.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("installer", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(a => a.Name.Contains("GameLauncherHub", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(bestAsset.Url))
                {
                    updateInfo.InstallerFileName = bestAsset.Name;
                    updateInfo.InstallerDownloadUrl = bestAsset.Url;
                    updateInfo.InstallerSizeBytes = bestAsset.Size;
                }
            }

            _logger?.LogInformation("Verificação concluída: Versão Atual={Current}, Mais Recente={Latest}, Atualização={Available}",
                updateInfo.CurrentVersion, updateInfo.LatestVersion, updateInfo.IsUpdateAvailable);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Falha de rede ao consultar releases do GitHub.");
            updateInfo.ErrorMessage = "Não foi possível conectar ao servidor do GitHub para verificar atualizações.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro inesperado durante a verificação de atualizações.");
            updateInfo.ErrorMessage = $"Erro ao verificar atualizações: {ex.Message}";
        }

        return updateInfo;
    }

    /// <inheritdoc />
    public async Task<string> DownloadUpdateAsync(
        AppUpdateInfo updateInfo,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateInfo.InstallerDownloadUrl))
        {
            throw new InvalidOperationException("A release informada não contém um instalador executável disponível para download.");
        }

        if (!Uri.TryCreate(updateInfo.InstallerDownloadUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("O download da atualização deve utilizar obrigatoriamente uma conexão segura HTTPS.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "GameLauncherHub_Updates");
        Directory.CreateDirectory(tempDirectory);

        var fileName = !string.IsNullOrWhiteSpace(updateInfo.InstallerFileName)
            ? updateInfo.InstallerFileName
            : $"GameLauncherHub_Setup_v{updateInfo.LatestVersion}.exe";

        var targetFilePath = Path.Combine(tempDirectory, fileName);

        // Se já existe um download completo correspondente, remove para garantir a integridade do novo arquivo
        if (File.Exists(targetFilePath))
        {
            try { File.Delete(targetFilePath); } catch { /* Ignore */ }
        }

        _logger?.LogInformation("Baixando atualização de {Url} para {Target}...", updateInfo.InstallerDownloadUrl, targetFilePath);
        progress.Report(new DownloadProgressInfo(0, 0, updateInfo.InstallerSizeBytes, "0 KB/s", "Iniciando download da atualização..."));

        var downloadedPath = await _downloadManager.DownloadInstallerAsync(
            updateInfo.InstallerDownloadUrl,
            targetFilePath,
            progress,
            cancellationToken
        );

        if (!File.Exists(downloadedPath))
        {
            throw new FileNotFoundException("O download foi concluído, mas o arquivo de instalação não foi localizado.", downloadedPath);
        }

        // Validação de Integridade de Executável Windows (Cabeçalho PE - MZ)
        ValidateExecutableHeader(downloadedPath);

        // Validação Authenticode opcional para instaladores oficiais assinados
        try
        {
            bool isSigned = _authenticodeVerifier.VerifyAuthenticodeSignature(downloadedPath, out var publisher, out var failureDetails);
            if (isSigned)
            {
                _logger?.LogInformation("Instalador verificado via Authenticode. Editor: {Publisher}", publisher);
            }
            else if (!string.IsNullOrEmpty(failureDetails))
            {
                _logger?.LogWarning("Aviso de verificação Authenticode no instalador baixado: {Details}", failureDetails);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Verificação de assinatura digital concluída.");
        }

        _logger?.LogInformation("Download da atualização concluído com sucesso: {Path}", downloadedPath);
        return downloadedPath;
    }

    /// <inheritdoc />
    public void LaunchInstallerAndExit(string installerPath, bool silent = false)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Arquivo de instalação não encontrado para execução.", installerPath);
        }

        _logger?.LogInformation("Disparando executável de atualização: {Path} (Silent={Silent})", installerPath, silent);

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = silent ? "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS" : string.Empty,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty
        };

        Process.Start(startInfo);

        // Notifica inscritos para encerramento gracioso da aplicação
        _logger?.LogInformation("Solicitando encerramento do GameLauncher Hub para permitir a instalação...");

        if (RequestApplicationShutdown != null)
        {
            RequestApplicationShutdown.Invoke();
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private async Task<string?> FetchLatestReleaseJsonAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", $"GameLauncherHub-Updater/{GetCurrentVersion()}");
        request.Headers.Add("Accept", "application/vnd.github+json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Normaliza uma instância de Version para o padrão do GameLauncher Hub (série 0.X.Y).
    /// Assegura compatibilidade entre notações como 11.10.3.0 e 0.11.10.3.
    /// </summary>
    public static Version NormalizeVersion(Version version)
    {
        if (version == null) return new Version(0, 0, 0, 0);

        if (version.Major >= 10 && version.Minor <= 99)
        {
            var rev = version.Build >= 0 ? version.Build : Math.Max(0, version.Revision);
            return new Version(0, version.Major, version.Minor, rev);
        }

        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
    }

    /// <summary>
    /// Analisa strings no formato SemVer ou tags git (ex: "v11.10b3", "v0.11.10b3", "0.10.11", "release-1.0.0") em um objeto System.Version.
    /// Garante que "v11.10b3", "11.10b3", "v0.11.10b3" e "0.11.10.3" mapeiem de forma equivalente para (0, 11, 10, 3).
    /// </summary>
    public static bool TryParseSemanticVersion(string rawTag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(rawTag))
            return false;

        var clean = rawTag.Trim();
        if (clean.StartsWith("release-", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring("release-".Length).Trim();
        }
        clean = clean.TrimStart('v', 'V');

        // Regex para capturar números e sufixo de pré-lançamento/beta
        var match = Regex.Match(clean, @"^(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:[.\-_]?([a-zA-Z]+)(\d*))?");
        if (!match.Success)
            return false;

        var p1 = int.Parse(match.Groups[1].Value);
        var p2 = int.Parse(match.Groups[2].Value);
        int? p3 = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : null;
        int? p4 = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : null;

        int beta = 0;
        if (match.Groups[6].Success && !string.IsNullOrEmpty(match.Groups[6].Value))
        {
            int.TryParse(match.Groups[6].Value, out beta);
        }

        // Caso 1: Dois segmentos principais (ex: "11.10b3" ou "11.10" ou "1.0")
        if (!p3.HasValue)
        {
            if (p1 == 0)
            {
                version = new Version(0, p2, 0, beta);
                return true;
            }
            if (p1 >= 10)
            {
                // Notação abreviada da série 0.X (ex: 11.10b3 -> 0.11.10.3)
                version = new Version(0, p1, p2, beta);
                return true;
            }

            version = new Version(p1, p2, beta, 0);
            return true;
        }

        // Caso 2: Três ou mais segmentos
        if (p1 == 0)
        {
            int rev = beta > 0 ? beta : (p4 ?? 0);
            version = new Version(0, p2, p3.Value, rev);
            return true;
        }

        int standardRev = beta > 0 ? beta : (p4 ?? 0);
        version = new Version(p1, p2, p3.Value, standardRev);
        return true;
    }

    private static void ValidateExecutableHeader(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length < 64)
        {
            throw new InvalidDataException("O arquivo baixado possui tamanho insuficiente para ser um executável válido do Windows.");
        }

        var buffer = new byte[2];
        var bytesRead = stream.Read(buffer, 0, 2);
        if (bytesRead < 2 || buffer[0] != 0x4D || buffer[1] != 0x5A) // 'M' e 'Z'
        {
            throw new InvalidDataException("O arquivo baixado não possui um cabeçalho executável PE válido do Windows (MZ).");
        }
    }
}
