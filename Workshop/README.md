# Workshop presentation material

Everything needed to fill in the Steam Workshop page for **RimCore Data Centers**. Nothing here is shipped
inside the mod package.

| File | Use |
| --- | --- |
| `title.txt` | Item title |
| `short-description.txt` | One-paragraph summary (for announcements, the item's summary line or a store blurb) |
| `description.bbcode` | Full description in Steam BBCode: paste into the item's *Description* box |
| `tags.txt` | Tags the game sets automatically and the ones to add by hand |
| `changelog.txt` | Change note for the version being uploaded (BBCode) |
| `preview.png` | The preview image (also shipped as `About/Preview.png`, which is what the game uploads) |
| `Screenshots/` | In-game screenshots of the packaged mod (Core + this mod only) to add to the item page |

The in-game uploader sets the title, the description **on creation only**, the preview image, the tags
`Mod` and `1.5`, and the content. Everything else (this BBCode description, screenshots, extra tags,
visibility) is set on the Workshop page. See [../docs/PUBLISHING.md](../docs/PUBLISHING.md).

Screenshots show RimWorld's own artwork (terrain, colonists, UI) around the mod's original buildings; they
were taken from a Core-only test profile, so no other mods' content appears in them. The preview image
contains no RimWorld artwork.
