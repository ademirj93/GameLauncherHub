using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço responsável pela validação rigorosa de segurança dos instaladores baixados.
/// Valida HTTPS, domínios autorizados, assinatura digital Authenticode e compatibilidade de arquitetura.
/// </summary>
public interface IInstallerSecurityService
{
    /// <summary>
    /// Valida a URL de download antes de iniciar a transferência.
    /// </summary>
    bool ValidateDownloadUrl(string url, LauncherPlatform platform, out string? failureReason);

    /// <summary>
    /// Valida o arquivo do instalador baixado (Authenticode, editor, arquitetura e integridade).
    /// </summary>
    Task<SecurityValidationResult> ValidateInstallerFileAsync(
        string filePath,
        LauncherPlatform platform,
        CancellationToken cancellationToken = default
    );
}

public record SecurityValidationResult(
    bool IsValid,
    bool IsAuthenticodeSigned,
    string? SubjectPublisher,
    string? Sha256Hash,
    string? Details,
    string? ErrorMessage
);

