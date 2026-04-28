param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$project = Join-Path $repoRoot "src\Posiflora.Recovery.App\Posiflora.Recovery.App.csproj"
$output = Join-Path $repoRoot "artifacts\publish\POS_Radar-$Runtime"

if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = "dotnet"
}

& $dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $output

Write-Host ""
Write-Host "Published: $output\Posiflora.Recovery.App.exe"
Write-Host "Run it by double-clicking the exe file."
