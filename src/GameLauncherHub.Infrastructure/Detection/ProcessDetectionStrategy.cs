using System.Diagnostics;
using System.IO;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Detection;

/// <summary>
/// Estratégia de detecção baseada em processos ativos no Windows.
/// </summary>
public class ProcessDetectionStrategy : IDetectionStrategy
{
    private readonly ILogger<ProcessDetectionStrategy>? _logger;

    public string StrategyName => "ActiveProcess";

    public ProcessDetectionStrategy(ILogger<ProcessDetectionStrategy>? logger = null)
    {
        _logger = logger;
    }

    public Task<StrategyDetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        if (platform.Detection.KnownExecutableNames.Count == 0)
        {
            return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Nenhum executável conhecido configurado."));
        }

        try
        {
            foreach (var exeName in platform.Detection.KnownExecutableNames)
            {
                var processNameWithoutExtension = Path.GetFileNameWithoutExtension(exeName);
                if (string.IsNullOrWhiteSpace(processNameWithoutExtension))
                    continue;

                // Process.GetProcessesByName consulta diretamente o kernel sem enumerar centenas de processos desnecessários
                var matches = Process.GetProcessesByName(processNameWithoutExtension);
                if (matches.Length == 0)
                    continue;

                try
                {
                    var matchedProcess = matches[0];
                    string? verifiedPath = null;
                    string? version = null;

                    try
                    {
                        verifiedPath = matchedProcess.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(verifiedPath) && File.Exists(verifiedPath))
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(verifiedPath);
                            version = fvi.FileVersion ?? fvi.ProductVersion;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Acesso ao MainModule restrito para o processo {ProcessName}", matchedProcess.ProcessName);
                    }

                    return Task.FromResult(new StrategyDetectionResult(
                        Success: true,
                        ExecutablePath: verifiedPath,
                        Version: version,
                        StrategyUsed: StrategyName,
                        Details: $"Processo em execução ativo (PID: {matchedProcess.Id})"
                    ));
                }
                finally
                {
                    // Descarta imediatamente os handles nativos alocados para evitar retenção de memória e leaks
                    foreach (var p in matches)
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Erro ao consultar processos ativos para {Launcher}", platform.Name);
        }

        return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Processo não em execução"));
    }
}

