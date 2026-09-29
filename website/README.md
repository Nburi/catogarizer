# Catogarizer website

A one-page static site: plain HTML and CSS, no framework, no build step.

    website/
      public/    everything that gets served (index.html, style.css, favicon.ico, img/)
      deploy/    server side: nginx config + deploy.ps1 (never served)

Preview locally from the repo root:

    python -m http.server 5173 --directory website/public

Theme values (colors, radii) mirror the app's default "Fjord" theme in
`app/src/Catogarizer.Core/Theming/ThemeCatalog.cs`. Keep them in sync if the default changes.

## Deploy

1. Build the installer: `app/installer/build-installer.ps1 -Version 1.0.0` (needs Inno Setup 6, `winget install JRSoftware.InnoSetup`).
2. Upload: `website/deploy/deploy.ps1 -Server user@host -WithInstaller`.
3. Once on the server: copy `deploy/catogarizer.nginx.conf` to `/etc/nginx/sites-available/`, set `server_name`, enable it, reload nginx.

The download button links to `downloads/Catogarizer-Setup.exe`. The exe is not in git; without step 1+2 the link is a 404.

## Open items
- The hero screenshot is an HTML/CSS mock. Replace it with a real screenshot or GIF of a switch.
- `deploy.ps1` and the nginx config are untested against the real server.
