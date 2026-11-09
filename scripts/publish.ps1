# ==============================================================================
# GameLauncher Hub - Script de Publicacao para Distribuicao no Windows
# ==============================================================================
[CmdletBinding()]
param (
    [switch]$SelfContained = $true,
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  GAMELAUNCHER HUB - PUBLICACAO E DISTRIBUICAO WINDOWS" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$dotnet = "dotnet"
if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    $dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
    $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
}

$projectPath = Join-Path $PSScriptRoot "..\src\GameLauncherHub.UI\GameLauncherHub.UI.csproj"
$publishOutputDir = Join-Path $PSScriptRoot "..\publish\$Runtime"

if (Test-Path $publishOutputDir) {
    Remove-Item $publishOutputDir -Recurse -Force
}

Write-Host "`n--> Publicando executavel para $Runtime (SelfContained=$SelfContained)..." -ForegroundColor Yellow

$publishArgs = @(
    "publish", $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:PublishReadyToRun=true",
    "--output", $publishOutputDir
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
} else {
    $publishArgs += "--self-contained"
    $publishArgs += "false"
}

& $dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERRO] Falha ao publicar o projeto." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n[OK] Publicacao concluida com sucesso!" -ForegroundColor Green
Write-Host "Arquivos gerados em: $publishOutputDir" -ForegroundColor White

$exePath = Join-Path $publishOutputDir "GameLauncherHub.UI.exe"
if (Test-Path $exePath) {
    $fileSizeMB = [math]::Round(((Get-Item $exePath).Length / 1MB), 2)
    Write-Host "Executavel principal: GameLauncherHub.UI.exe ($fileSizeMB MB)" -ForegroundColor Cyan
}

Write-Host "`nInstrucoes pos-publicacao:" -ForegroundColor Yellow
Write-Host "1. Assinar digitalmente o executavel com SignTool." -ForegroundColor Gray
Write-Host "2. O executavel pode ser distribuido diretamente ou empacotado via Inno Setup." -ForegroundColor Gray
