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
