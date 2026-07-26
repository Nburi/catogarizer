# Stack

## Chosen: .NET 10 + WPF (C#)

Originally scoped for .NET 8, but the SDK actually available on this machine
is 10.0.302 — .NET 10 is the current LTS release (Nov 2025), so the project
targets `net10.0-windows` instead. Nothing else about the reasoning below
changes.

The hard part of this app isn't the UI, it's manipulating *other processes'*
windows — moving, resizing, minimizing, detecting close, launching with saved
args, polling for blocked processes. That's Win32 territory, and .NET's
P/Invoke story for it is the most direct and best-documented of the options
considered (this is how tools like PowerToys FancyZones are built). It's also
the lightest of the options for something that sits in the tray all day.

## Alternatives considered

| Stack | Why not (for this project) |
|---|---|
| Tauri (Rust + Web UI) | Genuinely easier for the neon CSS look, but requires picking up Rust for the Win32-heavy backend. Runner-up. |
| Electron (Node + Web UI) | Fastest to build in familiar JS/TS, but bundles Chromium+Node (~150-200MB, ~100MB+ idle RAM) — a real cost for a tool meant to run in the background continuously. |
| Python (PySide6 + pywin32) | Quick to prototype, but packaging as a polished distributable is historically painful (large frozen exes, AV false positives). |
| AutoHotkey | Great at raw window manipulation, not suited to a custom, sellable UI. |

## Key libraries
- `CommunityToolkit.Mvvm` — MVVM (RelayCommand, ObservableObject)
- `H.NotifyIcon.Wpf` — system tray icon + context menu
- `System.Text.Json` — versioned local config persistence
- `xUnit` — unit tests for `Catogarizer.Core`

## Project layout
```
Catogarizer.sln
  src/Catogarizer.Core     — models, interfaces, persistence (no WPF/Win32 dependency)
  src/Catogarizer.Win32    — P/Invoke layer: window mgmt, process launch, tray, autostart, hotkeys
  src/Catogarizer.App      — WPF UI (MVVM)
  tests/Catogarizer.Core.Tests
```

## Packaging
Self-contained single-file publish profile at
`src/Catogarizer.App/Properties/PublishProfiles/win-x64.pubxml` — no .NET
runtime install required on the target machine. Build with:
```
dotnet publish src/Catogarizer.App/Catogarizer.App.csproj -p:PublishProfile=win-x64
```
Output lands in `src/Catogarizer.App/bin/Release/net10.0-windows/publish/win-x64/Catogarizer.exe`
(~140MB, since the .NET runtime + WPF are bundled in). Trimming is
intentionally left off (`PublishTrimmed=false`) since WPF apps don't reliably
survive IL trimming.
