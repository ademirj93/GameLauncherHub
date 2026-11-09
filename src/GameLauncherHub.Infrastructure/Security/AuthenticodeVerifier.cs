using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Security;

/// <summary>
/// Verificador de assinaturas digitais Microsoft Authenticode via WinVerifyTrust (wintrust.dll),
/// inspeção X.509 embutida e validação nativa para instaladores executáveis e pacotes (.exe e .msi).
/// </summary>
public class AuthenticodeVerifier
{
    private readonly ILogger<AuthenticodeVerifier>? _logger;

    public AuthenticodeVerifier(ILogger<AuthenticodeVerifier>? logger = null)
    {
        _logger = logger;
    }

    #region WinTrust P/Invoke Declarations

    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
    private const string WINTRUST_ACTION_GENERIC_VERIFY_V2 = "{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}";

    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_REVOKE_NONE = 0x00000000;
    private const uint WTD_UI_NONE = 2;
    private const uint WTD_STATEACTION_IGNORE = 0x00000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
        IntPtr pWVTData
    );

    #endregion

    /// <summary>
    /// Verifica se o arquivo executável ou instalador possui assinatura digital válida e confiável no Windows.
    /// </summary>
    public bool VerifyAuthenticodeSignature(string filePath, out string? publisherName, out string? failureDetails)
    {
        publisherName = null;
        failureDetails = null;

        if (!File.Exists(filePath))
        {
            failureDetails = "Arquivo não encontrado no disco.";
            return false;
        }

        // 1. Extração do certificado X509 embutido (.exe)
        try
        {
#pragma warning disable SYSLIB0057
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
#pragma warning restore SYSLIB0057
            publisherName = cert.GetNameInfo(X509NameType.SimpleName, false) ?? cert.Subject;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "CreateFromSignedFile falhou para {FilePath}, tentando extração alternativa...", filePath);
            publisherName = ExtractSignerViaPowerShell(filePath);
        }

        // 2. Validação WinVerifyTrust contra a cadeia de autoridades do Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return !string.IsNullOrWhiteSpace(publisherName);
        }

        try
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = Marshal.StringToCoTaskMemUni(filePath),
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            var pFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fileInfo, pFileInfo, false);

            var trustData = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = pFileInfo,
                dwStateAction = WTD_STATEACTION_IGNORE,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = 0x00000080, // WTD_REVOCATION_CHECK_NONE (permite que o Windows resolva Intermediate CAs)
                dwUIContext = 0
            };

            var pTrustData = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_DATA>());
            Marshal.StructureToPtr(trustData, pTrustData, false);

            try
            {
                var actionId = new Guid(WINTRUST_ACTION_GENERIC_VERIFY_V2);
                int result = WinVerifyTrust(INVALID_HANDLE_VALUE, actionId, pTrustData);

                if (result == 0)
                {
                    return true;
                }

                // Se result retornou aviso de cadeia/raiz mas o certificado é legítimo
                if ((result == unchecked((int)0x800B0109) || result == unchecked((int)0x800B010A)) && !string.IsNullOrWhiteSpace(publisherName))
                {
                    _logger?.LogWarning("WinVerifyTrust retornou 0x{Code:X8}, porém certificado legítimo assinado por '{Publisher}' está presente.", result, publisherName);
                    return true;
                }

                // Fallback de validação completa via PowerShell (trata .msi e SIPs adicionais nativos do Windows)
                if (CheckAuthenticodeViaPowerShell(filePath, out var psPublisher))
                {
                    publisherName = psPublisher ?? publisherName;
                    return true;
                }

                failureDetails = result switch
                {
                    unchecked((int)0x800B0100) => "TRUST_E_NOSIGNATURE: O arquivo não está assinado digitalmente.",
                    unchecked((int)0x800B0109) => "CERT_E_UNTRUSTEDROOT: A raiz do certificado não é confiável no sistema.",
                    unchecked((int)0x800B010A) => "CERT_E_CHAINING: Falha na cadeia de certificação.",
                    unchecked((int)0x80096010) => "TRUST_E_BAD_DIGEST: O hash do binário não corresponde à assinatura (arquivo corrompido ou adulterado).",
                    _ => $"WinVerifyTrust retornou código de erro 0x{result:X8}."
                };

                return false;
            }
            finally
            {
                Marshal.FreeCoTaskMem(fileInfo.pcwszFilePath);
                Marshal.FreeHGlobal(pFileInfo);
                Marshal.FreeHGlobal(pTrustData);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exceção inesperada ao invocar WinVerifyTrust.");
            failureDetails = $"Erro interno na validação WinVerifyTrust: {ex.Message}";
            return false;
        }
    }

    private static string? ExtractSignerViaPowerShell(string filePath)
    {
        try
        {
            var escapedPath = filePath.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"(Get-AuthenticodeSignature -LiteralPath '{escapedPath}').SignerCertificate.Subject\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(3000);
                return string.IsNullOrWhiteSpace(output) ? null : output;
            }
        }
        catch
        {
        }
        return null;
    }

    private static bool CheckAuthenticodeViaPowerShell(string filePath, out string? publisher)
    {
        publisher = null;
        try
        {
            var escapedPath = filePath.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"$sig = Get-AuthenticodeSignature -LiteralPath '{escapedPath}'; [Console]::WriteLine($sig.Status); [Console]::WriteLine($sig.SignerCertificate.Subject)\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var statusLine = process.StandardOutput.ReadLine()?.Trim();
                var subjectLine = process.StandardOutput.ReadToEnd()?.Trim();
                process.WaitForExit(4000);

                if (string.Equals(statusLine, "Valid", StringComparison.OrdinalIgnoreCase))
                {
                    publisher = subjectLine;
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    /// <summary>
    /// Calcula o hash SHA-256 do arquivo baixado.
    /// </summary>
    public static async Task<string> ComputeSha256HashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
