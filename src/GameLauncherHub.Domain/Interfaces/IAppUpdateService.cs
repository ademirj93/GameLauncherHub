using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato de serviço para verificação, download e execução de atualizações do GameLauncher Hub via GitHub Releases.
/// </summary>
public interface IAppUpdateService
{
    /// <summary>
    /// Evento disparado para solicitar o encerramento seguro e gracioso da aplicação no momento da instalação.
    /// </summary>
    event Action? RequestApplicationShutdown;

    /// <summary>
    /// Obtém a versão atual compilada e em execução da aplicação.
    /// </summary>
    Version GetCurrentVersion();

    /// <summary>
    /// Consulta o GitHub Releases e determina se uma nova versão está disponível.
    /// </summary>
    Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Faz o download seguro do instalador oficial da nova versão com acompanhamento de progresso.
    /// </summary>
    Task<string> DownloadUpdateAsync(
        AppUpdateInfo updateInfo,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispara o processo do instalador da nova versão no Windows e encerra a aplicação atual de forma graciosa.
    /// </summary>
    void LaunchInstallerAndExit(string installerPath, bool silent = false);
}
