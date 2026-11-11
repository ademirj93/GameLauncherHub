using System.Net.Http;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Downloads;

/// <summary>
/// Resolve e pré-valida URLs oficiais de download antes do início da transferência.
/// Inspeciona cabeçalhos HTTP, trata redirecionamentos e previne downloads acidentais de páginas HTML (ex: Amazon Luna).
/// </summary>
public class LauncherDownloadResolver : ILauncherDownloadResolver
{
    private readonly HttpClient _httpClient;
    private readonly IOfficialDownloadCatalogService _catalogService;
    private readonly IInstallerSecurityService _securityService;
    private readonly ILogger<LauncherDownloadResolver>? _logger;

    public LauncherDownloadResolver(
        IOfficialDownloadCatalogService catalogService,
        IInstallerSecurityService securityService,
        HttpClient? httpClient = null,
        ILogger<LauncherDownloadResolver>? logger = null)
    {
        _catalogService = catalogService;
        _securityService = securityService;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<ResolvedDownloadInfo> ResolveAndValidateInstallerUrlAsync(
        LauncherPlatform platform,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Identifica a URL canônica a ser testada
            string? downloadUrl = platform.InstallerDirectUrl;
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                var source = _catalogService.GetSourceByPlatformId(platform.Id);
                downloadUrl = source?.DirectInstallerUrl;
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return new ResolvedDownloadInfo(
                    IsValid: false,
                    ResolvedUrl: null,
                    ContentType: null,
                    ContentLength: null,
                    ErrorMessage: $"A plataforma {platform.Name} não possui uma URL direta de download oficial configurada."
                );
            }

            // 2. Validação inicial de segurança (HTTPS e Domínio)
            if (!_securityService.ValidateDownloadUrl(downloadUrl, platform, out var securityError))
            {
                return new ResolvedDownloadInfo(
                    IsValid: false,
                    ResolvedUrl: downloadUrl,
                    ContentType: null,
                    ContentLength: null,
                    ErrorMessage: securityError ?? "URL rejeitada pela política de segurança."
                );
            }

            // Resolução dinâmica de releases no GitHub caso o asset possua numeração de versão (ex: Heroic Games Launcher)
            if (downloadUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase) && downloadUrl.Contains("/releases/", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedGithubAsset = await TryResolveGitHubLatestReleaseAssetAsync(downloadUrl, cancellationToken);
                if (!string.IsNullOrWhiteSpace(resolvedGithubAsset))
                {
                    downloadUrl = resolvedGithubAsset;
                }
            }

            _logger?.LogInformation("Resolvendo e inspecionando URL de instalador para {Platform}: {Url}", platform.Name, downloadUrl);

            // 3. Inspeção HTTP via ResponseHeadersRead
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.Add("User-Agent", "GameLauncherHub/0.11.11b1 (+https://github.com/ademirjunior/GameLauncherHub)");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var finalUri = response.RequestMessage?.RequestUri ?? new Uri(downloadUrl);
            var finalHost = finalUri.Host.ToLowerInvariant();

            // 4. Validação do domínio de destino final após eventuais redirecionamentos
            if (!_securityService.ValidateDownloadUrl(finalUri.ToString(), platform, out var redirectSecurityError))
            {
                return new ResolvedDownloadInfo(
                    IsValid: false,
                    ResolvedUrl: finalUri.ToString(),
                    ContentType: response.Content.Headers.ContentType?.MediaType,
                    ContentLength: response.Content.Headers.ContentLength,
                    ErrorMessage: $"Redirecionamento inseguro para host não autorizado: {finalHost} ({redirectSecurityError})"
                );
            }

            // 5. Validação de status HTTP
            if (!response.IsSuccessStatusCode)
            {
                return new ResolvedDownloadInfo(
                    IsValid: false,
                    ResolvedUrl: finalUri.ToString(),
                    ContentType: response.Content.Headers.ContentType?.MediaType,
                    ContentLength: null,
                    ErrorMessage: $"Servidor oficial retornou código de erro HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."
                );
            }

            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            var contentLength = response.Content.Headers.ContentLength;

            // 6. Prevenção de páginas HTML (especificamente redirecionamentos para Amazon Luna ou portais de login)
            if (contentType.Contains("text/html") || contentType.Contains("text/plain"))
            {
                _logger?.LogWarning("Resposta com Content-Type HTML detectada para {Platform} em {Url}. Possível página promocional ou redirecionamento incorreto.", platform.Name, finalUri);

                return new ResolvedDownloadInfo(
                    IsValid: false,
                    ResolvedUrl: finalUri.ToString(),
                    ContentType: contentType,
                    ContentLength: contentLength,
                    ErrorMessage: $"O endereço oficial redirecionou para uma página Web ({contentType}) em vez do binário executável do instalador."
                );
            }

            _logger?.LogInformation("URL resolvida e validada com sucesso para {Platform}: {FinalUrl} (Tamanho: {Size} bytes, Tipo: {Type})",
                platform.Name, finalUri, contentLength, contentType);

            return new ResolvedDownloadInfo(
                IsValid: true,
                ResolvedUrl: finalUri.ToString(),
                ContentType: contentType,
                ContentLength: contentLength,
                ErrorMessage: null
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao resolver instalador oficial para {Platform}", platform.Name);
            return new ResolvedDownloadInfo(
                IsValid: false,
                ResolvedUrl: null,
                ContentType: null,
                ContentLength: null,
                ErrorMessage: $"Falha de conexão ao verificar o instalador oficial: {ex.Message}"
            );
        }
    }

    private async Task<string?> TryResolveGitHubLatestReleaseAssetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                var owner = segments[0];
                var repo = segments[1];
                var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

                using var apiReq = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                apiReq.Headers.Add("User-Agent", "GameLauncherHub/0.11.11b1 (+https://github.com/ademirjunior/GameLauncherHub)");

                using var apiResp = await _httpClient.SendAsync(apiReq, cancellationToken);
                if (apiResp.IsSuccessStatusCode)
                {
                    var json = await apiResp.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        string? bestUrl = null;
                        foreach (var asset in assets.EnumerateArray())
                        {
                            if (asset.TryGetProperty("name", out var nameProp) &&
                                asset.TryGetProperty("browser_download_url", out var urlProp))
                            {
                                var name = nameProp.GetString() ?? string.Empty;
                                var assetUrl = urlProp.GetString();
                                if (!string.IsNullOrWhiteSpace(assetUrl))
                                {
                                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".blockmap", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (name.Contains("x64", StringComparison.OrdinalIgnoreCase) && name.Contains("setup", StringComparison.OrdinalIgnoreCase))
                                        {
                                            return assetUrl;
                                        }
                                        bestUrl ??= assetUrl;
                                    }
                                }
                            }
                        }
                        if (bestUrl != null) return bestUrl;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Falha ao resolver asset do GitHub API para {Url}", url);
        }
        return null;
    }
}

