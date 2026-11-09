using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço de gerenciamento de preferências da aplicação.
/// </summary>
public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    event Action<AppSettings>? SettingsChanged;
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

