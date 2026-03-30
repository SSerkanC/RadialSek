param(
    [string]$ReleaseRoot = "C:\Users\salih\OneDrive\Desktop\Kendi Yaptığım Programlar\Radial Menü\Radial Sek"
)

$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $ReleaseRoot "RadialSek_Setup.iss"
$possibleIscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$iscc = $possibleIscc | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 bulunamadi. Lutfen Inno Setup 6 kurup tekrar deneyin."
}

if (-not (Test-Path $scriptPath)) {
    throw "Installer scripti bulunamadi: $scriptPath"
}

Write-Host "Installer derleniyor..."
& $iscc $scriptPath

Write-Host "Tamamlandi. Cikti klasoru: $ReleaseRoot"
