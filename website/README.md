# Catogarizer website

A one-page static site: plain HTML and CSS, no framework, no build step.

    website/
      public/    everything that gets served (index.html, style.css, favicon.ico, img/)

The server pulls this repo with `git pull`; nginx serves `website/public/` as its root.
The nginx config is not part of the repo.

Preview locally from the repo root:

    python -m http.server 5173 --directory website/public

Theme values (colors, radii) mirror the app's default "Fjord" theme in
`app/src/Catogarizer.Core/Theming/ThemeCatalog.cs`. Keep them in sync if the default changes.

## Releasing a new installer

1. Build it: `app/installer/build-installer.ps1 -Version x.y.z` (needs Inno Setup 6, `winget install JRSoftware.InnoSetup`). Output: `app/installer/Output/Catogarizer-Setup.exe`.
2. Copy the exe by hand to the server's downloads folder, which nginx serves at `/downloads/`. It is not in git, so `git pull` does not bring it.
3. **Every release:** set `"version"` in `website/public/version.json` to the same `x.y.z` you passed to the build script, commit, and `git pull` on the server. The app checks this file (`UpdateNotifier.VersionUrl`) and tells users a newer version exists; if you forget, nobody is told. The build script stamps `-Version` into the exe as well, so the exe and `version.json` agree.

The download button links to `downloads/Catogarizer-Setup.exe`; without step 2 the link is a 404.

## Open items
- The hero screenshot is an HTML/CSS mock. Replace it with a real screenshot or GIF of a switch.
