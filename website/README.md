# Catogarizer website

A one-page static site: plain HTML and CSS, no framework, no build step.

Preview locally from the repo root:

    python -m http.server 5173 --directory website

Theme values (colors, radii) mirror the app's default "Fjord" theme in
`app/src/Catogarizer.Core/Theming/ThemeCatalog.cs`. Keep them in sync if the default changes.

Open items:
- The download button is disabled until the first public build exists (see the TODO comment in `index.html`).
- The screenshot in the hero is an HTML/CSS mock. Replace it with a real screenshot or GIF of a switch when available.
- Hosting is not decided yet (any static host works, e.g. GitHub Pages or Cloudflare Pages).
