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
- **Window management**: plain `user32.dll` P/Invoke (`SetWindowPlacement`,
  `ShowWindow`, `PostMessage(WM_CLOSE)`), chosen over UI Automation after
  prototyping both against Notepad/Calculator/Edge — identical outcomes in
  every case, so it wins on being dependency-free, faster (no COM/UIA tree
  walking), and simpler. A launch-time position hint via `CreateProcess`
  STARTUPINFO was also tried and dropped — every tested app ignored it.
  Findings that need to carry into the real implementation:
  - **Maximized/full-screen windows must be explicitly restored first**
    (`IsZoomed` + `ShowWindow(SW_RESTORE)`), then positioned via
    `SetWindowPlacement` (sets `rcNormalPosition` + show state atomically) —
    plain `SetWindowPos` on a still-maximized window is unreliable.
  - **Settle-and-retry**: some apps (Edge/Chromium observed) asynchronously
    re-apply their own remembered window state shortly after launch,
    clobbering an early positioning call — reposition once, wait ~300ms,
    verify, and retry once if it didn't stick.
  - **Window titles are localized** — don't hardcode a single English title
    substring for the window-finding fallback; keep a small list of
    candidates per app. Discovered via Calculator: on German Windows its
    window is titled "Rechner" and owned by `ApplicationFrameHost.exe`, not
    `calc.exe`/`CalculatorApp.exe` (UWP/packaged apps route through a host
    process that isn't the process actually launched).
  - **Category actions must include already-running windows**, not just
    ones the app itself launched — match by owning-process name first
    (unambiguous for normal apps), title-substring as fallback (needed for
    host processes like `ApplicationFrameHost` that can own windows for
    several different packaged apps at once).
  - Chromium windows enforce their own minimum width — a saved layout
    narrower than that will get clamped; not a bug to work around.
- **Global hotkey**: `RegisterHotKey`/`UnregisterHotKey` via a hidden
  message-only window (`HwndSource`).
- **App blocking watcher**: polling `Process.GetProcesses()` (~350ms
  interval), diffing against the previous snapshot for newly-appeared
  PIDs. Originally planned as WMI `Win32_ProcessStartTrace` eventing for
  near-instant detection, but that requires administrator privileges
  (verified directly: subscribing from a non-elevated process throws
  `ManagementException: Access denied`) - which would have broken the
  "soft block, no admin rights" decision from CONCEPT.md. Polling keeps
  that promise at the cost of a small, already-accepted detection delay
  instead of true instant detection.
- **Autostart**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (no
  elevation needed).
- **App search picker**: Start Menu `.lnk` shortcuts (both per-user and
  all-users folders) plus registry `Uninstall` keys as a secondary source.
- **Config/persistence**: local JSON file.
- **Category switching (v2)**: windows are parked with `ShowWindow(SW_HIDE)`
  and brought back with `SW_SHOW`, never closed. Only Alt-Tab-eligible
  top-level windows are tracked (no tool windows, no desktop, never
  Catogarizer's own). Every hide is first written to `hidden.json` next to
  the config, so a crash or power loss can't strand a window: the next
  start shows everything in that ledger. Hide/show was chosen over moving
  windows off-screen or to virtual desktops because it's instant (~200 ms
  for a full restore), needs no undocumented APIs and leaves the window's
  own state untouched.
- **CLI relay**: a second process started with `switch`/`back`/`show-all`/
  `run` hands the command to the running instance over a named pipe; the
  instance queues it on a worker thread so the pipe listener never blocks
  on a switch.
- **Themes**: palettes are data in Core (`Theming/ThemeCatalog`, defined in
  OKLCH so lightness steps stay even across hues), converted to WPF brushes
  at runtime by `ThemeService`; contrast is unit-tested. XAML only uses
  `DynamicResource` so themes switch live. Title bars follow via
  `DwmSetWindowAttribute` (immersive dark mode).
- **App icons**: `PrivateExtractIcons` for crisp exe icons, `SHGetFileInfo`
  as fallback, cached per path.
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
