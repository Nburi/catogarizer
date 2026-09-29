# Ends a live test safely: every hidden window comes back BEFORE anything is killed or deleted.
$s = Split-Path $MyInvocation.MyCommand.Path
$dir = "$env:LOCALAPPDATA\Catogarizer"
$exe = "C:\projects\01 projects\catogarizer\src\Catogarizer.App\bin\Debug\net10.0-windows\Catogarizer.App.exe"
if (Get-Process Catogarizer.App -ErrorAction SilentlyContinue) {
  & $exe show-all; Start-Sleep -Seconds 2
  "ledger after show-all: " + (Get-Content "$dir\hidden.json" -Raw -ErrorAction SilentlyContinue)
}
Get-Job | Remove-Job -Force
Stop-Process -Name Catogarizer.App -Force -ErrorAction SilentlyContinue
Get-Process charmap, msinfo32 -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process Notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "Unbenannt*" -and $_.StartTime -gt (Get-Date).AddMinutes(-20) } | Stop-Process -Force
Start-Sleep -Milliseconds 500
# Last safety net: nothing from the pre-test snapshot may stay hidden.
Add-Type -Namespace TD -Name U -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr h, int c); [DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h); [DllImport("user32.dll")] public static extern bool IsWindow(System.IntPtr h);'
foreach ($w in (Get-Content "$s\before.json" | ConvertFrom-Json)) {
  $h = [IntPtr][int64]$w.Handle
  if ([TD.U]::IsWindow($h) -and -not [TD.U]::IsWindowVisible($h)) { [void][TD.U]::ShowWindow($h, 5); "WARNING - had to show: $($w.Process) '$($w.Title)'" }
}
Remove-Item "$dir\hidden.json" -ErrorAction SilentlyContinue
Copy-Item "$s\config.backup.json" "$dir\config.json" -Force
"config restored: " + ((Get-FileHash "$dir\config.json").Hash -eq (Get-FileHash "$s\config.backup.json").Hash)
if (-not (Get-Process Catogarizer -ErrorAction SilentlyContinue)) { Start-Process "C:\projects\03 Apps\Catogarizer.exe" }
Start-Sleep -Seconds 1
"installed app running: " + [bool](Get-Process Catogarizer -ErrorAction SilentlyContinue)
