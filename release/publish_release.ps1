param(
    [string]$OutputRoot = $PSScriptRoot,
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "radial_sek.csproj"
$publishDir = Join-Path $OutputRoot "publish"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

Write-Host "Publish klasoru hazirlaniyor: $publishDir"
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
Get-ChildItem -Force -Path $publishDir | Remove-Item -Recurse -Force

Write-Host "Release publish aliniyor..."
dotnet publish $projectPath `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    /p:PublishSingleFile=false

Write-Host "Hazir. Cikti klasoru: $OutputRoot"
