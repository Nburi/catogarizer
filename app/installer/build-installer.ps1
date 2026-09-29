<#
  Builds the self-contained app and wraps it into app/installer/Output/Catogarizer-Setup.exe.
  Needs the .NET SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).
  Usage: .\build-installer.ps1 -Version 1.0.0
#>
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$appRoot = Split-Path -Parent $PSScriptRoot

dotnet publish "$appRoot\src\Catogarizer.App\Catogarizer.App.csproj" -c Release -p:PublishProfile=win-x64-selfcontained
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$iscc = @(
  (Get-Command iscc -ErrorAction SilentlyContinue).Source,
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }

& $iscc "/DAppVersion=$Version" "$PSScriptRoot\Catogarizer.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }

Write-Host "Done: $PSScriptRoot\Output\Catogarizer-Setup.exe"
