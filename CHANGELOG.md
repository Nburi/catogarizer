# Changelog

## [0.1.0] - Initial build
### Added
- Project concept, requirements, design exploration (`design/concepts.html`)
  and tech stack decided: .NET 10 + WPF, Launcher Grid layout, Cyberpunk Neon
  theme.
- Category management: create, rename, delete categories with inline
  validation (duplicate-name checking) and a delete confirmation dialog.
- App management within a category: add/edit/delete apps with executable
  path, arguments, working directory, window position/size, and launch
  delay; a "capture window position" helper that lists currently open
  windows and copies a selected one's real position/size into the form.
- One-click Open / Minimize / Close for a whole category, with a status
  banner reporting a friendly summary (including partial failures) instead
  of crashing on a bad path or missing executable.
- System tray icon with a context menu (Show, quick per-category Open,
  Exit); minimizing or closing the main window hides it to the tray instead
  of exiting.
- Settings page: autostart with Windows (registry Run key) with an option
  to start minimized to tray, and a blocked-apps list that force-closes
  matching processes as soon as they launch, with a toast notification.
- Cyberpunk Neon visual theme throughout, plus motion polish: animated
  toggle switch, button hover glow, loading spinner, custom keyboard focus
  ring, window fade-in.
- Automation (`AutomationRule`) and to-do integration (`ITodoIntegrationProvider`)
  seams - data model and interface only, not wired into any UI yet.
- Self-contained single-file win-x64 publish profile; product metadata and
  app icon.
- 18 unit tests covering Core domain logic (config persistence, validation,
  category action orchestration).

### Changed
- Theme simplified to a single primary accent color (magenta) — the
  magenta/cyan gradient and all cyan-only usages (focus ring, spinner, status
  banner, textbox focus) were replaced with one solid `PrimaryBrush`. Danger
  red stays separate as a semantic error color.
- App tiles are now perfectly square, with the remove button moved to a
  top-right overlay corner. The edit pencil/remove-button row was removed:
  a single click on a tile now opens that one app, and a double click opens
  the edit dialog (also reachable from the right-click context menu, which
  gained Edit/Remove entries).

### Fixed
- Category and app-tile Close/Minimize actions now verify the target window
  actually closed or minimized before reporting success, instead of assuming
  the fire-and-forget Win32 request worked. Apps that ignore the close
  request, prompt to save, or minimize-to-tray instead of exiting (Discord,
  Spotify, and similar apps) are now correctly reported as failed rather than
  the status banner falsely claiming "closed"/"minimized".
- Fixed the deeper reason Close/Minimize did nothing at all for several real
  apps: `FindRunning` required an exact "process name AND file path" match and
  stopped at the first name hit, so multi-process apps (Discord, Spotify -
  renderer/GPU/utility processes share the visible one's name but not its
  window) and helper-process apps (Steam's window is owned by
  `steamwebhelper.exe`, never `steam.exe`) were invisible to the app - Close/
  Minimize silently reported "Not running" for something clearly running.
  `FindRunning` now prefers any same-name match that owns a window over an
  exact-path match that doesn't, and falls back to scanning for a
  window-owning process under the same install directory.
