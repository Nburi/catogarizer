# TODO

## Build plan (in order)
- [ ] 1. Solution scaffolding, project docs, `.gitignore`
- [ ] 2. Core domain models + JSON store + unit tests
- [ ] 3. Win32 layer: launch process, enumerate/move/resize/minimize/close windows
- [ ] 4. Main window shell: custom chrome, sidebar category list, empty state
- [ ] 5. App tile grid wired to Open/Minimize/Close-all, with loading state
- [ ] 6. Add/Edit Category & App dialogs, "capture window position" helper, validation
- [ ] 7. Tray icon, minimize-to-tray, quick category switch from tray menu
- [ ] 8. Autostart toggle + Settings page
- [ ] 9. App blocking: background process watcher + notification
- [ ] 10. Cyberpunk Neon visual polish pass
- [ ] 11. Automation/integration seams (`ITrigger`/`IAction`, `ITodoIntegration` stub)
- [ ] 12. Self-contained publish profile + app icon
- [ ] 13. Full test run + manual pass (golden path + edge cases)
- [ ] 14. User guide

## Backlog (future features, not yet built)
- Scheduled/automatic category triggers (e.g. open a category at a set time)
- To-do list API integration
- Global hotkey for quick category switch
- Additional themes (Dark Focus, Light Clean, Nord Cool, Minimal Mono — already
  designed in `design/concepts.html`, just need the theme to be swappable)

## Known issues
(none yet)
