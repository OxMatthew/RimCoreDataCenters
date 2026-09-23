using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>References to everything spawned by <see cref="DemoDataCenter.Build"/>.</summary>
    internal class DemoLayout
    {
        public IntVec3 Origin;
        public CellRect Interior;
        public CellRect Site;
        public readonly List<Thing> Racks = new List<Thing>();
        public Thing Core;
        public Thing Cooler;
        public Thing Console;
        public readonly List<Thing> Ups = new List<Thing>();
        public readonly List<Thing> Generators = new List<Thing>();
        public IntVec3 AisleCell;
        public IntVec3 DoorCell;
        public IntVec3 RackSlotWithheld = IntVec3.Invalid;
    }

    /// <summary>
    /// Builds a small, fully wired demo data center (walls, roof, concrete floor, door, power
    /// conduits, generators and every mod building). Developer/test tooling only: it clears whatever is
    /// in its footprint. Used by the "Spawn demo data center" debug action and the self-test.
    /// </summary>
    internal static class DemoDataCenter
    {
        public const int InteriorWidth = 12;
        public const int InteriorHeight = 7;

        // Everything is laid out relative to the interior's south-west corner (the "origin").
        // Row A racks face south into the aisle, row B racks face north into it.
        private static readonly IntVec3[] RackCellsA = { new IntVec3(2, 0, 5), new IntVec3(3, 0, 5) };
        private static readonly IntVec3[] RackCellsB = { new IntVec3(2, 0, 1), new IntVec3(3, 0, 1) };

        public static CellRect SiteFor(IntVec3 origin)
        {
            return new CellRect(origin.x - 2, origin.z - 2, 21, 12);
        }

        public static bool TryFindSite(Map map, out IntVec3 origin)
        {
            IntVec3 center = map.Center;
            for (int radius = 0; radius < 60; radius += 3)
            {
                for (int dx = -radius; dx <= radius; dx += 3)
                {
                    for (int dz = -radius; dz <= radius; dz += 3)
                    {
                        if (radius > 0 && System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius)
                        {
                            continue;
                        }
                        IntVec3 candidate = new IntVec3(center.x + dx, 0, center.z + dz);
                        if (SiteIsUsable(map, SiteFor(candidate)))
                        {
                            origin = candidate;
                            return true;
                        }
                    }
                }
            }
            origin = IntVec3.Invalid;
            return false;
        }

        public static bool SiteFitsAt(Map map, IntVec3 origin)
        {
            return SiteIsUsable(map, SiteFor(origin));
        }

        private static bool SiteIsUsable(Map map, CellRect site)
        {
            if (!site.ExpandedBy(1).InBounds(map))
            {
                return false;
            }
            foreach (IntVec3 c in site)
            {
                TerrainDef terrain = c.GetTerrain(map);
                if (terrain == null || terrain.passability == Traversability.Impassable || terrain.IsWater)
                {
                    return false;
                }
                if (c.Roofed(map) && c.GetRoof(map) != null && c.GetRoof(map).isNatural)
                {
                    return false;
                }
                // The builder clears its footprint, so refuse sites with things that cannot be destroyed (steam geysers, ...).
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i].def.category != ThingCategory.Pawn && !things[i].def.destroyable)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <param name="withheldRack">Leave this rack slot empty (used by the self-test, which builds that rack with colonists).</param>
        public static DemoLayout Build(Map map, IntVec3 origin, bool withPower, bool withholdFirstRack)
        {
            DemoLayout layout = new DemoLayout();
            layout.Origin = origin;
            layout.Interior = new CellRect(origin.x, origin.z, InteriorWidth, InteriorHeight);
            layout.Site = SiteFor(origin);
            layout.AisleCell = origin + new IntVec3(4, 0, 3);
            layout.DoorCell = origin + new IntVec3(-1, 0, 3);

            ClearSite(map, layout.Site);

            ThingDef steel = ThingDefOf.Steel;
            CellRect shell = layout.Interior.ExpandedBy(1);

            // Floor and roof.
            foreach (IntVec3 c in shell)
            {
                map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            }

            // Cooler footprint: cells on the north wall row that the cooler occupies instead of a wall.
            ThingDef coolerDef = RcdcDefOf.RCDC_PrecisionCoolingUnit;
            IntVec3 coolerMin = origin + new IntVec3(3, 0, InteriorHeight);
            IntVec3 coolerCenter = CenterForMin(coolerDef, Rot4.North, coolerMin);
            CellRect coolerRect = GenAdj.OccupiedRect(coolerCenter, Rot4.North, coolerDef.size);

            // Walls and door around the interior.
            foreach (IntVec3 c in shell.EdgeCells)
            {
                if (c == layout.DoorCell || coolerRect.Contains(c))
                {
                    continue;
                }
                Spawn(map, ThingDefOf.Wall, c, Rot4.North, steel);
            }
            Spawn(map, ThingDefOf.Door, layout.DoorCell, Rot4.North, steel);

            // Power conduits: a ring just inside the north and south walls, a spine down the east side,
            // and a feeder under the east wall out to the generators.
            for (int x = 2; x <= 11; x++)
            {
                Spawn(map, ThingDefOf.PowerConduit, origin + new IntVec3(x, 0, 6), Rot4.North, null);
                Spawn(map, ThingDefOf.PowerConduit, origin + new IntVec3(x, 0, 0), Rot4.North, null);
            }
            for (int z = 1; z <= 5; z++)
            {
                Spawn(map, ThingDefOf.PowerConduit, origin + new IntVec3(11, 0, z), Rot4.North, null);
            }
            for (int x = 12; x <= 13; x++)
            {
                Spawn(map, ThingDefOf.PowerConduit, origin + new IntVec3(x, 0, 3), Rot4.North, null);
            }
            for (int z = 0; z <= 7; z++)
            {
                if (z != 3)
                {
                    Spawn(map, ThingDefOf.PowerConduit, origin + new IntVec3(13, 0, z), Rot4.North, null);
                }
            }

            // Cooler over the north wall: cold air enters the room, hot exhaust leaves to the north.
            layout.Cooler = Spawn(map, coolerDef, coolerCenter, Rot4.North, null);

            // Racks.
            for (int i = 0; i < RackCellsA.Length; i++)
            {
                IntVec3 cell = origin + RackCellsA[i];
                if (withholdFirstRack && i == 0)
                {
                    layout.RackSlotWithheld = cell;
                    continue;
                }
                layout.Racks.Add(Spawn(map, RcdcDefOf.RCDC_ServerRack, cell, Rot4.North, null));
            }
            for (int i = 0; i < RackCellsB.Length; i++)
            {
                layout.Racks.Add(Spawn(map, RcdcDefOf.RCDC_ServerRack, origin + RackCellsB[i], Rot4.South, null));
            }

            // Network core, console and UPS units.
            layout.Core = Spawn(map, RcdcDefOf.RCDC_NetworkCore, CenterForMin(RcdcDefOf.RCDC_NetworkCore, Rot4.North, origin + new IntVec3(8, 0, 5)), Rot4.North, null);
            layout.Console = Spawn(map, RcdcDefOf.RCDC_OperationsConsole, CenterForMin(RcdcDefOf.RCDC_OperationsConsole, Rot4.South, origin + new IntVec3(8, 0, 1)), Rot4.South, null);
            layout.Ups.Add(Spawn(map, RcdcDefOf.RCDC_UpsUnit, origin + new IntVec3(10, 0, 5), Rot4.North, null));
            layout.Ups.Add(Spawn(map, RcdcDefOf.RCDC_UpsUnit, origin + new IntVec3(10, 0, 4), Rot4.North, null));

            if (withPower)
            {
                ThingDef gen = DefDatabase<ThingDef>.GetNamedSilentFail("WoodFiredGenerator");
                if (gen != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        IntVec3 min = origin + new IntVec3(14, 0, i * 2);
                        Thing g = Spawn(map, gen, CenterForMin(gen, Rot4.North, min), Rot4.North, null);
                        layout.Generators.Add(g);
                        Refuel(g);
                    }
                }
                foreach (Thing ups in layout.Ups)
                {
                    CompPowerBattery battery = ups.TryGetComp<CompPowerBattery>();
                    if (battery != null)
                    {
                        battery.SetStoredEnergyPct(1f);
                    }
                }
            }

            map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            return layout;
        }

        public static void Refuel(Thing generator)
        {
            CompRefuelable fuel = generator == null ? null : generator.TryGetComp<CompRefuelable>();
            if (fuel != null)
            {
                fuel.Refuel(fuel.Props.fuelCapacity);
            }
        }

        public static Thing Spawn(Map map, ThingDef def, IntVec3 cell, Rot4 rot, ThingDef stuff)
        {
            Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? (stuff ?? GenStuff.DefaultStuffFor(def)) : null);
            if (thing.def.CanHaveFaction)
            {
                thing.SetFaction(Faction.OfPlayer);
            }
            GenSpawn.Spawn(thing, cell, map, rot, WipeMode.Vanish);
            return thing;
        }

        /// <summary>The spawn position that makes a building of this size occupy a rectangle whose minimum corner is <paramref name="min"/>.</summary>
        public static IntVec3 CenterForMin(ThingDef def, Rot4 rot, IntVec3 min)
        {
            for (int dx = 0; dx <= def.size.x + def.size.z; dx++)
            {
                for (int dz = 0; dz <= def.size.x + def.size.z; dz++)
                {
                    IntVec3 candidate = new IntVec3(min.x + dx, 0, min.z + dz);
                    CellRect rect = GenAdj.OccupiedRect(candidate, rot, def.size);
                    if (rect.minX == min.x && rect.minZ == min.z)
                    {
                        return candidate;
                    }
                }
            }
            return min;
        }

        private static void ClearSite(Map map, CellRect site)
        {
            List<Thing> toRemove = new List<Thing>();
            foreach (IntVec3 c in site)
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t.def.category != ThingCategory.Pawn && t.def.destroyable && !toRemove.Contains(t))
                    {
                        toRemove.Add(t);
                    }
                }
                map.roofGrid.SetRoof(c, null);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                if (!toRemove[i].Destroyed)
                {
                    toRemove[i].Destroy(DestroyMode.Vanish);
                }
            }
        }
    }
}
