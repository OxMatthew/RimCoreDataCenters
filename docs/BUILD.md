# Building from source

## What you need

* **RimWorld 1.5 or 1.6** installed (any store). The build compiles against the managed assemblies from
  the installed version and does not copy or redistribute them. Use `-RwVersion 1.6` / `/p:RwVersion=1.6`
  when building against RimWorld 1.6; otherwise the project defaults to 1.5.
* The **.NET SDK** (6.0 or newer; 8.0 was used). RimWorld 1.5 runs on Unity's Mono with the .NET Framework
  4.7.2 profile, so the project targets `net472`; the SDK supplies the compiler and the official
  `Microsoft.NETFramework.ReferenceAssemblies.net472` NuGet package supplies the reference assemblies.
* PowerShell 5.1 or newer (Windows).

## Point the build at RimWorld

The default is the standard Steam location
(`C:\Program Files (x86)\Steam\steamapps\common\RimWorld`). If yours differs, set an environment variable
once, or pass the path on each call:

```powershell
$env:RIMWORLD_DIR = "D:\Games\RimWorld"
.\Tools\Build.ps1 -RimWorldDir "D:\Games\RimWorld"
```

## Everyday commands

```powershell
.\Tools\Build.ps1              # compile 1.5 -> Mod\1.5\Assemblies\RimCoreDataCenters.dll
.\Tools\Build.ps1 -RwVersion 1.6 # compile 1.6 -> Mod\1.6\Assemblies\RimCoreDataCenters.dll
.\Tools\Validate-Xml.ps1       # XML well-formedness, About.xml, def names, keyed strings, textures, sounds
.\Tools\Generate-Assets.ps1    # regenerate textures + sounds (add -Preview for the Workshop preview image)
.\Tools\Install-ToGame.ps1     # copy Mod\ into the game's Mods folder for playing/testing
.\Tools\Release.ps1            # build + validate + clean package in Release\RimCoreDataCenters (+ zip, hashes)
.\Tools\Test-InGame.ps1        # isolated in-game self-test (Core + this mod only)
```

Direct compile without the scripts:

```powershell
dotnet build Source\RimCoreDataCenters\RimCoreDataCenters.csproj -c Release /p:RimWorldDir="C:\path\to\RimWorld"
```

The assembly is written into `Mod\<version>\Assemblies` for the selected RimWorld version. Symbols are not
produced and source paths are mapped to `Source`, so the DLL contains no machine-specific paths.

## The in-game self-test

`Tools\Test-InGame.ps1` starts RimWorld with `-savedatafolder` pointing at a throwaway `.testprofile`
folder inside this repository, a Core-only mod list plus this mod, a private log file, `-quicktest` (auto
test colony) and `-rcdc-selftest`. Your real settings, saves and mod list are never touched, and only the
process the script started is ever stopped.

The mod then drives the simulation itself and checks, with real colonists and real ticks: definitions and
textures, research gating and the whole upgrade tree (each upgrade's real effect), construction from a
blueprint, power, network capacity, heat behaviour, maintenance jobs, operations shifts, output, stacking,
hauling, trading, UPS behaviour, the research uplink, both security doors (pathing, colonists really
walking through, fail-secure, the access-denied alert), secure certification and the trader price, alerts,
inspect text, sounds, and a save/load round trip. Results are printed as `[SELFTEST] PASS/FAIL` lines and a
final `SELFTEST RESULT` line in `.testprofile\Player.log`. It does not stop at the first failure and it
fails if the game logged any error or warning during the run.

Two RimWorld behaviours matter when writing checks: the power net switches new or re-enabled consumers on
one at a time (a lone consumer can take up to 200 ticks), so wait for `PowerOn` rather than a fixed delay;
and always pass an **absolute** path to `-ExpectPackage`.

To test the exact release package:

```powershell
.\Tools\Release.ps1
.\Tools\Install-ToGame.ps1 -Source .\Release\RimCoreDataCenters
.\Tools\Test-InGame.ps1 -ExpectPackage .\Release\RimCoreDataCenters   # refuses to run unless byte-identical
```

`-Scene` builds a working demo data center for screenshots and leaves the game open.

## Project structure

```
Source/RimCoreDataCenters/
  Comps/         CompServerRack, CompNetworkCore, CompOperationsConsole, RackStatus
  Network/       MapComponent_DataCenterNetwork (rack <-> core assignment, rebuilt after load)
  Buildings/     Building_UpsUnit, PlaceWorker_NetworkCoreRange
  Jobs/          work givers, job drivers, work speed helper
  Alerts/        overheating, maintenance, offline alerts
  DevTools/      debug actions, demo builder, self-test, screenshot scene (all opt-in)
```

Save data: each rack saves production progress, wear, the shutdown latch, cartridges produced and its
message flags; each console saves its coverage and shift count. Network links, statuses and efficiencies
are derived at runtime, so they are always consistent after loading.
