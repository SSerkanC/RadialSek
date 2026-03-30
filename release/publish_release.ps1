param(
    [string]$OutputRoot = "C:\Users\salih\OneDrive\Desktop\Kendi Yaptığım Programlar\Radial Menü\Radial Sek"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $OutputRoot "publish"

Write-Host "Publish klasoru hazirlaniyor: $publishDir"
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
Get-ChildItem -Force -Path $publishDir | Remove-Item -Recurse -Force

Write-Host "Release publish aliniyor..."
dotnet publish (Join-Path $projectRoot "radial_sek.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir `
    /p:PublishSingleFile=false

Write-Host "Release notlari ve installer scripti kopyalaniyor..."
Copy-Item (Join-Path $PSScriptRoot "RadialSek_Setup.iss") $OutputRoot -Force
Copy-Item (Join-Path $PSScriptRoot "RELEASE_NOTLARI.txt") $OutputRoot -Force

Write-Host "Hazir. Cikti klasoru: $OutputRoot"
