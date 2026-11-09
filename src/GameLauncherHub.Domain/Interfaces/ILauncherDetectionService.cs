using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço responsável por identificar se um launcher está instalado ou em execução no Windows.
/// </summary>
public interface ILauncherDetectionService
{
    Task<DetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
    Task<bool> IsRunningAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
}

public record DetectionResult(
    bool IsInstalled,
    string? ExecutablePath,
    string? InstalledVersion,
    bool IsRunning,
    string? Notes
);

