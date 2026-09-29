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
