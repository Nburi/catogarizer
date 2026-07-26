# Concept

## The idea
A Windows desktop app that groups applications into **categories** (workspaces) —
e.g. "Deep Work", "Design", "Comms" — each with a saved set of apps, window sizes,
and positions. One click opens, closes, or minimizes an entire category at once,
so switching mental context is a single action instead of manually arranging a
dozen windows.

## Target audience
Primarily a personal productivity tool for the author — a developer/knowledge
worker who juggles several distinct app contexts a day and loses focus to the
friction of manually opening/arranging/closing windows each time. Secondary
audience: other power users with the same problem (this is built to a standard
where it could be sold, not just a personal script).

## Core requirements
- Categories of apps, each app launched at a specific window size + position
- One-click Open / Close / Minimize per category, plus per-app override
- Quick switching between categories
- Blocking distracting apps (process-level)
- Auto-start with Windows
- System tray presence
- Polished custom UI (no native alert/confirm dialogs), proper loading/empty/
  error states, inline form validation
- Multi-monitor aware

## Future (designed for, not built yet)
- Automation: rule-based triggers (e.g. time-of-day) that open a category
  automatically
- To-do list API integration

## USP
Combines what window-manager utilities (e.g. FancyZones) and app-blockers
(e.g. Cold Turkey) do separately, into one "whole workspace, one click" tool
with saved window geometry per app.

## Chosen design
- **Layout:** Launcher Grid — sidebar of categories, tile grid of that
  category's apps, Open/Minimize/Close toolbar always visible.
- **Theme:** Cyberpunk Neon — ink-black surfaces, magenta/cyan accents.
- Full design exploration (6 layouts × 5 themes): `design/concepts.html`
