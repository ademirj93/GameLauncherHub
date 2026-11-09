using System.IO;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato para o gerenciador de cache local de artes e ícones obtidos do SteamGridDB.
/// </summary>
public interface IArtworkCacheService
{
    string CacheDirectory { get; }
    bool HasCachedArtwork(string platformId);
    string? GetCachedArtworkPath(string platformId);
    Task<string?> SaveArtworkAsync(string platformId, Stream imageStream, string extension = ".png", CancellationToken cancellationToken = default);
    Task<string?> DownloadAndCacheArtworkAsync(string platformId, string imageUrl, CancellationToken cancellationToken = default);
    void ClearCache();
}

