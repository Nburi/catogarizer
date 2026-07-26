# Errors & Gotchas

Notes on issues hit during development and how they were resolved, to avoid
re-discovering them.

## `dotnet new classlib -f net10.0-windows` fails
The classlib template's `-f` option only accepts bare TFMs (`net10.0`,
`netstandard2.0/2.1`) - it doesn't know about OS-specific suffixes. Scaffold
with `-f net10.0` and then hand-edit `<TargetFramework>` in the `.csproj` to
`net10.0-windows` afterward. (The `wpf`/`winforms` templates *do* accept
`-windows` suffixes as valid framework choices directly.)

## `SupportedOSPlatformVersion` + plain `net10.0-windows` TFM = NETSDK1135
Setting `<SupportedOSPlatformVersion>` explicitly while the TFM itself has no
Windows version suffix (just `net10.0-windows`, not
`net10.0-windows10.0.19041.0`) throws `NETSDK1135: SupportedOSPlatformVersion
must not be higher than TargetPlatformVersion`. Either omit
`SupportedOSPlatformVersion` (fine for our case - P/Invoke doesn't need it) or
add the version suffix to the TFM itself.

## WPF `TextBlock` has no letter-spacing property
Unlike WinUI/Avalonia, classic WPF's `TextBlock` doesn't support
`CharacterSpacing` (compile error: property not found, on both `TextBlock`
and `TextElement`). For the "eyebrow"-style spaced-out labels, we just insert
literal spaces between characters in the string instead.

## GDI screen capture is unreliable on this dev machine for visual QA
This machine has multiple monitors at different DPI scales and sometimes a
fullscreen-exclusive game running on one of them. Both of those break naive
`Graphics.CopyFromScreen`-based screenshots (blank/white captures, or
capturing the wrong window entirely due to it being behind a
fullscreen-exclusive surface). For verifying WPF UI during development,
prefer querying the live UI Automation tree
(`AutomationElement.FromHandle` + `TreeWalker`) over screenshots - it reflects
the actual rendered element tree regardless of monitor/z-order, and buttons
can be driven directly via `InvokePattern` instead of pixel-coordinate
clicks.

## Some launched apps fight back against our window positioning
- Apps with their own session-restore feature (e.g. modern Windows Notepad
  reopening its last document/size) can silently override the position/size
  we just applied a moment after we apply it.
- Launcher-stub executables (e.g. `control.exe`) start, hand off to the real
  UI in a different process, and exit immediately - our launch-and-poll-for-
  MainWindowHandle approach never finds a window on the stub process, so
  positioning is skipped and the app won't be recognized as "running"
  afterward.

Neither is a bug in our code to "fix" outright; see `TODO.md` Known Issues.
