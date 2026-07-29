# Implementation plan / TODO

Build order below is dependency-driven: each phase needs the ones above it.
Every phase gets xUnit coverage for its Core logic as it's built (not
deferred to the end), plus a manual pass in the running app. Checked off as
work lands; this file is the between-session source of truth for progress.

## Phase 1 — Foundation
- [x] Solution + projects: `Catogarizer.App` (WPF), `Catogarizer.Core`
      (models/services/interfaces, no Win32 dependency), `Catogarizer.Win32`
      (P/Invoke implementations), `Catogarizer.Core.Tests` (xUnit).
- [x] Core models: `Category`, `AppEntry`, `WindowRect` (X/Y/W/H + monitor),
      `AppSettings`, `BlockedApp`.
- [x] JSON persistence (`IConfigStore`/`JsonConfigStore`) with a specific,
      readable error on a corrupt config file (never a raw exception/crash).
- [x] App shell: Dashboard Home layout + Daylight Studio theme (base
      styles/ControlTemplates, no native dialogs), empty state for zero
      categories.
- [x] Single-instance enforcement — a second launch now signals the running
      instance to show itself (a named `EventWaitHandle`) instead of just
      showing a message and exiting; resolved in Phase 7 once there was a
      window-to-restore path to target.

## Phase 2 — Win32 interop layer
Built directly from the prototype findings in `STACK.md`.
- [x] `IWindowFinder`/`IWindowManager`/`IProcessLauncher` interfaces in
      Core; Win32 P/Invoke implementations in `Catogarizer.Win32`.
- [x] Window finding: process-handle first, title-candidate-list fallback
      (multiple candidates per app for localization + host-process cases).
- [x] Positioning: restore-if-maximized + `SetWindowPlacement` +
      settle-and-retry (`WindowPositioningService` in Core, unit-tested
      against a fake with no real desktop/sleep needed).
- [x] Minimize (`ShowWindow`), graceful close (`WM_CLOSE`), force-kill via
      the owning process. (Automatic force-kill-after-timeout-if-graceful-
      close-doesn't-take is Phase 5's call to make, not this layer's -
      this layer just exposes the primitive.)
- [x] "Adopt already-running windows" enabled via
      `IWindowFinder.FindAllRunningWindows` (process-name match, title
      fallback) - verified end-to-end against a real Notepad instance.
      Wiring it into actual category-action tracking state is Phase 5.
- [x] Monitor enumeration service for multi-monitor placement, using a
      stable per-monitor device name (not a positional index) as the id.
- [x] Fakes for the retry policy (`FakeWindowManager`, `FakeDelay`); more
      added in Phase 5 as `CategoryActionService` needs them.

## Phase 3 — Category & app CRUD
- [x] Add/edit/remove categories and apps; themed dialogs, inline validation
      (duplicate names, invalid paths) next to the field.
- [ ] Reorder categories/apps - `LibraryService.ReorderCategories` exists
      and is tested, but no drag-drop (or other) UI calls it yet. Deferred;
      not blocking since apps/categories are still fully usable unordered
      beyond creation order.
- [x] App search picker: enumerate Start Menu `.lnk` shortcuts (user + all
      users) plus registry Uninstall keys (honoring the `SystemComponent`
      flag and filtering uninstaller executables out); type-ahead search.
- [x] Manual `.exe` path entry fallback, with existence/executable
      validation.

## Phase 4 — Window placement setup ("grab, don't type")
- [x] Capture flow: launch the target app's live window, let the user
      drag/resize it into place, "Capture position" reads the current rect
      via the Phase 2 layer instead of typing coordinates. A "Clear
      captured placement" link resets an app back to its own default
      position.
- [x] Store placement relative to the chosen monitor (multi-monitor safe) -
      `WindowPlacementResolver.CaptureFromBounds` identifies the monitor by
      the window's center point and offsets from its origin.

## Phase 5 — One-click category actions
- [x] Open/Close/Minimize per category and per app, wired through Phase 2 -
      `CategoryActionService` (launch-or-adopt-already-running, position if
      captured, graceful close falling back to force-kill after a 2s grace
      period). Verified end-to-end against real VS Code, not just fakes:
      "Open" launched the real process, "Close all" actually terminated it.
- [x] Loading state while a category opens - actions run on a background
      thread via `Task.Run` with an `IsBusy`/`BusyMessage`-driven overlay,
      not a frozen UI thread.
- [x] Specific, readable errors for launch failures (missing/moved exe,
      etc.) surfaced in a dismissible banner, never a raw exception.

## Phase 6 — Command palette + global hotkey
- [x] Global hotkey registration - `GlobalHotkeyService` runs a dedicated
      thread with a hidden `HWND_MESSAGE` window for `RegisterHotKey`/
      `WM_HOTKEY`, independent of WPF's own message loop. Reads
      `AppSettings.CommandPaletteHotkey` ("Ctrl+Alt+Space" default) at
      startup via `HotkeyStringParser`; a Settings UI to change it without
      editing the config file by hand is still Phase 9's job.
- [x] Borderless topmost overlay, search-as-you-type over categories
      (scoped to categories, not individual apps - matches "quickly switch
      between categories"; per-app actions already live in the main
      window), same Open/Minimize/Close actions, Enter/Esc/click-outside
      all keyboard- and mouse-friendly.
      Verified for real: registered the actual OS hotkey, simulated the
      physical Ctrl+Alt+Space key combo, confirmed the palette appeared
      with live category data, clicked Open and confirmed the real app
      launched and the palette closed itself afterward.

## Phase 7 — Tray + autostart
- [x] Tray icon (`H.NotifyIcon`) with a generated placeholder app icon
      (`Assets/app.ico` - accent-colored rounded square with a "C", built
      with proper multi-size BMP-DIB+PNG frames after an all-PNG first
      attempt failed to load in the legacy GDI Icon loader; replace with
      real branding before shipping). Minimize/close-to-tray: closing or
      minimizing the main window hides it instead of exiting
      (`ShutdownMode=OnExplicitShutdown`, only the tray's own Exit item
      really shuts down). Quick category switch and a "Start with Windows"
      toggle live in the tray context menu (rebuilt fresh from current data
      each time it opens).
- [x] Autostart toggle (`HKCU\...\Run` key) via `AutostartService`.
      Verified directly against the real registry (enable → key written →
      disable → key cleanly removed, no leftover).

Verified for real, not just builds: closed the main window via
`WindowPattern.Close()` (same path as clicking the title-bar X) and
confirmed the process stayed alive with the window actually hidden;
launched a second instance while the first was hidden and confirmed the
first instance's window reappeared and the second process exited cleanly.
That second check caught a real bug - the second instance crashed with an
unhandled `ApplicationException` on shutdown because it called
`Mutex.ReleaseMutex()` without ever having owned the mutex (`initiallyOwned`
only grants ownership when the calling instance is the one that creates
the named mutex, not on every instance that references it by name) - fixed
by tracking ownership explicitly and only releasing when true.

## Phase 8 — App blocking
- [ ] WMI `Win32_ProcessStartTrace` watcher; per-category blocklist
      enforced only while that category is active.
- [ ] Non-native toast/notification when a blocked app is closed out from
      under the user.

## Phase 9 — Settings + polish pass
- [ ] Settings page: autostart, hotkey rebind, start-minimized, etc.
- [ ] Full UX pass: empty states, loading states everywhere async,
      responsive resizing, contrast check against the Daylight Studio
      palette, motion (ease-out, reduced-motion support).
- [ ] Automation + to-do integration seams: data model and interface stubs
      only, per CONCEPT.md - not wired to UI/behavior yet.
- [ ] App icon/branding asset (placeholder is fine to start; replace before
      first real deploy).

## Phase 10 — Tests & packaging
- [ ] Full xUnit suite green.
- [ ] Self-contained single-file `win-x64` publish profile.
- [ ] Manual end-to-end pass in the actual running (published) app, not
      just tests.

## Known open items (not blocking, revisit if they bite)
- Edge/Chromium windows enforce their own minimum width — expected, not a
  bug to fix.
- Global hotkey mechanism itself (`RegisterHotKey` vs. a low-level hook)
  not yet prototyped — resolve in Phase 6.
