# Catogarizer — User Guide

Catogarizer groups the apps you use together into **categories** (e.g. "Deep
Work", "Design", "Comms"), so switching context is one click instead of
manually opening, arranging, and closing a dozen windows.

## Getting started

Run `Catogarizer.exe`. The first time, you'll see an empty state — click the
**+** next to WORKSPACES in the sidebar to create your first category.

## Categories

- **Create**: click **+** next to WORKSPACES, name it, save.
- **Rename**: click the pencil icon on a category in the sidebar.
- **Delete**: click the **✕** on a category (asks for confirmation — this
  also removes its apps).
- **Switch**: click a category in the sidebar to view its apps.

## Adding an app to a category

Select the category, click **+ Add app**, then fill in:

- **Name** — whatever you want to call it.
- **Executable** — browse to the `.exe`.
- **Arguments** / **Working directory** — optional.
- **Window position & size** — where it should land when opened.
- **Launch delay** — a pause (ms) before the *next* app in the category
  starts, useful if you don't want everything launching at the exact same
  instant.

**Fastest way to set the window position**: open the app manually, drag/size
its window where you want it, then in the *Capture from a window that's
already open* section pick it from the dropdown and click **Capture** —
it fills in X/Y/Width/Height for you.

Use the pencil/✕ icons on a tile to edit or remove an app. **Right-click a
tile** for Open / Minimize / Close on just that one app, without touching
the rest of the category.

## Using a category

With a category selected:

- **Open** — launches every app in it (or brings an already-running one to
  the position you set) in order, respecting each app's launch delay.
- **Minimize** — minimizes every running app in the category.
- **Close** — sends a normal close request to every running app in the
  category (so it'll still prompt you to save if it has unsaved work).

A status banner reports what happened, including which apps (if any)
failed and why.

## System tray

Catogarizer lives in the system tray:

- Minimizing or clicking the window's **✕** hides it to the tray — the app
  keeps running in the background.
- Click the tray icon (or **Show Catogarizer** in its right-click menu) to
  bring the window back.
- Right-click the tray icon to **open a category directly** without opening
  the main window at all.
- **Exit** in that same menu is the only way to fully quit.

## Settings

Click **⚙ Settings** in the sidebar footer:

- **Start with Windows** — launches Catogarizer automatically at sign-in.
- **Start minimized to tray** — when starting via Windows, keep the window
  hidden until you open it yourself.
- **Blocked apps** — type a process name (e.g. `steam.exe`) and click
  **Block**; that process gets closed automatically within a couple of
  seconds of launching, with a tray notification. Click the **✕** next to
  an entry to unblock it.

## A couple of things to know

- Some apps (e.g. modern Windows Notepad) restore their own last window
  size/position right after launch, which can override the position you
  set — this is the app doing its own thing, not something Catogarizer can
  fully prevent.
- A handful of programs (e.g. `control.exe`) are just launcher stubs that
  hand off to another process and exit immediately — Catogarizer can't
  track or reposition those specifically, so pick the real application
  executable where possible.
- There's currently no drag-and-drop reordering — new categories/apps are
  added at the end of the list.
