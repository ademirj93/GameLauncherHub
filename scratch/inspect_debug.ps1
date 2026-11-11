$proc = Start-Process -FilePath "src\GameLauncherHub.UI\bin\Debug\net10.0-windows\GameLauncherHub.UI.exe" -PassThru
Start-Sleep -Seconds 4
$p = Get-Process -Id $proc.Id
[PSCustomObject]@{
    Mode = "Debug (Framework-dependent, non-single-file)"
    WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 2)
    PrivateMemoryMB = [math]::Round($p.PrivateMemorySize64 / 1MB, 2)
} | Format-List
Stop-Process -Id $proc.Id -Force

