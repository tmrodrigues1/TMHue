[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    # Apenas para teste visual da splash do Setup.exe (payload inflado; não distribuir).
    [ValidateRange(0, 1024)]
    [int]$SplashTestPadMB = 0
)

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'packaging\velopack\build-velopack.ps1'

& $script -Version $Version -SkipSigning -SplashTestPadMB $SplashTestPadMB

$installer = Join-Path $PSScriptRoot "artifacts\velopack\$Version\com.thiagorodrigues.TMHue-win-Setup.exe"
Write-Host "Instalador pronto: $installer"
Write-Host 'Abra esse arquivo para iniciar a instalação one-click do TMHue.'
