using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Resultado da resolução prévia de uma URL oficial de download.
/// </summary>
public record ResolvedDownloadInfo(
    bool IsValid,
    string? ResolvedUrl,
    string? ContentType,
    long? ContentLength,
    string? ErrorMessage
);

/// <summary>
/// Serviço responsável por resolver e pré-validar a URL do instalador antes do download,
/// tratando redirecionamentos, verificando cabeçalhos HTTP e impedindo downloads inválidos (como páginas HTML).
/// </summary>
public interface ILauncherDownloadResolver
{
    Task<ResolvedDownloadInfo> ResolveAndValidateInstallerUrlAsync(LauncherPlatform platform, CancellationToken cancellationToken = default);
}

