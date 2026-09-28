# Principles

> **Status: DRAFT, waiting for approval.** Once approved, this file is binding
> and does not change. Every feature plan is checked against it; if a feature
> breaks a principle, the feature changes, not the principle.
> Interactive version with design previews: `design/concepts-v2.html`.

## What makes Catogarizer unique

One keystroke swaps your whole context on Windows. The windows, layout and
distractions of one task step aside, the next task's come back exactly as you
left them, and you never had to file anything.

| Close relative | What it misses |
|---|---|
| Windows virtual desktops | Switch in order, not by name. Never launch a fresh workspace. Can't block anything. |
| PowerToys Workspaces | Launches app layouts, but forgets your session the moment you leave it. |
| Cold Turkey / Freedom | Only block. |
| FancyZones | Only arranges windows. |

Catogarizer is the one tool where switching, restoring, launching and blocking
are the same action.

## Core values

1. **Nothing is ever lost.** Catogarizer hides windows, it never closes them.
   Every hidden window can be found in one step, and all of them come back when
   Catogarizer exits, crashes or updates.
   *Rule: a feature that can leave a window unreachable is rejected. Closing
   always needs an explicit per-app action.*

2. **Switching is the product.** A switch feels instant. Restoring a session
   shows no spinner and no overlay. Editing, settings and automation never slow
   down or clutter the switch path.
   *Rule: measure it. Hotkey to restored windows under 300 ms.*

3. **Keyboard first, mouse welcome.** Any switch takes the hotkey plus at most
   two keys. Every action done more than once a day has a key, and every key
   action also has a visible mouse path.
   *Rule: no feature ships with only one of the two paths.*

4. **Zero bookkeeping.** The user never maintains sessions. A window joins the
   active category by appearing. Templates change only on explicit intent.
   *Rule: if a feature needs the user to tidy up regularly, redesign it.*

5. **Glance, don't study.** The home answers "where am I, what's parked where,
   what's held back" in two seconds. No thumbnails, no live previews, no charts
   for the sake of charts.
   *Rule: information that doesn't help decide where to go next moves one level
   deeper.*

6. **Quiet by default.** Runs all day at close to zero idle CPU. Speaks only
   when it acted for the user (an app was held back, a trigger switched). No
   streaks, no guilt, no nagging.
   *Rule: every notification names an action Catogarizer already took.*

7. **Yours, local, no admin.** No account, no cloud, no telemetry, no
   elevation. The config is a readable JSON file on disk.
   *Rule: a feature that needs admin rights or a server is out.*

## Conventions (derived from the existing codebase, binding)

- Every dialog, toast and confirmation is a themed app component. Native
  `MessageBox` only where the app's own UI may be what's broken (crash handler,
  single-instance message) and the OS toast for blocked apps.
- "Grab, don't type": capture from reality (a real window, a real open app)
  instead of asking for typed values.
- Problems surface as a readable, dismissible inline banner that says what to
  do, never a raw exception.
- Calm visuals: one accent, soft surfaces, muted secondary text, short
  (~150 ms) ease-out motion on hover and press.
- Risky logic lives in `Catogarizer.Core` as pure, unit-tested code; the Win32
  layer stays a thin P/Invoke wrapper with no WPF dependency.
- Polling over OS eventing where eventing would need admin rights or new
  interop surface.

## Never

- Window thumbnails or live previews (value 5)
- Closing windows as part of a switch (value 1)
- Hard blocking that needs admin rights (value 7)
- Cloud sync, accounts, telemetry (value 7)
- Productivity scores or streaks (value 6)
