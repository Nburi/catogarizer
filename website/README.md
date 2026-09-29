# Catogarizer website

A one-page static site: plain HTML and CSS, no framework, no build step.

Preview locally from the repo root:

    python -m http.server 5173 --directory website

Theme values (colors, radii) mirror the app's default "Fjord" theme in
`app/src/Catogarizer.Core/Theming/ThemeCatalog.cs`. Keep them in sync if the default changes.

Open items:
- The download button links to `downloads/Catogarizer-Setup.exe`. Build it with `app/installer/build-installer.ps1` and upload it to the server's downloads folder (it is not in git).
- The screenshot in the hero is an HTML/CSS mock. Replace it with a real screenshot or GIF of a switch when available.
- Hosting is not decided yet (any static host works, e.g. GitHub Pages or Cloudflare Pages).
- `deploy/catogarizer.nginx.conf` is the nginx site config for the prod server (HTTP only, TLS via Cloudflare tunnel).
