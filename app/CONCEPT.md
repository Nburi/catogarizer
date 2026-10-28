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

### Category switching & sessions (v2 — replaces the old Open/Close/Minimize model)
- The core interaction is **switching**, not launching: at any moment
  exactly one category is "active" (including an implicit **Sonstiges**
  category for whatever's open that isn't assigned anywhere — see below).
  There's no "nothing active" state.
- Switching to category B: every top-level window currently attributed to
  the active category A is hidden (not closed) and that exact window set is
  remembered as A's **session**. Category B's session is restored if it has
  one from before (windows re-shown as they were); otherwise B's saved
  **template** is opened fresh.
- Window attribution is per **window**, not per process — e.g. a browser
  open in two categories at once is two separately tracked windows, so
  switching away from one doesn't hide the other's browser window too.
- Any window the user opens ad-hoc while a category is active gets
  auto-attributed to that category's session, just by appearing while it's
  active — no manual "add to category" step. It's **session-only**: it does
  not get added to the category's permanent template, so templates don't
  accumulate one-off clutter over time.
- **Sonstiges** (Uncategorized) is a real, always-present pseudo-category
  covering whatever's open before/outside any real category — the same
  hide/restore rules apply to it. Whether a given app gets hidden when its
  category (including Sonstiges) goes inactive is configurable per app:
  most apps hide by default, but specific apps (e.g. Spotify, WhatsApp) can
  be **pinned** to stay visible across every category switch.
- Per-app manual Open/Close/Minimize still exist as an override for a
  single app, independent of the category-level switch.
- Quick switching between categories from an always-reachable surface (tray
  and/or main window), not buried in a menu tree — plus the command palette
  below, which is the primary, most-used path (see "Chosen design").

### Temporary categories ("Quick category", decided 2026-10-10)
A category for the next hour that costs no bookkeeping: it is made in a
second and removes itself, so the shelf never fills with leftovers.
- **Create**: home button **+ Quick category**, key `+` on the home and in
  the palette, a row in the palette, or the tray menu. A small dialog asks
  for an optional name; Enter confirms. An empty name becomes `Temp 1`
  (the lowest free number, so a fresh one is never `Temp 4`).
- **You land in it at once** - it is a normal category (switchable, number
  key, tile, palette row, "Move to" target) that starts empty. Windows opened
  while it is active join it like in any category.
- **The watch starts with the first window.** An empty, never-used temporary
  category stays until the user leaves or closes it; it cannot vanish before
  anything was ever in it.
- **It disappears when**
  1. its last window has closed (after a 3 s grace period, so closing one
     window and opening the next does not lose the category),
  2. the user closes it (hero button / Ctrl+W), or
  3. Catogarizer restarts (temporary categories are never written to the
     config file).
- **Where you land**: if it was the active category, back in the category
  you came from (the pill confirms it), otherwise Unsorted.
- **Closing it by hand** asks to close its windows (themed confirm, only when
  there are windows). The windows are asked to close gracefully (`WM_CLOSE`),
  never killed, so an app with unsaved work still gets to ask. The category
  then goes as soon as the last window is gone; if a window stays open, so
  does the category. This is an explicit, per-category user action with a
  confirmation, not part of a switch, so it stays within PRINCIPLES.md
  values 1 and "Never: closing windows as part of a switch".
- **Not editable**: no template, no held-back list, no colour/rename editor.
  It also does not show up in the automation pickers (a trigger cannot point
  at something that vanishes).
- Out of scope for now: promoting a temporary category to a permanent one
  ("Keep"), a CLI command, closing an inactive temporary category from the
  shelf.

### Resource saving for parked categories (decided 2026-10-19)
Hiding a window only hides it; the app behind it keeps running at full speed.
With many categories parked that is what makes a laptop struggle. Parked
categories now cost as little as they safely can. Preview of every visual
change: `design/resource-saving-preview.html`.
- **Eco (always, when parked)**: every process tree whose windows are all
  parked gets Windows' efficiency mode (EcoQoS) and a low priority. Undone on
  the way back, off the switch path (value 2: a switch stays under 300 ms).
- **Asleep (only under memory pressure)**: at 80 % RAM the working set of
  parked trees is trimmed, longest-parked category first, only after it has
  been parked 2 minutes. Calm again below 70 %.
- **Frozen (only where the user allowed it)**: suspended processes. Allowed per
  app (window context menu, like pinning) or per category (category editor:
  never / when the PC is under load / as soon as parked). "Under load" is any of:
  memory still above 85 % 30 s after trimming; CPU or GPU at 90 % or more for 30 s
  (an episode survives dips down to 80 %, resets below). The CPU/GPU case freezes
  only parked trees that use at least 5 % of the machine's CPU or of the busiest
  GPU engine themselves (decided 2026-10-23): freezing an idle app would not
  relieve anything. The enum value stays `FreezeMode.WhenTight`, so saved
  settings keep working. A frozen app stays frozen until its
  category is shown again. A tree is only frozen when every one of its windows
  is parked and allowed.
- **Sound (decided 2026-10-19)**: an app playing sound is left alone at every
  level - while its category keeps sound awake, which is the default. Windows
  does not say whether a stream is music or a game's effects, so instead of
  guessing it is a per-category choice (`Category.KeepSoundAwake`, editor:
  "Keep apps that play sound running"). Off, the category rests and freezes by
  its normal rules, and the editor warns that sound stops. An app explicitly
  allowed to freeze is no longer protected by sound (otherwise "allowed" would
  silently never happen for a game). The most careful window of a shared tree
  decides. A category awake only because of sound shows a speaker badge on its
  tile (it lights up as the tile dims, so the badge is the "light left on").
- **Never touched**: pinned apps, Catogarizer itself, system and host
  processes (ApplicationFrameHost, explorer, dwm ...), any tree that owns a
  visible window of the active category. No admin rights: what cannot be
  opened is skipped and named in the Resources card.
- **Crash safety (value 1)**: every applied state is written to a ledger first;
  exit, crash handler and the next start undo it (a frozen process must never
  outlive Catogarizer).
- **Display (value 5)**: a parked tile shows one state word and the memory of
  its whole process tree; CPU, share bar and "Free memory now" live in a
  Resources card one level deeper. Quiet (value 6): the only message is a pill
  when memory pressure was relieved ("Freed 1.4 GB").
- **Signature moment**: the tile falls asleep. On leaving a category its tile
  dims, the dot turns into a ring and, when memory was actually released, the
  number counts down; returning reverses it. Reduced motion: fade only.
- **Close a category** (hero button + Ctrl+W, tile context menu): windows are
  asked to close (`WM_CLOSE`), never the processes; confirm lists the windows;
  windows still open after ~4 s are listed in a banner, one row per program,
  with the reason it stayed and "Show" / "End...". Works for every category
  except Unsorted. Same explicit per-category user action as closing a quick
  category, so it stays within PRINCIPLES.md value 1.
- **End program** (`StubbornProgramService`): the only place a process is ever
  killed on the user's behalf, and only after a normal close was tried. Two
  clicks in the row (End..., End program) with the cost spelled out; never
  Catogarizer, the shell or system processes; failures (administrator apps) are
  named in the row. Signature moment: the row says why the program stayed -
  "probably asking you to save" puts Show first, so the expensive mistake (losing
  unsaved work) is the harder one to make.
- Out of scope for now: UWP apps (host-process windows), waking frozen chat
  apps for notifications, closing from palette/tray/CLI, thresholds as
  settings, GPU figures in the Resources card (the GPU is only read to decide
  on freezing), a "never sleep" flag per category.

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

## Chosen design (v2 UI rebuild — backend/window-management layer below is retained as-is)
- **Scope of the rebuild:** the Win32 window-management, blocking,
  autostart, search-picker, and automation layers (see `STACK.md`/
  `TODO.md`, Phases 2–12) are proven and stay. What's being redesigned from
  scratch is the UI layer and the category-action model it drives (see
  "Category switching & sessions" above) — the old
  Open/Close/Minimize-buttons-per-card interaction is gone, replaced by
  switching.
- **Primary surface: command palette.** Switching is now the main, frequent
  action (used constantly, not an occasional "open my workspace" click), so
  the palette is the everyday tool, not a secondary shortcut to the
  dashboard. Global, user-configurable keyboard shortcut (already built —
  `GlobalHotkeyService`, default `Ctrl+Alt+Space`), works from anywhere.
  Palette scope extends from "search/switch category" to also surfacing the
  live session state (what's parked where) where useful.
- **Secondary surface: a minimalist dashboard/overview window** — for
  editing templates/pinned apps/settings, and for a quick visual overview
  with click-to-switch (clicking a card = switch to that category, not a
  separate Open button). Category cards stay simple: name, avatar-stack of
  its apps, a small "N windows parked" count. Deliberately no thumbnails or
  live previews — an overview to glance at, not a console to live in.
- **Home layout (decided 2026-09-28): "Now + Shelf".** The top half is the
  present: the active category's name, time in it, its live windows, what it
  holds back, pinned apps, and a "Back to <previous>" action. The other
  categories (and Unsorted) sit on a shelf of tiles below; clicking a tile
  switches. Editing moves out of the home into a separate category editor.
- **Theme (decided 2026-09-28):** Fjord is the default; every theme from
  `design/concepts-v2.html` is selectable in Settings.
- **Signature moment (decided 2026-09-28):** double-tap the global hotkey to
  jump back to the previous category without opening the palette; a small
  pill in that category's color confirms it.
- **Naming:** the UI is English and the implicit category is shown as
  **Unsorted** ("Sonstiges" in earlier notes).
- Binding product principles: `PRINCIPLES.md`. v2 design exploration:
  `design/concepts-v2.html` (the pre-pivot `design/concepts.html` stays for
  reference).

## Open technical questions
Resolved during stack research (step 3) and the window-management prototype
(step 4) — see `STACK.md` for the specifics: installed-app enumeration,
soft-block watcher mechanism, autostart mechanism, and window
open/position/minimize/close (plain Win32 P/Invoke, with restore-then-place
and settle-and-retry for maximized/async-repositioning apps, and
localization-aware/host-process-aware window finding).

Global hotkey mechanism and borderless overlay behavior: resolved and built
(`GlobalHotkeyService`, Phase 6 in `TODO.md`).

Still open, to resolve for the v2 session-switching pivot — planned as a
small prototype/spike before real implementation, same approach as the
original Phase 2 Win32 spike:
- **Hide/show reliability**: does `SW_HIDE`/`SW_SHOW` behave predictably
  across the same tricky app categories already identified for
  minimize/restore (fullscreen-exclusive apps, Chromium's async
  re-apply-on-launch behavior, UWP/`ApplicationFrameHost` host-process
  windows) — or does hiding introduce new failure modes minimizing didn't
  have?
- **New-window detection**: how to reliably notice a new top-level window
  appearing so it can be auto-attributed to the active category's session —
  polling (same pattern as the existing blocking watcher) vs.
  `SetWinEventHook` (`EVENT_OBJECT_CREATE`/`EVENT_SYSTEM_FOREGROUND`).
