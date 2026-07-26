# TODO

## Build plan (in order)
- [x] 1. Solution scaffolding, project docs, `.gitignore`
- [x] 2. Core domain models + JSON store + unit tests
- [x] 3. Win32 layer: launch process, enumerate/move/resize/minimize/close windows
- [x] 4. Main window shell: custom chrome, sidebar category list, empty state
- [x] 5. App tile grid wired to Open/Minimize/Close-all, with loading state
- [x] 6. Add/Edit Category & App dialogs, "capture window position" helper, validation
- [x] 7. Tray icon, minimize-to-tray, quick category switch from tray menu
- [x] 8. Autostart toggle + Settings page
- [x] 9. App blocking: background process watcher + notification
- [x] 10. Cyberpunk Neon visual polish pass
- [x] 11. Automation/integration seams (`ITrigger`/`IAction`, `ITodoIntegration` stub)
- [x] 12. Self-contained publish profile + app icon
- [ ] 13. Full test run + manual pass (golden path + edge cases)
- [ ] 14. User guide

## Backlog (future features, not yet built)
- Scheduled/automatic category triggers (e.g. open a category at a set time) -
  data model (`AutomationRule`) and persistence already in place, no scheduler
  or UI to create rules yet
- To-do list API integration - `ITodoIntegrationProvider` seam exists, no
  concrete provider or UI
- Global hotkey for quick category switch
- Additional themes (Dark Focus, Light Clean, Nord Cool, Minimal Mono — already
  designed in `design/concepts.html`) aren't wired up as switchable options
  yet; only Cyberpunk Neon ships
- Reordering categories/apps (drag-and-drop, or simple up/down). Not built yet
  - new categories/apps are just appended at the end (`Order = current count`)
- Secondary/icon buttons (ActionButtonSecondary, RowIconButton,
  CategoryListBoxItem) still use instant hover-color swaps rather than the
  animated-overlay pattern used for the primary button/toggle switch -
  animating those safely needs a template restructure (to avoid mutating a
  shared brush instance), deliberately deferred rather than rushed
- App/tray icon (`Assets/app.ico`) is a single 64x64 resolution rather than a
  true multi-resolution icon (16/32/48/256) - a hand-rolled multi-size ICO was
  attempted and produced corrupted pixel data at some sizes, so it was reverted
  in favor of the known-good single size. Windows scales it adequately for
  taskbar/title bar/tray; it just won't be pixel-crisp at every size.

## Known issues
- Apps that restore their own last window state on launch (e.g. modern Windows
  Notepad reopening its previous session) can override the position/size we
  just applied, shortly after we apply it. Not fixable in general; a future
  option is re-applying the saved rect once more after a short delay.
- Launcher-stub executables that spawn the real UI in a different process and
  exit immediately (e.g. `control.exe`) won't get a window handle from our
  launch-and-poll approach, so positioning is skipped and the category won't
  recognize them as "running" afterward. Verified during manual testing of
  milestone 5 - stick to apps whose main .exe stays resident when adding test
  categories.
- (Fixed in milestone 6) `CategoryEditDialogViewModel.NameError` was missing
  `[NotifyPropertyChangedFor(nameof(HasError))]`, so the inline validation
  message never became visible even though validation itself worked. See
  ERRORS.md.
