Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinUtil {
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

$proc = Start-Process -FilePath "publish\win-x64\GameLauncherHub.UI.exe" -PassThru
Start-Sleep -Seconds 3

$p = Get-Process -Id $proc.Id
Write-Host "1. Janela Ativa (Primeiro Plano):"
Write-Host "   WorkingSet: $([math]::Round($p.WorkingSet64/1MB, 2)) MB | PrivateMemory: $([math]::Round($p.PrivateMemorySize64/1MB, 2)) MB"

# Minimizar janela para disparar StateChanged / Hide / TrimMemory
[WinUtil]::ShowWindow($p.MainWindowHandle, 6) # SW_MINIMIZE = 6
Start-Sleep -Seconds 2

$p.Refresh()
Write-Host "`n2. Janela Minimizada / Em Segundo Plano (Apos Trim e EmptyWorkingSet):"
Write-Host "   WorkingSet: $([math]::Round($p.WorkingSet64/1MB, 2)) MB | PrivateMemory: $([math]::Round($p.PrivateMemorySize64/1MB, 2)) MB"

Stop-Process -Id $proc.Id -Force

