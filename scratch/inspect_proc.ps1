$proc = Start-Process -FilePath "publish\win-x64\GameLauncherHub.UI.exe" -PassThru
Start-Sleep -Seconds 4
$p = Get-Process -Id $proc.Id
[PSCustomObject]@{
    WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 2)
    PrivateMemoryMB = [math]::Round($p.PrivateMemorySize64 / 1MB, 2)
    PeakWorkingSetMB = [math]::Round($p.PeakWorkingSet64 / 1MB, 2)
    VirtualMemoryMB = [math]::Round($p.VirtualMemorySize64 / 1MB, 2)
    PagedMemoryMB = [math]::Round($p.PagedMemorySize64 / 1MB, 2)
} | Format-List
Stop-Process -Id $proc.Id -Force

