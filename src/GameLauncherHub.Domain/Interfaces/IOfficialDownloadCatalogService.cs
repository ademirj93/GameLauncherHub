using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Catálogo centralizado de fontes de download oficiais e autorizadas.
/// </summary>
public interface IOfficialDownloadCatalogService
{
    IReadOnlyList<OfficialDownloadSource> GetAllSources();
    OfficialDownloadSource? GetSourceByPlatformId(string platformId);
    bool IsDomainAuthorized(string platformId, string domain);
}

