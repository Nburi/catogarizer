# Errors and pitfalls

Things that broke once and could break again, with the fix.

## WPF: "Cannot set Visibility or call Show/Close while a Window is closing"
**Seen:** 2026-09-28, double-tapping the global hotkey. The second tap was handled
via `Dispatcher.Invoke` while the first tap's palette `Show()` was still running,
closed the half-shown palette, and WPF then threw when `Show()` continued.
**Fix:** marshal hotkey presses with `Dispatcher.BeginInvoke` (queued, in order),
guard the handler against reentrancy, take tap timestamps on the hotkey thread,
and make every close path of a window go through one idempotent `Dismiss()` that
checks an `_isClosing` flag set in `Closing`.
**Rule:** never `Dispatcher.Invoke` from a background thread into code that shows
or closes windows; never call `Close()` on a window without a closing guard.

## Switching launched a second copy of an app that was already open
**Seen:** 2026-09-28. Right after startup every window is in Unsorted. Switching
to a category hid Unsorted (including the template's app), then the template
found no *visible* window and launched the app again.
**Fix:** `CategorySwitchService.ClaimUnsortedWindows` moves one matching Unsorted
window per template app into the category before hiding (never from other real
categories), so `CategoryActionService.Open` adopts it.

## Testing: a user's window stayed hidden after a live test
**Seen:** 2026-09-28. A template claimed the user's open Explorer window into the
test category; the test cleanup then force-killed the app and *deleted
hidden.json* - so neither the exit path nor the next-start recovery could show
the window again. Restored by hand the same minute.
**Rule:** never delete `hidden.json` after a kill. End every live test with
`Catogarizer.App.exe show-all` (added for this), then verify against a snapshot of
the user's windows taken before the test.

## Testing: screen captures come back black/white, hotkeys do nothing
**Seen:** 2026-09-28. The workstation had locked mid-test (`LogonUI` running,
`OpenInputDesktop` fails). Synthetic keys go to the secure desktop and BitBlt
returns blank frames. Check for the lock screen before debugging the app.
Also: `Graphics.CopyFromScreen` skips layered windows (`AllowsTransparency`), so
it never shows the palette or the switch pill; use `BitBlt` with `CAPTUREBLT`.
