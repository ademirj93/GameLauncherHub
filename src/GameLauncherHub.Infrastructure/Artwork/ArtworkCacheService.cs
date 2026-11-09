using System.IO;
using System.Net.Http;
using GameLauncherHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Artwork;

/// <summary>
/// Serviço de gerenciamento de cache local para imagens e artes dos launchers obtidas do SteamGridDB.
/// Armazena os arquivos em %LOCALAPPDATA%/GameLauncherHub/Cache/SteamGridDB/.
/// </summary>
public class ArtworkCacheService : IArtworkCacheService
{
    private readonly ILogger<ArtworkCacheService>? _logger;
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;

    public string CacheDirectory => _cacheDirectory;

    public ArtworkCacheService(HttpClient? httpClient = null, ILogger<ArtworkCacheService>? logger = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(localAppData, "GameLauncherHub", "Cache", "SteamGridDB");

        try
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Falha ao criar diretório de cache de arte em {Path}", _cacheDirectory);
        }
    }

    public bool HasCachedArtwork(string platformId)
    {
        var path = GetCachedArtworkPath(platformId);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) && new FileInfo(path).Length > 0;
    }

    public string? GetCachedArtworkPath(string platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId)) return null;

        var cleanId = platformId.Trim().ToLowerInvariant();

        // Tenta extensões comuns suportadas pelo WPF
        string[] extensions = { ".png", ".ico", ".jpg", ".webp" };
        foreach (var ext in extensions)
        {
            var candidate = Path.Combine(_cacheDirectory, $"{cleanId}{ext}");
            if (File.Exists(candidate) && new FileInfo(candidate).Length > 0)
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task<string?> SaveArtworkAsync(
        string platformId,
        Stream imageStream,
        string extension = ".png",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cleanId = platformId.Trim().ToLowerInvariant();
            if (!extension.StartsWith(".")) extension = "." + extension;

            var targetPath = Path.Combine(_cacheDirectory, $"{cleanId}{extension}");
            var tempPath = targetPath + ".tmp";

            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await imageStream.CopyToAsync(fileStream, cancellationToken);
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
            _logger?.LogInformation("Arte do launcher {Id} armazenada no cache: {Path}", platformId, targetPath);

            return targetPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao salvar arte do launcher {Id} no cache.", platformId);
            return null;
        }
    }

    public async Task<string?> DownloadAndCacheArtworkAsync(
        string platformId,
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            _logger?.LogInformation("Baixando arte para {Id} de {Url}...", platformId, imageUrl);

            using var response = await _httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Falha ao baixar imagem de {Url}: HTTP {Status}", imageUrl, (int)response.StatusCode);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
            string ext = ".png";
            if (contentType.Contains("jpeg") || contentType.Contains("jpg")) ext = ".jpg";
            else if (contentType.Contains("webp")) ext = ".webp";
            else if (contentType.Contains("x-icon") || contentType.Contains("ico")) ext = ".ico";

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await SaveArtworkAsync(platformId, stream, ext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao baixar e armazenar arte para {Id} de {Url}", platformId, imageUrl);
            return null;
        }
    }

    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                var files = Directory.GetFiles(_cacheDirectory);
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                }
                _logger?.LogInformation("Cache de artes do SteamGridDB esvaziado com sucesso.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao limpar cache de artes.");
        }
    }
}

