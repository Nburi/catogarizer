# Changelog

Notable changes per version. Dates are when the work was committed.

## 2.0.0 — unreleased (branch `feature/v2-switching-ui`)

Catogarizer moves from "open/close a group of apps" to **switching between
kinds of work**. Exactly one category is active; switching parks the old
category's windows and brings back the new one's.

### Added
- **Category switching.** Switching hides (never closes) the windows of the
  category you leave and restores the parked windows of the one you enter,
  or launches its apps if nothing is parked there yet. New windows join the
  active category on their own. The implicit **Unsorted** category holds
  everything not filed anywhere.
- **Double-tap the hotkey** to go back to the previous category, confirmed by
  a small "← ● Category" pill. Also on the home (**Back to …**) and in the
  tray menu.
- **Crash safety.** Every hidden window is recorded in `hidden.json` before
  it's hidden; after a crash or power loss the next start brings them all
  back. **Show all hidden windows** in Settings, the tray and the CLI does
  the same on demand. Exiting always restores everything.
- **Pinned apps** ("Always visible") that no switch ever hides.
- **"Now + Shelf" home**: the active category with its windows (click a row
  to bring the window to front), what it holds back, and a shelf of the
  other categories with what's parked or what they'll open.
- **Category editor** in its own window, with **From open windows** to add
  apps that are running right now.
- **Command palette v2**: number keys (1–9, 0 = Unsorted), search that
  ignores case and accents and matches word initials, last category
  preselected.
- **Seven themes** (Fjord by default, plus Daylight Studio, Paper & Ink,
  High-Contrast Mono, Graphite Mica, Night Shift, Slate Dusk), switchable
  live, with matching title bars.
- **Category colors** and real app icons everywhere.
- **CLI**: `switch "<category>"`, `back`, `show-all` next to `run "<trigger>"`.

### Changed
- Blocking follows the active category only. The outgoing category's
  blocklist is lifted before the incoming category launches its apps.
- A category without parked windows takes over matching windows from
  Unsorted instead of launching a second copy.
- Clicking a category is a switch; the per-category Open / Minimize all /
  Close all buttons are gone. Per-app open/minimize/close stay in the
  editor.
- The trigger action "Open category" is now **Switch to category**.

### Fixed
- Tray notifications (e.g. "app held back") could crash the app.
- CLI commands sent while a switch was running could get lost.
- Many review findings: honest empty/loading/error states, a missing app's
  row says "Not found at <path>" and only offers Edit/Remove, labels for
  screen readers, placement dialog Save only after a change, "Run now" on an
  empty trigger explains itself.

## 1.x — 2026-07-29 to 2026-08-13

First version: categories of apps opened, minimized or closed together,
captured window placement, command palette on a global hotkey, tray icon
and autostart, soft app blocking, triggers (Windows start, time, manual)
with a `run` CLI, installed-app and PWA search.
