<#
  Uploads the website (and optionally the installer) to the server via scp.
  Usage: .\deploy.ps1 -Server user@host [-WithInstaller] [-RemoteRoot /var/www/catogarizer]
  Needs an SSH key login to the server. The remote folders public/ and downloads/ must exist and be writable.
  Build the installer first with app/installer/build-installer.ps1.
#>
param(
  [Parameter(Mandatory)][string]$Server,
  [string]$RemoteRoot = "/var/www/catogarizer",
  [switch]$WithInstaller
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

scp -r "$repo\website\public\*" "${Server}:$RemoteRoot/public/"
if ($LASTEXITCODE -ne 0) { throw "Uploading the site failed" }

if ($WithInstaller) {
  $exe = "$repo\app\installer\Output\Catogarizer-Setup.exe"
  if (-not (Test-Path $exe)) { throw "Installer not built yet: run app/installer/build-installer.ps1 first" }
  scp $exe "${Server}:$RemoteRoot/downloads/"
  if ($LASTEXITCODE -ne 0) { throw "Uploading the installer failed" }
}

Write-Host "Deployed to $Server`:$RemoteRoot"
