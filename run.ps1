param(
    [switch]$SkipBuild,
    [switch]$Tests
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

if ($Tests) {
    Write-Host "Rodando testes unitarios..." -ForegroundColor Cyan
    dotnet test "$root\tests\TMHue.UnitTests\TMHue.UnitTests.csproj"
    exit $LASTEXITCODE
}

if (-not $SkipBuild) {
    Write-Host "Compilando TMHue.sln (Debug)..." -ForegroundColor Cyan
    dotnet build "$root\TMHue.sln" -c Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build falhou. Corrija os erros acima antes de rodar o app." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Host "Iniciando TMHue..." -ForegroundColor Green
dotnet run --project "$root\src\TMHue.App\TMHue.App.csproj" -c Debug
