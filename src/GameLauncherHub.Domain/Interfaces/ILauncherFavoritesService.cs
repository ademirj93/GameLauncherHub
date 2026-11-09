namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato de gerenciamento do sistema de favoritos dos launchers.
/// </summary>
public interface ILauncherFavoritesService
{
    event Action? FavoritesChanged;
    bool IsFavorite(string platformId);
    Task<bool> ToggleFavoriteAsync(string platformId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetFavoriteIdsAsync(CancellationToken cancellationToken = default);
    Task SetFavoriteAsync(string platformId, bool isFavorite, CancellationToken cancellationToken = default);
}

