using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Detection;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Services;

/// <summary>
/// Serviço central de detecção multi-estratégia para launchers de jogos no Windows.
/// Orquestra a verificação no Registro, App Paths, Protocolos, Diretórios Padrão, Atalhos e Processos.
/// </summary>
public class LauncherDetectionService : ILauncherDetectionService
{
    private readonly ILogger<LauncherDetectionService> _logger;
    private readonly List<IDetectionStrategy> _strategies;
    private readonly ProcessDetectionStrategy _processStrategy;
    private readonly WindowsStorePackageDetectionStrategy _storePackageStrategy;

    public LauncherDetectionService(
        ILogger<LauncherDetectionService> logger,
        IEnumerable<IDetectionStrategy>? customStrategies = null)
    {
        _logger = logger;
        _processStrategy = new ProcessDetectionStrategy();
        _storePackageStrategy = new WindowsStorePackageDetectionStrategy();

        if (customStrategies != null && customStrategies.Any())
        {
            _strategies = customStrategies.ToList();
        }
        else
        {
            // Estratégias padrão na ordem de confiabilidade
            _strategies = new List<IDetectionStrategy>
            {
                new RegistryUninstallDetectionStrategy(),
                new AppPathsDetectionStrategy(),
                new ProtocolRegistrationDetectionStrategy(),
                new KnownPathsDetectionStrategy(),
                new StartMenuDetectionStrategy()
            };
        }
    }

    public async Task<DetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            // 1. Plataforma não suportada nativamente no Windows (ex: Lutris)
            if (!platform.IsSupportedOnWindows)
            {
                return new DetectionResult(
                    IsInstalled: false,
                    ExecutablePath: null,
                    InstalledVersion: null,
                    IsRunning: false,
                    Notes: platform.UnsupportedReason ?? "Plataforma não suportada no Windows"
                );
            }

            // 2. Verificação de processo em execução em tempo real
            var processCheck = await _processStrategy.DetectAsync(platform, cancellationToken);
            bool isRunning = processCheck.Success;

            // 3. Caso especial: Aplicativo da Windows Store / MSIX (ex: Xbox App)
            if (platform.IsWindowsStorePackage)
            {
                var storeResult = await _storePackageStrategy.DetectAsync(platform, cancellationToken);
                return new DetectionResult(
                    IsInstalled: storeResult.Success,
                    ExecutablePath: storeResult.ExecutablePath,
                    InstalledVersion: storeResult.Version,
                    IsRunning: isRunning,
                    Notes: storeResult.Details ?? (storeResult.Success ? "Instalado via Windows Store" : "Pacote não encontrado")
                );
            }

            // 4. Se o processo já está ativo e conseguimos extrair o caminho do módulo principal
            if (processCheck.Success && !string.IsNullOrWhiteSpace(processCheck.ExecutablePath))
            {
                return new DetectionResult(
                    IsInstalled: true,
                    ExecutablePath: processCheck.ExecutablePath,
                    InstalledVersion: processCheck.Version,
                    IsRunning: true,
                    Notes: processCheck.Details
                );
            }

            // 5. Execução sequencial das estratégias de detecção de instalação
            foreach (var strategy in _strategies)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var result = await strategy.DetectAsync(platform, cancellationToken);
                    if (result.Success && !string.IsNullOrWhiteSpace(result.ExecutablePath))
                    {
                        _logger.LogInformation(
                            "Plataforma {Platform} detectada com sucesso via estratégia '{Strategy}'. Caminho: {Path}",
                            platform.Name, strategy.StrategyName, result.ExecutablePath);

                        return new DetectionResult(
                            IsInstalled: true,
                            ExecutablePath: result.ExecutablePath,
                            InstalledVersion: result.Version,
                            IsRunning: isRunning,
                            Notes: $"{strategy.StrategyName}: {result.Details}"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Falha na execução da estratégia {Strategy} para {Platform}",
                        strategy.StrategyName, platform.Name);
                }
            }

            // 6. Não detectado por nenhuma estratégia
            return new DetectionResult(
                IsInstalled: false,
                ExecutablePath: null,
                InstalledVersion: null,
                IsRunning: isRunning,
                Notes: "Não detectado no sistema."
            );
        }, cancellationToken);
    }

    public async Task<bool> IsRunningAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        var result = await _processStrategy.DetectAsync(platform, cancellationToken);
        return result.Success;
    }
}
