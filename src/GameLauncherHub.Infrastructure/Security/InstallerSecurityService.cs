using System.IO;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Catalog;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Security;

/// <summary>
/// Serviço de validação de segurança para instaladores.
/// Aplica as diretrizes de Secure by Design: HTTPS obrigatório, domínios oficiais,
/// verificação de integridade PE e assinatura digital Authenticode com validação de editor.
/// </summary>
public class InstallerSecurityService : IInstallerSecurityService
{
    private readonly ILogger<InstallerSecurityService>? _logger;
    private readonly AuthenticodeVerifier _authenticodeVerifier;
    private readonly IOfficialDownloadCatalogService? _officialCatalogService;

    public InstallerSecurityService(
        ILogger<InstallerSecurityService>? logger = null,
        AuthenticodeVerifier? authenticodeVerifier = null,
        IOfficialDownloadCatalogService? officialCatalogService = null)
    {
        _logger = logger;
        _authenticodeVerifier = authenticodeVerifier ?? new AuthenticodeVerifier();
        _officialCatalogService = officialCatalogService ?? new OfficialDownloadCatalogService();
    }

    public bool ValidateDownloadUrl(string url, LauncherPlatform platform, out string? failureReason)
    {
        failureReason = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            failureReason = "URL de download não fornecida.";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            failureReason = "Formato de URL inválido.";
            return false;
        }

        // 1. Obrigatório HTTPS
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "Conexão insegura: Apenas downloads via HTTPS são autorizados.";
            return false;
        }

        // 2. Validação contra lista de domínios oficiais e CDNs autorizadas
        var host = uri.Host.ToLowerInvariant();
        bool isAuthorized = false;

        if (_officialCatalogService != null)
        {
            isAuthorized = _officialCatalogService.IsDomainAuthorized(platform.Id, host);
        }

        if (!isAuthorized)
        {
            failureReason = $"Domínio '{host}' não consta na lista de fornecedores oficiais ou CDNs autorizadas para a plataforma {platform.Name}.";
            return false;
        }

        return true;
    }

    public async Task<SecurityValidationResult> ValidateInstallerFileAsync(
        string filePath,
        LauncherPlatform platform,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new SecurityValidationResult(
                IsValid: false,
                IsAuthenticodeSigned: false,
                SubjectPublisher: null,
                Sha256Hash: null,
                Details: "Arquivo inexistente",
                ErrorMessage: $"O instalador não foi encontrado em: {filePath}"
            );
        }

        var fileInfo = new FileInfo(filePath);

        // 1. Validação de tamanho mínimo (evita payloads vazios ou páginas de erro salvas como .exe)
        if (fileInfo.Length < 100 * 1024) // < 100 KB
        {
            return new SecurityValidationResult(
                IsValid: false,
                IsAuthenticodeSigned: false,
                SubjectPublisher: null,
                Sha256Hash: null,
                Details: $"Tamanho insuficiente: {fileInfo.Length} bytes",
                ErrorMessage: "O arquivo baixado possui tamanho incompatível com um instalador oficial."
            );
        }

        // 2. Validação da assinatura do cabeçalho (.exe com MZ ou .msi com OLE Compound File)
        if (!HasValidInstallerHeader(filePath))
        {
            return new SecurityValidationResult(
                IsValid: false,
                IsAuthenticodeSigned: false,
                SubjectPublisher: null,
                Sha256Hash: null,
                Details: "Assinatura do cabeçalho inválida",
                ErrorMessage: "O arquivo baixado não é um instalador executável válido para o Windows (cabeçalho PE/MSI ausente ou corrompido)."
            );
        }

        // 3. Cálculo do Hash SHA-256
        string sha256;
        try
        {
            sha256 = await AuthenticodeVerifier.ComputeSha256HashAsync(filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Falha ao calcular hash SHA-256.");
            return new SecurityValidationResult(false, false, null, null, null, $"Erro ao calcular hash: {ex.Message}");
        }

        // 4. Validação de Assinatura Digital Authenticode
        bool isSigned = _authenticodeVerifier.VerifyAuthenticodeSignature(
            filePath,
            out var publisherName,
            out var failureDetails
        );

        if (!isSigned)
        {
            // Tratamento para softwares de código aberto que não possuem certificados Authenticode comerciais pagos
            bool isUnsignedOpenSource = (!platform.RequiresDigitalSignature || platform.IsOpenSource) &&
                                        (string.IsNullOrWhiteSpace(publisherName) ||
                                         (failureDetails != null && (failureDetails.Contains("TRUST_E_NOSIGNATURE") || failureDetails.Contains("0x800B0100"))));

            if (isUnsignedOpenSource)
            {
                _logger?.LogInformation(
                    "Instalador de {Platform} é um software de código aberto sem assinatura comercial Authenticode. Procedência validada via HTTPS e domínio oficial autorizado.",
                    platform.Name);

                return new SecurityValidationResult(
                    IsValid: true,
                    IsAuthenticodeSigned: false,
                    SubjectPublisher: "[Código Aberto]",
                    Sha256Hash: sha256,
                    Details: "Binário oficial de código aberto validado com sucesso via conexão segura HTTPS e domínio homologado.",
                    ErrorMessage: null
                );
            }

            return new SecurityValidationResult(
                IsValid: false,
                IsAuthenticodeSigned: false,
                SubjectPublisher: publisherName,
                Sha256Hash: sha256,
                Details: failureDetails,
                ErrorMessage: $"Falha na verificação de assinatura digital Authenticode: {failureDetails}"
            );
        }

        // 5. Validação de Correspondência do Editor (ExpectedPublisher)
        if (!string.IsNullOrWhiteSpace(platform.ExpectedPublisher) && !string.IsNullOrWhiteSpace(publisherName))
        {
            bool publisherMatches = IsPublisherMatch(publisherName, platform.ExpectedPublisher);

            // Fundações e CAs comunitárias reconhecidas para distribuição de código aberto (ex: SignPath Foundation para Playnite)
            if (!publisherMatches && (platform.IsOpenSource || string.Equals(platform.Id, "playnite", StringComparison.OrdinalIgnoreCase)))
            {
                if (publisherName.Contains("SignPath Foundation", StringComparison.OrdinalIgnoreCase) ||
                    publisherName.Contains("Open Source Developer", StringComparison.OrdinalIgnoreCase) ||
                    publisherName.Contains("Certum", StringComparison.OrdinalIgnoreCase))
                {
                    publisherMatches = true;
                }
            }

            if (!publisherMatches)
            {
                return new SecurityValidationResult(
                    IsValid: false,
                    IsAuthenticodeSigned: true,
                    SubjectPublisher: publisherName,
                    Sha256Hash: sha256,
                    Details: $"Editor encontrado: '{publisherName}'. Esperado: '{platform.ExpectedPublisher}'",
                    ErrorMessage: $"Incompatibilidade de editor: O arquivo foi assinado por '{publisherName}', que difere do esperado ('{platform.ExpectedPublisher}')."
                );
            }
        }

        _logger?.LogInformation(
            "Instalador de {Platform} validado com sucesso. Editor: {Publisher}, SHA-256: {Hash}",
            platform.Name, publisherName, sha256);

        return new SecurityValidationResult(
            IsValid: true,
            IsAuthenticodeSigned: true,
            SubjectPublisher: publisherName,
            Sha256Hash: sha256,
            Details: $"Validado com sucesso via Authenticode por '{publisherName}'",
            ErrorMessage: null
        );
    }

    private static bool HasValidInstallerHeader(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 4) return false;

            var ext = Path.GetExtension(filePath);
            if (string.Equals(ext, ".msi", StringComparison.OrdinalIgnoreCase))
            {
                byte[] oleMagic = reader.ReadBytes(4);
                return oleMagic.Length == 4 &&
                       oleMagic[0] == 0xD0 &&
                       oleMagic[1] == 0xCF &&
                       oleMagic[2] == 0x11 &&
                       oleMagic[3] == 0xE0;
            }

            // Para arquivos .exe e executáveis PE: "MZ" em ASCII = 0x4D, 0x5A
            ushort magic = reader.ReadUInt16();
            if (magic == 0x5A4D) return true;

            // Se for arquivo com formato OLE/MSI
            stream.Seek(0, SeekOrigin.Begin);
            byte[] magic4 = reader.ReadBytes(4);
            return magic4.Length == 4 &&
                   magic4[0] == 0xD0 &&
                   magic4[1] == 0xCF &&
                   magic4[2] == 0x11 &&
                   magic4[3] == 0xE0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Compara de forma segura e resiliente o editor do certificado com o editor esperado do catálogo,
    /// tratando variações corporativas legítimas (ex.: "Amazon.com Services LLC" vs "Amazon Services LLC",
    /// "Epic Games, Inc." vs "Epic Games", "Valve Corp." vs "Valve").
    /// </summary>
    public static bool IsPublisherMatch(string? actualPublisher, string? expectedPublisher)
    {
        if (string.IsNullOrWhiteSpace(expectedPublisher)) return true;
        if (string.IsNullOrWhiteSpace(actualPublisher)) return false;

        // 1. Verificação direta de substring bidirecional
        if (actualPublisher.Contains(expectedPublisher, StringComparison.OrdinalIgnoreCase) ||
            expectedPublisher.Contains(actualPublisher, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Extração e correspondência de tokens semânticos ignorando sufixos corporativos
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inc", "inc.", "corp", "corp.", "corporation", "llc", "llc.", "ltd", "ltd.", "services", "gmbh", "sarl", "co", "co.", "company", "com", ".com"
        };

        static string[] ExtractSignificantTokens(string input, HashSet<string> stops)
        {
            var cleaned = new string(input.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
            return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(t => t.Length > 1 && !stops.Contains(t))
                          .ToArray();
        }

        var actualTokens = ExtractSignificantTokens(actualPublisher, stopWords);
        var expectedTokens = ExtractSignificantTokens(expectedPublisher, stopWords);

        if (expectedTokens.Length == 0 || actualTokens.Length == 0)
        {
            return false;
        }

        // Se todos os tokens relevantes esperados estiverem contidos no certificado real
        bool allExpectedInActual = expectedTokens.All(exp =>
            actualTokens.Any(act => act.Equals(exp, StringComparison.OrdinalIgnoreCase)));

        if (allExpectedInActual) return true;

        // Ou se todos os tokens do certificado estiverem contidos no esperado
        bool allActualInExpected = actualTokens.All(act =>
            expectedTokens.Any(exp => exp.Equals(act, StringComparison.OrdinalIgnoreCase)));

        return allActualInExpected;
    }
}

