# Balance reference (v0.6.0)

Every number below is an XML value (`Mod/Defs/RCDC_Buildings.xml`, `RCDC_Items.xml`,
`RCDC_Research.xml`, `RCDC_Security.xml`) unless noted. Time: 60,000 ticks = 1 day, 2,500 ticks = 1 hour.
Prices use vanilla market values (steel 1.9, component 32, gold 10 silver).

## Design goals

* Achievable in the midgame, and a real investment in power, cooling, space, skilled labor and components.
* Profitable when managed, but not an infinite-money machine.
* Heat, outages, maintenance and network capacity should each force a decision.
* Neglect and overheating make racks slower and then stop them. They never explode or start fires.
* Start conservative: it is easier to raise output later than to take it away.

## Research

| Item | Value | Notes |
| --- | --- | --- |
| Data Center Infrastructure | 3500 points | Comparable to Fabrication / Multi-analyzer (4000). Needs a hi-tech research bench. Prerequisites: Microelectronics (3000), Air conditioning (500). |

## Buildings

| Building | Power | Heat (per second) | Cost | Work to build | Notes |
| --- | --- | --- | --- | --- | --- |
| Server Rack (1x1) | 450 W (90 W standby) | +8.0 (+1.2 standby) | 120 steel, 8 components, 10 gold (about 584 silver) | 3600 | 180 HP, mass 45, flammability 0.5, beauty -2, needs construction 6 |
| Network Core (2x1) | 150 W | +3.0 | 100 steel, 6 components, 8 gold (about 462 silver) | 2800 | Serves 6 racks within 12.9 cells |
| Precision Cooling Unit (2x1) | 480 W (12% when idle) | -55 x efficiency | 150 steel, 6 components (about 477 silver) | 3400 | Vanilla cooler behaviour, target 24 C (10 to 30 C) |
| UPS Unit (1x1) | none | none | 60 steel, 4 components, 12 gold (about 362 silver) | 1800 | 100 Wd at 70% efficiency (vanilla battery: 600 Wd, 50%) |
| Operations Console (2x1) | 100 W | +1.5 | 60 steel, 4 components (about 242 silver) | 1600 | 1 shift = 2400 work (about 1 h) |

For scale: a vanilla heater produces 21 heat/s and a vanilla cooler removes 21 heat/s for 200 W.

## Production

| Value | Setting |
| --- | --- |
| Base cycle | 36,000 ticks per cartridge (0.6 day), i.e. 1.67 cartridges/day at 100% efficiency |
| Cartridge | market value 80, stack 25, mass 0.08, sellable only |
| Sale price | traders pay roughly 55-60% of market value (measured 48-63 silver each in testing) |
| Output per rack | about 90 silver/day at 100% efficiency |

Efficiency is the product of three factors:

| Factor | Curve |
| --- | --- |
| Heat | 100% up to 32 C, falling linearly to 25% just below 50 C, 0% (shutdown) at 50 C. After a shutdown the rack restarts once the air is at or below 40 C. |
| Wear | 100% up to 50% wear, falling linearly to 35% just before 100%, 0% at 100% (halted). |
| Monitoring | 100% while an Operations Console has coverage, 65% without. |

## Maintenance

| Value | Setting |
| --- | --- |
| Wear to halt | 12 days of continuous running |
| Service requested at | 50% wear (about 6 days) |
| Wear multipliers | x1.35 when unmonitored, x1.5 while heat-throttled |
| Service visit | 1500 work (about 0.6 h) + 1 component, resets wear to 0 |
| Operations shift | 2400 work per shift, gives 1 day of coverage, trims 5% wear per rack |
| Skill | Operations: 75% research speed (Intellectual) + 25% construction speed. Servicing: 25% / 75%. |

## Worked example: a 4-rack starter data center

| | |
| --- | --- |
| Materials | 4 racks + core + cooler + console + 1 UPS = about 3,880 silver: 52 components, 60 gold, about 1,000 steel |
| Power | 4 x 450 + 150 + 100 + 480 = 2,530 W (about 2.5 wood-fired generators, or solar plus batteries) |
| Heat | 4 x 8 + 3 + 1.5 = 36.5 heat/s against 55 heat/s of cooling |
| Income | 4 racks x about 90 silver/day = about 360 silver/day at full efficiency, about 300 realistically |
| Upkeep | 1 component (about 32 silver) per rack per 6 days = about 21 silver/day, plus about 1 h/day of operator time |
| Payback | roughly 13-15 days of good operation for the materials, before power infrastructure |

Growing beyond 6 racks needs a second core (150 W) and a second cooler. Ten racks means about 4.5 kW
plus about 3.4 kW of cooling and support: that is the intended expansion decision.

## Upgrade tree (v0.2.0)

Each project carries an `UpgradeEffects` block in `Defs/RCDC_Research.xml`. Multipliers multiply,
additive values add, and the total is recomputed automatically whenever research finishes (nothing is
saved: after loading, the finished projects are simply read again). All need a hi-tech research bench.
The three marked * also need a multi-analyzer, like vanilla's advanced projects.

| Project | Cost | Needs | Effect |
| --- | --- | --- | --- |
| Optimized Firmware | 1800 | root | x1.15 production speed (no extra heat or power) |
| Immersion Cooling | 2200 | root | x0.7 rack heat; throttle, shutdown and restart temperatures +4 C |
| Predictive Maintenance | 1800 | root | x0.65 wear rate |
| Network Fabric | 2200 | root | +4 racks per core (10), +4 cells of range |
| Redundant Power | 1500 | root | UPS capacity x2 (200 Wd), efficiency +0.15 (85%) |
| Distributed Computing * | 4200 | Firmware, Fabric | research uplink +3% per rack, +25% cap |
| Autonomous Operations * (Spacer) | 5000 | Maintenance, Distributed Computing | unmonitored output +0.25 (90%), unmonitored wear penalty -0.25 (x1.10) |
| Access Control | 1500 | root | unlocks the biometric access door |
| Threat Screening | 2000 | Access Control | unlocks the metal detector gate |
| Secure Certification * | 3000 | Threat Screening | +20% cartridge price while certified |

The whole tree is about 26,000 research points on top of the 3,500 root, the same order as a vanilla
mid-game tech branch. Intended trade-offs:

* **Firmware is the plain income upgrade.** +15% on a 4-rack center (about 360 silver/day) is about +50
  silver/day. It costs research time only, no new equipment.
* **Cooling and Fabric are density upgrades.** They let the same room hold more racks (10 per core
  instead of 6, and 30% less heat each) before you need another core or cooler, which is where the real
  cost of scaling was.
* **Maintenance and Autonomous Operations trade colonist time for research.** A center that needs one
  service visit a day and one console shift a day can run on almost no attention.
* **Redundant Power is small and cheap on purpose.** It doubles the outage ride-through of a UPS
  (measured 1.9 h for two 100 Wd units at 2.5 kW; now about 3.8 h) but it does not replace generators.
* **Effects stack multiplicatively where they are multipliers.** Firmware x1.15 with Secure Certification
  x1.20 is x1.38 income for the same racks, which is why the tree is long and expensive.

## Research uplink (v0.2.0)

The idle compute of running racks speeds up your researchers, on the map they are on.

| Value | Setting |
| --- | --- |
| Bonus | +4% research speed per running rack, capped at +20% (root project) |
| After Distributed Computing | +7% per rack, capped at +45% |
| What counts as a rack | each rack counts as its current efficiency (0 to 1): a throttled, worn or unmonitored rack counts for less, a halted or unpowered one for nothing |
| Who benefits | every player-faction pawn on the map (it is a multiplier on the vanilla Research Speed stat) |
| Refresh | about once a second |

Six well-run racks reach the +20% cap at the start (about one extra researcher for every five you have) and
+42% after Distributed Computing (six racks x 7%; the +45% cap needs seven). It is deliberately not
per-colonist stacking: it scales with the size of your investment and is capped.

## Security doors (v0.2.0)

| Building | Power | Cost | Work | Who may pass |
| --- | --- | --- | --- | --- |
| Biometric Access Door | 60 W | 30 steel, 4 components, 6 gold + 25 of any door material | 2400 | colony members only |
| Metal Detector Gate | 90 W | 40 steel, 4 components + 25 of any door material | 2600 | colony members and unarmed visitors |

Rules, all for humanlike pawns: colonists and colony slaves always pass; a biometric door refuses everyone
else; a metal detector gate lets an unarmed non-colonist through and refuses one carrying any weapon
(wielded or in a pack); with no power a scanner cannot recognise anyone, so it admits colony members only.
Animals and mechs use the vanilla door rules. Hostile pawns are handled by the vanilla door rules too
(they cannot open a colony door and have to break it), so these doors are access control, not a raid defence.

The **access log**: a pawn the scanner turns away while standing at the door is logged once per 2,500
ticks, raises a message, a sound and the Access denied alert for two hours. Visitors that cannot reach a
room never walk up to its door, so expect the log to fill only when someone lingers beside it (an armed
visitor at the gate, or a prisoner that wandered close).

**Secure Certification** is what security pays for: with one working (powered, switched on) door of each
kind on the map, cartridges are worth 20% more (80 to 96 silver, roughly 52 to 62 at a trader). For a
4-rack center (about 360 silver/day) that is about +70 silver/day for about 540 silver of doors
(steel counted as the door material), so the doors pay for themselves in about a week. Lose power to either scanner and the premium goes away until it returns; a UPS on the door
circuit keeps it. Both doors also protect the room from wandering visitors, prisoners and armed guests,
which is the reason to build them even before the research.

## The AI core (v0.3.0)

| Item | Value | Notes |
| --- | --- | --- |
| Cognitive Computing | 5500 points (Spacer) | Needs Distributed Computing and a multi-analyzer. The deepest project in the tree, so the AI is a late-game reward. |
| Adaptive Learning | 3500 points (Spacer) | AI benefits x1.15 strength (additive +0.15), glitches 30% rarer. |
| AI core (2x2) | 800 W, +6 heat/s | 100 steel, 12 components, 25 gold, 20 plasteel (about 850 silver), work 6000, construction 8. Shuts down above 45 C. |

**What it replaces.** An Operations Console shift costs about an hour of colonist time a day and gives
monitoring (racks at 100% instead of 65%, wear at the normal rate). The AI gives that monitoring for 800 W and
no colonist time, plus a 10% wear reduction and a forecast. On a 4-rack center that is worth roughly the
hour a day of an intellectual colonist, for about 3 wood-fired generators' worth of extra power.

**Strength.** Every AI benefit is scaled by *strength* = 0.5 at rapport 0, rising linearly to 1.0 at rapport 100
(plus 0.15 with Adaptive Learning, capped at 1.5). Multipliers move toward 1 by that fraction: at rapport 50,
Efficiency gives x1.075 output instead of x1.10.

| Directive | Gain | Cost |
| --- | --- | --- |
| Balanced | monitoring, forecast, wear x0.9 | none |
| Efficiency | output x1.10, plus the wear benefit | heat x1.20 and the core draws 1,000 W instead of 800 W |
| Stewardship | wear x0.675 in total, temperature limits +2 C | output x0.95 |
| Curiosity | research uplink +2% per rack (cap +10%) | output x0.92 |

Efficiency is worth about +36 silver/day on a 4-rack center (+10% of 360) for +200 W and +20% heat, so it is a
real trade-off rather than a free upgrade. Curiosity gives up 8% of income (about 29 silver/day) for about
+8% research speed at four racks.

**Rapport (0 to 100, starts at 50).** +1 per healthy day (no rack in trouble and at least one operational),
-1 per day with any rack too hot, worn, unpowered or unlinked, -3 per day offline once it has booted. Accepting a
request +5, declining -2, ignoring it for five days -4. A colony that runs the data center well settles near
the top; a neglected one drifts to frosty and loses up to half of the AI's benefit.

**Requests** arrive every 8 to 14 days (never the same kind twice in a row):

| Request | If accepted | Trade-off |
| --- | --- | --- |
| Compute loan | research uplink x2 for one day | racks produce 20% less for that day (about -70 silver) |
| Overclock window | output x1.25 for one day | racks give off 40% more heat: needs cooling headroom |
| Maintenance window | every rack recovers 20% wear at once | output x0.5 for three hours (about -15 silver) |

**Glitches are cosmetic, not punishing.** Mean days between glitches: 45 at rapport 60 or more, 25 at 30 to 59, 12
below 30. A security-certified data center halves the rate; Adaptive Learning cuts it by 30% more. A glitch is a
1 to 2 hour reboot (monitoring returns by itself), a one-day sulk (no monitoring or benefits; the console
covers if you have one), or one rack losing its current cartridge progress (at most about 0.6 day of one rack's
output, roughly 50 silver). Nothing is destroyed and no hostile event ever fires.

## Data specialization (v0.4.0)

| Item | Value | Notes |
| --- | --- | --- |
| Data Classification | 1800 points | Needs only the root project (not the rest of the tree), so it is reachable early. Unlocks the Specialization command and all three variants. |
| Financial Data Cartridge | 110 market value (+37.5%) | No stored-cartridge bonus. The straightforward "more silver" choice, and the thing an espionage raid wants most (see below). |
| Research Data Cartridge | 80 market value | +0.4% research speed per cartridge stored on the map, capped at +8% (reached at 20 stored). Stacks with the research uplink. |
| Medical Data Cartridge | 80 market value | +1% immunity gain speed per cartridge stored on the map, capped at +15% (reached at 15 stored). Applies to every colonist on that map. |

All three need only Data Classification, so a colony can pick whichever fits its situation as soon as it is
researched, without committing to the rest of the tree. Switching a rack's specialization keeps its progress
toward the next cartridge - only the type of the *next* one changes, so there is no cost to changing your mind.

The stored bonuses are a genuine hold-or-sell decision: 20 Research cartridges sitting in a stockpile are worth
1,600 silver un-sold, for a permanent +8% to every colonist's research speed on top of the uplink. Selling them
gives the silver immediately and loses the bonus. Security certification's +20% price applies to every
specialization equally, so certification does not change which one is more profitable relative to the others.

## Espionage (v0.4.0)

A small, themed raid built entirely from the game's own raid system (`LordJob_AssaultColony` with looting and
fleeing turned on) - no custom pathing or combat code, so it behaves exactly like any other raid the storyteller
can send, and interacts with vanilla mechanics (walls, doors, combat, downing, fire) exactly the way any raid does.

| Value | Setting |
| --- | --- |
| Category | ThreatSmall (ignoreRecentSelectionWeighting is *not* set, so it competes normally against other small threats) |
| Base chance | 1.2, minimum 5 days between firings |
| Points | the storyteller's normal threat points, capped at 700 (a handful of pawns, not a full raid) |
| Gate | only ever fires on a map that already has a spawned server rack |
| Faction | any faction already hostile to the player able to field a combat group; if none exists, it simply does not fire that time, like any vanilla incident |
| Behaviour | `canSteal` and `canTimeoutOrFlee` are both on: raiders grab valuable haulable items (cartridges especially - Financial Data is the single most valuable thing to grab) and try to leave once they have enough or are losing, rather than fighting to the last pawn |
| Security tie-in | a colony door refuses a non-colonist exactly like any vanilla door; a locked biometric door or metal detector gate around the server room is therefore a real obstacle, not flavor text. (Certification's own effect - the AI core's glitches becoming rarer - is unrelated to this incident; see the AI core section.) |

Because it reuses `IncidentWorker_RaidEnemy`'s underlying building blocks (pawn group generation, arrival modes,
`LordJob_AssaultColony`) rather than inventing new ones, an espionage raid is exactly as dangerous as any other
small raid of the same point value - it is not a scripted-safe event. Colonists can be hurt or killed defending
the data center. The "espionage" framing is about the raiders' *goal* (loot and leave) and the flavor text, not
a guarantee of safety.

## Data contracts (v0.5.0)

A periodic offer letter, not a building or a raid: a buyer wants N cartridges of one type within a deadline
for a bonus over their current market value. `MapComponent_DataContracts` handles offer/accept/fulfill/expire
entirely through vanilla letter and drop-pod machinery (`DropPodUtility.DropThingsNear`) - no custom UI.

| Value | Setting |
| --- | --- |
| Gate | only offered on a map with at least one spawned server rack |
| Cooldown between offers | 5-10 days after the previous one resolves (accepted, declined or expired) |
| Offer timeout | 3 days to accept before it is withdrawn on its own |
| Quantity | 15-35 cartridges |
| Bonus | 20-40% over the cartridge's current market value at the moment the offer is *accepted* (so certification and stored-cartridge bonuses already active are captured, but the quote does not drift afterward) |
| Deadline | 4-8 days from acceptance |
| Cartridge type | standard only until Data Classification is researched; any of the four types after |

Declining or missing the deadline costs nothing - these are opportunities layered on top of the existing
trade loop, not an obligation. A contract's silver bonus is still bounded by how many cartridges the colony
can actually produce in the deadline window, so it cannot be used to convert an idle data center into free
silver. Since 0.6.0, a contract's quoted bonus also captures whatever market dynamics multiplier is active
on that cartridge type at accept time - timing a contract during a rival-buyer or shortage event compounds
with the contract's own bonus.

## Market dynamics (v0.6.0)

Cartridge prices are no longer static. `MapComponent_MarketDynamics` drifts each of the four data types'
price within a band via a bounded random walk, and occasionally layers a named event on top of one type.
Read entirely through a single `StatPart` on `MarketValue` (`StatPart_MarketDynamics`) - no new building, no
letter requiring a decision, just numbers that move and a line on the Operations Console.

| Value | Setting |
| --- | --- |
| Drift step | every 2-4 days, each type's baseline takes a random step of up to +/-5% |
| Drift band | clamped to 85-120% of base value |
| Event check | every 4-8 days while no event is active, a 35% chance one starts |
| Event duration | 4-7 days |
| Rival buyer / Shortage | +15-35% on one cartridge type for the event's duration |
| Market glut | -15-30% on one cartridge type for the event's duration |

Stacks multiplicatively with everything else already affecting `MarketValue`: data specialization's base
value differences, Secure Certification's +20%, and a data contract's own quoted bonus. Only one event is
active per map at a time, and it targets a random one of the four cartridge types - there is no way to
predict or force which type it lands on, by design.

## Why it is not an infinite-money exploit

* **Trader silver is finite.** Orbital and caravan traders carry a few thousand silver. A 4-rack center
  makes about 6-7 cartridges a day, roughly what one trader visit can absorb per 5-10 days.
* **Capacity, heat and power all scale together.** More racks means more cores, more coolers, more
  generators, more components and more colonist time.
* **Traders never sell cartridges**, so there is no buy-low-sell-high loop.
* **Neglect is expensive.** Unmonitored, unserviced or overheated racks lose up to 65-100% of their output.

## Sanity checks measured in the in-game self-test

Production rate matches `ticksPerCartridge x efficiency` to four decimals; racks heat a closed room by
about 9 C in 2000 ticks with the cooler off and the cooler restores it; a fully charged UPS pair (200 Wd)
carried a 2.5 kW load for about 4,800 ticks (1.9 h); an emergency shutdown drops draw to 20% and stops
production; full wear halts a rack without damaging it.
