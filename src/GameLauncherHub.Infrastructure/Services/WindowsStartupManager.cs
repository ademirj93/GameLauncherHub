using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameLauncherHub.Infrastructure.Services;

/// <summary>
/// Gerenciador de inicialização automática do Windows via chave HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public static class WindowsStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "GameLauncherHub";

    public static bool IsLaunchOnStartupEnabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetLaunchOnStartup(bool enable, ILogger? logger = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                    logger?.LogInformation("Inicialização automática com o Windows configurada: {Path}", exePath);
                }
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                    logger?.LogInformation("Inicialização automática com o Windows removida.");
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Erro ao configurar inicialização automática no registro do Windows.");
        }
    }
}

