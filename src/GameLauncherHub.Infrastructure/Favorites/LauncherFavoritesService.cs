using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Favorites;

/// <summary>
/// Serviço de gerenciamento do sistema de favoritos dos launchers.
/// Persiste as preferências no arquivo settings.json do usuário de forma estável por ID da plataforma.
/// </summary>
public class LauncherFavoritesService : ILauncherFavoritesService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<LauncherFavoritesService>? _logger;
    private readonly HashSet<string> _favoriteIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isLoaded;

    public event Action? FavoritesChanged;

    public LauncherFavoritesService(ISettingsService settingsService, ILogger<LauncherFavoritesService>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;

        LoadFavoritesFromSettings(_settingsService.CurrentSettings, notify: false);
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        LoadFavoritesFromSettings(settings, notify: true);
    }

    private void LoadFavoritesFromSettings(AppSettings? settings, bool notify = false)
    {
        bool changed = false;
        lock (_favoriteIds)
        {
            var newIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (settings?.FavoriteLauncherIds != null)
            {
                foreach (var id in settings.FavoriteLauncherIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        newIds.Add(id.Trim().ToLowerInvariant());
                    }
                }
            }

            if (!_favoriteIds.SetEquals(newIds))
            {
                _favoriteIds.Clear();
                foreach (var id in newIds)
                {
                    _favoriteIds.Add(id);
                }
                changed = true;
            }
            _isLoaded = true;
        }

        if (notify && changed)
        {
            FavoritesChanged?.Invoke();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded) return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_isLoaded)
            {
                var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
                LoadFavoritesFromSettings(settings, notify: false);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public bool IsFavorite(string platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId)) return false;

        lock (_favoriteIds)
        {
            return _favoriteIds.Contains(platformId.Trim().ToLowerInvariant());
        }
    }

    public async Task<bool> ToggleFavoriteAsync(string platformId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platformId)) return false;

        await EnsureLoadedAsync(cancellationToken);

        bool newState;
        var cleanId = platformId.Trim().ToLowerInvariant();

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            lock (_favoriteIds)
            {
                if (_favoriteIds.Contains(cleanId))
                {
                    _favoriteIds.Remove(cleanId);
                    newState = false;
                }
                else
                {
                    _favoriteIds.Add(cleanId);
                    newState = true;
                }
            }

            var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
            lock (_favoriteIds)
            {
                settings.FavoriteLauncherIds = _favoriteIds.ToList();
            }
            await _settingsService.SaveSettingsAsync(settings, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }

        _logger?.LogInformation("Status de favorito do launcher {Id} alterado para: {Status}", platformId, newState);
        FavoritesChanged?.Invoke();

        return newState;
    }

    public async Task SetFavoriteAsync(string platformId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platformId)) return;

        await EnsureLoadedAsync(cancellationToken);

        var cleanId = platformId.Trim().ToLowerInvariant();

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            bool modified = false;
            lock (_favoriteIds)
            {
                if (isFavorite && !_favoriteIds.Contains(cleanId))
                {
                    _favoriteIds.Add(cleanId);
                    modified = true;
                }
                else if (!isFavorite && _favoriteIds.Contains(cleanId))
                {
                    _favoriteIds.Remove(cleanId);
                    modified = true;
                }
            }

            if (modified)
            {
                var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
                lock (_favoriteIds)
                {
                    settings.FavoriteLauncherIds = _favoriteIds.ToList();
                }
                await _settingsService.SaveSettingsAsync(settings, cancellationToken);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        FavoritesChanged?.Invoke();
    }

    public async Task<IReadOnlyList<string>> GetFavoriteIdsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        lock (_favoriteIds)
        {
            return _favoriteIds.ToList().AsReadOnly();
        }
    }
}
