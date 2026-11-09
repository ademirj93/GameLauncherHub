using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Infrastructure.Detection;

/// <summary>
/// Contrato para uma estratégia individual de detecção de launcher no Windows.
/// </summary>
public interface IDetectionStrategy
{
    string StrategyName { get; }

    /// <summary>
    /// Tenta detectar a instalação e o caminho do executável da plataforma especificada.
    /// </summary>
    Task<StrategyDetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
}

public record StrategyDetectionResult(
    bool Success,
    string? ExecutablePath,
    string? Version,
    string StrategyUsed,
    string? Details
)
{
    public static StrategyDetectionResult Failed(string strategyName, string? reason = null) =>
        new(false, null, null, strategyName, reason);
}

