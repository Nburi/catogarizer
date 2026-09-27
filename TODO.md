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
- [x] Full UX pass: empty states and loading states for category/app
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
      minimum gets clamped by Windows itself). Motion/hover transitions
      are now also done - `PrimaryButtonStyle`/`SecondaryButtonStyle`/
      `GhostIconButtonStyle`/`GhostRowButtonStyle` in
      `Themes/DaylightStudio.xaml` all animate their hover/press state
      over ~150ms (80ms for the primary button's press) instead of
      snapping instantly, via `Trigger.EnterActions`/`ExitActions` +
      `Storyboard`. Styles that swap a shared `SurfaceAltBrush` on hover
      use a second overlay `Border` whose own `Opacity` is animated,
      rather than animating the shared brush's `Color` directly (which
      would affect every consumer of that brush app-wide). Did not wire
      up `SystemParameters.ClientAreaAnimation` (OS reduced-motion) -
      not straightforward per-Trigger in pure XAML; short/subtle
      durations used as the documented fallback. Verified for real by
      sampling the actual rendered pixel color (GDI `GetPixel`) at a
      button before/during/after a simulated hover - confirmed the
      color reaches an exact match of the intended theme color and
      reverts correctly for both the direct-opacity and overlay
      techniques.
- [x] Automation + to-do integration seams: `AutomationRule`/
      `AutomationTriggerType`/`AutomationActionType` and
      `ITodoIntegrationProvider`/`TodoItem` added to Core - data model/
      interface only, nothing wired to UI or behavior.
- [x] App icon/branding asset - done in Phase 7 (placeholder, real
      branding still needed before shipping).

## Phase 10 — Tests & packaging
- [x] Full xUnit suite green - 61/61, re-confirmed after Phase 9 finished.
- [x] Self-contained single-file `win-x64` publish profile -
      `src/Catogarizer.App/Properties/PublishProfiles/win-x64-selfcontained.pubxml`,
      run via `dotnet publish src/Catogarizer.App/Catogarizer.App.csproj
      -c Release -p:PublishProfile=win-x64-selfcontained`. Isolated to
      the profile rather than the .csproj directly so plain `dotnet
      build`/`dotnet run` are unaffected.
- [x] Manual end-to-end pass in the actual running (published) app, not
      just tests - launched the published exe fresh and confirmed: WPF
      theme/resources render correctly (pack:// URIs resolve from the
      single-file bundle), category/app CRUD works, the enforced
      MinWidth/MinHeight clamp a too-small resize the same as in debug,
      clicking Open launches a real process (the riskiest Win32-interop
      path for a self-contained publish), Settings reads live
      autostart/hotkey state correctly, closing the main window keeps
      the process alive in the tray, and a second instance launched
      while the first was hidden exits via the single-instance mutex
      while the first instance's window reappears.

## Phase 11 — Trigger/Action automation
- [x] Replaced the Phase 9 `AutomationRule` data-model-only stub with a real
      `Trigger`/`TriggerAction` model (`src/Catogarizer.Core/Automation/`):
      three trigger types (Startup, Time, Manual) each holding an ordered
      list of actions (OpenCategory, OpenApp, CloseApps). Persisted as
      `AppConfig.Triggers`, CRUD in `LibraryService` mirroring the existing
      Category/App/BlockedApp sections exactly.
- [x] `TriggerRunner` - a thin dispatcher over the existing
      `ICategoryActionService`, not a reimplementation of process handling.
      "Open an app at a specific location" reuses `AppEntry.Placement`
      as-is - no new fields needed.
- [x] `TriggerSchedulerService` - polls (same "polling over OS eventing"
      call as `AppBlockingService`, see `STACK.md`) every 20s for Time-type
      triggers, firing at most once per calendar day, with an optional
      day-of-week filter.
- [x] CLI: `catogarizer start` (wired into the autostart Run key, fires
      enabled Startup triggers) and `catogarizer run "<name>"` (fires one
      named trigger on demand - relayed over a named pipe to the already-
      running instance if there is one, so it stays scriptable/headless
      rather than popping the window).
- [x] Settings → **Automation...** opens a Triggers management window (add/
      edit/delete/enable/Run Now), with a dedicated trigger editor
      (name, type, time+day-of-week when applicable, an ordered action list
      with up/down reordering - the app's first manual-reorder UI).
- [x] xUnit coverage: `TriggerRunner` action dispatch (including skipping
      actions that reference a since-deleted category/app), the scheduler's
      time/day-of-week/once-per-day logic, `CliCommand` parsing, and
      `LibraryService` trigger CRUD + validation. 99/99 green.

Verified for real: launched the built debug app and reviewed it live. Caught
a real gap doing this - the trigger editor's "Open app"/"Close apps"
pickers only listed apps already in the library (whatever had been added to
a category before), with no way to reference a new one, so a fresh install
effectively could only automate 1-2 apps. Fixed by adding a "+ Add app..."
button to both pickers, reusing the same installed-app-search-or-manual-entry
flow (`AppEditDialogViewModel`/`IDialogService.ShowAppEdit`) already used
for adding an app to a category - newly added apps flow into a shared
`ObservableCollection<AppEntry>` so every action row in the same trigger
picks it up live, not just the row that added it.

## Phase 12 — PWA (installed web app) support
- [x] Search picker now discovers installed PWAs (Edge/Chrome "Install as
      app") the same way as any other app - `InstalledAppFinder` now reads a
      Start Menu shortcut's launch arguments (`shortcut.Arguments`), not just
      its target path, and `InstalledApp`/`AppEditDialogViewModel.PickInstalledApp`
      carry that through to the saved `AppEntry`. Previously a picked PWA
      would launch `msedge_proxy.exe`/`chrome_proxy.exe` bare, with no
      `--app-id` - opening the wrong thing (or nothing) instead of the PWA.
- [x] De-dup key in `InstalledAppFinder.FindInstalledApps` changed from
      executable path alone to path+arguments - every PWA installed under
      one browser/profile shares the identical proxy exe path, differing
      only by `--app-id`, so the old key silently collapsed every installed
      PWA down to one entry.
- [x] `WindowFinder.FindByProcessHandle` now catches the
      `InvalidOperationException` thrown by polling `MainWindowHandle` on an
      already-exited process, instead of letting it propagate as a launch
      failure - needed because a PWA's shortcut launches a short-lived proxy
      stub that hands off to the real browser and exits before the app ever
      calls back in. Falls through cleanly to the existing title-substring
      window search (the same fallback already used for UWP/
      `ApplicationFrameHost` host-process windows) instead.
- [x] `CategoryActionService.FindRunningWindows` adds the `_proxy`-stripped
      process name as a second match candidate (e.g. "msedge_proxy" also
      matches windows owned by "msedge") for adopt-already-running/Close/
      Minimize, since the proxy exe never owns the actual window.
- [x] Picker shows a small "Web App" tag next to detected PWA entries
      (`InstalledApp.IsPwa`, true when the shortcut's arguments contain
      `--app-id=`).
- [x] xUnit: `CategoryActionServiceTests` covers a proxy-path `AppEntry`
      still matching/minimizing a window owned by the unsuffixed process
      name. 100/100 green.

No new UI, no new data model fields on `AppEntry` - a picked PWA is a
completely ordinary `AppEntry`, so placement capture/positioning and the
Phase 11 trigger/automation system already work for it without further
changes.

**Verified for real** against a genuine installed PWA ("nothing-to-do", an
Edge-installed to-do app) once the user installed one and gave the go-ahead
to test. Live testing surfaced and fixed three real bugs the unit suite
couldn't catch (all in Win32/App-layer glue outside its scope):
- `InstalledAppFinder` crashed silently on every single shortcut: mixing a
  `dynamic` COM parameter with a named value-tuple return type erases the
  tuple's field names at runtime (the DLR only sees `Item1`/`Item2`, not
  `Target`/`Arguments`), throwing `RuntimeBinderException` on every access -
  caught by the picker's own broad `catch { apps = []; }`, so the whole
  search silently came up empty instead of surfacing an error. Fixed by
  returning a small `ResolvedShortcut` record instead of a tuple - a
  record's properties are real runtime members, unaffected by this. Confirmed
  via a standalone harness that bypassed the swallowing catch: 95 apps
  found, "nothing-to-do" correctly resolved with `IsPwa=True` and its real
  `--app-id`.
- Search picker tagging, Open (launched the PWA's real content window, not a
  blank browser - confirmed via its window title), Minimize (`IsIconic`
  confirmed true afterward), and Close (process confirmed gone afterward)
  all individually verified against the real running window.
- `WindowManager.GetBounds` used `GetWindowRect`, which reports an
  off-screen sentinel rect (-32000,-32000, near-zero size) for a window
  that's minimized at query time - a PWA last closed while minimized
  reopens minimized (Chromium remembers this per-profile), so placement
  capture could silently save that garbage. Fixed to read
  `GetWindowPlacement().rcNormalPosition` instead, which holds the real
  restore-to bounds regardless of current show state - already how
  `Position()` writes placements, so this makes the read path symmetric
  with the write path. `PlacementDialogViewModel.OpenApp` also now
  explicitly restores (twice, settle-and-retry - the same Chromium
  async-reapply race `STACK.md` already documents for positioning) after
  finding the window, so the user can actually see it to drag/resize.

**Still open**: even with the fix above, one placement capture on this
machine's two-monitor, mixed-DPI setup (built-in display at 200% scaling,
external "TC242W" at 100%) captured an implausibly large size
(4830×1830) - traced partway to DPI virtualization (a DPI-unaware caller
and a DPI-aware one legitimately see different numbers for the same
window) but not fully root-caused before the screen went dark mid-
investigation. Not urgent for Open/Close/Minimize (plain Win32-handle
operations, unaffected) - only the number placement capture writes down is
suspect. Needs a fresh, isolated repro on this same dual-monitor setup
before trusting placement capture here; see "Known open items" below.

## Known open items (not blocking, revisit if they bite)
- Edge/Chromium windows enforce their own minimum width — expected, not a
  bug to fix.
- Placement capture may record an oversized rect on this machine's
  dual-monitor, mixed-DPI setup (see Phase 12) - not yet root-caused, only
  reproduced once. Re-test placement capture (any app, not just PWAs) here
  before relying on it; likely DPI-virtualization-related, not PWA-specific.
- PWA window matching (Phase 12) uses process-name-candidates + title-
  substring, not the window's `System.AppUserModel.ID` - matches this app's
  established preference for plain P/Invoke over new COM interop (see
  `STACK.md`), and is the same precision already accepted for UWP host-
  process windows. A regular browser tab whose title happens to contain a
  PWA's name could in theory still be misidentified. Revisit with
  `SHGetPropertyStoreForWindow`-based AUMID matching only if this actually
  causes a wrong match in practice.

## Phase 13 — Session-based category switching (v2 pivot) — planned
Concept updated in `CONCEPT.md` ("Category switching & sessions" /
"Chosen design"): the category action model moves from per-category
Open/Close/Minimize buttons to always-active **switching**, with a saved
per-category window session (hide-on-leave, restore-on-return), an implicit
"Sonstiges" bucket for unassigned windows, and pinned always-visible apps.
Backend layers from Phases 2–12 (window management, blocking, autostart,
search picker, automation) are retained; the UI layer is being rebuilt
around a command-palette-first interaction, per the updated CONCEPT.md.

- [x] **Spike first** (not full UI), same approach as the original Phase 2
      Win32 prototype - validate before building on top of it:
  - [x] `SW_HIDE`/`SW_SHOW` reliability - added `Hide`/`Show`/
        `IsWindowVisible` to `IWindowManager`/`WindowManager`
        (`src/Catogarizer.Win32/WindowManager.cs`) and a throwaway
        `tools/HideShowSpike` console harness (`hide-show <substring>` /
        `watch` modes). Verified for real against Notepad (throwaway),
        Arc/Chromium, Spotify (actively playing), and the Realtek Audio
        Console UWP app - hide then show left bounds, `IsWindowOpen`, and
        `IsWindowVisible` exactly as expected in every case, no drift, no
        Chromium async-reflow clobbering observed. Real finding: the
        Realtek UWP app exposes **two separate top-level windows**
        (`RtkUWP` + `ApplicationFrameHost`, different bounds) for what's
        conceptually one app - not a problem for the session model since
        tracking stays per-window anyway, both just end up as two entries
        in the same session. User-verified separately against a real
        fullscreen-exclusive game: disappeared from Alt-Tab/taskbar while
        hidden, reappeared correctly on show - the one remaining risk from
        `STACK.md` didn't materialize.
  - [x] New-window detection - went with polling (matches the existing
        `AppBlockingService`/`ProcessWatcher` pattern) over
        `SetWinEventHook`, for consistency with the rest of the codebase
        and to avoid a new interop surface. Added `IWindowFinder.
        FindAllVisibleWindows`, `IWindowWatcher`/`WindowWatcher`
        (`src/Catogarizer.Win32/WindowWatcher.cs`, diffs snapshots by
        handle, 350ms interval) plus `FakeWindowWatcher`/
        `FakeWindowFinder.FindAllVisibleWindows` for future consumer
        tests. Verified for real via the harness's `watch` mode: opening
        Calculator was detected within one poll tick, correctly reporting
        the localized title ("Rechner") and `ApplicationFrameHost` as
        owner - same UWP host-process shape already known from `STACK.md`.
- [x] Session/window-attribution data model (per-window, not per-process;
      session vs. template distinction) - `CategorySwitchService`/
      `ICategorySwitchService` in `Catogarizer.Core`, built directly on the
      spike-validated `Hide`/`Show`/`IWindowWatcher` primitives. 10 unit
      tests against `FakeWindowManager`/`FakeWindowFinder`/
      `FakeWindowWatcher`/`FakeCategoryActionService`.
- [x] "Sonstiges" implicit category + per-app pinned/always-visible flag -
      `CategorySwitchService.Uncategorized` (`Guid.Empty`, no CRUD/UI needed
      since it's implicit) + `PinnedApp` model/`AppConfig.PinnedApps`.
- [ ] Wire `CategorySwitchService` into the actual running app/CLI as the
      primary action, replacing the Phase 5 `CategoryActionService`
      category-level Open/Close/Minimize (per-app manual Open/Close/Minimize
      stays as an override). Not done yet - the service exists and is
      tested in isolation, but nothing in `Catogarizer.App`/the CLI calls it.
- [ ] New UI: command palette as primary surface, minimalist dashboard as
      secondary overview + click-to-switch (no thumbnails/previews).
- [ ] `design/concepts.html` v2 pass once the new layout is sketched.
