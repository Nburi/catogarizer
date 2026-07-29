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
- [x] Single-instance enforcement — currently a native message + exit on a
      second launch, not yet "focus the existing window" (no tray/IPC to
      target until Phase 7); revisit then.

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
- [ ] Add/edit/remove/reorder categories and apps; themed dialogs, inline
      validation (duplicate names, invalid paths) next to the field.
- [ ] App search picker: enumerate Start Menu `.lnk` shortcuts (user + all
      users) plus registry Uninstall keys; type-ahead search.
- [ ] Manual `.exe` path entry fallback, with existence/executable
      validation.

## Phase 4 — Window placement setup ("grab, don't type")
- [ ] Capture flow: launch/select the target app's live window, let the
      user drag/resize it into place, "Capture" reads the current rect via
      the Phase 2 layer instead of typing coordinates.
- [ ] Store placement relative to the chosen monitor (multi-monitor safe).

## Phase 5 — One-click category actions
- [ ] Open/Close/Minimize per category and per app, wired through Phase 2.
- [ ] Loading state while a category opens (apps launch with visible
      progress, not a frozen UI).
- [ ] Specific, readable errors for launch failures (missing/moved exe,
      etc.), never a raw exception.

## Phase 6 — Command palette + global hotkey
- [ ] Global hotkey registration (`RegisterHotKey` via a hidden
      message-only window), configurable in Settings.
- [ ] Borderless topmost overlay, search-as-you-type over
      categories/apps, same Open/Close/Minimize actions, keyboard-driven.

## Phase 7 — Tray + autostart
- [ ] Tray icon (`H.NotifyIcon`), minimize/close-to-tray, quick category
      switch from the tray context menu.
- [ ] Autostart toggle (`HKCU\...\Run` key).

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
