using System.Runtime.InteropServices;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameLauncherHub.Infrastructure.Detection;

/// <summary>
/// Estratégia de detecção para pacotes MSIX / Windows Store (ex: Xbox App).
/// </summary>
public class WindowsStorePackageDetectionStrategy : IDetectionStrategy
{
    private readonly ILogger<WindowsStorePackageDetectionStrategy>? _logger;

    public string StrategyName => "WindowsStorePackage";

    public WindowsStorePackageDetectionStrategy(ILogger<WindowsStorePackageDetectionStrategy>? logger = null)
    {
        _logger = logger;
    }

    public Task<StrategyDetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        if (!platform.IsWindowsStorePackage || string.IsNullOrWhiteSpace(platform.PackageFamilyName))
        {
            return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Não é um aplicativo Windows Store."));
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Não suportado fora do Windows."));
        }

        try
        {
            // 1. Verificação direta no sistema de arquivos em %LOCALAPPDATA%\Microsoft\WindowsApps
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var packageDir = System.IO.Path.Combine(localAppData, "Microsoft", "WindowsApps", platform.PackageFamilyName);
            if (System.IO.Directory.Exists(packageDir))
            {
                var exeFiles = System.IO.Directory.GetFiles(packageDir, "*.exe");
                string exeTarget = exeFiles.FirstOrDefault(f => !f.Contains("AdminServer", StringComparison.OrdinalIgnoreCase))
                                   ?? exeFiles.FirstOrDefault()
                                   ?? "xbox:";

                _logger?.LogInformation("Pacote Windows Store {Family} localizado em disco: {Path}", platform.PackageFamilyName, exeTarget);

                return Task.FromResult(new StrategyDetectionResult(
                    Success: true,
                    ExecutablePath: exeTarget,
                    Version: "Windows Store App",
                    StrategyUsed: StrategyName,
                    Details: $"Instalado em {packageDir}"
                ));
            }

            // 2. Verificação no repositório de pacotes do usuário atual
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
            {
                if (key != null)
                {
                    var prefix = platform.PackageFamilyName.Split('_')[0];
                    var matchedSubKey = key.GetSubKeyNames()
                        .FirstOrDefault(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(matchedSubKey))
                    {
                        var version = ExtractVersionFromPackageName(matchedSubKey);
                        return Task.FromResult(new StrategyDetectionResult(
                            Success: true,
                            ExecutablePath: "xbox:",
                            Version: version ?? "Store Package",
                            StrategyUsed: StrategyName,
                            Details: $"Pacote registrado: {matchedSubKey}"
                        ));
                    }
                }
            }

            // 3. Verificação no registro de famílias de pacotes da máquina (HKLM)
            using (var famKey = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\Repository\Families\{platform.PackageFamilyName}"))
            {
                if (famKey != null)
                {
                    var packageNames = famKey.GetSubKeyNames();
                    if (packageNames.Length > 0)
                    {
                        var version = ExtractVersionFromPackageName(packageNames[0]);
                        return Task.FromResult(new StrategyDetectionResult(
                            Success: true,
                            ExecutablePath: "xbox:",
                            Version: version ?? "Store Package",
                            StrategyUsed: StrategyName,
                            Details: $"Família de pacote ativa: {platform.PackageFamilyName}"
                        ));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Falha ao verificar registro de pacotes MSIX para {Family}", platform.PackageFamilyName);
        }

        return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Pacote Windows Store não encontrado."));
    }

    private static string? ExtractVersionFromPackageName(string packageName)
    {
        // Formato típico: Microsoft.GamingApp_2203.1001.4.0_x64__8wekyb3d8bbwe
        var parts = packageName.Split('_');
        if (parts.Length >= 2)
        {
            return parts[1];
        }
        return null;
    }
}

