# Builds the Rivet installer: self-contained publish + Inno Setup -> artifacts\Rivet-Setup-<ver>.exe
# Usage:  installer\build.ps1 [-Version 0.1.0]
param([string] $Version = '0.2.1')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Write-Host "Publishing self-contained win-x64 ($Version)..."
dotnet publish "$root\src\Rivet.App\Rivet.App.csproj" -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -o "$root\publish"

$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { throw 'Inno Setup (ISCC.exe) not found. Install it: winget install JRSoftware.InnoSetup' }

Write-Host 'Compiling installer...'
& $iscc "/DMyAppVersion=$Version" "$root\installer\Rivet.iss"
Write-Host "Done -> $root\artifacts"
