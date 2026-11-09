using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Artwork;

/// <summary>
/// Orquestrador de artes que associa ícones do SteamGridDB aos launchers,
/// priorizando o cache local e consultando a API externa de forma segura e assíncrona.
/// </summary>
public class LauncherArtworkService : ILauncherArtworkService
{
    private readonly ISteamGridDBService _steamGridDbService;
    private readonly IArtworkCacheService _cacheService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<LauncherArtworkService>? _logger;

    public LauncherArtworkService(
        ISteamGridDBService steamGridDbService,
        IArtworkCacheService cacheService,
        ISettingsService settingsService,
        ILogger<LauncherArtworkService>? logger = null)
    {
        _steamGridDbService = steamGridDbService;
        _cacheService = cacheService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public string? GetArtworkPath(string platformId)
    {
        return _cacheService.GetCachedArtworkPath(platformId);
    }

    public async Task<string?> GetOrFetchArtworkAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        // 1. Verifica cache local existente
        var cached = _cacheService.GetCachedArtworkPath(platform.Id);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        // 2. Obtém as configurações e a chave protegida
        var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.SteamGridDbApiKeyProtected))
        {
            // Nenhuma chave configurada -> permanece com ícone vetorial local
            return null;
        }

        var apiKey = DataProtectionHelper.UnprotectString(settings.SteamGridDbApiKeyProtected);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        // 3. Consulta a API oficial do SteamGridDB
        try
        {
            var iconUrl = await _steamGridDbService.SearchAndGetIconUrlAsync(platform.Name, apiKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(iconUrl))
            {
                var downloadedPath = await _cacheService.DownloadAndCacheArtworkAsync(platform.Id, iconUrl, cancellationToken);
                return downloadedPath;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao obter arte do SteamGridDB para {Platform}", platform.Name);
        }

        return null;
    }

    public async Task RefreshAllArtworksAsync(IEnumerable<LauncherPlatform> platforms, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.SteamGridDbApiKeyProtected))
        {
            return;
        }

        var apiKey = DataProtectionHelper.UnprotectString(settings.SteamGridDbApiKeyProtected);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        foreach (var platform in platforms)
        {
            try
            {
                var iconUrl = await _steamGridDbService.SearchAndGetIconUrlAsync(platform.Name, apiKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(iconUrl))
                {
                    await _cacheService.DownloadAndCacheArtworkAsync(platform.Id, iconUrl, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Erro ao atualizar arte para {Name}", platform.Name);
            }
        }
    }
}

