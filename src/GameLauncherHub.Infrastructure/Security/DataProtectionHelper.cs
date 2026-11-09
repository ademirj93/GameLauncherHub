using System.Runtime.InteropServices;
using System.Text;

namespace GameLauncherHub.Infrastructure.Security;

/// <summary>
/// Utilitário para proteção criptográfica de dados sensíveis locais (como API Keys)
/// utilizando a API DPAPI nativa do Windows (CryptProtectData / CryptUnprotectData em crypt32.dll).
/// </summary>
public static class DataProtectionHelper
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GameLauncherHub_Entropy_v1");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static string ProtectString(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        var inBlob = new DATA_BLOB();
        var entropyBlob = new DATA_BLOB();
        var outBlob = new DATA_BLOB();

        try
        {
            inBlob.cbData = plainBytes.Length;
            inBlob.pbData = Marshal.AllocHGlobal(plainBytes.Length);
            Marshal.Copy(plainBytes, 0, inBlob.pbData, plainBytes.Length);

            entropyBlob.cbData = Entropy.Length;
            entropyBlob.pbData = Marshal.AllocHGlobal(Entropy.Length);
            Marshal.Copy(Entropy, 0, entropyBlob.pbData, Entropy.Length);

            if (CryptProtectData(ref inBlob, "GameLauncherHub", ref entropyBlob, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob))
            {
                byte[] cipherBytes = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, cipherBytes, 0, outBlob.cbData);
                return Convert.ToBase64String(cipherBytes);
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
            if (entropyBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entropyBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }

    public static string UnprotectString(string cipherTextBase64)
    {
        if (string.IsNullOrWhiteSpace(cipherTextBase64))
        {
            return string.Empty;
        }

        byte[] cipherBytes;
        try
        {
            cipherBytes = Convert.FromBase64String(cipherTextBase64);
        }
        catch
        {
            return string.Empty;
        }

        var inBlob = new DATA_BLOB();
        var entropyBlob = new DATA_BLOB();
        var outBlob = new DATA_BLOB();

        try
        {
            inBlob.cbData = cipherBytes.Length;
            inBlob.pbData = Marshal.AllocHGlobal(cipherBytes.Length);
            Marshal.Copy(cipherBytes, 0, inBlob.pbData, cipherBytes.Length);

            entropyBlob.cbData = Entropy.Length;
            entropyBlob.pbData = Marshal.AllocHGlobal(Entropy.Length);
            Marshal.Copy(Entropy, 0, entropyBlob.pbData, Entropy.Length);

            if (CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob))
            {
                byte[] plainBytes = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, plainBytes, 0, outBlob.cbData);
                return Encoding.UTF8.GetString(plainBytes);
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
            if (entropyBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entropyBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }
}
