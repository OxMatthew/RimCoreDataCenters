# RimCore Data Centers

A RimWorld 1.5 / 1.6 mod that lets you research, build and run a data center: networked server racks that
produce valuable **Data Cartridges** for traders, kept alive by power, precision cooling, a UPS and
colonists who monitor and service the equipment.

* **Package ID:** `RimCore.DataCenters` (permanent, do not change between releases)
* **Version:** 0.5.0 &nbsp;|&nbsp; **Supports:** RimWorld 1.5 and 1.6 &nbsp;|&nbsp; **License:** MIT
* **Requires:** nothing (no DLC, no Harmony, no other mods)

## Development approach

Development is AI-assisted. Claude has been used extensively for implementation, while project direction, feature design, testing, iteration, release management and ongoing maintenance are handled by the project maintainer. AI-generated changes are tested against the game and reviewed as part of the release workflow.

## Playing it

1. Finish the research project **Data Center Infrastructure** (needs a hi-tech research bench; the
   prerequisites are Microelectronics and Air conditioning).
2. Open the new **Data center** architect tab and build a small server room:
   * **Server Rack** - produces cartridges. 450 W, gives off 8 heat/s.
   * **Network Core** - every rack must be linked to a powered core in range (12.9 cells). One core
     serves 6 racks. Select it to see its range, links and `connected / capacity`.
   * **Precision Cooling Unit** - a heavy-duty wall cooler. Cold air leaves the south side of the
     placement ghost, hot exhaust the north side (same as the vanilla cooler).
   * **UPS Unit** - a compact 100 Wd battery that rides through short power cuts. It does not replace a
     real battery bank (a vanilla battery stores 600 Wd).
   * **Operations Console** - where colonists run the recurring *Data Center Operations* shift.
3. In the **Work** tab, enable the new **Data center** work type for at least one colonist.
   (In an existing save the new column starts disabled.)
4. Keep a few **components** in storage: servicing a rack consumes one.
5. Haul the finished **Data Cartridges** (they appear on the tile in front of each rack) to a stockpile
   and sell them to orbital traders, outlander caravans and outlander settlements.

### What each rack status means

| Status | Meaning | What to do |
| --- | --- | --- |
| **Operational** | Producing at the efficiency shown in the inspect pane | Nothing |
| **No Power** | The grid cannot supply the rack (or it is switched off) | Fix generation, add a UPS or battery |
| **No Network** | No powered Network Core in range with a free slot | Build a core or free a slot; check the core has power |
| **Too Hot** | Throttled above 32 C; emergency shutdown at 50 C; restarts below 40 C | Add or repair cooling |
| **Maintenance Required** | Worn: needs servicing. At 100% wear the rack halts | Assign a technician and stock a component |
| **Output Full** | 25 cartridges are waiting in front of the rack | Haul them away (build a stockpile nearby) |

Racks never explode or burn from neglect or heat. Neglect makes them slower, then stops them.

### Operations coverage

An *Operations Console* linked to the same core lets colonists run a monitoring shift (about 1 hour of
work per day). While the network is monitored, racks run at full output and wear normally; without
coverage they run at 65% output and wear 35% faster. A finished shift also trims 5% wear from every
rack. Intellectual skill matters most for operations, Construction skill most for servicing.

## Upgrade tree (0.2.0)

After the root project there are nine follow-ups. Every effect is an XML value on the project
(`Defs/RCDC_Research.xml`), and finished research applies automatically:

| Project | Effect |
| --- | --- |
| Optimized Firmware | racks produce 15% faster |
| Immersion Cooling | racks give off 30% less heat, and every temperature threshold is 4 C higher |
| Predictive Maintenance | racks wear 35% more slowly |
| Network Fabric | a core serves 10 racks (was 6) and reaches 4 cells further |
| Redundant Power | the UPS stores twice the energy, at 85% efficiency (was 70%) |
| Distributed Computing | the research uplink grows (below) |
| Autonomous Operations | a rack with no console coverage runs at 90% output (was 65%) |
| Access Control, Threat Screening | unlock the two security doors |
| Secure Certification | certified data sells for 20% more |

### Research uplink

Every rack that is up and running adds research speed for all your colonists on the map: +4% per rack,
up to +20%. Distributed Computing raises that to +7% per rack, up to +45%. A throttled, worn or
unmonitored rack counts for less. The Operations Console shows the current bonus, and it appears in
each colonist's research-speed breakdown.

### Security

* **Biometric Access Door** - a powered door with a facial scanner. Colonists (and colony slaves) walk
  through; visitors, guests and prisoners cannot open it.
* **Metal Detector Gate** - colonists pass, unarmed visitors pass, anyone carrying a weapon (in hand or in
  a pack) is refused.
* Both are normal doors otherwise (hold open, materials, HP). Animals and mechs use the vanilla rules, so
  pets are never trapped. **No power = fail secure**: a dead scanner admits colony members only.
* Turned-away visitors are logged on the door, raise a message and sound, and the **Access denied** alert.
* **Secure Certification**: while you have a working door of each kind, data cartridges sell for +20%.
* These are access control, not a raid defence. Raiders still have to break a door down.

## The AI core (0.3.0)

Research **Cognitive Computing** (after Distributed Computing), then build the **AI core** in a cool room
within range of a network core. It is a simulated character called Meridian: no network access and no language
model, every line is a translatable string, and everything it does is bookkeeping over your racks' real state.

* **Runs the place:** while online it monitors every rack by itself (no operations shifts needed), applies
  constant diagnostics (racks wear 10% more slowly at full strength), shows a service forecast on each rack
  and writes a status report on request ("Ask Meridian").
* **Directives** (Directive command on the core): *Balanced*; *Efficiency* (+10% output, +20% heat, +25% power);
  *Stewardship* (25% slower wear, +2 C tolerance, -5% output); *Curiosity* (a bigger research uplink, -8% output).
* **Rapport** (0 to 100) rises a little each healthy day and falls when racks are in trouble or it is offline.
  Every benefit scales from half strength (0) to full strength (100).
* **Requests:** every 8 to 14 days it writes a letter with a small choice: lend compute to research for a day,
  run an overclock window, or run a maintenance window. Accept, decline, or leave it: each moves rapport.
* **Small risks, nothing hostile:** it may reboot for an hour or two, sulk for a day (no monitoring), or
  lose one rack's current cartridge progress. Rarer with high rapport, half as often in a security-certified
  data center. **Adaptive Learning** makes it 15% stronger and its glitches 30% rarer.
* It draws 800 W and gives off heat. If it goes down, the **AI core down** alert appears and racks fall back
  to console monitoring.

## Data specialization and espionage (0.4.0)

Research **Data Classification** (only needs the root project) to unlock a **Specialization** command on every
server rack and three new cartridge types. Switching keeps the rack's progress toward its next cartridge.

* **Financial Data Cartridge** - worth 110 instead of 80 (+37.5%). The plain "more silver" choice, and also the
  most valuable thing to an espionage raid.
* **Research Data Cartridge** - worth the same as standard data, but a stockpile of them on the map adds a
  small extra research-speed bonus (up to +8%) on top of the normal uplink.
* **Medical Data Cartridge** - worth the same as standard data, but a stockpile of them speeds up every
  colonist's immunity gain (up to +15%). Sell them for silver, or hold some back for the bonus.
* Security certification's +20% price lifts every data type together.

**Espionage** is a new, occasional small raid built entirely from the game's own raid system (a looting,
fleeing assault on the colony) rather than custom code, so it behaves like any other raid the storyteller can
send: colonists can be hurt defending the data center, and raiders can be driven off or killed. It only ever
fires on a map that already has a server rack, targets valuable haulable items (cartridges especially), and
tries to leave with its loot rather than fight to the end. A locked biometric door or metal detector gate
around the server room stops raiders like any other obstacle - it is not scripted safety, it is the same door
rule that already refuses any non-colonist. Security certification does not change how often it fires.

## Data contracts (0.5.0)

Once at least one server rack is running, a buyer occasionally offers a **data contract**: deliver a set
number of a given cartridge type within a deadline for a silver bonus (20-40%) over their current market
value. Accept, decline, or let the offer lapse on its own - nothing is ever taken from you for saying no,
and there is never more than one offer or active contract on a map at a time.

* Deliver at least the requested amount before the deadline and the silver bonus is dropped near your trade
  spot, consuming the cartridges.
* Miss the deadline and the contract simply expires - no penalty, no loss beyond the missed opportunity.
* Before Data Classification is researched, contracts only ask for standard data; after, they can ask for
  any of the specialized cartridge types too.
* Every number (offer frequency, quantity range, bonus range, deadline) is in `Defs/RCDC_Contracts.xml`.

## Balance at a glance

See [docs/BALANCE.md](docs/BALANCE.md) for every number and the reasoning. In short: a 4-rack starter data
center costs about 3,900 silver of materials (52 components, 60 gold), draws about 2.5 kW, and earns roughly
300 silver per day when well run, so it pays for itself in a few weeks. It is capped by rack capacity per
core, heat, power and how much silver traders carry.

## Tuning it

Everything important is an XML value. Edit `Defs/RCDC_Buildings.xml` (rack, core, cooler, UPS,
console), `Defs/RCDC_Items.xml` (cartridge market value), `Defs/RCDC_Research.xml` (the upgrade tree) and
`Defs/RCDC_Security.xml` (the doors), then restart the game:

| Want to change | Edit |
| --- | --- |
| Production speed | `ticksPerCartridge` on the rack (36000 = 0.6 day) |
| Value of a cartridge | `MarketValue` in `RCDC_Items.xml` |
| Heat output | `heatPerSecond` on the rack; cooling power `energyPerSecond` on the cooler |
| Power draw | `basePowerConsumption` on each building |
| Heat thresholds | `warmTemperature`, `shutdownTemperature`, `restartTemperature` |
| Wear / maintenance | `daysToFullWear`, `serviceThreshold`, `serviceWorkTicks`, `serviceItem` |
| Network size | `maxRacks` and `range` on the core |
| Build costs | `costList` on each building |
| What each upgrade does, and what it costs to research | the `UpgradeEffects` block and `baseCost` on each project in `RCDC_Research.xml` |
| Research uplink strength | `researchBonusPerRack` and `researchBonusCap` (on the root project and Distributed Computing) |
| Certification premium | `certifiedPriceBonus` on Secure Certification |
| Door power and cost | `basePowerConsumption` and `costList` in `RCDC_Security.xml` |
| The AI: directives, requests, rapport, glitch rates | the `CompProperties_AiCore` block of `RCDC_AiCore` in `RCDC_Ai.xml` |
| What the AI says | `Languages/English/Keyed/RCDC_KeyedAi.xml` (and add your own language folder) |

## Developer tools

Turn on developer mode and open the debug actions menu, category **RimCore Data Centers**: spawn a fully
wired demo data center, add wear, fill production, force a shutdown, heat or cool a room, print a report,
and run the in-game self-test. Verbose logging is off by default and only writes in developer mode.

## Building from source

See [docs/BUILD.md](docs/BUILD.md). The short version (needs the .NET SDK and a RimWorld 1.5 or 1.6 install):

```powershell
.\Tools\Build.ps1            # compile the assembly
.\Tools\Release.ps1          # build + validate + create Release\RimCoreDataCenters (Workshop-ready)
.\Tools\Test-InGame.ps1 -ExpectPackage .\Release\RimCoreDataCenters   # in-game self-test in an isolated profile
```

## Project layout

```
Mod/          the playable mod (About, Defs, Patches, Languages, Textures, Sounds, 1.5/Assemblies)
Source/       C# source (RimCoreDataCenters.csproj)
Tools/        build, validation, release, asset generation and in-game test scripts
Workshop/     Workshop page text, tags, screenshots
docs/         balance reference, build, release checklist, publishing notes
Release/      generated packages (not committed)
```

## Compatibility

* Supports RimWorld **1.5 and 1.6**. Works with or without Royalty, Ideology, Biotech and Anomaly.
* No Harmony and no patches to vanilla code. The only vanilla data changes are XML patches:
  a "buy" entry on a few trader kinds so they purchase cartridges (`Patches/RCDC_Traders.xml`), and
  two stat parts added to the vanilla Research Speed and Market Value stats (`Patches/RCDC_Stats.xml`).
* Safe to add mid-save. Removing the mod from a save that already contains its buildings or items is not
  supported.
* Mods that rewrite ticking (for example RocketMan) are fine: the mod uses the game's rare tick.

## License

Code, XML, textures and sounds are original work under the [MIT License](LICENSE). See
[NOTICE.md](NOTICE.md) for attributions. RimWorld is a trademark of Ludeon Studios; this mod is unofficial.
