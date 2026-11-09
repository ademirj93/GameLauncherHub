using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameLauncherHub.Infrastructure.Detection;

/// <summary>
/// Estratégia de detecção via protocolos oficiais registrados no Windows (ex: steam://, com.epicgames.launcher://).
/// </summary>
public partial class ProtocolRegistrationDetectionStrategy : IDetectionStrategy
{
    private readonly ILogger<ProtocolRegistrationDetectionStrategy>? _logger;

    public string StrategyName => "ProtocolRegistration";

    public ProtocolRegistrationDetectionStrategy(ILogger<ProtocolRegistrationDetectionStrategy>? logger = null)
    {
        _logger = logger;
    }

    public Task<StrategyDetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Não suportado em ambiente não-Windows."));
        }

        foreach (var rawProtocol in platform.Detection.ProtocolSchemes)
        {
            var cleanProtocol = rawProtocol.TrimEnd(':', '/');

            // Verifica em HKCR e HKCU\Software\Classes
            var searchKeys = new[]
            {
                (Root: Registry.ClassesRoot, Path: $@"{cleanProtocol}\shell\open\command"),
                (Root: Registry.CurrentUser, Path: $@"Software\Classes\{cleanProtocol}\shell\open\command")
            };

            foreach (var (root, subPath) in searchKeys)
            {
                try
                {
                    using var key = root.OpenSubKey(subPath);
                    if (key != null)
                    {
                        var commandValue = key.GetValue(null) as string;
                        if (!string.IsNullOrWhiteSpace(commandValue))
                        {
                            var exePath = ExtractExecutablePathFromCommand(commandValue);
                            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                            {
                                string? version = null;
                                try
                                {
                                    var fvi = FileVersionInfo.GetVersionInfo(exePath);
                                    version = fvi.FileVersion ?? fvi.ProductVersion;
                                }
                                catch
                                {
                                    // Ignora falha de versão
                                }

                                return Task.FromResult(new StrategyDetectionResult(
                                    Success: true,
                                    ExecutablePath: exePath,
                                    Version: version,
                                    StrategyUsed: StrategyName,
                                    Details: $"Protocolo {cleanProtocol} registrado com comando: {commandValue}"
                                ));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Erro ao inspecionar protocolo {Protocol}", cleanProtocol);
                }
            }
        }

        return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Nenhum protocolo registrado válido"));
    }

    private static string? ExtractExecutablePathFromCommand(string command)
    {
        var trimmed = command.Trim();

        // 1. Caso com aspas: "C:\Program Files\App\app.exe" "%1"
        if (trimmed.StartsWith("\""))
        {
            var nextQuoteIndex = trimmed.IndexOf('\"', 1);
            if (nextQuoteIndex > 1)
            {
                return trimmed.Substring(1, nextQuoteIndex - 1);
            }
        }

        // 2. Caso sem aspas: busca a ocorrência de .exe
        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex > 0)
        {
            return trimmed.Substring(0, exeIndex + 4);
        }

        // 3. Fallback: primeira palavra
        var firstSpace = trimmed.IndexOf(' ');
        return firstSpace > 0 ? trimmed.Substring(0, firstSpace) : trimmed;
    }
}

