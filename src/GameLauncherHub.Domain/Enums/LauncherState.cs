namespace GameLauncherHub.Domain.Enums;

/// <summary>
/// Representa os estados operacionais de um launcher de jogos.
/// </summary>
public enum LauncherState
{
    /// <summary>
    /// O launcher não foi detectado no sistema e está disponível para download e instalação.
    /// </summary>
    NOT_INSTALLED,

    /// <summary>
    /// O launcher está instalado e disponível para execução.
    /// </summary>
    INSTALLED,

    /// <summary>
    /// O instalador do launcher está sendo executado no sistema.
    /// </summary>
    INSTALLING,

    /// <summary>
    /// O instalador oficial do launcher está em processo de download.
    /// </summary>
    DOWNLOADING,

    /// <summary>
    /// O processo do launcher está atualmente em execução no Windows.
    /// </summary>
    RUNNING,

    /// <summary>
    /// Ocorreu um erro durante detecção, download, instalação ou execução.
    /// </summary>
    ERROR,

    /// <summary>
    /// A plataforma não possui suporte nativo para o ambiente Windows (ex: Lutris).
    /// </summary>
    UNSUPPORTED,

    /// <summary>
    /// O estado da plataforma ainda não foi verificado ou não pôde ser determinado.
    /// </summary>
    UNKNOWN
}

