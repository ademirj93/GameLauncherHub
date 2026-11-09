using System.IO;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Detection;

/// <summary>
/// Estratégia de detecção via atalhos do Menu Iniciar do Windows (%ProgramData% e %APPDATA%).
/// </summary>
public class StartMenuDetectionStrategy : IDetectionStrategy
{
    private readonly ILogger<StartMenuDetectionStrategy>? _logger;

    public string StrategyName => "StartMenuShortcuts";

    public StartMenuDetectionStrategy(ILogger<StartMenuDetectionStrategy>? logger = null)
    {
        _logger = logger;
    }

    public Task<StrategyDetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        var startMenuFolders = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
        };

        var shortcutNames = platform.Detection.StartMenuShortcutNames.Count > 0
            ? platform.Detection.StartMenuShortcutNames
            : new List<string> { $"{platform.Name}.lnk" };

        foreach (var folder in startMenuFolders)
        {
            if (!Directory.Exists(folder)) continue;

            try
            {
                var files = Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (shortcutNames.Any(s => string.Equals(s, fileName, StringComparison.OrdinalIgnoreCase)) ||
                        fileName.Contains(platform.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(new StrategyDetectionResult(
                            Success: true,
                            ExecutablePath: file, // Atalho válido encontrado
                            Version: null,
                            StrategyUsed: StrategyName,
                            Details: $"Atalho do Menu Iniciar identificado: {file}"
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Erro ao inspecionar atalhos em {Folder}", folder);
            }
        }

        return Task.FromResult(StrategyDetectionResult.Failed(StrategyName, "Atalho do Menu Iniciar não encontrado."));
    }
}

