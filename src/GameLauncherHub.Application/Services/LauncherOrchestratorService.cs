using System.IO;
using GameLauncherHub.Application.Interfaces;
using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Application.Services;

/// <summary>
/// Implementação do serviço de orquestração de operações de alto nível da aplicação.
/// </summary>
public class LauncherOrchestratorService : ILauncherOrchestratorService
{
    private readonly ILogger<LauncherOrchestratorService> _logger;
    private readonly ICatalogService _catalogService;
    private readonly ILauncherDetectionService _detectionService;
    private readonly ILauncherExecutionService _executionService;
    private readonly IOperationHistoryService _historyService;
    private readonly IDownloadManager _downloadManager;
    private readonly IInstallerSecurityService _securityService;
    private readonly IInstallerService _installerService;
    private readonly ISettingsService _settingsService;
    private readonly ILauncherDownloadResolver? _downloadResolver;

    public event EventHandler<LauncherPlatform>? PlatformStateChanged;

    public LauncherOrchestratorService(
        ILogger<LauncherOrchestratorService> logger,
        ICatalogService catalogService,
        ILauncherDetectionService detectionService,
        ILauncherExecutionService executionService,
        IOperationHistoryService historyService,
        IDownloadManager downloadManager,
        IInstallerSecurityService securityService,
        IInstallerService installerService,
        ISettingsService settingsService,
        ILauncherDownloadResolver? downloadResolver = null)
    {
        _logger = logger;
        _catalogService = catalogService;
        _detectionService = detectionService;
        _executionService = executionService;
        _historyService = historyService;
        _downloadManager = downloadManager;
        _securityService = securityService;
        _installerService = installerService;
        _settingsService = settingsService;
        _downloadResolver = downloadResolver;
    }

    public async Task<IReadOnlyList<LauncherPlatform>> LoadAndDetectAllAsync(CancellationToken cancellationToken = default)
    {
        var platforms = await _catalogService.GetCatalogAsync(cancellationToken);

        // Executa detecções em paralelo para desempenho rápido
        var tasks = platforms.Select(platform => RefreshPlatformStatusAsync(platform, cancellationToken));
        await Task.WhenAll(tasks);

        return platforms;
    }

    public async Task RefreshPlatformStatusAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!platform.IsSupportedOnWindows)
            {
                platform.State = LauncherState.UNSUPPORTED;
                platform.StatusMessage = platform.UnsupportedReason;
                OnPlatformChanged(platform);
                return;
            }

            var result = await _detectionService.DetectAsync(platform, cancellationToken);
            platform.DetectedExecutablePath = result.ExecutablePath;
            platform.DetectedVersion = result.InstalledVersion;

            if (result.IsRunning)
            {
                platform.State = LauncherState.RUNNING;
                platform.StatusMessage = "Em execução";
            }
            else if (result.IsInstalled)
            {
                platform.State = LauncherState.INSTALLED;
                platform.StatusMessage = !string.IsNullOrWhiteSpace(result.InstalledVersion)
                    ? $"Instalado ({result.InstalledVersion})"
                    : "Instalado";
            }
            else
            {
                platform.State = LauncherState.NOT_INSTALLED;
                platform.StatusMessage = "Disponível para instalação";
            }

            OnPlatformChanged(platform);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar estado de {Name}", platform.Name);
            platform.State = LauncherState.ERROR;
            platform.StatusMessage = $"Erro: {ex.Message}";
            OnPlatformChanged(platform);
        }
    }

    public async Task ExecuteLauncherAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        var log = new OperationLog
        {
            LauncherId = platform.Id,
            LauncherName = platform.Name,
            OperationType = OperationType.Execution
        };

        try
        {
            var result = await _executionService.LaunchAsync(platform, cancellationToken);
            log.Success = result.Success;
            log.Message = result.Message;

            if (result.Success)
            {
                platform.State = LauncherState.RUNNING;
                platform.StatusMessage = "Em execução";
                OnPlatformChanged(platform);
            }
            else
            {
                platform.StatusMessage = result.Message;
                OnPlatformChanged(platform);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na execução de {Name}", platform.Name);
            log.Success = false;
            log.Message = ex.Message;
            platform.StatusMessage = $"Falha ao iniciar: {ex.Message}";
            OnPlatformChanged(platform);
        }
        finally
        {
            await _historyService.LogAsync(log, cancellationToken);
        }
    }

    public async Task<string?> DownloadAndValidateInstallerAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        if (!platform.IsSupportedOnWindows)
        {
            platform.State = LauncherState.UNSUPPORTED;
            platform.StatusMessage = platform.UnsupportedReason;
            OnPlatformChanged(platform);
            return null;
        }

        var downloadUrl = platform.InstallerDirectUrl;

        // 1. Resolução e validação avançada com LauncherDownloadResolver (inspeção de cabeçalhos e prevenção de HTML/Luna)
        if (_downloadResolver != null)
        {
            var resolved = await _downloadResolver.ResolveAndValidateInstallerUrlAsync(platform, cancellationToken);
            if (!resolved.IsValid)
            {
                var error = resolved.ErrorMessage ?? "URL oficial de download inválida ou inacessível.";
                _logger.LogWarning("Resolução de download rejeitada para {Platform}: {Reason}", platform.Name, error);
                platform.State = LauncherState.ERROR;
                platform.StatusMessage = error;
                OnPlatformChanged(platform);

                await _historyService.LogAsync(new OperationLog
                {
                    LauncherId = platform.Id,
                    LauncherName = platform.Name,
                    OperationType = OperationType.Download,
                    Success = false,
                    Message = $"Resolução rejeitada: {error}"
                }, cancellationToken);

                return null;
            }

            downloadUrl = resolved.ResolvedUrl ?? downloadUrl;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                platform.State = LauncherState.ERROR;
                platform.StatusMessage = "URL direta oficial não configurada.";
                OnPlatformChanged(platform);
                return null;
            }

            // Validação prévia de URL padrão (HTTPS e Domínio Oficial)
            if (!_securityService.ValidateDownloadUrl(downloadUrl, platform, out var urlFailure))
            {
                _logger.LogWarning("URL rejeitada pela política de segurança: {Reason}", urlFailure);
                platform.State = LauncherState.ERROR;
                platform.StatusMessage = urlFailure;
                OnPlatformChanged(platform);

                await _historyService.LogAsync(new OperationLog
                {
                    LauncherId = platform.Id,
                    LauncherName = platform.Name,
                    OperationType = OperationType.Download,
                    Success = false,
                    Message = $"URL rejeitada: {urlFailure}"
                }, cancellationToken);

                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            platform.State = LauncherState.ERROR;
            platform.StatusMessage = "URL oficial de download não localizada.";
            OnPlatformChanged(platform);
            return null;
        }

        // 2. Prepara caminho de destino local
        var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
        var extension = platform.Installation.ExpectedInstallerExtension;
        var fileName = $"{platform.Id}_installer{extension}";
        var destinationPath = Path.Combine(settings.DownloadDirectory, fileName);

        // 3. Executa o Download com Progresso
        platform.State = LauncherState.DOWNLOADING;
        platform.StatusMessage = "Iniciando download oficial...";
        platform.OperationProgress = 0;
        platform.OperationSpeed = "0 KB/s";
        OnPlatformChanged(platform);

        var progressHandler = new Progress<DownloadProgressInfo>(info =>
        {
            platform.OperationProgress = info.Percentage;
            platform.OperationSpeed = info.TransferSpeedText;
            platform.StatusMessage = $"Baixando: {info.StatusText}";
            OnPlatformChanged(platform);
        });

        try
        {
            var downloadedFile = await _downloadManager.DownloadInstallerAsync(
                downloadUrl,
                destinationPath,
                progressHandler,
                cancellationToken
            );

            await _historyService.LogAsync(new OperationLog
            {
                LauncherId = platform.Id,
                LauncherName = platform.Name,
                OperationType = OperationType.Download,
                Success = true,
                Message = $"Download concluído: {fileName}"
            }, cancellationToken);

            // 4. Validação de Segurança Authenticode & Integridade
            platform.StatusMessage = "Validando assinatura digital Authenticode...";
            OnPlatformChanged(platform);

            var securityResult = await _securityService.ValidateInstallerFileAsync(downloadedFile, platform, cancellationToken);

            await _historyService.LogAsync(new OperationLog
            {
                LauncherId = platform.Id,
                LauncherName = platform.Name,
                OperationType = OperationType.SignatureVerification,
                Success = securityResult.IsValid,
                Message = securityResult.Details ?? securityResult.ErrorMessage
            }, cancellationToken);

            if (!securityResult.IsValid)
            {
                platform.State = LauncherState.ERROR;
                platform.StatusMessage = securityResult.ErrorMessage ?? "Falha na validação de segurança.";
                OnPlatformChanged(platform);
                return null;
            }

            platform.StatusMessage = $"Instalador verificado ({securityResult.SubjectPublisher}). Pronto para instalação.";
            OnPlatformChanged(platform);

            return downloadedFile;
        }
        catch (OperationCanceledException)
        {
            platform.State = LauncherState.NOT_INSTALLED;
            platform.StatusMessage = "Download cancelado.";
            OnPlatformChanged(platform);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no fluxo de download de {Launcher}", platform.Name);
            platform.State = LauncherState.ERROR;
            platform.StatusMessage = $"Falha no download: {ex.Message}";
            OnPlatformChanged(platform);
            return null;
        }
    }

    public async Task DownloadAndInstallLauncherAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        // 1. Previne operações simultâneas na mesma plataforma
        if (platform.State == LauncherState.DOWNLOADING || platform.State == LauncherState.INSTALLING)
        {
            _logger.LogWarning("Uma operação já está em andamento para a plataforma {Name}", platform.Name);
            return;
        }

        // 2. Download e Validação de Segurança
        var installerFile = await DownloadAndValidateInstallerAsync(platform, cancellationToken);
        if (string.IsNullOrWhiteSpace(installerFile) || !File.Exists(installerFile))
        {
            return;
        }

        // 3. Transição de Estado para INSTALANDO
        platform.State = LauncherState.INSTALLING;
        platform.StatusMessage = "Iniciando instalador oficial...";
        platform.OperationProgress = 0;
        platform.OperationSpeed = null;
        OnPlatformChanged(platform);

        var statusProgress = new Progress<string>(msg =>
        {
            platform.StatusMessage = msg;
            OnPlatformChanged(platform);
        });

        // 4. Execução do Instalador
        var installResult = await _installerService.InstallAsync(
            platform,
            installerFile,
            statusProgress,
            cancellationToken
        );

        await _historyService.LogAsync(new OperationLog
        {
            LauncherId = platform.Id,
            LauncherName = platform.Name,
            OperationType = OperationType.Installation,
            Success = installResult.Success,
            Message = installResult.Message
        }, cancellationToken);

        // 5. Re-detecção imediata pós-instalação
        platform.StatusMessage = "Verificando instalação concluída...";
        OnPlatformChanged(platform);

        var detectionResult = await _detectionService.DetectAsync(platform, cancellationToken);
        if (detectionResult.IsInstalled)
        {
            platform.State = LauncherState.INSTALLED;
            platform.DetectedExecutablePath = detectionResult.ExecutablePath;
            platform.DetectedVersion = detectionResult.InstalledVersion;
            platform.StatusMessage = !string.IsNullOrWhiteSpace(detectionResult.InstalledVersion)
                ? $"Instalado com sucesso ({detectionResult.InstalledVersion})"
                : "Instalado com sucesso";

            // 6. Limpeza automática do arquivo temporário se configurado
            var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
            if (settings.AutoCleanupInstallers)
            {
                try
                {
                    if (File.Exists(installerFile))
                    {
                        File.Delete(installerFile);
                        _logger.LogInformation("Instalador temporário removido: {Path}", installerFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Não foi possível remover arquivo temporário {Path}", installerFile);
                }
            }
        }
        else
        {
            platform.State = LauncherState.NOT_INSTALLED;
            platform.StatusMessage = installResult.Success
                ? "Instalador finalizado, mas a plataforma ainda não foi detectada."
                : (installResult.Message ?? "Instalação não concluída.");
        }

        OnPlatformChanged(platform);
    }

    public async Task SetCustomExecutablePathAsync(string platformId, string executablePath, CancellationToken cancellationToken = default)
    {
        await _catalogService.SetCustomExecutablePathAsync(platformId, executablePath, cancellationToken);
        var platform = _catalogService.GetById(platformId);
        if (platform != null)
        {
            platform.DetectedExecutablePath = executablePath;
            platform.State = LauncherState.INSTALLED;
            platform.StatusMessage = "Configurado manualmente";
            OnPlatformChanged(platform);
        }
    }

    public async Task AddCustomPlatformAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        await _catalogService.AddOrUpdateCustomPlatformAsync(platform, cancellationToken);
        OnPlatformChanged(platform);
    }

    private void OnPlatformChanged(LauncherPlatform platform)
    {
        PlatformStateChanged?.Invoke(this, platform);
    }
}
