using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço de seleção e associação de artes dos launchers utilizando cache e SteamGridDB.
/// </summary>
public interface ILauncherArtworkService
{
    string? GetArtworkPath(string platformId);
    Task<string?> GetOrFetchArtworkAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
    Task RefreshAllArtworksAsync(IEnumerable<LauncherPlatform> platforms, CancellationToken cancellationToken = default);
}

