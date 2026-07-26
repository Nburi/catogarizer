# Stack

## Chosen: .NET 8 + WPF (C#)

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
