# Notices and attributions

**RimCore Data Centers** is an original mod. This file lists everything that is redistributed in
the Workshop package and the license that applies to it.

## What is redistributed

| Item | Origin | License |
| --- | --- | --- |
| `1.5/Assemblies/RimCoreDataCenters.dll` | Compiled from the source in this repository | MIT (see `LICENSE`) |
| `Defs/`, `Patches/`, `Languages/`, `About/About.xml` | Original work | MIT |
| `Textures/RCDC/**`, `About/Preview.png` | Original placeholder art, drawn procedurally by `Tools/AssetGen` | MIT (free to reuse and modify) |
| `Sounds/RCDC/*.wav` | Original placeholder audio, synthesised by `Tools/AssetGen` (sine and noise partials) | MIT |

No artwork, sound, text or code from RimWorld, from other mods, or from any third-party library is
included in the package.

## What is referenced but NOT redistributed

* **RimWorld** (Ludeon Studios). The mod is compiled against the game's own assemblies, which are not
  copied into this repository or the package. The XML refers to vanilla definitions by name
  (for example `Steel`, `ComponentIndustrial`, `Building_Cooler`, `Damage/Corner`, the vanilla trader
  kinds). RimWorld is a trademark of Ludeon Studios. This mod is unofficial and not affiliated with
  or endorsed by Ludeon Studios.
* **Vanilla behaviour extended or reused:** the security doors subclass the game's `Building_Door`
  (overriding only its public virtual `PawnCanOpen`) and inherit the vanilla `DoorBase` definition; the
  research uplink and the certified price are `StatPart`s attached to the vanilla `ResearchSpeed` and
  `MarketValue` stats by XML patch. No Harmony and no code patching are involved.
* **Vanilla behaviour reused through XML:** `Building_Cooler` and `CompProperties_TempControl` for the
  precision cooling unit, `CompProperties_Battery` for the UPS, and the game's power, temperature,
  trading and work systems.

## Build and tooling dependencies (not shipped)

* Microsoft .NET SDK and the `Microsoft.NETFramework.ReferenceAssemblies.net472` package (MIT), used to
  compile the assembly.
* `System.Drawing.Common` (MIT), used only by `Tools/AssetGen` to draw the placeholder textures.

No third-party libraries are bundled in the mod package. No Harmony.
