using System.Globalization;

namespace GameLauncherHub.Domain.Models;

/// <summary>
/// Modelo com as informações sobre a versão e artefatos de atualização obtidos do GitHub Releases.
/// </summary>
public class AppUpdateInfo
{
    /// <summary>
    /// Versão atualmente instalada e em execução no sistema.
    /// </summary>
    public Version CurrentVersion { get; set; } = new(0, 0, 0);

    /// <summary>
    /// Versão mais recente identificada na release do GitHub.
    /// </summary>
    public Version LatestVersion { get; set; } = new(0, 0, 0);

    /// <summary>
    /// Tag da release no repositório (ex: "v0.12.0" ou "0.12.0").
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// Título ou nome da release no GitHub.
    /// </summary>
    public string ReleaseName { get; set; } = string.Empty;

    /// <summary>
    /// Notas de atualização / changelog (Markdown).
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// Data e hora da publicação da release no GitHub.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// URL da página web da release no GitHub.
    /// </summary>
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>
    /// Indica se a versão remota é estritamente superior à versão instalada.
    /// </summary>
    public bool IsUpdateAvailable { get; set; }

    /// <summary>
    /// Indica se a release é uma pré-versão (pré-lançamento).
    /// </summary>
    public bool IsPrerelease { get; set; }

    /// <summary>
    /// URL direta para download do executável oficial de instalação (Setup / Installer).
    /// </summary>
    public string? InstallerDownloadUrl { get; set; }

    /// <summary>
    /// Nome do arquivo do executável de instalação (ex: "GameLauncherHub_Setup_v0.12.0.exe").
    /// </summary>
    public string? InstallerFileName { get; set; }

    /// <summary>
    /// Tamanho do arquivo do instalador em bytes, quando informado pelo GitHub.
    /// </summary>
    public long? InstallerSizeBytes { get; set; }

    /// <summary>
    /// Hash criptográfico SHA-256 para auditoria de integridade do instalador (quando disponível).
    /// </summary>
    public string? Sha256Checksum { get; set; }

    /// <summary>
    /// Mensagem amigável de erro caso ocorra falha de rede ou acesso à API.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Retorna o tamanho formatado para exibição visual (ex: "48.5 MB").
    /// </summary>
    public string FormattedSize
    {
        get
        {
            if (!InstallerSizeBytes.HasValue || InstallerSizeBytes.Value <= 0)
                return "Tamanho desconhecido";

            const double mb = 1024 * 1024;
            return FormattableString.Invariant($"{InstallerSizeBytes.Value / mb:F1} MB");
        }
    }
}

