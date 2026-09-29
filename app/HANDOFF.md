# HANDOFF — Catogarizer

> **Created:** 2026-09-29
> **Context:** v2 rebuild (session-based category switching) on branch `feature/v2-switching-ui`: planned with the user, build steps 1–7 done and live-tested, review rounds 0–2 done and fixed, round 3 half fixed. Session ended because the context filled up.

---

## Project goal

Catogarizer is a Windows tray app (WPF, .NET 10) for switching between kinds of work. The user groups apps into **categories**; exactly one category is always active, including the implicit **Unsorted** one. **Switching** hides (never closes) the outgoing category's windows and restores the incoming category's windows, or launches its template apps if it has none parked. Categories can hold back (soft-block) distracting apps while active. The everyday surface is a **command palette** on a global hotkey (default Ctrl+Alt+Space). **Pressing it twice = back to the previous category** is the signature moment.

Read these first, in this order:
1. **`PRINCIPLES.md`** — binding core values plus the decisions made with the user (Fjord theme, "Now + Shelf" home, double-tap back, English UI, "Unsorted"). Check every plan against it.
2. **`TODO.md`** — Phase 13 section, "Build plan" steps 1–9 with detailed notes per step.
3. `ERRORS.md` — pitfalls that already bit (read before live testing!).
4. `CONCEPT.md`, `STACK.md` — background. `design/concepts-v2.html` is the approved design blueprint, published at https://claude.ai/artifact/JvwNRjtNaDKjBgAqR5bMmX.

---

## Already done (all on `feature/v2-switching-ui`, all committed, 227/227 tests green)

| Step | What | Commit |
|---|---|---|
| Plan | Blueprint page, principles, decisions | 455736f, 96cbcfc |
| 1 | Safety net: `hidden.json` ledger written *before* every hide, crash recovery on next start, `ShowAllAndReset`; Alt-Tab window filter (never hide desktop/tool windows); own windows never tracked | 0d3c8e4 |
| 2 | `CategorySwitcher` = the single switch path for home/palette/tray/CLI/triggers; blocking follows the active category; CLI `switch "<name>"`, `back`, `show-all`, `run "<trigger>"` over one named pipe | 596e9e9 |
| 3 | Double-tap hotkey → back, `SwitchPillWindow` "← ● Comms" pill | f56349f, fix 3c0ce5a |
| 4 | 7 themes defined in Core (`Theming/ThemeCatalog`, OKLCH) with contrast unit tests, live switching + Settings picker, themed title bars | 30efa3d |
| 5 | Category colors (`Category.Hue`), real app icons (`ShellIcons`, `AppIconCache`, `AppIconView`) | 3d7ae36 |
| 6 | "Now + Shelf" home, separate category editor window | adc1099 |
| 7 | Palette v2: number keys, smart/accent-insensitive search, preselects the last category | 5d2ef75 |
| 8 | Review rounds 0–2 plus fixes; round 3 part 1 | 1f22d03 … a40e3d6 |

Live-verified on the user's PC: switching, restore in ~200 ms, crash recovery of 8 hidden windows, blocking order, double-tap pill, themes, home, editor, palette.

---

## In progress

- **Review round 3 ("where the UI lies"), part 2.** Part 1 is committed (a40e3d6). Still open, with exact places:
  - **Hero window rows look clickable but aren't.** `MainViewModel.BringToFrontCommand(WindowRow)` already exists. Wire it in `src/Catogarizer.App/MainWindow.xaml` (the `ActiveWindows` ItemTemplate): make the row a `GhostRowButtonStyle` button with `Command="{Binding DataContext.BringToFrontCommand, RelativeSource={RelativeSource AncestorType=Window}}"`, plus a hover state.
  - **Editor row for an app whose exe is missing** (`Views/CategoryEditorWindow.xaml` "Opens with" list): it shows "Opens at its default position" and live ▶/—/⏹ buttons that do nothing. It should say "Not found at <path>" in `DangerBrush`, keep only ✎ Edit and Remove active, and the "wasn't found" notice should add "Use ✎ to point to the new location, or Remove it."
  - **Automation window** (`Views/TriggersWindow.xaml`, `ViewModels/TriggersViewModel.cs`):
    - "Run now" on a trigger with no actions is silent. Show "This trigger has no actions yet. Edit it to add some." and ideally disable the button.
    - The enable checkbox has no visible label. Add `Content="On"` or a text.
    - `TriggerRowViewModel` needs `public override string ToString() => Name;`, because the screen reader currently reads the type name.
  - **"Set window position" dialog** (`ViewModels/PlacementDialogViewModel.cs`): "Save" is enabled with nothing captured or changed. Give `SaveCommand` a `CanExecute` that's true only after a capture or a clear, or when a placement already existed.
  - Low priority: after "Show all hidden windows", tiles say "Opens 2 apps" even though those apps are open (the switch then adopts them, which is fine). Could say "Open in Unsorted" instead. Optional.

---

## Still to do

### High
- [ ] Finish round 3 part 2 (list above). Then build, test, commit, and do a short live check.
- [ ] **Round 4: user simulation.** Use agent `fs-nutzer-simulant` with a role that is *not* the planned user (see the role catalog in the feature-schmiede skill, e.g. "someone opening it for the first time" or a skewed role). Give it only a role, a goal and one sentence about the app, plus the same safety rules and `tools/livetest/` scripts as rounds 0–3 (copy the prompt structure from those rounds, summarized under "Commands" below). Evaluate: friction → fix; missing capability → "later" list; **did the signature moment (double-tap back) show up in its log at all?**
- [ ] Repeat rounds until a whole round finds nothing new (feature-schmiede stop rule).

### Medium (step 9)
- [ ] Update docs: `TODO.md` (tick step 8), `USER_GUIDE.md` (still describes v1 Open/Close/Minimize, so it needs a rewrite for switching, palette, double-tap, themes, editor, CLI `switch/back/show-all`), `CONCEPT.md`/`STACK.md` where outdated. Create `CHANGELOG.md` (required by the user's global rules, doesn't exist yet).
- [ ] Run the `code-review`, `simplify` and `security-review` skills (CLI named pipe input, `hidden.json`) before asking to merge.
- [ ] Hand over to the user for review. **Merge and deploy only when the user says so.** Deploy = publish (`dotnet publish src/Catogarizer.App/Catogarizer.App.csproj -c Release -p:PublishProfile=win-x64-selfcontained`), then copy the exe to `C:\projects\03 Apps` (on `%PATH%`), per the project `CLAUDE.md`. The docs branch `docs/v2-ui-plan` is the base of this branch and is also unmerged.

### Low / later
- [ ] Real app icon and branding (the tray/title icon `Assets/app.ico` is still the old blue placeholder and clashes with Fjord).
- [ ] Store apps (Windows 11 Notepad, Calculator) aren't in the "Add app" search. Their path contains a version number, so an app added from "open windows" breaks after a Store update and shows "1 app not found".
- [ ] PWAs show their browser's icon, not their own.
- [ ] Old open items from Phase 12: mixed-DPI placement capture bug, AUMID-based PWA matching.
- [ ] Drag-and-drop reordering of categories.

---

## Project structure (v2-relevant parts)

```
catogarizer/
├── PRINCIPLES.md            binding core values + decisions (read first)
├── TODO.md                  phase-by-phase log; Phase 13 = this work
├── ERRORS.md                pitfalls that already happened
├── design/concepts-v2.html  approved v2 blueprint (themes, 6 designs, plan)
├── tools/livetest/          safe live-test scripts (see Commands)
├── tools/HideShowSpike/     throwaway harness; `list` mode = what a switch would track
├── src/Catogarizer.Core/
│   ├── Services/CategorySwitchService.cs   sessions, hide/show, ledger, claiming, pinning
│   ├── Services/CategorySwitcher.cs        the one switch path (+ blocking order)
│   ├── Services/CategoryMatcher.cs         palette search scoring
│   ├── Services/DoubleTapDetector.cs       signature moment timing
│   ├── Services/AppWindowMatcher.cs        app ↔ window matching (incl. PWA proxy)
│   ├── Persistence/JsonHiddenWindowStore.cs crash-safety ledger
│   ├── Theming/                            Oklch, ThemeCatalog, CategoryHues
│   ├── Automation/TriggerPreview.cs        "Next: ..." footer
│   └── Cli/CliCommand.cs                   start/run/switch/back/show-all
├── src/Catogarizer.Win32/                  WindowFinder (Alt-Tab filter), WindowManager,
│                                           ShellIcons, TitleBarStyle, OverlayWindowStyle
├── src/Catogarizer.App/
│   ├── App.xaml.cs                         composition root, hotkey/double-tap, CLI queue, crash safety
│   ├── MainWindow.xaml(.cs)                "Now + Shelf" home
│   ├── ViewModels/MainViewModel.cs, HomeItems.cs
│   ├── ViewModels/CategoryEditorViewModel.cs + Views/CategoryEditorWindow.xaml
│   ├── ViewModels/CommandPaletteViewModel.cs + Views/CommandPaletteWindow.xaml
│   ├── Views/SwitchPillWindow.xaml(.cs)    signature moment, off-limits for "consistency" cleanups
│   ├── Services/ThemeService.cs, AppIconCache.cs, AppNames.cs, EditorServices.cs, TrayIconController.cs
│   ├── Controls/AppIconView.xaml(.cs)
│   └── Themes/Controls.xaml                styles only; colors come from ThemeService (DynamicResource)
└── tests/Catogarizer.Core.Tests/           xUnit, 227 tests, fakes in Fakes/
```

---

## Important decisions and context

- **Principles are binding** (`PRINCIPLES.md`). Examples: nothing is ever lost (hide, never close, and every hidden window must be recoverable); switching must feel instant, so there's no busy overlay on restore; keyboard first, but every key action also has a mouse path; no admin rights, no cloud.
- **Signature moment = double-tap hotkey → previous category + pill.** Don't "unify" or clean it up.
- **Theme palettes live in Core as data** (`ThemeCatalog`) and are converted at runtime by `ThemeService`. **All color references in XAML must be `DynamicResource`.** New themes need to pass `ThemingTests` (contrast).
- **Blocking order:** `CategorySwitcher` lifts the outgoing blocklist *before* the incoming template launches. Otherwise Deep Work blocking Slack would kill the Slack that Comms opens.
- **Claiming:** a category without a session takes over matching *Unsorted* windows of its template apps (one per app, never from other categories), instead of hiding them and launching duplicates.
- **Deviation from the blueprint, agreed implicitly:** no separate "switch receipt" toast, because it would compete with the pill. Palette v2 was built without a separate preview because the user asked not to stop between steps.
- **Store apps / Notepad:** accepted limitation (see Low).

---

## Known problems and pitfalls

- **Live tests touch the user's real windows.** Always use `tools/livetest/setup.ps1` and **always** end with `teardown.ps1`. **Never delete `%LOCALAPPDATA%\Catogarizer\hidden.json`** after killing the app (that stranded a user window once, see ERRORS.md). End with `Catogarizer.App.exe show-all`, which teardown already does. The user granted full access for live tests in this session ("full access"). The memory file says: ask again when the scope changes and the user may be at the PC.
- **Build fails while the test app runs** (exe locked). Run teardown first.
- **Windows PowerShell 5.1 needs a UTF-8 BOM** in scripts with umlauts (`setup.ps1` has one; keep it).
- **`Graphics.CopyFromScreen` misses layered windows** (palette, pill). Use `capture.ps1` (PrintWindow, test-app windows only). A locked PC makes captures black and keys go nowhere; check for `LogonUI`.
- **Tray icon must be force-created** (`TaskbarIcon.ForceCreate(false)` in `TrayIconController`). Without it, every notification crashed the app.
- **CLI commands are queued** (listener plus worker thread in `App.xaml.cs`). Don't run switches on the pipe listener thread again.
- The review agents (`fs-*`) left extra helper scripts (`btn.ps1`, `setedit.ps1`, `radio.ps1`, …) only in the old session scratchpad. They aren't needed; `uia.ps1` + `uia-dump.ps1` suffice.

---

## Next step (start here)

1. `git status` (should be clean) and `git log --oneline -3`. HEAD should be at or after `a40e3d6`, on `feature/v2-switching-ui`.
2. `dotnet build Catogarizer.slnx` and `dotnet test tests/Catogarizer.Core.Tests/Catogarizer.Core.Tests.csproj` should give 227/227.
3. Fix the **round 3 part 2** list under "In progress", starting with wiring `BringToFrontCommand` on the hero window rows in `MainWindow.xaml`. Then build, test, commit (`FIX: round-3 review findings, part 2 - ...`), and update `TODO.md`.
4. Launch round 4 (`fs-nutzer-simulant`) as described above.

---

## Commands

```bash
# Build / test
dotnet build Catogarizer.slnx
dotnet test tests/Catogarizer.Core.Tests/Catogarizer.Core.Tests.csproj

# Debug build exe (test instance, process name Catogarizer.App)
"src/Catogarizer.App/bin/Debug/net10.0-windows/Catogarizer.App.exe"
#   ... switch "Test A" | back | show-all | run "<trigger>"

# The user's installed app: "C:\projects\03 Apps\Catogarizer.exe" (process name Catogarizer)
```

```powershell
# Safe live test (PowerShell). setup backs up the real config on first run
# (tools/livetest/config.backup.json, git-ignored), snapshots the user's windows,
# stops the installed app and starts the debug build with test data.
& "tools\livetest\setup.ps1"            # standard data: Test A/Test B/Research/Broken, Claude pinned
& "tools\livetest\setup.ps1" -Empty     # first launch
& "tools\livetest\setup.ps1" -Full      # 18 categories, long names, umlauts
& "tools\livetest\setup.ps1" -Theme graphite
& "tools\livetest\capture.ps1" -OutPrefix "<dir>\shot"   # screenshot of test-app windows only
& "tools\livetest\uia-dump.ps1"                          # UI tree as text
& "tools\livetest\uia.ps1" -Action invoke -Name "Settings"   # modal dialogs: call via Start-Job
& "tools\livetest\doubletap.ps1" -OutDir "<dir>" -ShotsAtMs @()   # hotkey twice; -Single = once
& "tools\livetest\teardown.ps1"         # ALWAYS: show-all, stop test app, restore config, restart installed app
```

The review-agent prompts for rounds 0–3 all had the same structure: app description, safety rules (the "never" list above), the tools, the concrete task, and the output format (German, findings marked, no solution proposals). Reuse it for round 4, but give the simulant only a role, a goal and one sentence about the app.

---

## Notes for the next AI

- **The user wants no pauses between plan steps** ("Continue and no longer stop between steps until I tell you"). Keep building, testing and committing until they say stop. Only stop when genuinely blocked (global rule 6: the same thing failed twice means stop and explain).
- Global rules (`C:\Users\ninob\.claude\CLAUDE.md`): the user wants a co-worker who suggests alternatives; commit format `<CATEGORY>: <summary>` plus body; tests before every commit; no native dialogs; handle loading, empty and error states; English code and commits; don't merge, push or deploy without being told; ask before destructive actions.
- The project `CLAUDE.md` says to deploy the exe to `C:\projects\03 Apps` after the user's review or a merge to main. Don't edit `CLAUDE.md` without asking.
- The user is Swiss/German-speaking (Windows is German: "Zeichentabelle", "Rechner"). The app UI stays English.
- The user likes being shown results: send screenshots via SendUserFile at milestones.
