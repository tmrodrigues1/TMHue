[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\artifacts'),

    [string]$InnoSetupCompiler,

    [string]$SignToolCommand,

    [switch]$SkipSigning
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($SkipSigning -and $SignToolCommand) {
    throw 'Use -SkipSigning ou -SignToolCommand, não ambos.'
}

if (-not $SkipSigning -and [string]::IsNullOrWhiteSpace($SignToolCommand)) {
    throw 'A assinatura é obrigatória para release. Informe -SignToolCommand ou use -SkipSigning somente para testes locais.'
}

if (-not $InnoSetupCompiler) {
    $candidates = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    $InnoSetupCompiler = $candidates | Select-Object -First 1
}

if (-not $InnoSetupCompiler -or -not (Test-Path -LiteralPath $InnoSetupCompiler)) {
    throw 'Inno Setup 6 não foi encontrado. Instale-o ou informe -InnoSetupCompiler com o caminho do ISCC.exe.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src\TMHue.App\TMHue.App.csproj'
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\$Version"
$releaseDirectory = Join-Path $OutputRoot $Version
$installerScript = Join-Path $PSScriptRoot 'inno\TMHue.iss'

New-Item -ItemType Directory -Force -Path $publishDirectory, $releaseDirectory | Out-Null

& dotnet publish $project --configuration Release --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=false --output $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o TMHue.' }

$arguments = @(
    "/DAppVersion=$Version",
    "/DPublishDir=$publishDirectory",
    "/DOutputDir=$releaseDirectory"
)

if (-not $SkipSigning) {
    $arguments += '/DSignTool'
    $arguments += "/Sproduction=$SignToolCommand"
}

$arguments += $installerScript
& $InnoSetupCompiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o instalador.' }

$installer = Join-Path $releaseDirectory "TMHue-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw "Instalador não encontrado: $installer" }

$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
"$hash  $(Split-Path -Leaf $installer)" | Set-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt') -Encoding ascii

Write-Host "Instalador gerado: $installer"
Write-Host "SHA-256: $hash"
