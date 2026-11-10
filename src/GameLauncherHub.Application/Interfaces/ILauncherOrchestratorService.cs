using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Application.Interfaces;

/// <summary>
/// Orquestrador central de operações da aplicação.
/// </summary>
public interface ILauncherOrchestratorService
{
    event EventHandler<LauncherPlatform>? PlatformStateChanged;

    Task<IReadOnlyList<LauncherPlatform>> LoadAndDetectAllAsync(CancellationToken cancellationToken = default);
    Task RefreshPlatformStatusAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
    Task ExecuteLauncherAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
    Task<string?> DownloadAndValidateInstallerAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
    Task DownloadAndInstallLauncherAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
    Task SetCustomExecutablePathAsync(string platformId, string executablePath, CancellationToken cancellationToken = default);
    Task AddCustomPlatformAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
}
