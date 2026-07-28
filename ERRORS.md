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

## Owned dialog windows show up nested under their owner in UI Automation
When driving the app via `AutomationElement.FromHandle` for manual testing,
a modal dialog opened with `Window.Owner` set does **not** appear as a
sibling of the main window under `AutomationElement.RootElement.Children` -
it shows up as a `ControlType.Window` *descendant* of the owner's own
element. Search `mainWindowRoot.FindAll(TreeScope.Descendants, ...)` for the
dialog, not the desktop root, or you'll conclude the dialog never opened.

## `[ObservableProperty]` needs `NotifyPropertyChangedFor` on *every* source, not just one
`CategoryEditDialogViewModel` had a computed `HasError => !string.IsNullOrEmpty(NameError)`.
`Name` was annotated with `[NotifyPropertyChangedFor(nameof(HasError))]` but
`NameError` itself was not - so validation logic worked correctly (the error
was computed and stored) but the UI never found out `HasError` had changed,
and the inline error message stayed invisible. Found via manual UI
Automation testing (the dialog stayed open on a duplicate name, as expected,
but no error text was found in the tree) rather than by reading the code.
Lesson: when a computed property depends on N observable properties, all N
need the `NotifyPropertyChangedFor` attribute, not just the one that seemed
most obviously "the trigger."

## Renaming `<AssemblyName>` breaks scripts that hardcode the old process name
When milestone 12 changed the App project's `AssemblyName` from
`Catogarizer.App` to `Catogarizer`, the output exe/process name changed too
(`Catogarizer.App.exe` -> `Catogarizer.exe`). Every `Get-Process -Name
"Catogarizer.App"` in manual test scripts after that point needs updating to
`"Catogarizer"` - easy to forget and get a confusing "no such process" error
that looks like the app failed to launch when it actually started fine.

## `PostMessage(WM_CLOSE)` / `ShowWindow(SW_MINIMIZE)` return before the window actually changes state
`WindowManager.Close`/`Minimize` only issued the Win32 call and immediately
reported success to the caller, without ever checking whether the window
actually closed or minimized. `PostMessage` just enqueues `WM_CLOSE` and
returns instantly - apps that ignore it, pop a "save changes?" prompt, or
hide to tray instead of exiting (Discord, Spotify, Slack-style apps) still
got counted as "closed," so the category action banner could say "3 apps
closed" while one of them was still sitting right there. Fixed by polling
the real end state (`IsWindow` / `IsIconic`) for up to 2s after issuing the
request and only reporting success once it's confirmed - see
`WindowManager.CloseAsync`/`MinimizeAsync`. Verified against a synthetic
WinForms window whose `FormClosing` cancels the close, to simulate a
refusing app deterministically rather than relying on mocked test doubles
for that guarantee.

## Never test "app blocking" with a real running application's process name
During the milestone 13 end-to-end pass, `steam.exe` was added to the
blocked-apps list as test data without checking whether Steam was actually
running - it was, and the poller killed it within its ~1.5s cycle (confirmed
by the live "Steam" process disappearing and a `steamerrorreporter64` crash
handler appearing in its place). The block entry was removed immediately and
the user was informed. Lesson: before adding *anything* to a live block-list
during testing, check `Get-Process` first - test data for this feature can
have real side effects on whatever the user has open, unlike testing
"launch/close/minimize" against apps we started ourselves.
