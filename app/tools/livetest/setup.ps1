param([string]$Theme = "fjord", [switch]$Empty, [switch]$Full)
# Starts a live test: fresh snapshot of the user's windows, config backup check, test config, test build.
$s = Split-Path $MyInvocation.MyCommand.Path
$dir = "$env:LOCALAPPDATA\Catogarizer"
# The user's real config is backed up once; every later run must find it unchanged, or something went wrong last time.
if (-not (Test-Path "$s\config.backup.json")) { Copy-Item "$dir\config.json" "$s\config.backup.json" }
if ((Get-FileHash "$dir\config.json").Hash -ne (Get-FileHash "$s\config.backup.json").Hash) { throw "config.json differs from the backup - stop and check before overwriting anything" }
& "$s\winstate.ps1" -Save "$s\before.json" | Out-Null
Stop-Process -Name Catogarizer -Force -ErrorAction SilentlyContinue
$cm = [guid]::NewGuid(); $np = [guid]::NewGuid(); $ex = [guid]::NewGuid(); $mi = [guid]::NewGuid(); $bl = [guid]::NewGuid(); $gone = [guid]::NewGuid()
@{
  Categories = @(
    @{ Id = [guid]::NewGuid(); Name = "Test A"; SortOrder = 0; AppIds = @($cm, $np); BlockedAppIds = @($bl) },
    @{ Id = [guid]::NewGuid(); Name = "Test B"; SortOrder = 1; AppIds = @($mi); BlockedAppIds = @() },
    @{ Id = [guid]::NewGuid(); Name = "Research"; SortOrder = 2; AppIds = @(); BlockedAppIds = @() },
    @{ Id = [guid]::NewGuid(); Name = "Broken"; SortOrder = 3; AppIds = @($gone); BlockedAppIds = @() }
  )
  Apps = @(
    @{ Id = $cm; Name = "Character Map"; ExecutablePath = "C:\Windows\System32\charmap.exe" },
    @{ Id = $np; Name = "Notepad"; ExecutablePath = "C:\Windows\notepad.exe" },
    @{ Id = $mi; Name = "System Information"; ExecutablePath = "C:\Windows\System32\msinfo32.exe" },
    @{ Id = $gone; Name = "Old Tool"; ExecutablePath = "C:\Program Files\OldTool\oldtool.exe" }
  )
  BlockedApps = @( @{ Id = $bl; Name = "System Information"; ProcessNameOrPath = "msinfo32" } )
  PinnedApps = @( @{ Id = [guid]::NewGuid(); Name = "Claude"; ProcessNameOrPath = "claude" } )
  Triggers = @( @{ Id = [guid]::NewGuid(); Name = "Wrap-up"; IsEnabled = $true; Type = 1; TimeOfDay = "23:30"; DaysOfWeek = @(); Actions = @() } )
  Settings = @{ StartWithWindows = $false; StartMinimized = $false; CommandPaletteHotkey = "Ctrl+Alt+Space"; Theme = $Theme }
} | ForEach-Object {
  # -Empty: first-launch state (no categories, nothing pinned) - still with Claude pinned so the reviewer's host stays visible.
  if ($Empty) { $_.Categories = @(); $_.Apps = @(); $_.BlockedApps = @(); $_.Triggers = @() }
  # -Full: many categories, long names, umlauts - the "full state" for flow reviews.
  if ($Full) {
    $names = @("Prüfungsvorbereitung Mathematik und Physik für die Maturität", "Überarbeitung Bewerbungsunterlagen", "Orientierungslauf Trainingsplanung",
               "Grösseres Projekt: Catogarizer v2 Umbau und Review", "Mails", "Äusserst wichtige Dinge", "Research", "Design", "Admin & Finanzen",
               "Musik", "Lesen", "Schreiben", "Gaming (nur Wochenende)", "Z")
    $i = 4
    foreach ($n in $names) { $_.Categories += @{ Id = [guid]::NewGuid(); Name = $n; SortOrder = $i; AppIds = @($cm, $np, $mi); BlockedAppIds = @() }; $i++ }
  }
  $_
} | ConvertTo-Json -Depth 6 | Set-Content "$dir\config.json" -Encoding utf8
Start-Process "C:\projects\01 projects\catogarizer\src\Catogarizer.App\bin\Debug\net10.0-windows\Catogarizer.App.exe"
Start-Sleep -Seconds 4
"test build running: " + [bool](Get-Process Catogarizer.App -ErrorAction SilentlyContinue)
