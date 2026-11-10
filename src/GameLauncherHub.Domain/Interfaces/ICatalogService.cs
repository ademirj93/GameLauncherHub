using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço responsável por carregar e gerenciar o catálogo de plataformas.
/// </summary>
public interface ICatalogService
{
    Task<IReadOnlyList<LauncherPlatform>> GetCatalogAsync(CancellationToken cancellationToken = default);
    LauncherPlatform? GetById(string id);
    Task AddOrUpdateCustomPlatformAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
    Task SetCustomExecutablePathAsync(string platformId, string executablePath, CancellationToken cancellationToken = default);
    Task RemoveCustomPlatformAsync(string platformId, CancellationToken cancellationToken = default);
}

