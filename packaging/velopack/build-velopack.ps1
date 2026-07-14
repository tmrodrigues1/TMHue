# Empacota o TMHue com Velopack (substitui o antigo instalador Inno Setup).
# Gera Setup.exe (one-click, por usuário, sem UAC), pacote completo .nupkg, delta quando a
# release anterior estiver em -ReleasesDir, e releases.win.json (feed de atualização).
#
# Uso local (sem assinatura):
#   .\packaging\velopack\build-velopack.ps1 -Version 1.0.0 -SkipSigning
#
# Teste visual da splash (payload inflado para o Setup.exe ficar visível por mais tempo;
# NUNCA usar o resultado como instalador final):
#   .\packaging\velopack\build-velopack.ps1 -Version 1.0.0 -SkipSigning -SplashTestPadMB 300
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\..\artifacts\velopack'),

    # Comando de assinatura Authenticode passado ao vpk (ex.: 'signtool sign /fd sha256 ...').
    [string]$SignToolCommand,

    [switch]$SkipSigning,

    # Apenas para teste visual da splash: adiciona um arquivo aleatório de N MB ao payload.
    [ValidateRange(0, 1024)]
    [int]$SplashTestPadMB = 0
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($SkipSigning -and $SignToolCommand) {
    throw 'Use -SkipSigning ou -SignToolCommand, não ambos.'
}
if (-not $SkipSigning -and [string]::IsNullOrWhiteSpace($SignToolCommand)) {
    throw 'A assinatura é obrigatória para release. Informe -SignToolCommand ou use -SkipSigning somente para testes locais.'
}

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw 'A CLI do Velopack não foi encontrada. Instale com: dotnet tool install -g vpk'
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repositoryRoot 'src\TMHue.App\TMHue.App.csproj'
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\velopack-$Version"
$releaseDirectory = Join-Path $OutputRoot $Version
$splash = Join-Path $PSScriptRoot 'splash.gif'
$icon = Join-Path $repositoryRoot 'src\TMHue.App\Assets\tray-icon.ico'

if (Test-Path $publishDirectory) { Remove-Item -Recurse -Force $publishDirectory }
New-Item -ItemType Directory -Force -Path $publishDirectory, $releaseDirectory | Out-Null

# Velopack exige publish multi-arquivo (sem PublishSingleFile).
& dotnet publish $project --configuration Release --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:Version="$Version.0" -p:InformationalVersion=$Version `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o TMHue.' }

if ($SplashTestPadMB -gt 0) {
    Write-Warning "Payload inflado em $SplashTestPadMB MB apenas para teste visual da splash. NÃO distribua este build."
    $pad = Join-Path $publishDirectory 'splash-test-padding.bin'
    $bytes = [byte[]]::new(1MB)
    $rng = [System.Random]::new()
    $stream = [System.IO.File]::OpenWrite($pad)
    try {
        for ($i = 0; $i -lt $SplashTestPadMB; $i++) { $rng.NextBytes($bytes); $stream.Write($bytes, 0, $bytes.Length) }
    } finally { $stream.Dispose() }
}

# packId exclusivo: instala em %LocalAppData%\com.thiagorodrigues.TMHue, sem conflitar com
# %LocalAppData%\TMHue (configurações/histórico do usuário).
$vpkArgs = @(
    'pack',
    '--packId', 'com.thiagorodrigues.TMHue',
    '--packVersion', $Version,
    '--packDir', $publishDirectory,
    '--mainExe', 'TMHue.exe',
    '--packTitle', 'TMHue',
    '--packAuthors', 'Thiago Rodrigues',
    '--icon', $icon,
    '--splashImage', $splash,
    '--splashProgressColor', 'None',
    '--outputDir', $releaseDirectory,
    '--shortcuts', 'Desktop,StartMenuRoot'
)
if (-not $SkipSigning) {
    $vpkArgs += @('--signParams', $SignToolCommand)
}

& vpk @vpkArgs
if ($LASTEXITCODE -ne 0) { throw 'Falha ao empacotar com o Velopack.' }

Get-ChildItem $releaseDirectory | ForEach-Object {
    "{0,12:N0} KB  {1}" -f ($_.Length / 1KB), $_.Name | Write-Host
}
Write-Host "`nArtefatos do Velopack em: $releaseDirectory"
Write-Host 'Setup: instalação one-click por usuário, sem UAC, abre o TMHue ao concluir.'
