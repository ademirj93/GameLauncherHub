# ==============================================================================
# GameLauncher Hub - Script de Compilacao e Testes Automatizados
# ==============================================================================
[CmdletBinding()]
param (
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  GAMELAUNCHER HUB - SCRIPT DE BUILD E TESTES" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Localizacao do executavel dotnet
$dotnet = "dotnet"
if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    $dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
    $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
}

try {
    $dotnetVersion = & $dotnet --version
    Write-Host "[OK] .NET SDK detectado: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "[ERRO] .NET SDK nao foi encontrado no PATH do sistema." -ForegroundColor Red
    Write-Host "Por favor, instale o .NET 10 SDK antes de prosseguir." -ForegroundColor Yellow
    exit 1
}

$solutionPath = Join-Path $PSScriptRoot "..\GameLauncherHub.sln"

# 2. Restauracao de Dependencias
Write-Host "`n--> Restaurando pacotes NuGet..." -ForegroundColor Yellow
& $dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERRO] Falha ao restaurar pacotes NuGet." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 3. Compilacao da Solucao
Write-Host "`n--> Compilando a solucao ($Configuration)..." -ForegroundColor Yellow
& $dotnet build $solutionPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERRO] Falha durante a compilacao." -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "[OK] Solucao compilada com sucesso!" -ForegroundColor Green

# 4. Execucao dos Testes Automatizados
$testProjectPath = Join-Path $PSScriptRoot "..\tests\GameLauncherHub.UnitTests\GameLauncherHub.UnitTests.csproj"
Write-Host "`n--> Executando suite de testes unitarios..." -ForegroundColor Yellow
& $dotnet test $testProjectPath --configuration $Configuration --no-build --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERRO] Um ou mais testes falharam." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  SUCESSO: Todos os testes passaram e o build esta concluido!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
