# Teste manual da experiência de instalação do TMHue (Velopack).
#
# Gera um Setup.exe com payload inflado (para a splash "Color Scan" ficar visível por tempo
# suficiente), executa a instalação real e mostra um relatório:
# pasta de instalação, atalhos, entrada em "Aplicativos instalados" e preservação dos dados
# do usuário. NUNCA distribua o build gerado por este script.
#
# Uso:
#   .\packaging\velopack\test-installer.ps1                 # build + instala + relatório
#   .\packaging\velopack\test-installer.ps1 -SkipBuild      # reusa o último build de teste
#   .\packaging\velopack\test-installer.ps1 -PadMB 0        # instalador real (rápido demais p/ ver a splash)
#   .\packaging\velopack\test-installer.ps1 -Uninstall      # desinstala e verifica a limpeza
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    # Payload extra para a splash ficar visível (~300 MB ≈ alguns segundos de instalação).
    [ValidateRange(0, 1024)]
    [int]$PadMB = 300,

    [switch]$SkipBuild,

    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packId = 'com.thiagorodrigues.TMHue'
$installDir = Join-Path $env:LOCALAPPDATA $packId
$dataDir = Join-Path $env:LOCALAPPDATA 'TMHue'
$setup = Join-Path $PSScriptRoot "..\..\artifacts\velopack\$Version\$packId-win-Setup.exe"

function Write-Check([bool]$ok, [string]$label) {
    $mark = if ($ok) { '[OK]  ' } else { '[FALHA]' }
    $color = if ($ok) { 'Green' } else { 'Red' }
    Write-Host "$mark $label" -ForegroundColor $color
}

function Get-InstalledEntry {
    Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ItemProperty $_.PSPath } |
        Where-Object { $_.PSObject.Properties['DisplayName'] -and $_.DisplayName -eq 'TMHue' } |
        Select-Object -First 1
}

# ---------------------------------------------------------------- desinstalação
if ($Uninstall) {
    $updateExe = Join-Path $installDir 'Update.exe'
    if (-not (Test-Path $updateExe)) {
        Write-Host "TMHue (Velopack) não está instalado em $installDir — nada a desinstalar."
        exit 0
    }

    Write-Host 'Encerrando o TMHue, se estiver aberto…'
    Get-Process TMHue -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false

    Write-Host 'Desinstalando…'
    Start-Process $updateExe -ArgumentList '--uninstall' -Wait

    Start-Sleep -Seconds 2
    Write-Host "`n=== Verificação pós-desinstalação ===" -ForegroundColor Cyan
    Write-Check (-not (Test-Path (Join-Path $installDir 'current'))) "Pasta de instalação removida ($installDir)"
    Write-Check ($null -eq (Get-InstalledEntry)) 'Entrada removida de "Aplicativos instalados"'
    Write-Check (Test-Path $dataDir) "Dados do usuário PRESERVADOS ($dataDir)"
    exit 0
}

# ---------------------------------------------------------------- build
if (-not $SkipBuild) {
    Write-Host "Gerando instalador de teste (payload +$PadMB MB para a splash ficar visível)…" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'build-velopack.ps1') -Version $Version -SkipSigning -SplashTestPadMB $PadMB
}

if (-not (Test-Path $setup)) {
    throw "Setup não encontrado: $setup (rode sem -SkipBuild)."
}

# ---------------------------------------------------------------- instalação
$hadDataBefore = Test-Path $dataDir

Write-Host "`nAbrindo o Setup.exe — observe a experiência:" -ForegroundColor Cyan
Write-Host '  1. Splash "Color Scan" (logo, faixa de amostras, varredura azul-violeta em loop)'
Write-Host '  2. Nenhuma barra de progresso (desabilitada via --splashProgressColor None)'
Write-Host '  3. Nenhum prompt de UAC (instalação por usuário)'
Write-Host '  4. TMHue abre automaticamente ao concluir'
Write-Host ''

$sw = [System.Diagnostics.Stopwatch]::StartNew()
Start-Process $setup -Wait
$sw.Stop()

# O Setup retorna quando termina; o app abre em seguida. Dá um instante para o processo subir.
Start-Sleep -Seconds 3

Write-Host "`n=== Relatório da instalação ===" -ForegroundColor Cyan
Write-Host ("Tempo de instalação (janela do Setup): {0:N1} s" -f $sw.Elapsed.TotalSeconds)
Write-Host ("Tamanho do Setup.exe: {0:N1} MB" -f ((Get-Item $setup).Length / 1MB))

$exe = Join-Path $installDir 'current\TMHue.exe'
Write-Check (Test-Path $exe) "Instalado em $installDir\current"
Write-Check ($null -ne (Get-Process TMHue -ErrorAction SilentlyContinue)) 'TMHue abriu automaticamente'
Write-Check ($null -ne (Get-InstalledEntry)) 'Aparece em Configurações > Aplicativos instalados'

$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'TMHue.lnk'
$startMenuShortcut = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\TMHue.lnk'
Write-Check (Test-Path $desktopShortcut) "Atalho na área de trabalho"
Write-Check (Test-Path $startMenuShortcut) "Atalho no Menu Iniciar"

if ($hadDataBefore) {
    Write-Check (Test-Path $dataDir) "Dados do usuário preservados ($dataDir)"
}

if ($PadMB -gt 0) {
    Write-Host "`nLembrete: este build tem $PadMB MB de payload falso — só para avaliar a experiência." -ForegroundColor Yellow
}
Write-Host "Para desinstalar e verificar a limpeza: .\packaging\velopack\test-installer.ps1 -Uninstall"
