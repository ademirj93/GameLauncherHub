using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço de registro e consulta do histórico de operações.
/// </summary>
public interface IOperationHistoryService
{
    Task LogAsync(OperationLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperationLog>> GetRecentLogsAsync(int count = 50, CancellationToken cancellationToken = default);
    Task ClearHistoryAsync(CancellationToken cancellationToken = default);
}

