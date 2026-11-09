using GameLauncherHub.Domain.Enums;

namespace GameLauncherHub.Domain.Models;

/// <summary>
/// Registro de auditoria de operações realizadas pelo Hub.
/// </summary>
public class OperationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string LauncherId { get; set; }
    public required string LauncherName { get; set; }
    public OperationType OperationType { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool Success { get; set; }
    public string? InstalledVersion { get; set; }
    public string? Message { get; set; }
    public string? Details { get; set; }
}

