# Concept

## The idea
A Windows desktop app that groups other applications into **categories**
(workspaces) — e.g. "Deep Work", "Design", "Comms" — each with a saved set of
apps, window sizes, and positions. One click opens, closes, or minimizes an
entire category at once, so switching mental context is a single action
instead of manually arranging a dozen windows. Distracting apps can be
blocked while a category is active, turning category-switching into a
lightweight focus tool as well as a window-layout tool.

## Target audience
Primarily a personal productivity tool for the author — a developer/knowledge
worker who juggles several distinct app contexts a day and loses focus to the
friction of manually opening, arranging, and closing windows each time.
Secondary audience: other power users with the same problem. Built to a
standard where it could be sold, not just a personal script.

## USP
Combines what window-manager utilities (e.g. FancyZones/PowerToys) and
app-blockers (e.g. Cold Turkey) do separately, into one "whole workspace, one
click" tool with saved per-app window geometry, driven by categories instead
of a flat rule list.

## Core requirements (functional)

### Categories & apps
- A category is a named, user-ordered group of apps.
- An app can belong to more than one category (e.g. Slack in both "Deep
  Work" and "Comms").
- Add an app to a category two ways:
  1. **Search picker** — type-ahead search over installed apps, matching
     what shows when you press the Windows key (Start Menu / installed
     app index), so the common case needs no typing of paths.
  2. **Manual entry** — browse to / paste an .exe path, for apps the
     search index misses (portable apps, custom tools, etc.).
- Edit/remove apps and categories; reorder both.

### Window placement (size, position, monitor)
- Each app entry stores a target window rectangle (position + size) and a
  target monitor.
- Setup must be "grab, don't type": user positions/resizes a real (or
  live-preview) window where they want it and the app captures those
  coordinates — no manually typing X/Y/width/height, no repeated
  launch-check-relaunch cycles to get it right.
- Multi-monitor aware: placement is stored relative to the chosen monitor so
  it's still correct if the primary monitor changes.

### One-click category actions
- Per category: **Open** (launch/restore+position every app in it),
  **Close** (close every window belonging to its apps), **Minimize**
  (minimize every window belonging to its apps).
- Per-app override of the same three actions, for when you don't want the
  whole category.
- Quick switching between categories from a always-reachable surface (tray
  and/or main window), not buried in a menu tree — plus the command palette
  below, which is the fast path.

### App blocking
- A category can carry a blocklist of apps that are not allowed to run
  while that category is the active/open one (soft block: a background
  watcher detects a blocked process starting and closes it, no admin
  rights required — accepted trade-off: a blocked app can flash open
  briefly before being closed).
- Blocking is scoped to categories, not global: opening "Deep Work" enforces
  its blocklist; closing it (or switching to a category without that app
  blocked) lifts the restriction.
- Blocking a running app the user is actively trying to use should give
  clear, non-native feedback (toast/notification), not a silent kill.

### Autostart & background presence
- Optional "start with Windows" setting.
- Runs from/minimizes to the system tray; closing the main window doesn't
  necessarily exit the app (the point is it should be running in the
  background for blocking/quick-switch to work).

## Non-functional / UX requirements
- **No native `alert()`/`confirm()`/`prompt()`** — every dialog, toast, and
  confirmation uses the app's own themed components.
- Loading states for anything async (launching apps, searching installed
  apps, resolving icons).
- Empty states for zero categories / zero apps in a category, with a clear
  next action.
- Inline form validation on every field (e.g. invalid path, duplicate
  category name) — errors shown next to the field, not a popup.
- Responsive layout — usable resized smaller, not just at one fixed window
  size.
- Errors (a launch failure, a missing exe, a corrupt config) surface as a
  readable, specific message — never a raw stack trace or exception dialog.
- Lightweight background footprint — this is meant to run all day.

## Future (design for, don't build yet)
- **Automation**: rule-based triggers (e.g. time-of-day, system event) that
  open a category automatically without a click.
- **To-do list integration**: pull/show tasks from an external to-do API
  inside the app (provider not yet chosen).
- Keep the data model and service interfaces open to these (e.g. an
  automation-rule shape, a pluggable to-do provider interface) without
  wiring up UI or behavior for them now.

## Chosen design
- **Layout:** Dashboard Home — a grid of category cards (name, app-count,
  avatar-stack preview of its apps, one-click Open right on the card);
  clicking a card's name drills into a detail panel listing its apps with
  per-app Open/Minimize/Close.
- **Theme:** Daylight Studio — light, low-glare surfaces for daytime desk
  use, single indigo-blue accent (no gradient/multi-tone accent).
- **Command palette:** in addition to the Dashboard Home main window, a
  Spotlight/Ctrl+K-style overlay — opened by a **global, user-configurable
  keyboard shortcut** (works from anywhere, not just while Catogarizer's
  window has focus, since the point is switching category without
  breaking out of whatever app you're in) — for searching and
  opening/closing/minimizing a category without touching the mouse.
  Default shortcut assumed as `Ctrl+Alt+Space`, changeable in Settings;
  flag if a system-wide hotkey wasn't what you meant and you want it
  in-app-window-only instead.
- Full layout/theme exploration tool: `design/concepts.html`.

## Open technical questions (to resolve during stack research, step 3)
- Exact mechanism for enumerating "installed apps" for the search picker
  (Start Menu shortcut index vs. registry uninstall keys vs. package
  manager APIs) — likely a combination.
- Exact mechanism for the soft-block watcher (poll running processes vs.
  WMI process-start events) and its resource cost running all day.
- Whether autostart is implemented via Registry Run key vs. a Startup
  folder shortcut vs. a Scheduled Task.
- Mechanism for a global keyboard shortcut (system-wide hotkey registration
  vs. a low-level keyboard hook) for the command palette, and how a
  borderless overlay window is shown/dismissed/focused on top of whatever
  app is currently active.
