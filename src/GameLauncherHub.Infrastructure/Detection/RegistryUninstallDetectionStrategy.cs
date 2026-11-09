using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameLauncherHub.Infrastructure.Detection;

/// <summary>
/// Estratégia de detecção via chaves de registro do Windows (Uninstall e chaves diretas da aplicação).
/// </summary>
public class RegistryUninstallDetectionStrategy : IDetectionStrategy
{
    private readonly ILogger<RegistryUninstallDetectionStrategy>? _logger;

    public string StrategyName => "WindowsRegistry";

    public RegistryUninstallDetectionStrategy(ILogger<RegistryUninstallDetectionStrategy>? logger = null)
    {
        _logger = logger;
    }

    public Task<StrategyDetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Não suportado em ambiente não-Windows."));
        }

        try
        {
            var hives = new[] { Registry.CurrentUser, Registry.LocalMachine };

            // 1. Verificação por chaves diretas configuradas na plataforma
            foreach (var hive in hives)
            {
                foreach (var relKey in platform.Detection.RegistryPaths)
                {
                    using var key = hive.OpenSubKey(relKey);
                    if (key != null)
                    {
                        var result = TryResolveFromRegistryKey(key, platform);
                        if (result != null) return Task.FromResult(result);
                    }
                }
            }

            // 2. Varredura nas chaves de desinstalação (Uninstall 32 e 64 bits)
            var uninstallRoots = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var hive in hives)
            {
                foreach (var rootPath in uninstallRoots)
                {
                    using var rootKey = hive.OpenSubKey(rootPath);
                    if (rootKey == null) continue;

                    foreach (var subKeyName in rootKey.GetSubKeyNames())
                    {
                        using var subKey = rootKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName") as string;
                        if (!string.IsNullOrWhiteSpace(displayName) &&
                            displayName.Contains(platform.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            var result = TryResolveFromRegistryKey(subKey, platform);
                            if (result != null) return Task.FromResult(result);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Erro ao varrer o registro do Windows para {Launcher}", platform.Name);
        }

        return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Nenhum registro válido encontrado."));
    }

    private StrategyDetectionResult? TryResolveFromRegistryKey(RegistryKey key, LauncherPlatform platform)
    {
        var displayVersion = key.GetValue("DisplayVersion") as string;
        var installLocation = key.GetValue("InstallLocation") as string
                           ?? key.GetValue("Path") as string
                           ?? key.GetValue("InstallDir") as string;

        // Tenta encontrar o executável dentro de InstallLocation
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            foreach (var exeName in platform.Detection.KnownExecutableNames)
            {
                var fullExePath = Path.Combine(installLocation, exeName);
                if (File.Exists(fullExePath))
                {
                    var version = displayVersion ?? GetFileVersion(fullExePath);
                    return new StrategyDetectionResult(
                        Success: true,
                        ExecutablePath: fullExePath,
                        Version: version,
                        StrategyUsed: StrategyName,
                        Details: $"Detectado via InstallLocation no registro: {key.Name}"
                    );
                }
            }
        }

        // Tenta resolver a partir de DisplayIcon (ex: "C:\Program Files\App\app.exe,0")
        var displayIcon = key.GetValue("DisplayIcon") as string;
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var cleanPath = displayIcon.Split(',')[0].Trim('"', ' ');
            if (File.Exists(cleanPath) && cleanPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var version = displayVersion ?? GetFileVersion(cleanPath);
                return new StrategyDetectionResult(
                    Success: true,
                    ExecutablePath: cleanPath,
                    Version: version,
                    StrategyUsed: StrategyName,
                    Details: $"Detectado via DisplayIcon no registro: {key.Name}"
                );
            }
        }

        return null;
    }

    private static string? GetFileVersion(string filePath)
    {
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(filePath);
            return fvi.FileVersion ?? fvi.ProductVersion;
        }
        catch
        {
            return null;
        }
    }
}

