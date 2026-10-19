# Catogarizer — User Guide

Catogarizer swaps your whole working context with one keystroke. You group
the apps you use together into **categories**, like "Deep Work" or "Comms".
**Switching** to a category hides the windows of the one you leave and brings
back the windows of the one you enter, exactly as you left them. Nothing is
closed, only parked out of sight.

Exactly one category is always active. If you haven't switched anywhere yet,
that's **Unsorted**, where every window starts out.

---

## Getting started

1. Open Catogarizer. The home shows **You're in: Unsorted** with the windows
   that are open right now.
2. Click **+ New category**, give it a name and a color.
3. In the category editor, under **Opens with**, add the apps this kind of
   work needs:
   - **+ Add app**: search your installed apps (the same list as the Start
     menu). If an app isn't listed, switch to manual entry and browse to its
     `.exe`.
   - **From open windows**: pick from the apps that are open right now.
4. Click **Done**. The category now sits on the shelf under **Switch to**.

An app can be in several categories at once.

## Switching

Click a category tile on the home, or press **Ctrl+Alt+Space** from anywhere
to open the command palette:

- Type a few letters. Search ignores case and accents and matches the first
  letters of words ("dw" finds "Deep Work").
- Or press a number: **1–9** are your categories in shelf order, **0** is
  Unsorted.
- **↑/↓** and **Enter** work too. **Esc** or clicking outside closes it.
- The palette opens with your last category preselected.

What happens on a switch:

- The windows of the category you leave are hidden, never closed.
- If the new category has parked windows, they come back where they were.
- If it has none yet, its **Opens with** apps are launched and moved to the
  position you set for them. If one of those apps is already open in
  Unsorted, Catogarizer takes that window over instead of starting a second
  copy.
- Anything you open while a category is active joins that category. You
  never have to file windows by hand.

### Back to where you were

**Press the hotkey twice quickly** to jump straight back to the previous
category. A small pill ("← ● Comms") confirms where you landed. Press it
twice again to go back and forth between two kinds of work.

With the mouse: the **Back to …** card on the home, or **← Back to …** in the
tray menu.

## The home

- **You're in** shows the active category and its windows. Click a window row
  to bring that window to the front.
- **Held back while you're here** lists apps the active category blocks.
- **Resources** shows what every category costs right now (see "Saving
  resources").
- **Always visible** lists pinned apps (see below).
- **Switch to** is the shelf with every other category, showing what's parked
  there or what it will open. "1 app not found" means an app's `.exe` has
  moved; open the category's editor (✎) to fix it.

## Editing a category

Click ✎ on a tile to open the editor. There you can rename it, pick a color,
and manage two lists.

**Opens with.** Each app has these buttons:

- ▶ open now, — minimize, ⏹ close this app
- ⛶ **Set where it opens**: click **Open app**, drag and resize the real
  window where you want it, then **Capture position**. No typing
  coordinates. **Clear** makes the app open at its own default position
  again.
- ✎ change the name, path or arguments
- **Remove** takes it out of this category (the app itself isn't touched)

If an app's `.exe` can't be found, the row says **Not found at …** and only
✎ and **Remove** stay active.

**Held back while active.** Apps listed here are closed as soon as they start
while this category is active, and you get a notification. You can hold back
an app that isn't installed by typing its process name. This is a soft
block: it checks about three times a second and doesn't need administrator
rights, so a held-back app can flash open for a moment. When you switch away,
the category's list stops applying.

**Delete category** removes it. Windows parked in it come back to Unsorted.

## Always visible (pinned apps)

Apps you pin under **Always visible** on the home, like a music player or a
chat assistant, stay on screen in every category and are never hidden by a
switch.

## Saving resources

Hiding a window doesn't stop its app, so many parked categories can slow a
laptop down. Catogarizer puts parked categories to rest, and wakes them the
moment you switch back:

- **Eco** (always, when parked): the apps run in Windows' efficiency mode with
  a low priority. You won't notice, and nothing is closed or lost.
- **Asleep** (only when memory is tight, above 80 %): the memory of parked apps
  is paged out, the longest-parked category first, and only after it has been
  parked for two minutes. The first thing you see when you come back is a
  moment of paging in.
- **Frozen** (only if you allowed it): the apps are paused completely. Chat
  apps miss messages, downloads and syncs wait, calls drop - until you switch
  back. Allow it for one app (right-click a window on the home, **Allow freezing
  while parked**) or for a whole category (in its editor: **While parked** -
  never, when memory is tight, or as soon as it is parked). Apps that are
  playing sound are never frozen, or touched at all.

Each parked tile shows what its category holds in memory, and a leaf (Eco), moon
(Asleep) or snowflake (Frozen). Falling asleep is visible: the tile dims and its
dot turns into a ring. The **Resources** card on the home lists every category
with its state, memory and CPU. **Free memory now** pages out everything parked
that may be touched, whatever the load. When memory got tight and Catogarizer
freed some, a small pill says so once ("Freed 1.4 GB").

Never touched: pinned apps, Catogarizer itself, Windows' own processes, anything
with a window in the category you're in (a browser with windows in two
categories is left alone entirely), and apps that run as administrator
(Catogarizer never asks for admin rights - the card says how many it skipped).
Turn the whole thing off in **Settings**.

## Closing a category

**Close** on the home (or **Ctrl+W**), or right-click a tile on the shelf and
choose **Close windows...**. The confirmation lists the windows. They are asked to
close the way a click on X does, so an app with unsaved work asks you first. Only
the windows close, never the programs: a chat app may stay in the tray.

If some windows are still open after a few seconds, a note says "3 of 5 windows
closed. 2 are still open" and **Show them** takes you there. Closing the category
you're in takes you back to the one you came from (or Unsorted). The category itself
stays, with its template. Unsorted can't be closed as a whole.

## Quick categories

For work you only need for a while, make a quick category instead of a real
one: **+ Quick category** on the home, the **+** key (on the home or in the
palette), or **New quick category...** in the tray menu.

- A small dialog asks for a name. It's optional: press **Enter** with the
  field empty and it's called **Temp 1** (or the next free number).
- You land in it at once, with nothing open. Windows you open join it like in
  any category, and it shows a **TEMP** tag.
- It removes itself when its last window has been closed for a few seconds
  (closing one window and opening the next keeps it), when you close it, or
  when Catogarizer restarts. It's never saved. A quick category you haven't
  opened a window in yet stays until you leave or close it.
- When it goes while you're in it, you land back in the category you came
  from, with the usual pill to say so.
- **Close** on the home (or **Ctrl+W**) closes it (see "Closing a category").
  Apps with unsaved work ask you first, and the category stays until they're
  closed.
- It has no editor and holds nothing back. To keep what you built, make a
  normal category and use **From open windows** in its editor.

## Lost a window?

Nothing Catogarizer hides is lost:

- **Settings → Show all hidden windows**, or **Show all hidden windows** in
  the tray menu, brings back every parked window in every category and
  switches to Unsorted.
- When Catogarizer exits, all hidden windows come back.
- If Catogarizer crashes or the PC loses power while windows are hidden, they
  are brought back the next time it starts.

## The tray

Catogarizer lives in the system tray. Closing the main window only hides it,
so the hotkey and blocking keep working. Left-click the tray icon to show the
home. Right-click for:

- **← Back to …** and your categories, to switch without the palette
- **New quick category...**
- **Show all hidden windows**
- **Show Catogarizer**
- **Start with Windows**
- **Exit**, the only thing that really quits the app

## Settings

Click ⚙ on the home:

- **Theme**: seven themes, from light (Fjord, Daylight Studio, Paper & Ink,
  High-Contrast Mono) to dark (Graphite Mica, Night Shift, Slate Dusk).
  Changes show right away; **Cancel** takes you back to the one you had.
- **Start with Windows** and **Start minimized to tray**.
- **Save resources while categories are parked** (on by default) and the list of
  apps you allowed to freeze (see "Saving resources").
- **Command palette shortcut**: click the box, press the combination you
  want, and **Save**. It applies immediately.
- **Show all hidden windows** (see "Lost a window?").
- **Automation…** opens the trigger list.

## Automation

A **trigger** runs a list of actions, top to bottom, either:

- **Windows start**: when Catogarizer starts with Windows (turn on **Start
  with Windows**; nothing else to set up),
- **Time**: once a day at a set time, optionally only on some weekdays, or
- **Manual**: only when you run it.

Actions are **Switch to category**, **Open app** (at its set position, if it
has one) and **Close apps**. Every trigger has **Run now** for testing, and
the **On** checkbox turns it on or off without deleting it.

## Command line

For scripts, Stream Deck or AutoHotkey. If Catogarizer is already running,
the command is handed to it; otherwise it starts in the tray first. The main
window never pops up.

```
catogarizer.exe switch "Deep Work"   switch to a category by name
catogarizer.exe back                 back to the previous category
catogarizer.exe show-all             bring back every hidden window
catogarizer.exe run "Morning"        run a trigger by name
```

## Where your data lives

Everything is stored locally in `%LocalAppData%\Catogarizer\`. There's no
account and no cloud.

- `config.json`: categories, apps, triggers and settings, as readable JSON.
  To start fresh, **Exit** from the tray menu and delete this file.
- `hidden.json`: the list of windows that are hidden right now, used to bring
  them back after a crash. Don't delete it while windows are hidden.
- `throttled.json`: the apps that are slowed down or frozen right now, so the
  next start can wake them after a crash. Don't delete it while apps are frozen.
