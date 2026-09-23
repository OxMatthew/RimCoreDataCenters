using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCore.DataCenters
{
    public class CompProperties_NetworkCore : CompProperties
    {
        /// <summary>How many server racks this core can serve.</summary>
        public int maxRacks = 6;

        /// <summary>Radius in cells within which racks and consoles can link to this core.</summary>
        public float range = 12.9f;

        /// <summary>Waste heat while powered, in heat units per second.</summary>
        public float heatPerSecond = 3f;

        public CompProperties_NetworkCore()
        {
            compClass = typeof(CompNetworkCore);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            if (maxRacks < 1)
            {
                yield return "CompProperties_NetworkCore.maxRacks must be at least 1.";
            }
            if (range < 1f)
            {
                yield return "CompProperties_NetworkCore.range must be at least 1.";
            }
            if (parentDef.GetCompProperties<CompProperties_Power>() == null)
            {
                yield return "Network Core requires CompProperties_Power.";
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
            {
                yield return entry;
            }
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "RCDC_StatRackCapacity".Translate(),
                maxRacks.ToString(), "RCDC_StatRackCapacityDesc".Translate(), 4200);
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "RCDC_StatNetworkRange".Translate(),
                "RCDC_CellsValue".Translate(range.ToString("F0")), "RCDC_StatNetworkRangeDesc".Translate(), 4190);
        }
    }

    /// <summary>
    /// The Network Core links nearby server racks (and operations consoles) into one data center.
    /// Links are recomputed at runtime by <see cref="MapComponent_DataCenterNetwork"/> so nothing here
    /// needs to be saved; the assignment is deterministic and stable across save/load.
    /// </summary>
    public class CompNetworkCore : ThingComp
    {
        private readonly List<CompServerRack> racks = new List<CompServerRack>();
        private readonly List<CompOperationsConsole> consoles = new List<CompOperationsConsole>();
        private CompPowerTrader power;

        public CompProperties_NetworkCore Props
        {
            get { return (CompProperties_NetworkCore)props; }
        }

        public IList<CompServerRack> ConnectedRacks
        {
            get { return racks; }
        }

        public IList<CompOperationsConsole> ConnectedConsoles
        {
            get { return consoles; }
        }

        /// <summary>How many racks this core can serve, including the Network Fabric upgrade.</summary>
        public int Capacity
        {
            get { return Props.maxRacks + RcdcUpgrades.Current.ExtraRacksPerCore; }
        }

        /// <summary>Link radius in cells, including the Network Fabric upgrade.</summary>
        public float Range
        {
            get { return Props.range + RcdcUpgrades.Current.ExtraCoreRange; }
        }

        public int ConnectedCount
        {
            get { return racks.Count; }
        }

        public bool HasFreeSlot
        {
            get { return racks.Count < Capacity; }
        }

        public bool IsOnline
        {
            get { return parent != null && parent.Spawned && power != null && power.PowerOn; }
        }

        /// <summary>True while at least one connected operations console has active monitoring coverage.</summary>
        public bool IsMonitored
        {
            get
            {
                // An active AI core watches the whole data center by itself.
                MapComponent_DataCenterNetwork network = parent == null ? null : MapComponent_DataCenterNetwork.For(parent.Map);
                if (network != null && network.Ai.Monitoring)
                {
                    return true;
                }
                for (int i = 0; i < consoles.Count; i++)
                {
                    if (consoles[i] != null && consoles[i].HasCoverage)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader>();
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
            if (network != null)
            {
                network.Register(this);
            }
        }

#if RIMWORLD_1_6
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
#else
        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
#endif
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            if (network != null)
            {
                network.Unregister(this);
            }
            racks.Clear();
            consoles.Clear();
        }

        internal void ClearLinks()
        {
            racks.Clear();
            consoles.Clear();
        }

        internal void LinkRack(CompServerRack rack)
        {
            if (rack != null && !racks.Contains(rack))
            {
                racks.Add(rack);
            }
        }

        internal void LinkConsole(CompOperationsConsole console)
        {
            if (console != null && !consoles.Contains(console))
            {
                consoles.Add(console);
            }
        }

        public bool InRange(Thing other)
        {
            if (other == null || !other.Spawned || parent == null || !parent.Spawned || other.Map != parent.Map)
            {
                return false;
            }
            return (other.Position - parent.Position).LengthHorizontal <= Range;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (parent.Spawned && IsOnline && Props.heatPerSecond > 0f)
            {
                GenTemperature.PushHeat(parent, Props.heatPerSecond * 4.1666665f);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("RCDC_NetworkStatus".Translate()).Append(": ");
            sb.Append(IsOnline ? "RCDC_NetworkOnline".Translate() : "RCDC_NetworkOffline".Translate());
            sb.Append('\n').Append("RCDC_ConnectedRacks".Translate(ConnectedCount, Capacity));
            if (ConnectedCount >= Capacity)
            {
                sb.Append(" (").Append("RCDC_NetworkAtCapacity".Translate()).Append(')');
            }
            sb.Append('\n').Append("RCDC_ConnectedConsoles".Translate(consoles.Count));
            if (consoles.Count > 0)
            {
                sb.Append(" - ").Append(IsMonitored ? "RCDC_Monitored".Translate() : "RCDC_Unmonitored".Translate());
            }
            return sb.ToString();
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (!parent.Spawned)
            {
                return;
            }
            GenDraw.DrawRadiusRing(parent.Position, Range);
            Vector3 from = parent.TrueCenter();
            for (int i = 0; i < racks.Count; i++)
            {
                CompServerRack rack = racks[i];
                if (rack != null && rack.parent != null && rack.parent.Spawned)
                {
                    GenDraw.DrawLineBetween(from, rack.parent.TrueCenter(), SimpleColor.Cyan);
                }
            }
            for (int i = 0; i < consoles.Count; i++)
            {
                CompOperationsConsole console = consoles[i];
                if (console != null && console.parent != null && console.parent.Spawned)
                {
                    GenDraw.DrawLineBetween(from, console.parent.TrueCenter(), SimpleColor.Green);
                }
            }
        }
    }
}
