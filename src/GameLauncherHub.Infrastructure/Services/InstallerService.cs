using System.Diagnostics;
using System.IO;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Services;

/// <summary>
/// Serviço de execução de instaladores de launchers no Windows.
/// Suporta instaladores executáveis (.exe) e MSI (.msi), com parâmetros silenciosos quando homologados
/// ou modo interativo oficial com elevação UAC padrão do Windows.
/// </summary>
public class InstallerService : IInstallerService
{
    private readonly ILogger<InstallerService>? _logger;

    public InstallerService(ILogger<InstallerService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<InstallationResult> InstallAsync(
        LauncherPlatform platform,
        string installerFilePath,
        IProgress<string> statusProgress,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(installerFilePath))
        {
            return new InstallationResult(false, $"Arquivo de instalação não encontrado: {installerFilePath}");
        }

        try
        {
            var extension = Path.GetExtension(installerFilePath).ToLowerInvariant();
            ProcessStartInfo startInfo;

            // 1. Configuração específica por formato (.msi vs .exe)
            if (extension == ".msi")
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = platform.Installation.SupportsSilentInstall && !string.IsNullOrWhiteSpace(platform.Installation.SilentInstallArguments)
                        ? $"/i \"{installerFilePath}\" {platform.Installation.SilentInstallArguments}"
                        : $"/i \"{installerFilePath}\"",
                    UseShellExecute = true,
                    Verb = "runas" // Solicita elevação administrativa UAC para instalação do MSI
                };
            }
            else
            {
                var arguments = platform.Installation.SupportsSilentInstall && !string.IsNullOrWhiteSpace(platform.Installation.SilentInstallArguments)
                    ? platform.Installation.SilentInstallArguments
                    : string.Empty;

                startInfo = new ProcessStartInfo
                {
                    FileName = installerFilePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(installerFilePath) ?? string.Empty,
                    UseShellExecute = true
                };
            }

            _logger?.LogInformation("Iniciando instalador para {Name}: {FileName} {Args}",
                platform.Name, startInfo.FileName, startInfo.Arguments);

            statusProgress.Report("Iniciando instalador oficial...");

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new InstallationResult(false, "Não foi possível iniciar o processo de instalação.");
            }

            statusProgress.Report("Aguardando conclusão da instalação...");

            // 2. Aguarda a conclusão do processo do instalador
            await process.WaitForExitAsync(cancellationToken);

            int exitCode = process.ExitCode;
            _logger?.LogInformation("Instalador de {Name} finalizado com código de saída {ExitCode}",
                platform.Name, exitCode);

            // Código 0 = Sucesso; Código 3010 = Sucesso (reboot do sistema pendente)
            if (exitCode == 0 || exitCode == 3010)
            {
                return new InstallationResult(true, "Instalação concluída com sucesso.");
            }

            // Código 1602 = Cancelado pelo usuário no Windows Installer
            if (exitCode == 1602)
            {
                return new InstallationResult(false, "Instalação cancelada pelo usuário.");
            }

            return new InstallationResult(false, $"O instalador encerrou com código {exitCode}.");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Acompanhamento da instalação de {Name} cancelado.", platform.Name);
            return new InstallationResult(false, "Operação de instalação cancelada.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao executar instalador de {Name}", platform.Name);
            return new InstallationResult(false, $"Falha ao executar instalador: {ex.Message}");
        }
    }
}

