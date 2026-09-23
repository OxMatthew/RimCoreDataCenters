# Publishing to the Steam Workshop

RimWorld 1.5 has a built-in uploader (Steamworks `SteamUGC`), so no extra tools are needed. This is what
the installed game does, read from its code, and what to expect. **Uploading is a manual step that uses your
Steam login and Steam's Workshop legal agreement; none of these scripts publish anything.**

## First upload

1. Run `Tools\Release.ps1` and, if it reports a clean scan, `Tools\Install-ToGame.ps1 -Source
   .\Release\RimCoreDataCenters` so the game's `Mods\RimCoreDataCenters` folder is the release package.
2. Start RimWorld through Steam (the uploader needs the Steam client running and logged in; the main menu
   shows "Logged into Steam as ..." in its top-left corner when it is).
3. Turn on **Development mode** (the checkbox near the bottom of the Options dialog). **The upload entry is hidden unless
   developer mode is on**: in the game's code it requires `Prefs.DevMode`, an initialised Steam connection,
   and a mod that lives in the local `Mods` folder.
4. Main menu > **Mods**. Select **RimCore Data Centers** (a local mod has a folder icon). Click
   **Advanced...** and choose **Upload to Steam Workshop** (it reads **Update on Steam Workshop** once the mod
   has a `PublishedFileId.txt`).
5. A confirmation dialog appears with a **"Tag as translation"** checkbox. Leave it unchecked. After you confirm
   there is a second yes/no confirmation that you are the author or have the rights (it has a short delay).

What the uploader does with the package (from `Verse.Steam.Workshop`):

| Steam field | Source |
| --- | --- |
| Title | `<name>` in `About.xml` ("RimCore Data Centers") |
| Description | `<description>` in `About.xml`, **set on creation only** (later updates do not overwrite it) |
| Preview image | `About/Preview.png` |
| Tags | `Mod` plus the supported game version(s), for example `1.5` |
| Content | the whole mod folder |
| Visibility | **not set by the game**. Steam normally creates a new Workshop item as Private until you change it; confirm on the item page before assuming |
| Change note | "[Auto-generated text]: Initial upload." / "Update on <date>." |

6. The game opens the item's Workshop page. Steam will ask you to accept the **Steam Workshop Legal
   Agreement** for a new item. Then, on that page, do the parts the game does not do:
   * paste the Steam-formatted description from `Workshop/description.bbcode` (Edit description),
   * add the screenshots in `Workshop/Screenshots`,
   * set the tags you want that the page offers (see `Workshop/tags.txt`),
   * choose **Visibility** (recommended: leave Private, or Friends-only, for a final check, then Public).
7. The game writes `Mods\RimCoreDataCenters\About\PublishedFileId.txt`. **Keep that file** (see below).

## Updating the same Workshop item later

* Keep `<packageId>RimCore.DataCenters</packageId>` unchanged forever. Changing it breaks load orders and
  saves for every subscriber.
* Keep `About/PublishedFileId.txt` in the mod folder you upload from. It is the link between the folder and
  the Workshop item: with it the button reads **Update on Steam Workshop** and the same item is updated; without
  it, a *new* item would be created. Do not commit it to git; back it up outside the release folder.
  (`Tools\Release.ps1` refuses to package it and `Tools\Install-ToGame.ps1` mirrors the folder, so re-copy the
  file after installing a new package.)
* Bump `<modVersion>`, add a `CHANGELOG.md` entry, run the checklist in [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md),
  install the new release package into the game's Mods folder (restore `PublishedFileId.txt`), then use
  **Update on Steam Workshop**.
* Description, screenshots and tags edited on the Workshop page are kept; the game only re-sends the title,
  preview image, tags and content.

## After publishing

Subscribe from a clean Steam profile (or a second machine), start the game with only Core and this mod, and
run the checklist's new-game and save/load pass against the downloaded copy.
