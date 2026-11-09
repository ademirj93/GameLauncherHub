using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameLauncherHub.Infrastructure.Detection;

/// <summary>
/// Estratégia de detecção via 'App Paths' do Windows (HKLM e HKCU).
/// </summary>
public class AppPathsDetectionStrategy : IDetectionStrategy
{
    private readonly ILogger<AppPathsDetectionStrategy>? _logger;

    public string StrategyName => "AppPaths";

    public AppPathsDetectionStrategy(ILogger<AppPathsDetectionStrategy>? logger = null)
    {
        _logger = logger;
    }

    public Task<StrategyDetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Não suportado em ambiente não-Windows."));
        }

        var candidateExeNames = platform.Detection.AppPathNames.Count > 0
            ? platform.Detection.AppPathNames
            : platform.Detection.KnownExecutableNames;

        foreach (var exeName in candidateExeNames)
        {
            var relativeSubKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}";
            var hives = new[] { Registry.LocalMachine, Registry.CurrentUser };

            foreach (var hive in hives)
            {
                try
                {
                    using var key = hive.OpenSubKey(relativeSubKey);
                    if (key != null)
                    {
                        var rawPath = key.GetValue(null) as string;
                        if (!string.IsNullOrWhiteSpace(rawPath))
                        {
                            var cleanPath = rawPath.Trim('"', ' ');
                            if (File.Exists(cleanPath))
                            {
                                string? version = null;
                                try
                                {
                                    var fvi = FileVersionInfo.GetVersionInfo(cleanPath);
                                    version = fvi.FileVersion ?? fvi.ProductVersion;
                                }
                                catch
                                {
                                    // Ignora falha de leitura de versão
                                }

                                return Task.FromResult(new StrategyDetectionResult(
                                    Success: true,
                                    ExecutablePath: cleanPath,
                                    Version: version,
                                    StrategyUsed: StrategyName,
                                    Details: $"Encontrado em {hive.Name}\\{relativeSubKey}"
                                ));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Falha ao consultar App Paths para {ExeName}", exeName);
                }
            }
        }

        return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Nenhum App Path registrado encontrado."));
    }
}

