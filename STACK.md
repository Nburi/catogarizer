# Stack

## Chosen
**WPF on .NET 10 (C#)**, self-contained single-file `win-x64` publish.

## Why
This app's distinctive work is deep Win32 interop — moving/closing *other
processes'* windows, a global keyboard hotkey, process-start monitoring for
app blocking, and Start Menu/registry enumeration for the app search picker.
.NET/WPF has the most mature, best-documented coverage of exactly these
APIs, which matters more here than UI framework aesthetics since the UI
itself is fully custom-styled regardless of framework (no native chrome/
dialogs either way). It also keeps idle footprint low (no bundled Chromium/
Node runtime) for something meant to run in the tray all day, and publishes
to the single portable `.exe` the existing deploy flow (`C:\projects\03
Apps`, already on `%PATH%`) expects.

## Key libraries / mechanisms
- **UI**: WPF, ControlTemplates/Styles for the Dashboard Home + command
  palette overlay, `CommunityToolkit.Mvvm` for MVVM (source-generator based,
  no heavyweight framework).
- **Tray icon**: `H.NotifyIcon.Wpf`.
- **Window management**: P/Invoke (`user32.dll`) — see
  `tools/window-approach-test` findings once run.
- **Global hotkey**: `RegisterHotKey`/`UnregisterHotKey` via a hidden
  message-only window (`HwndSource`).
- **App blocking watcher**: WMI `Win32_ProcessStartTrace` eventing (reacts
  near-instantly vs. polling `Process.GetProcesses()`).
- **Autostart**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (no
  elevation needed).
- **App search picker**: Start Menu `.lnk` shortcuts (both per-user and
  all-users folders) plus registry `Uninstall` keys as a secondary source.
- **Config/persistence**: local JSON file.
- **Tests**: xUnit, with interfaces around every Win32-touching service
  (window manager, process launcher, hotkey registrar, blocklist watcher)
  so core logic is unit-testable without mocking real OS calls.

## Alternatives considered
| Option | Why not |
|---|---|
| Tauri (Rust + WebView2) | Lightest footprint and could reuse the HTML mockup directly, but the Win32-interop depth this app needs (arbitrary external window manipulation, global hotkeys, process-start watching) is less mature in the Rust ecosystem — riskiest part of the app, don't want to fight the ecosystem there. |
| WinUI 3 / Windows App SDK | Nicer native Fluent/Mica look, but more packaging friction for a plain portable single-exe deploy; smaller ecosystem maturity than WPF for this vintage of Win32 interop. |
| Electron | Best case for visual polish, worst case for footprint (bundled Chromium+Node) on an always-on background tool; native window/process manipulation needs fragile native Node addons. |
| Avalonia | Solid WPF-alike, but its main selling point (cross-platform) buys nothing since this app is Windows-only by design. |
| .NET MAUI | Built for mobile+desktop parity; adds abstraction that doesn't help since heavy platform-specific interop is unavoidable anyway. |
| Python (PyQt/PySide) | Faster to prototype, but PyInstaller packaging is heavier/slower-starting than a compiled single-file .NET exe; less ergonomic native interop. |
| Plain C++/Win32 | Maximum control, but building a themed, animated, no-native-dialogs UI from scratch is a time sink not justified here. |
