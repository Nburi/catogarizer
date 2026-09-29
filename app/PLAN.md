# PLAN.md — PWA launch support

## Produkt

**Aufbau:** PWAs (Progressive Web Apps installed via Edge/Chrome/other Chromium
browsers) become pickable in the *existing* "Add app" search picker, exactly
like any native app — no new screen, no new concept in the UI beyond a small
identifying tag. Once picked, a PWA is a completely normal `AppEntry`: it
flows through the same Open/Close/Minimize category actions and the same
"grab, don't type" placement-capture flow as Notepad or VS Code. Positioning
a PWA at a specific location isn't a separate feature to build — it's what
already happens for every `AppEntry` once window-finding recognizes the
PWA's window correctly (see Umsetzung).

**Design:** one small visual addition to the existing search-picker list
(`AppEditWindow.xaml`) — a "Web App" tag next to entries detected as an
installed PWA, so it's clear at a glance what you're adding:

```
┌─────────────────────────────────────────┐
│ Search installed apps                    │
│ ┌───────────────────────────────────┐   │
│ │ [ youtube          ]               │   │
│ └───────────────────────────────────┘   │
│  ┌─────────────────────────────────┐    │
│  │ YouTube Music        [Web App]  │    │
│  │ C:\...\Application\msedge_proxy… │    │
│  ├─────────────────────────────────┤    │
│  │ YouTube Studio                  │    │  <- picked from Start Menu,
│  │ C:\...\Application\msedge_proxy… │    │     unrelated app, no tag
│  └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
```

No other screen changes — the category dashboard, placement-capture dialog,
and command palette all already treat every `AppEntry` uniformly and need no
PWA-specific UI at all.

## Umsetzung

**Research findings (Chromium's own docs + a scan of this machine's real
Start Menu shortcuts):**
- An installed PWA's Start Menu shortcut targets a small stub binary —
  `msedge_proxy.exe` (Edge) / `chrome_proxy.exe` (Chrome), same mechanism on
  other Chromium browsers — with arguments
  `--profile-directory=<profile> --app-id=<32-char id>`. The stub relaunches
  the real browser with the same flags and exits; the real browser process
  (`msedge.exe`/`chrome.exe`) ends up owning the actual window. Firefox PWAs
  use a different, unofficial mechanism and are out of scope.
- This machine currently has no PWAs installed (Start Menu only has the
  plain Chrome/Edge shortcuts, no `--app-id`) — real end-to-end testing needs
  at least one installed, see "Verification" below.
- Three concrete gaps found by tracing the existing code against this:
  1. `InstalledAppFinder` only reads a shortcut's `TargetPath`, never its
     `Arguments` — so a PWA picked from search today would silently launch
     as `msedge_proxy.exe` with **no** `--app-id`, not the actual app. Manual
     entry (path + arguments field) already works around this by hand today,
     just not discoverably.
  2. `InstalledAppFinder`'s de-dup keys results by `ExecutablePath` alone —
     every PWA installed under the same browser/profile shares the identical
     proxy exe path, differing only in `Arguments`, so today's key would
     collapse every installed PWA down to just one entry.
  3. `WindowFinder.FindByProcessHandle` polls `Process.MainWindowHandle` on
     the PID `ProcessLauncher.Launch` returns — for a PWA that's the
     short-lived proxy's PID, which exits before owning any window. Once it
     exits, `.MainWindowHandle` throws `InvalidOperationException`
     ("process has exited"), currently uncaught — falls back to the *right*
     mechanism (title-substring search, already used for the analogous
     UWP/`ApplicationFrameHost` host-process case) only if this is caught
     rather than left to propagate as a failure.

**Design decision — window matching precision:** worth flagging explicitly
since it's a real trade-off, not a forced choice. Once a PWA is just an
`AppEntry` pointing at `msedge_proxy.exe`, matching its *running* window
(for adopt-already-open, Close, Minimize) needs the real browser's process
name, not the proxy's — so the plan strips a `_proxy` suffix and adds the
un-suffixed name as a second process-name candidate to the *existing*
`FindAllRunningWindows(processNameCandidates, titleCandidates)` call (which
already accepts more than one candidate, unused so far). Combined with the
existing title-substring match, this is the same precision level the app
already accepts for UWP host-process windows (`ApplicationFrameHost.exe`
owning windows for several packaged apps) — a regular browser tab whose
title happens to contain the PWA's name could in theory still match. A
fully precise fix exists (reading the window's `System.AppUserModel.ID`
property via `SHGetPropertyStoreForWindow` COM interop, which is how
Windows' own taskbar tells PWA windows apart) but is a materially bigger
lift — new COM interop this codebase doesn't otherwise use anywhere, versus
this app's established preference for plain P/Invoke (see `STACK.md`).
**Recommendation: ship the process-name+title approach now**, note the AUMID
route in `TODO.md`'s "known open items" the same way existing accepted
limitations are tracked, and only build it if the imprecision actually bites
in practice. Flagging this for your call rather than deciding it silently.

**Stack:** no new dependencies — plain string/exception-handling changes in
`Catogarizer.Core`/`Catogarizer.Win32`, one small XAML tag in
`Catogarizer.App`. No matching existing skill (this is Win32/.NET interop
work, not a covered file-format/UI-pattern skill).

**Order of changes:**
1. `WindowFinder.FindByProcessHandle` — catch `InvalidOperationException`
   from an exited PID, fall through to the title-substring search. Fixes
   both category-open and the placement-capture dialog (`PlacementDialogViewModel`
   reuses the same `FindMainWindow` call) in one place.
2. `InstalledApp` — add `string? Arguments`; `InstalledAppFinder` reads
   `shortcut.Arguments` alongside `TargetPath`; de-dup key becomes
   path+arguments instead of path alone.
3. `AppEditDialogViewModel.PickInstalledApp` — pass `app.Arguments` through
   to `Result` instead of the current hardcoded `null`.
4. `CategoryActionService.FindRunningWindows` — process-name candidates
   include the `_proxy`-suffix-stripped name when applicable.
5. `InstalledApp` — add computed `bool IsPwa` (`Arguments` contains
   `--app-id=`); `AppEditWindow.xaml` shows the "Web App" tag when true,
   reusing the existing `BoolToVis` converter already in that file.
6. xUnit coverage: dedup-by-path+arguments and `Arguments`/`IsPwa` handling
   in `InstalledAppFinder`'s scope (Win32, mirrors this project's existing
   pattern of leaving real shell/P-Invoke glue untested) is limited to what's
   feasible without a real shortcut on disk; the process-name-candidate
   expansion in `CategoryActionService` is pure Core logic and gets a real
   test alongside the existing `CategoryActionServiceTests.cs`.

**Verification (needs your go-ahead before any of it runs — see chat):**
install one real PWA (your choice of site) to get a genuine `--app-id`
shortcut, then run the built app and confirm: it shows up tagged in the
picker, Open/Close/Minimize all target only that PWA's window (not your
regular browser windows), and a captured placement re-applies on next Open.

## Entscheidung

Confirmed 2026-08-12: proceed with the plan as written, including the
simple process-name+title matching approach (not AUMID/COM). Implementation
(Step 5) starts now; actually running/launching anything to test (Step 6)
stays gated behind a separate check-in per the user's explicit request.
