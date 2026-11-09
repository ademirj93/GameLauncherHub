using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço responsável por executar o launcher com segurança.
/// </summary>
public interface ILauncherExecutionService
{
    Task<ExecutionResult> LaunchAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
}

public record ExecutionResult(
    bool Success,
    int? ProcessId,
    string? Message
);

