using System.Diagnostics;
using System.IO;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Detection;

/// <summary>
/// Estratégia de detecção baseada em caminhos e diretórios conhecidos de instalação padrão.
/// Valida estritamente a presença do executável, não apenas a pasta.
/// </summary>
public class KnownPathsDetectionStrategy : IDetectionStrategy
{
    private readonly ILogger<KnownPathsDetectionStrategy>? _logger;

    public string StrategyName => "KnownInstallPaths";

    public KnownPathsDetectionStrategy(ILogger<KnownPathsDetectionStrategy>? logger = null)
    {
        _logger = logger;
    }

    public Task<StrategyDetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        foreach (var dirTemplate in platform.Detection.DefaultInstallDirectories)
        {
            try
            {
                var expandedDir = Environment.ExpandEnvironmentVariables(dirTemplate);
                if (Directory.Exists(expandedDir))
                {
                    foreach (var exeName in platform.Detection.KnownExecutableNames)
                    {
                        var candidatePath = Path.Combine(expandedDir, exeName);
                        if (File.Exists(candidatePath))
                        {
                            string? version = null;
                            try
                            {
                                var fvi = FileVersionInfo.GetVersionInfo(candidatePath);
                                version = fvi.FileVersion ?? fvi.ProductVersion;
                            }
                            catch
                            {
                                // Ignora falha de versão
                            }

                            return Task.FromResult(new StrategyDetectionResult(
                                Success: true,
                                ExecutablePath: candidatePath,
                                Version: version,
                                StrategyUsed: StrategyName,
                                Details: $"Binário encontrado no diretório conhecido: {candidatePath}"
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Erro ao inspecionar diretório padrão {Dir}", dirTemplate);
            }
        }

        return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Executável não encontrado nos caminhos padrão."));
    }
}

