# Catogarizer — User Guide

Catogarizer groups the apps you use together into **categories** — like "Deep
Work" or "Comms" — so you can open, arrange, minimize, or close an entire
group of windows with one click instead of doing it app by app.

---

## Getting started

On first launch you'll see an empty dashboard with a **+ New category**
button. Click it, give the category a name (e.g. "Deep Work"), and it
appears as a card on the dashboard.

Click a category's name to open its detail panel below the card grid. From
there, **+ Add app** lets you add apps to it two ways:

- **Search** — type a few letters and pick from your installed apps (the
  same list you'd see pressing the Windows key).
- **Can't find it?** — switch to manual entry and browse to the `.exe`
  directly. Useful for portable apps or anything not in the Start Menu.

An app can belong to more than one category — e.g. Slack in both "Deep Work"
and "Comms" — adding it to a second category doesn't remove it from the
first.

## Setting a window position

Click the ⛶ icon next to an app to open **Set window position**. This is
"grab, don't type": click **Open app** to launch it, then drag/resize the
real window wherever you want it, and click **Capture position** — Catogarizer
reads back the window's actual position, size, and monitor. There's no
coordinate typing involved. **Clear captured placement** removes a saved
position if you want the app to just open at its own default spot instead.

If you don't set a position, opening the app just launches or restores it
without moving or resizing it.

## Opening, minimizing, and closing

Each category card has an **Open** button that launches (or restores, if
already running) every app in it, positioning each one if you've captured a
placement for it. In the detail panel you also get **Minimize all** and
**Close all** for the whole category, plus per-app ▶ Open / — Minimize / ⏹
Close buttons if you only want to act on one app.

Closing tries a graceful close first and only force-closes an app if it
doesn't respond within about 2 seconds.

## Quickly switching categories

Press **Ctrl+Alt+Space** (or whatever you've set in Settings) from anywhere
— even while a completely different app is focused — to bring up the command
palette. Type to filter categories, press **Enter** to open the top match, or
use the — / ⏹ buttons next to a result to minimize/close it instead. **Esc**
or clicking outside the palette dismisses it.

## The system tray

Catogarizer lives in the system tray. Closing or minimizing the main window
doesn't quit the app — it just hides it, so the command palette hotkey and
app blocking (below) keep working in the background. Left-click the tray
icon to bring the main window back. Right-click it for a menu with:

- Your categories, for a one-click open without the command palette
- **Show Catogarizer**
- **Start with Windows** (toggle)
- **Exit** — this is the only thing that actually quits the app

## Blocking distracting apps

A category can carry a list of apps that aren't allowed to run while it's
open. In the detail panel, **+ Block app** adds one (same search-or-manual
picker as adding a regular app, except here you can block something not
currently installed by typing its process name).

While the category is active, Catogarizer watches for any blocked app
starting and closes it automatically, along with a notification telling you
what happened. This is a **soft block**, checked every ~350ms — a blocked app
can flash open briefly before it's closed, and this doesn't need
administrator rights. If you have several categories open at once, their
blocklists all apply together.

## Automation

Settings → **Automation...** opens the Triggers window, where you can set up
apps to open or close on their own — no click needed.

A **trigger** has a type that decides when it fires automatically:

- **Windows start** — fires when Catogarizer is launched via
  `catogarizer.exe start`. Turning on **Start with Windows** in Settings
  already wires this up for you (it writes the Registry Run key to launch
  with that argument) — you don't need to touch Task Scheduler or the
  Startup folder yourself.
- **Time of day** — fires once a day at a time you set (e.g. `08:00`),
  optionally limited to specific days of the week. Leave every day
  unchecked to run daily.
- **Manual only** — never fires on its own; only runs when you trigger it
  yourself (see below).

Every trigger, regardless of type, also has a **Run Now** button in the
Triggers list — handy for testing a Startup or Time trigger without waiting
for it to fire for real.

Each trigger runs an ordered list of **actions**, top to bottom:

- **Open category** — opens every app in a chosen category, same as
  clicking that category's Open button.
- **Open app** — opens one specific app. If that app has a captured window
  placement (see "Setting a window position" above), it opens right there —
  this is how you "open an app at a specific location."
- **Close apps** — closes one or more specific apps.

### Running a trigger from the command line

`catogarizer.exe run "<trigger name>"` fires a trigger by name from a
terminal, script, or hotkey tool — useful for triggers you want to run on
demand from outside the app. It works whether or not Catogarizer is already
running: if it's not, this launches it (hidden, staying resident in the
tray afterward, same as any other launch); if it already is, the request is
handed off to the running instance and nothing new opens. Either way, this
never pops the main window — it's meant to run quietly in the background.

## Settings

The ⚙ icon in the top corner opens Settings:

- **Start with Windows** — reflects the actual Windows registry state, so it
  always matches what you'd see in Windows' own Startup Apps settings.
- **Start minimized to tray** — skip showing the main window on launch.
- **Command palette shortcut** — click the box, press the key combination you
  want, and Save. It takes effect immediately, no restart needed.

## Where your data lives

Categories, apps, and settings are stored in a plain JSON file at
`%LocalAppData%\Catogarizer\config.json`. If you ever want to start fresh,
close Catogarizer completely (Exit from the tray menu) and delete that file.
