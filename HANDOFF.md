# HANDOFF — Catogarizer

> **Created:** 2026-07-29, end of session
> **Context:** Full rebuild of Catogarizer (a Windows tray app that groups other apps into one-click-openable "categories") on branch `refactor/full-rebuild`. Phases 1–8 fully done and tested; Phase 9 partially done (Settings page shipped, UX polish pass still open). Stopping here because the session's context window is full.

---

## Project goal

Catogarizer is a Windows desktop productivity app: group apps into **categories** (e.g. "Deep Work", "Comms"), each with saved window position/size per app, and open/close/minimize the whole category with one click. Categories can also **block** distracting apps while they're the active one (soft-block: kill the process shortly after it starts, no admin rights). A global-hotkey **command palette** gives a fast keyboard-driven way to switch categories from anywhere. Runs from the **system tray**, autostarts with Windows.

Read **`CONCEPT.md`** first — it's the full requirements doc and is short. Then **`STACK.md`** (tech choices + why, including two decisions that were *reversed* after empirical testing — see "Known issues and pitfalls" below). Then **`TODO.md`** — this is the authoritative, up-to-date phase-by-phase implementation plan/checklist; every phase 1–9(partial) has detailed notes on what was built, what was tested, and what's still open. **Read `TODO.md` in full before doing anything else** — it has more implementation detail than this handoff repeats.

This is a **complete rebuild** — do not go looking in other branches or old chat history for "how the first version did X". The user was explicit about this at the start of the rebuild.

---

## Already done (Phases 1–8, fully tested and committed)

All of these were verified against the **real running app** (not just unit tests) — see commit messages (`git log --oneline`) for exactly what was tested each time. 61 xUnit tests in `tests/Catogarizer.Core.Tests`, all passing.

- **Phase 1 — Foundation**: solution structure (`Catogarizer.Core` / `Catogarizer.Win32` / `Catogarizer.App` / `Catogarizer.Core.Tests`), Core models, JSON persistence, Dashboard Home shell in the "Daylight Studio" theme, single-instance handling, top-level crash handler.
- **Phase 2 — Win32 interop layer**: window finding (multi-strategy, locale-aware), positioning (`SetWindowPlacement` + restore-if-maximized + settle-and-retry), minimize/close/force-kill, monitor enumeration. All the actually-risky logic (retry policy, placement math) lives in **Core** as pure/tested code; Win32 project is thin P/Invoke wrapping, verified via throwaway smoke-test console apps (built, run, deleted each time).
- **Phase 3 — Category & app CRUD**: `LibraryService` (Core) owns all mutation; installed-app search picker (Start Menu `.lnk` + registry Uninstall keys, filtered for uninstallers/system components); manual path fallback; themed dialogs.
- **Phase 4 — Window placement capture**: "grab, don't type" — launch the app, drag it into place, capture reads back the real bounds.
- **Phase 5 — One-click category actions**: `CategoryActionService` (Core) — the actual value proposition. Open/Minimize/Close per category and per app, background-threaded with a busy overlay, readable errors in a dismissible banner.
- **Phase 6 — Command palette + global hotkey**: `GlobalHotkeyService` (Win32) runs its own hidden-window message loop for `RegisterHotKey`/`WM_HOTKEY`. Borderless overlay window, search-as-you-type over categories.
- **Phase 7 — Tray + autostart**: `H.NotifyIcon`-based tray icon, close/minimize-to-tray, per-user autostart via Registry Run key, second-instance-signals-first-instance-to-show.
- **Phase 8 — App blocking**: `AppBlockingService` (Core) ties a process watcher to per-category blocklists, activated on category open / deactivated on close, unioned across simultaneously-open categories. Real system toast notification when something gets blocked.
- **Phase 9 (partial) — Settings page**: gear icon in the main window header opens a Settings dialog — Start with Windows (reads live registry state, not a persisted flag), Start minimized to tray (now actually wired into startup), and a hotkey-rebind capture box that re-registers the hotkey live in the running app.
- Also done as part of Phase 9: `AutomationRule`/`AutomationTriggerType`/`AutomationActionType` and `ITodoIntegrationProvider`/`TodoItem` added to Core (data model/interface stubs only, nothing wired — per CONCEPT.md's "future, prepare for" section). App icon (`Assets/app.ico`, placeholder, generated procedurally — see pitfalls below).

---

## Still to do

### High priority — finish Phase 9, then do Phase 10

- [ ] **Loading state for the installed-apps search dialog.** `AppEditDialogViewModel`'s constructor calls `IInstalledAppFinder.FindInstalledApps()` synchronously (~300ms for ~79 apps on the dev machine). Fine today, but blocks dialog-open on a slower/bigger Start Menu. Should show the dialog immediately with a loading indicator, populate results once the background scan completes. This was explicitly named in CONCEPT.md's UX requirements ("loading states for... searching installed apps").
- [ ] **Responsive resizing check.** Never explicitly tested resizing `MainWindow` smaller. The `WrapPanel` (category cards) + `ScrollViewer` should handle it reasonably, but verify — especially the category-detail panel and the app/blocked-app rows, which have fixed-ish `Grid` columns.
- [ ] **Motion/hover polish.** Zero animation anywhere in the app right now — every hover/press state (`GhostIconButtonStyle`, `PrimaryButtonStyle`, etc. in `Themes/DaylightStudio.xaml`) snaps instantly via plain property `Trigger`s. Add short (~150ms) ease-out `DoubleAnimation`s via `Trigger.EnterActions`/`ExitActions` + `BeginStoryboard`. Check whether `SystemParameters.ClientAreaAnimation` exists in this WPF version to respect the OS reduced-motion setting; if uncertain/unavailable, keeping animations short and subtle is an acceptable fallback (was the plan when this session ran out of room).
- [ ] **Contrast check** on anything added since Phase 1's original pass (the blocked-app red-bordered boxes reuse existing ink/muted text colors on existing backgrounds, so likely fine — just wasn't re-verified explicitly).

### Phase 10 — Tests & packaging (not started)
- [ ] Full xUnit suite green (already true — 61/61 — but re-confirm after Phase 9 finishes).
- [ ] Self-contained single-file `win-x64` publish profile (`dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`) — matches the deploy flow in the project's `CLAUDE.md` (deploy to `C:\projects\03 Apps`, already on `%PATH%`).
- [ ] Manual end-to-end pass in the actual **published** exe, not just `dotnet build`/`dotnet run` — single-file publish can surface issues (missing runtime assets, resource loading via `pack://` URIs, etc.) that don't show up in a normal debug build.

### After Phase 10 — the two remaining items from the original 7-step plan (see the very first user message in this conversation / project CLAUDE.md)
1. **Step 6 wrap-up**: once Phase 10 is done, the "build the app" step is complete.
2. **Step 7 — user guide**: write a brief USER_GUIDE.md once everything is finished and tested. Don't do this before Phase 10 is done and manually verified.

Also: the user's global `CLAUDE.md` says to **deploy the built .exe to `C:\projects\03 Apps`** once a feature is reviewed/approved and merged, and **not to merge or push branches without being told to**. Nothing has been merged or pushed — still on `refactor/full-rebuild`, all work is local commits only. Do not merge/push/deploy without the user explicitly asking.

---

## Project structure

```
catogarizer/
├── CLAUDE.md              — project-specific instructions (deploy procedure). Don't touch without asking.
├── CONCEPT.md              — requirements, chosen design (Dashboard Home layout, Daylight Studio theme, command palette)
├── STACK.md                 — tech stack + why, including the two decisions reversed by empirical testing
├── TODO.md                — THE authoritative phase-by-phase plan/checklist — read this in full
├── Catogarizer.slnx
├── design/
│   └── concepts.html        — interactive design exploration tool (6 layouts × 6 themes) from step 2, kept for reference
├── src/
│   ├── Catogarizer.Core/    — pure logic, NO Win32/WPF dependency, targets plain net10.0
│   │   ├── Models/           — Category, AppEntry, WindowRect, AppSettings, BlockedApp, AppConfig
│   │   ├── Persistence/      — IConfigStore / JsonConfigStore / ConfigCorruptException
│   │   ├── Services/         — LibraryService (CRUD), CategoryActionService (open/close/minimize),
│   │   │                       AppBlockingService, validators, Window*/Monitor* interfaces + pure helpers
│   │   ├── Automation/        — AutomationRule + enums (stub, unwired)
│   │   └── Integrations/      — ITodoIntegrationProvider + TodoItem (stub, unwired)
│   ├── Catogarizer.Win32/    — P/Invoke implementations of the Core interfaces, targets net10.0-windows
│   │   ├── Interop/NativeMethods.cs — all P/Invoke declarations in one place
│   │   ├── WindowFinder.cs, WindowManager.cs, MonitorService.cs, ProcessLauncher.cs
│   │   ├── ProcessWatcher.cs  — polling-based (NOT WMI — see pitfalls)
│   │   ├── GlobalHotkeyService.cs, HotkeyStringParser.cs
│   │   ├── InstalledAppFinder.cs, AutostartService.cs
│   │   └── Assets/... (icon lives in Catogarizer.App, not here)
│   └── Catogarizer.App/     — WPF UI, composition root is App.xaml.cs
│       ├── App.xaml(.cs)     — startup, single-instance, tray/hotkey/blocking wiring
│       ├── MainWindow.xaml(.cs) — Dashboard Home
│       ├── Themes/DaylightStudio.xaml — all colors/styles (contrast-checked OKLCH→sRGB)
│       ├── ViewModels/        — MainViewModel is the big one; one ViewModel per dialog
│       ├── Views/             — one Window per dialog (CategoryEdit, AppEdit, Confirm, Placement,
│       │                        CommandPalette, Settings)
│       ├── Services/          — DialogService (View-showing, kept out of ViewModels), TrayIconController
│       ├── Converters/
│       └── Assets/app.ico     — placeholder icon (see pitfalls)
└── tests/
    └── Catogarizer.Core.Tests/ — xUnit, only tests Core (Win32 is smoke-tested manually, not unit-tested)
        └── Fakes/              — Fake* implementations of every Core interface, used across test files
```

### Key files to read before changing things

| File | Why it matters |
|---|---|
| `TODO.md` | The real source of truth for what's done/open, phase by phase, with implementation notes |
| `CONCEPT.md` | Requirements + the two design decisions that came out of the user picking from `design/concepts.html` |
| `STACK.md` | Why WPF/.NET 10, plus the WMI→polling and WM_HOTKEY-architecture corrections |
| `src/Catogarizer.App/App.xaml.cs` | Composition root — every service gets constructed and wired here |
| `src/Catogarizer.App/ViewModels/MainViewModel.cs` | Biggest ViewModel, most commands live here |
| `src/Catogarizer.Core/Services/LibraryService.cs` | All persisted-data mutation goes through this one class |

---

## Important decisions and context

- **No native dialogs, ever, except two deliberate exceptions**: the very-first-startup "already running" message and the top-level unhandled-exception handler in `App.xaml.cs` use native `MessageBox` on purpose (they need to work even if the app's own UI/theme system is what's broken). The blocked-app toast (`TaskbarIcon.ShowNotification`) is also intentionally a native OS toast, not a themed in-app dialog — the user is very likely in a *different* app when it fires, so an in-app dialog tied to Catogarizer's own (possibly hidden) window wouldn't be seen. Every other dialog in the app is a themed WPF window.
- **Blocking is soft, not hard**: kills the process ~350ms after it starts, doesn't prevent it from starting at all. This is a deliberate, user-confirmed trade-off (avoids needing admin rights). Don't try to make it a true prevention mechanism without checking with the user first — that would need elevation.
- **An app can belong to multiple categories.** `Category.AppIds`/`BlockedAppIds` are references (`List<Guid>`), not ownership — `LibraryService.DeleteApp`/`DeleteBlockedApp` clean up references from every category, but the entry itself is a shared library item.
- **`WindowRect` placement is monitor-relative, not absolute** (`OffsetX`/`OffsetY` + `MonitorId`), specifically so saved layouts survive monitor reconfiguration. `WindowPlacementResolver` (Core, tested) does the resolution both directions (capture and apply).
- **The Win32 project has zero WPF dependency on purpose** (`GlobalHotkeyService` runs its own hidden-window message loop on a dedicated thread rather than hooking into WPF's `HwndSource`/dispatcher) — keep it that way; don't add `UseWPF=true` to `Catogarizer.Win32.csproj`.
- **`AppSettings.StartWithWindows` field exists in the model but is intentionally never read/written by the app** — the Registry Run key (via `IAutostartService.IsEnabled`) is the actual source of truth, checked live everywhere it's shown (Settings page, tray menu), so it can never drift out of sync with what Windows itself displays in Startup Apps. Don't "fix" this by wiring the field — that would introduce the exact drift bug it was designed to avoid.

---

## Known issues and pitfalls (things already tried/discovered — don't re-derive)

- **`Win32_ProcessStartTrace` (WMI) requires administrator privileges.** `STACK.md` originally planned this for the app-blocking watcher. Verified directly with a throwaway console test: subscribing from a non-elevated process throws `ManagementException: Access denied`. This would have broken the no-admin-required soft-block design the user had already confirmed. **Switched to polling** `Process.GetProcesses()` every ~350ms instead (`ProcessWatcher.cs` in Win32). Don't revisit WMI eventing unless the user explicitly says the app can require elevation.
- **Setting `AutomationProperties.Name` on an `ItemsControl`'s auto-generated `ContentPresenter` container breaks WPF's automation-peer child enumeration** for that item (discovered via UI Automation testing in Phase 3 — children silently stopped showing up in the automation tree). Fix: put `AutomationProperties.Name` on the actual `DataTemplate` root element (e.g. the `Border`) instead of via `ItemContainerStyle` targeting `ContentPresenter`. Already fixed everywhere it was hit; just don't reintroduce the pattern in new `ItemsControl`s.
- **`Mutex.ReleaseMutex()` throws if the calling instance never actually owned the mutex.** `new Mutex(initiallyOwned: true, name, out createdNew)` only grants ownership when `createdNew` is true — a second instance gets a handle to the existing named mutex but never owns it. `App.xaml.cs` tracks this explicitly (`_ownsSingleInstanceMutex`) — don't remove that guard.
- **`GlobalHotkeyService`/`RegisterGlobalHotkey` must be safely re-callable** (Settings can trigger a hotkey change after startup) — `Register()` internally unregisters any previous combo, and `RegisterGlobalHotkey` in `App.xaml.cs` reuses the existing service instance rather than creating a new one each call (a first draft of this leaked a background thread and would have double-subscribed the handler; fixed before commit).
- **`AppEditDialogViewModel` is reused for two different pickers** (adding an app to a category vs. blocking an app) with different validation needs — adding requires a real, existing `.exe`; blocking should accept a bare process name with no path at all. This needed an explicit `relaxedValidation` constructor parameter — don't assume the dialog's manual-entry validation is uniform if you touch it again.
- **The legacy GDI `System.Drawing.Icon` loader chokes on an all-PNG-encoded multi-resolution `.ico`** ("range exceeds array bounds"), even though WPF/WIC loads the same file fine. `Assets/app.ico` was rebuilt with proper BMP-DIB frames for sizes ≤128px and PNG only for 256px (the standard approach) — verified against both loaders. If regenerating the icon, keep this in mind.
- **UI Automation testing via PowerShell + `System.Windows.Automation` is genuinely useful for verifying real app behavior** (caught several real bugs this session — see `TODO.md`'s Phase 3/6/7 notes) but gets flaky with rapid successive external connections to the same live process. If a query comes back suspiciously empty/sparse, don't trust it as evidence of a real bug — re-query with a short settle delay before concluding something's broken. Ground-truth checks (actual process counts via `Get-Process`, actual registry values) are more reliable than automation-tree dumps when in doubt.
- **`Process.GetProcesses()`/`.MainModule` can throw for processes you don't have query rights to** (elevated/protected/different-bitness) — always wrapped in try/catch with graceful degradation (name-only matching), don't remove those.
- The **installed-app search misses classic `notepad.exe`** on this dev machine (not in the Start Menu shortcut index) — not a bug, just means test flows should pick something known-searchable (Visual Studio Code, Google Chrome both confirmed present) or use manual entry.
- PowerShell tool notes for whoever tests next: `-ErrorAction SilentlyContinue` suppresses error *output* but the tool still reports exit code 1 on cmdlet failure — that's expected, not a real failure signal by itself.

---

## Next step (start here)

1. Run `dotnet build Catogarizer.slnx` and `dotnet test tests/Catogarizer.Core.Tests/Catogarizer.Core.Tests.csproj` from the project root to confirm the checked-out state still builds clean and all 61 tests pass (it did at handoff time).
2. Read `TODO.md` in full, especially the Phase 9 section, to see exactly what's marked done vs. open.
3. Pick up Phase 9's remaining item: **loading state for the installed-apps search dialog** (`AppEditDialogViewModel` constructor / `AppEditWindow.xaml`) — this is the most concrete, well-scoped remaining task. Make `FindInstalledApps()` run on a background thread, show the dialog immediately with a "Searching installed apps…" indicator, populate `FilteredApps` once it completes.
4. Then the responsive-resize check and motion/hover polish (both in `Themes/DaylightStudio.xaml` + `MainWindow.xaml`), per the checklist above.
5. Once Phase 9 is fully done and tested, move to Phase 10 (packaging) — see TODO.md.
6. Keep committing per logical change (one phase or sub-feature = one commit, matching the existing history), and keep testing against the *real running app* before considering something done, not just `dotnet build`/unit tests — this session caught multiple real bugs (mutex ownership, automation-peer breakage, WMI permissions, dialog validation mismatch) specifically by launching the actual app and driving it, that unit tests alone would not have caught.

---

## Commands

```bash
# Build everything
dotnet build Catogarizer.slnx

# Run the Core test suite (61 tests)
dotnet test tests/Catogarizer.Core.Tests/Catogarizer.Core.Tests.csproj

# Run the app for manual testing (from a bash-like shell)
"/c/projects/01 projects/catogarizer/src/Catogarizer.App/bin/Debug/net10.0-windows/Catogarizer.App.exe" &

# Stop it / clear test config between manual test runs
powershell -NoProfile -Command "Stop-Process -Name Catogarizer.App -Force -ErrorAction SilentlyContinue"
rm -f "/c/Users/ninob/AppData/Local/Catogarizer/config.json"
```

The app's real config lives at `%LocalAppData%\Catogarizer\config.json` — delete it to start a manual test session from a clean slate (this was done before most manual test rounds this session).

For driving the real app in tests, this session used PowerShell + `System.Windows.Automation` (UI Automation) rather than any dedicated testing framework — see the many `PowerShell` tool calls in this session's history for the pattern (find element by Name+ControlType, `InvokePattern.Invoke()`, `ValuePattern.SetValue()`). It works but see the flakiness note above.

---

## Notes for the next session

- The user explicitly asked (mid-session) to **stop pausing for confirmation between phases** and just keep building/testing autonomously, only stopping if something is genuinely broken. That instruction should still apply — keep moving through the remaining Phase 9 items and into Phase 10 without asking for permission at each step, same as this session did for Phases 4–9.
- The user's global `CLAUDE.md` (outside this repo) requires: commit after every logical change with `<CATEGORY>: <summary>` format, work in branches for multi-commit work (already on `refactor/full-rebuild`), never merge/push without being told, never guess (ask if genuinely blocked), run tests before every commit, and confirm before destructive actions.
- The project's own `CLAUDE.md` says to deploy the built `.exe` to `C:\projects\03 Apps` once the user has reviewed a finished feature, or once a branch merges to main — not applicable yet, nothing has been merged.
- Don't reference the old (pre-rebuild) app's implementation, git history, or prior chat sessions — this was an explicit instruction from the user at the very start of this rebuild.
