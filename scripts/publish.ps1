# ==============================================================================
# GameLauncher Hub - Script de Publicacao e Empacotamento Windows
# ==============================================================================
[CmdletBinding()]
param (
    [switch]$SelfContained = $true,
    [switch]$SingleFile = $true,
    [switch]$ReadyToRun = $true,
    [switch]$BuildInstaller = $true,
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  GAMELAUNCHER HUB - PUBLICACAO E DISTRIBUICAO WINDOWS" -ForegroundColor Cyan
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
    exit 1
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "src\GameLauncherHub.UI\GameLauncherHub.UI.csproj"
$publishOutputDir = Join-Path $repoRoot "publish\$Runtime"
$installerOutputDir = Join-Path $repoRoot "publish\installer"

# Finaliza instâncias ativas do executável caso esteja aberto para permitir sobrescrita
Get-Process -Name "GameLauncherHub.UI" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

if (Test-Path $publishOutputDir) {
    try {
        Remove-Item $publishOutputDir -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Host "[AVISO] Não foi possível limpar totalmente a pasta '$publishOutputDir'. Verifique se o executável ainda está aberto." -ForegroundColor Yellow
    }
}
New-Item -ItemType Directory -Path $publishOutputDir -Force | Out-Null

# 2. Execucao do dotnet publish
Write-Host "`n--> Publicando executavel para $Runtime (SelfContained=$SelfContained, SingleFile=$SingleFile)..." -ForegroundColor Yellow

$publishArgs = @(
    "publish", $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "-m:1",
    "-nr:false",
    "-p:UseSharedCompilation=false",
    "--output", $publishOutputDir
)

if ($SingleFile) {
    $publishArgs += "-p:PublishSingleFile=true"
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

if ($ReadyToRun) {
    $publishArgs += "-p:PublishReadyToRun=true"
}

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

# 3. Copia de Ativos de Suporte e Recursos da Aplicacao
Write-Host "`n--> Copiando recursos complementares para distribuicao..." -ForegroundColor Yellow

$resourcesDir = Join-Path $publishOutputDir "Resources"
New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null

$logoPng = Join-Path $repoRoot "src\GameLauncherHub.UI\Resources\logo.png"
if (Test-Path $logoPng) {
    Copy-Item $logoPng -Destination $resourcesDir -Force
}

if (Test-Path (Join-Path $repoRoot "icon.ico")) {
    Copy-Item (Join-Path $repoRoot "icon.ico") -Destination $publishOutputDir -Force
    Copy-Item (Join-Path $repoRoot "icon.ico") -Destination $resourcesDir -Force
}

$catalogSource = Join-Path $repoRoot "src\GameLauncherHub.Infrastructure\Catalog\catalog.json"
$catalogDestDir = Join-Path $publishOutputDir "Catalog"
if (Test-Path $catalogSource) {
    New-Item -ItemType Directory -Path $catalogDestDir -Force | Out-Null
    Copy-Item $catalogSource -Destination $catalogDestDir -Force
}

# Limpeza opcional de arquivos .pdb de depuracao
Get-ChildItem -Path $publishOutputDir -Filter "*.pdb" | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "[OK] Publicacao concluida com sucesso!" -ForegroundColor Green
Write-Host "Pasta de publicacao: $publishOutputDir" -ForegroundColor White

$exePath = Join-Path $publishOutputDir "GameLauncherHub.UI.exe"
if (Test-Path $exePath) {
    $fileSizeMB = [math]::Round(((Get-Item $exePath).Length / 1MB), 2)
    Write-Host "Executavel standalone: GameLauncherHub.UI.exe ($fileSizeMB MB)" -ForegroundColor Cyan
}

# 4. Compilacao automatica do Instalador Inno Setup (se habilitado)
if ($BuildInstaller) {
    Write-Host "`n--> Verificando compilador Inno Setup..." -ForegroundColor Yellow

    $innoCompilerPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
    )

    $iscc = $null
    foreach ($path in $innoCompilerPaths) {
        if (Test-Path $path) {
            $iscc = $path
            break
        }
    }

    if (-not $iscc) {
        $cmdCheck = Get-Command "iscc" -ErrorAction SilentlyContinue
        if ($cmdCheck) {
            $iscc = $cmdCheck.Source
        }
    }

    if ($iscc) {
        Write-Host "[OK] Inno Setup localizado em: $iscc" -ForegroundColor Green
        Write-Host "--> Compilando instalador oficial Windows (Inno Setup)..." -ForegroundColor Yellow

        if (-not (Test-Path $installerOutputDir)) {
            New-Item -ItemType Directory -Path $installerOutputDir -Force | Out-Null
        }

        $issPath = Join-Path $repoRoot "installer\GameLauncherHub.iss"
        & $iscc $issPath

        if ($LASTEXITCODE -eq 0) {
            Write-Host "`n[SUCESSO] Instalador gerado com sucesso em: $installerOutputDir" -ForegroundColor Green
            Get-ChildItem -Path $installerOutputDir -Filter "*.exe" | ForEach-Object {
                $sizeMB = [math]::Round(($_.Length / 1MB), 2)
                Write-Host "Instalador Setup: $($_.Name) ($sizeMB MB)" -ForegroundColor Cyan
            }
        } else {
            Write-Host "[AVISO] Falha ao compilar o script Inno Setup." -ForegroundColor Yellow
        }
    } else {
        Write-Host "`n[INFO] Inno Setup 6 (ISCC.exe) nao foi detectado no caminho padrao." -ForegroundColor Yellow
        Write-Host "O executavel standalone GameLauncherHub.UI.exe ja esta 100% pronto para uso em:" -ForegroundColor White
        Write-Host "  $publishOutputDir\GameLauncherHub.UI.exe" -ForegroundColor Cyan
        Write-Host "`nPara gerar o instalador Setup.exe:" -ForegroundColor Gray
        Write-Host "1. Baixe o Inno Setup 6 em: https://jrsoftware.org/isdl.php" -ForegroundColor Gray
        Write-Host "2. Execute o instalador ou reexecute este script." -ForegroundColor Gray
    }
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  PROCESSAMENTO DE DISTRIBUICAO CONCLUIDO!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
