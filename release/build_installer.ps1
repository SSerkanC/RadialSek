param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$releaseRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $releaseRoot
$projectPath = Join-Path $projectRoot "radial_sek.csproj"
$scriptPath = Join-Path $releaseRoot "RadialSek_Setup.iss"
$publishDir = Join-Path $releaseRoot "publish"
$possibleIscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$iscc = $possibleIscc | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 bulunamadi. Lutfen Inno Setup 6 kurup tekrar deneyin."
}

if (-not $Version) {
    if (-not (Test-Path $projectPath)) {
        throw "Project file not found: $projectPath"
    }

    [xml]$projectXml = Get-Content $projectPath
    $Version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1

    if (-not $Version) {
        throw "Project version not found in: $projectPath"
    }
}

if (-not (Test-Path $scriptPath)) {
    throw "Installer scripti bulunamadi: $scriptPath"
}

if (-not (Test-Path $publishDir)) {
    throw "Publish klasoru bulunamadi. Once release\publish_release.ps1 calistirin."
}

Write-Host "Installer derleniyor. Surum: $Version"
& $iscc "/DMyAppVersion=$Version" $scriptPath

Write-Host "Tamamlandi. Cikti klasoru: $releaseRoot"
