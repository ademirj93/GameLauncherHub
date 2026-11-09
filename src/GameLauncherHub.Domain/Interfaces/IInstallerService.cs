using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço responsável pela validação e execução de instaladores.
/// </summary>
public interface IInstallerService
{
    Task<InstallationResult> InstallAsync(
        LauncherPlatform platform,
        string installerFilePath,
        IProgress<string> statusProgress,
        CancellationToken cancellationToken = default
    );
}

public record InstallationResult(
    bool Success,
    string? Message
);

