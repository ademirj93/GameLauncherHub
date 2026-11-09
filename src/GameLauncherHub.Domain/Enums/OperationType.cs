namespace GameLauncherHub.Domain.Enums;

/// <summary>
/// Representa o tipo de operação executada no ciclo de vida de um launcher.
/// </summary>
public enum OperationType
{
    Detection,
    Download,
    SignatureVerification,
    Installation,
    Execution,
    StateCheck
}

