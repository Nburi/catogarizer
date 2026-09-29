# CLAUDE.md - catogarizer (monorepo)

Catogarizer groups apps into categories that can be switched with one keystroke.

## Layout
- `app/` - the Windows desktop app (WPF, .NET 10). Has its own `CLAUDE.md` with the build/deploy procedure, plus its docs (CONCEPT, STACK, TODO, ...).
- `website/` - the public marketing/download site. Plain static HTML/CSS/JS, no framework, no build step. It reuses the app's default theme (Fjord, see `app/src/Catogarizer.Core/Theming/ThemeCatalog.cs`).

When working on the app, read `app/CLAUDE.md` first. Git is initialised once, here in the root.
