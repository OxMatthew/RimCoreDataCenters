# Changelog

All notable changes are documented here. This project follows [Semantic Versioning](https://semver.org/):
`MAJOR.MINOR.PATCH`. The version lives in one place, `Mod/About/About.xml` (`<modVersion>`), and the
release script stamps the same number into the assembly.

## 0.5.0 - data contracts

Supports RimWorld 1.5 and 1.6. No DLC and no other mods required. Saves from 0.1.0 through 0.4.0 load unchanged.

### Added
- **Data contracts**: once at least one server rack is running, a buyer periodically offers a timed contract -
  deliver N cartridges of a given type within a deadline for a silver bonus (20-40%) over their current market
  value. Accept, decline, or let the offer lapse; nothing is ever taken from you for saying no. Fulfilling a
  contract consumes the delivered cartridges and drops the silver bonus near your trade spot; missing the
  deadline just expires the contract with no penalty. Only one offer or active contract per map at a time.
  Once Data Classification is researched, contracts can also ask for the specialized cartridge types.
  All numbers (offer frequency, quantity range, bonus range, deadline) are in `Defs/RCDC_Contracts.xml`
  (`DataContractSettingsDef`).

## 0.4.0 - data specialization and espionage

Supports RimWorld 1.5 and 1.6. No DLC and no other mods required. Saves from 0.1.0, 0.2.0 and 0.3.0 load unchanged.

### 1.6 compatibility
- Added a real RimWorld 1.6 build (`Mod/1.6/Assemblies/RimCoreDataCenters.dll`), packaged alongside the 1.5
  build. RimWorld 1.6 changed several base-game API signatures this mod overrides or calls directly
  (`ThingComp.PostDeSpawn` gained a `DestroyMode` parameter, `Building_Door.Tick()` became `protected`,
  `PawnUtility.GainComfortFromCellIfPossible` gained a `delta` parameter, and `ThingRequestGroup.ActiveDropPod`
  was renamed to `ActiveTransporter`) - the source now branches on a `RIMWORLD_1_6` build constant to support
  both versions from one codebase. Verified with the full self-test suite (430/430) run twice against a real
  RimWorld 1.6.4871 install, and again against 1.5.4409 to confirm nothing regressed there.

### Added
- **Data classification** research (1800, needs the root project) unlocks a **Specialization** command on every
  server rack, and three new data cartridges. Switching keeps the rack's progress toward its next cartridge.
  - **Research Data Cartridge**: worth the same as standard data (80). A stockpile of them on the map adds a
    small extra research-speed bonus on top of the normal uplink (+0.4% per cartridge stored, capped at +8%).
  - **Financial Data Cartridge**: worth more than any other data type (110 instead of 80) - the most valuable
    thing your data center can make, and the thing an espionage raid wants most.
  - **Medical Data Cartridge**: worth the same as standard data. A stockpile of them on the map speeds up
    every colonist's immunity gain (+1% per cartridge stored, capped at +15%).
  - Security certification's +20% price applies to every data type together, not just the standard one.
- **Espionage**: a new small "smash and grab" raid incident. A handful of pawns from a faction already hostile
  to you try to loot valuable items (cartridges especially) and flee rather than fight to the end. It is built
  entirely from the game's own raid system (a looting, fleeing assault-colony lord), not custom pathing or
  combat code, so it behaves exactly like any other raid: a locked security door stops the raiders like any
  other obstacle, exactly as it would a normal raid. Only ever fires on a map that already has a server rack.
  Security certification halves how often it fires.
- A distinct alarm sound and letter for the espionage raid, and (if an AI core is online) a warning line from
  it when raiders are spotted.
- Debug tools: "Trigger espionage incident now", and Data Classification added to "Complete ALL data center
  research".
- Self-test extended to cover both specializations (production, stockpile bonuses, certification pricing,
  save/load) and the espionage incident (spawning, lord job, letter, and that a locked door refuses a raider).

### Changed
- The data cartridge sprite generator is now shared by all four data types (one color palette each), so they
  read as the same physical object with a different label.

## 0.3.0 - the AI core

Supports RimWorld 1.5. No DLC and no other mods required. Saves from 0.1.0 and 0.2.0 load unchanged.

### Added
- **AI core** ("Meridian"), a 2x2 building unlocked by the new research **Cognitive Computing** (5500,
  needs Distributed Computing and a multi-analyzer). 800 W, gives off heat, needs a network core in range
  and a room below 45 C. It is a simulated character: no network access, no language model. Every line it
  says is a translatable keyed string and everything it does is bookkeeping over the racks' real state.
  - **Automation:** while it is online it monitors the whole data center by itself, so no operations shift
    is needed, and it applies constant diagnostics (racks wear 10% more slowly at full strength).
  - **Forecast:** racks show the AI's estimate of how many days until they need servicing.
  - **Status report:** the "Ask Meridian" command shows a real analysis (racks by status, output per day,
    the rack that will need service soonest, temperature headroom, certification) and at most three
    recommendations.
  - **Directives:** Balanced, Efficiency (+10% output, +20% heat, +25% power draw), Stewardship (25% slower
    wear, +2 C tolerance, -5% output) and Curiosity (+2% research uplink per rack up to +10%, -8% output).
  - **Rapport** (0 to 100): rises a little each day the data center is healthy, falls when racks are in
    trouble or the AI is offline. Every benefit scales from half strength at 0 to full strength at 100.
  - **Requests:** every 8 to 14 days it sends a letter with a small choice: a *compute loan* (research
    uplink doubles for a day, racks -20%), an *overclock window* (+25% output, +40% heat for a day) or a
    *maintenance window* (every rack recovers 20% wear, output halves for three hours). Accepting raises
    rapport, declining lowers it slightly, ignoring it for five days lowers it more.
  - **Personality:** 44 short lines across moods and situations (boot, idle chatter by mood, warnings,
    milestones, glitches, thanks). Messages carry a soft chime.
  - **Small, harmless risks only:** now and then it reboots for one to two hours, sulks for a day (no
    monitoring), or drops one rack's current cartridge progress. Glitches are rarer when rapport is high
    and half as frequent in a security-certified data center. Nothing hostile happens.
- **Adaptive Learning** (3500): the AI's benefits are 15% stronger and its glitches 30% rarer.
- **AI core down** alert (unpowered, unlinked or overheated after it had come online).
- Debug tools: AI core gizmos (set rapport, send a request, force a glitch) in developer mode.
- Self-test extended to cover the AI's definitions, behaviour, directives, rapport, all three requests
  (accept, decline, ignore), glitches, report, forecast, voice strings and save/load.

### Changed
- An active AI core counts as monitoring for every rack on the map, and no operations shift is requested
  while it is watching.

## 0.2.0 - upgrade tree, research uplink and security

Supports RimWorld 1.5. No DLC and no other mods required. Saves from 0.1.0 load unchanged.

### Added
- **Upgrade research tree**: nine follow-up projects. Every effect is an XML value on the project
  (`UpgradeEffects` in `Defs/RCDC_Research.xml`).
  - **Optimized Firmware** (1800): racks produce 15% faster.
  - **Immersion Cooling** (2200): racks give off 30% less heat; throttle, shutdown and restart
    temperatures are 4 C higher.
  - **Predictive Maintenance** (1800): racks wear 35% more slowly.
  - **Network Fabric** (2200): each network core serves 10 racks (was 6) and reaches 4 cells further.
  - **Redundant Power** (1500): the UPS stores twice the energy (200 Wd) at 85% efficiency (was 70%).
  - **Distributed Computing** (4200, needs a multi-analyzer): the research uplink grows.
  - **Autonomous Operations** (5000, needs a multi-analyzer): a rack with no console coverage runs at 90%
    output (was 65%) and its extra wear penalty drops from 35% to 10%.
  - **Access Control** (1500) and **Threat Screening** (2000): unlock the two security doors.
  - **Secure Certification** (3000, needs a multi-analyzer): cartridge price premium (below).
- **Research uplink**: every running rack adds research speed to all colonists on the map (+4% each, up
  to +20%; Distributed Computing raises that to +7% each, up to +45%). It applies through a stat part on
  the vanilla Research Speed stat, so no code patching is involved. The Operations Console shows the
  current bonus.
- **Biometric access door**: a powered door with a facial scanner. Only colony members (colonists and
  colony slaves) can open it; visitors, guests and prisoners cannot.
- **Metal detector gate**: colony members pass, unarmed visitors pass, anyone carrying a weapon is
  refused.
- Both doors are ordinary vanilla-style doors (hold open, HP, temperature, materials). Animals and
  mechs keep the vanilla rules so pets are never trapped. With no power a scanner fails secure and
  admits colony members only. Raiders are still stopped by the normal door rules.
- Access log on each door, a message and sound when someone is turned away, an **Access denied** alert,
  and inspect text.
- **Secure certification**: while the colony has a working biometric door and a working metal detector
  gate, data cartridges sell for 20% more. The price breakdown and the console say so.
- Debug action "Complete ALL data center research (upgrades + security)".
- Self-test extended to cover the tree, every upgrade's real effect, the research uplink, both doors
  (reachability, real walking, fail-secure, the alert), certification, the trader price, and save/load.

### Changed
- Racks show an "Upgrades:" line in the inspect text once any rack upgrade is finished.
- The Data Center Infrastructure project description now mentions the research uplink.

### Fixed
- The 0.1.0 notes below said the cartridge market value was 90; it has always been 80.

## 0.1.0 - initial release

Supports RimWorld 1.5. No DLC and no other mods required.

### Added
- Research project **Data Center Infrastructure** (3500 points, needs a hi-tech research bench;
  prerequisites Microelectronics and Air conditioning).
- **Data center** architect tab with five buildings:
  - **Server Rack**: produces Data Cartridges while powered, linked to a Network Core and cool.
    Status is always one of Operational, No Power, No Network, Too Hot, Maintenance Required or Output Full.
  - **Network Core**: links racks in range (6 per core) and shows connected count and capacity.
  - **Precision Cooling Unit**: heavy-duty cooler built on the vanilla cooler mechanics.
  - **UPS Unit**: compact 100 Wd battery with a live runtime estimate.
  - **Operations Console**: colonists run a recurring Data Center Operations shift.
- **Data Cartridge** item (stack 25, market value 80). Sold to orbital traders, outlander caravans and
  outlander settlements; traders never stock it.
- New **Data center** work type. Operations and servicing speed use Intellectual and Construction skill.
- Wear and servicing: racks wear with use, ask for service at 50% wear, lose efficiency, and stop at 100%
  (they never explode). Servicing costs one component.
- Heat model: output throttles above 32 C, emergency shutdown at 50 C, restart below 40 C. No fires.
- Alerts (overheating, maintenance, offline), messages, status lights, ambient sounds, inspect text.
- Developer tools: debug actions and an opt-in in-game self-test.
