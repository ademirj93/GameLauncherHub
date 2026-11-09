namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato de integração com a API oficial v2 do SteamGridDB para obtenção de ícones e artes dos launchers.
/// </summary>
public interface ISteamGridDBService
{
    Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<string?> SearchAndGetIconUrlAsync(string launcherName, string apiKey, CancellationToken cancellationToken = default);
}

