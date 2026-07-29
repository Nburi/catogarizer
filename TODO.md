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
- [x] Process watcher; per-category blocklist enforced only while that
      category is active (activated right before a category opens so it
      catches side-effect launches too, deactivated after it closes;
      multiple simultaneously-open categories' blocklists union together
      rather than assuming only one category is ever "active"). **Not**
      WMI `Win32_ProcessStartTrace` as originally planned in STACK.md -
      verified directly that subscribing to it throws Access Denied
      without administrator privileges, which would have broken the
      no-admin-required soft-block decision from CONCEPT.md. Switched to
      polling `Process.GetProcesses()` every ~350ms instead, diffing
      against the previous snapshot for newly-appeared PIDs - keeps the
      no-admin promise at the cost of a small, already-accepted detection
      delay rather than instant notification.
- [x] Non-native toast/notification (`TaskbarIcon.ShowNotification`) when
      a blocked app is closed out from under the user - a real system
      toast is the *correct* choice here, not a compromise against the
      "no native dialogs" rule, since the user is very likely in a
      different app when it fires and wouldn't see a themed in-app one.
- [x] UI for managing a category's blocklist (add/remove) - reused the
      app-picker dialog (search installed apps or manual entry) rather
      than building a separate one; caught and fixed a real validation
      mismatch doing this - the dialog's manual-entry mode required a
      real, existing `.exe` (right for adding a launchable app, wrong for
      blocking, where you might want to block something not currently
      installed by bare process name) - added a relaxed-validation mode
      so the two reuses of the dialog each enforce what they actually need.

Verified for real, not just fakes: blocked "notepad" via manual entry
(bare process name, no path - confirming the relaxed-validation fix
actually works), opened the category to activate blocking, launched a
real notepad.exe, and confirmed via an unambiguous before/after process
count that it was running immediately after launch and gone ~900ms
later (poll interval is 350ms).

## Phase 9 — Settings + polish pass
- [x] Settings page: autostart (reads live `IAutostartService.IsEnabled`,
      not a persisted flag, so it can't drift out of sync with the actual
      Registry state), start-minimized (wired into `App.OnStartup` - skips
      the initial `mainWindow.Show()`), and hotkey rebind (a capture box:
      click it, press a combo, `HotkeyStringParser` validates it live).
      Saving a changed hotkey re-registers it immediately in the running
      app via a callback into `App.xaml.cs`'s `RegisterGlobalHotkey`,
      fixed to be safely re-callable (reuses the existing
      `GlobalHotkeyService`/thread and subscribes the handler only once,
      rather than leaking a new background thread and double-firing the
      palette on every hotkey change).
      Verified for real: launched the app, opened Settings via the new
      gear icon, confirmed it showed the correct live values (unchecked
      autostart, "Ctrl+Alt+Space"), saved, confirmed the app stayed
      responsive and no duplicate-registration bug fired.
- [ ] Full UX pass: empty states and loading states for category/app
      actions are done (Phase 5). Loading state for the installed-apps
      search is now also done - `AppEditDialogViewModel` runs
      `FindInstalledApps()` on a background thread and shows a
      "Searching installed apps..." indicator (`IsSearchingApps`) instead
      of blocking dialog-open; verified for real via UI Automation
      (dialog appears with the indicator visible, then populates with
      real installed apps once the scan completes). Responsive resizing
      is now also done - `MainWindow` has `MinWidth="600"`/
      `MinHeight="450"` so it can't be squeezed into a state where the
      category-detail header or app rows have no room to lay out;
      `TextTrimming="CharacterEllipsis"` added to the category card
      name, detail heading, and app/blocked-app name/path/placement text
      so long values truncate with an ellipsis instead of overflowing.
      Verified for real via UI Automation + screenshots at the enforced
      minimum width (card name, detail header, and app row all trim
      correctly, no overlapping controls; a resize request below the
      minimum gets clamped by Windows itself). Still open: motion/hover
      transitions (everything currently snaps instantly, zero animation
      anywhere).
- [x] Automation + to-do integration seams: `AutomationRule`/
      `AutomationTriggerType`/`AutomationActionType` and
      `ITodoIntegrationProvider`/`TodoItem` added to Core - data model/
      interface only, nothing wired to UI or behavior.
- [x] App icon/branding asset - done in Phase 7 (placeholder, real
      branding still needed before shipping).

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
