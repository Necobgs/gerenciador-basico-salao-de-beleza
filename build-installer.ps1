<#
.SYNOPSIS
    Script automatizado para compilar o projeto e gerar o instalador do Gerenciador de Salão.
.DESCRIPTION
    1. Publica a aplicação .NET em modo Release Single-File Win-x64 (Self-Contained)
    2. Localiza o compilador do Inno Setup (ISCC.exe)
    3. Gera o arquivo executável de instalação na pasta Output\
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Gerador de Instalador - Karla Borges Centro de Estética " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verificar .NET SDK
Write-Host "`n[1/3] Verificando SDK do .NET..." -ForegroundColor Green
try {
    $dotnetVer = & dotnet --version
    Write-Host "  .NET SDK detectado: $dotnetVer" -ForegroundColor Gray
} catch {
    Write-Error "O comando 'dotnet' nao foi encontrado. Certifique-se de que o .NET SDK 10 esta instalado."
}

# 2. Localizar Inno Setup (ISCC.exe)
Write-Host "`n[2/3] Verificando Inno Setup..." -ForegroundColor Green
$isccPath = $null

$possiblePaths = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles (x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

foreach ($path in $possiblePaths) {
    if ($path -and (Test-Path $path)) {
        $isccPath = $path
        break
    }
}

if (-not $isccPath) {
    Write-Host "  Inno Setup nao foi encontrado no sistema." -ForegroundColor Yellow
    Write-Host "  Tentando instalar via winget..." -ForegroundColor Yellow
    try {
        & winget install --id JRSoftware.InnoSetup --accept-package-agreements --accept-source-agreements --silent
        $isccPath = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    } catch {
        Write-Error "Nao foi possivel instalar o Inno Setup automaticamente. Instale manualmente em https://jrsoftware.org/isinfo.php"
    }
}

if (-not (Test-Path $isccPath)) {
    Write-Error "ISCC.exe nao foi encontrado em: $isccPath"
}
Write-Host "  Compilador Inno Setup encontrado: $isccPath" -ForegroundColor Gray

# 3. Publicar aplicação .NET
Write-Host "`n[3/3] Compilando e gerando arquivos com 'dotnet publish'..." -ForegroundColor Green
$publishDir = Join-Path $PSScriptRoot "publish\win-x64"
$projectPath = Join-Path $PSScriptRoot "GerenciadorSalao\GerenciadorSalao.csproj"

& dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Falha na publicacao do projeto .NET."
}

# 4. Compilar instalador com Inno Setup
Write-Host "`n[+] Empacotando instalador com Inno Setup..." -ForegroundColor Green
$issFile = Join-Path $PSScriptRoot "installer.iss"
& $isccPath $issFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "Falha ao compilar o instalador Inno Setup."
}

$outputDir = Join-Path $PSScriptRoot "Output"
$installerFile = Get-ChildItem -Path $outputDir -Filter "*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($installerFile) {
    $sizeMb = [math]::Round($installerFile.Length / 1MB, 2)
    Write-Host "`n==========================================================" -ForegroundColor Cyan
    Write-Host "  INSTALADOR GERADO COM SUCESSO!" -ForegroundColor Green
    Write-Host "  Arquivo: $($installerFile.FullName)" -ForegroundColor White
    Write-Host "  Tamanho: $sizeMb MB" -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Cyan
} else {
    Write-Warning "Nenhum arquivo executavel encontrado na pasta Output."
}

