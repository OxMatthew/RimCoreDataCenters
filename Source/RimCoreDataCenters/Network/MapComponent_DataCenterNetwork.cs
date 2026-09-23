using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Per-map registry of racks, network cores and consoles, and the deterministic assignment of
    /// racks to cores. Nothing here is persisted: links are rebuilt from the saved buildings, and the
    /// ordering (by thing ID, nearest core first) makes the result identical after every load.
    /// </summary>
    public class MapComponent_DataCenterNetwork : MapComponent
    {
        /// <summary>How often (ticks) links are refreshed even if nothing changed.</summary>
        private const int RefreshIntervalTicks = 250;

        private readonly List<CompServerRack> racks = new List<CompServerRack>();
        private readonly List<CompNetworkCore> cores = new List<CompNetworkCore>();
        private readonly List<CompOperationsConsole> consoles = new List<CompOperationsConsole>();
        private readonly Dictionary<CompServerRack, CompNetworkCore> rackToCore = new Dictionary<CompServerRack, CompNetworkCore>();
        private readonly Dictionary<CompServerRack, NetworkFailure> rackFailure = new Dictionary<CompServerRack, NetworkFailure>();
        private readonly Dictionary<CompOperationsConsole, CompNetworkCore> consoleToCore = new Dictionary<CompOperationsConsole, CompNetworkCore>();

        private readonly List<Building_AccessDoor> accessDoors = new List<Building_AccessDoor>();

        private bool dirty = true;
        private int lastComputeTick = -99999;
        private int researchCacheTick = -99999;
        private float researchEquivalents;

        public MapComponent_DataCenterNetwork(Map map) : base(map)
        {
        }

        public IList<Building_AccessDoor> AccessDoors
        {
            get { return accessDoors; }
        }

        private readonly List<CompAiCore> aiCores = new List<CompAiCore>();
        private CompAiCore activeAiCache;
        private int activeAiTick = -99999;

        public IList<CompAiCore> AiCores
        {
            get { return aiCores; }
        }

        public void Register(CompAiCore ai)
        {
            if (ai != null && !aiCores.Contains(ai))
            {
                aiCores.Add(ai);
                activeAiTick = -99999;
            }
        }

        public void Unregister(CompAiCore ai)
        {
            aiCores.Remove(ai);
            activeAiTick = -99999;
        }

        /// <summary>Forget which AI core is active (it is re-read on the next query).</summary>
        public void InvalidateAiCache()
        {
            activeAiTick = -99999;
        }

        /// <summary>The AI core that is running the data center on this map (the oldest one that is online), or null.</summary>
        public CompAiCore ActiveAi
        {
            get
            {
                int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
                if (now != activeAiTick)
                {
                    activeAiTick = now;
                    activeAiCache = null;
                    for (int i = 0; i < aiCores.Count; i++)
                    {
                        CompAiCore ai = aiCores[i];
                        if (ai != null && ai.parent != null && ai.parent.Spawned && ai.IsActive
                            && (activeAiCache == null || ai.parent.thingIDNumber < activeAiCache.parent.thingIDNumber))
                        {
                            activeAiCache = ai;
                        }
                    }
                }
                return activeAiCache;
            }
        }

        /// <summary>What the active AI core is currently doing to the data center (neutral when there is none).</summary>
        public AiModifiers Ai
        {
            get
            {
                CompAiCore ai = ActiveAi;
                return ai == null ? AiModifiers.Neutral : ai.Modifiers;
            }
        }

        public void Register(Building_AccessDoor door)
        {
            if (door != null && !accessDoors.Contains(door))
            {
                accessDoors.Add(door);
            }
        }

        public void Unregister(Building_AccessDoor door)
        {
            accessDoors.Remove(door);
        }

        /// <summary>True if this map has a powered access door of the given kind that belongs to the player.</summary>
        public bool HasWorkingDoor(AccessDoorKind kind)
        {
            for (int i = 0; i < accessDoors.Count; i++)
            {
                Building_AccessDoor door = accessDoors[i];
                if (door != null && door.Spawned && door.Kind == kind && door.Faction == Faction.OfPlayer && door.DoorPowerOn)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Security certification: the data center has a working biometric door AND a working metal detector
        /// gate. Only matters once the certification research is done (it is what earns the price premium).
        /// </summary>
        public bool IsCertified
        {
            get { return HasWorkingDoor(AccessDoorKind.Biometric) && HasWorkingDoor(AccessDoorKind.MetalDetector); }
        }

        /// <summary>
        /// How many fully-productive racks the data center is running right now (a throttled or unmonitored
        /// rack counts for less than one). Refreshed about once a second.
        /// </summary>
        public float ResearchRackEquivalents
        {
            get
            {
                int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
                if (now - researchCacheTick >= 60 || now < researchCacheTick)
                {
                    researchCacheTick = now;
                    float sum = 0f;
                    for (int i = 0; i < racks.Count; i++)
                    {
                        CompServerRack rack = racks[i];
                        if (rack != null && rack.parent != null && rack.parent.Spawned && rack.IsRunning)
                        {
                            sum += rack.Efficiency;
                        }
                    }
                    researchEquivalents = sum;
                }
                return researchEquivalents;
            }
        }

        /// <summary>Forget the cached research figure (used by tests and debug tools after they change racks directly).</summary>
        public void InvalidateResearchCache()
        {
            researchCacheTick = -99999;
        }

        /// <summary>Research speed bonus the data center currently gives colonists on this map (0.12 = +12%).</summary>
        public float ResearchBonus
        {
            get
            {
                UpgradeTotals totals = RcdcUpgrades.Current;
                AiModifiers ai = Ai;
                float perRack = totals.ResearchBonusPerRack + ai.ResearchPerRack;
                float cap = totals.ResearchBonusCap + ai.ResearchCap;
                float fromRacks = 0f;
                if (totals.ResearchBonusPerRack > 0f && totals.ResearchBonusCap > 0f)
                {
                    fromRacks = System.Math.Min(cap, perRack * ResearchRackEquivalents) * ai.ResearchMultiplier;
                }
                return fromRacks + StoredCartridgeBonus(RcdcDefOf.RCDC_DataCartridge_Research);
            }
        }

        /// <summary>
        /// How many of a data type are sitting on this map right now, summed across every stack (haulable
        /// containers included). Used by the specialization stat parts and by loot-value checks.
        /// </summary>
        public int StoredCartridgeCount(ThingDef def)
        {
            if (def == null || map == null)
            {
                return 0;
            }
            return map.listerThings.ThingsOfDef(def).Sum(t => t.stackCount);
        }

        /// <summary>The stored-cartridge bonus a specialized data type gives, per its own <see cref="CartridgeSpecialEffect"/>.</summary>
        public float StoredCartridgeBonus(ThingDef cartridgeDef)
        {
            CartridgeSpecialEffect effect = cartridgeDef == null ? null : cartridgeDef.GetModExtension<CartridgeSpecialEffect>();
            if (effect == null)
            {
                return 0f;
            }
            float perStored = effect.researchBonusPerStored > 0f ? effect.researchBonusPerStored : effect.immunityBonusPerStored;
            float capStored = effect.researchBonusPerStored > 0f ? effect.researchBonusCapStored : effect.immunityBonusCapStored;
            if (perStored <= 0f)
            {
                return 0f;
            }
            return System.Math.Min(capStored, perStored * StoredCartridgeCount(cartridgeDef));
        }

        /// <summary>The colony-wide immunity gain speed bonus from stored Medical Data Cartridges on this map.</summary>
        public float StoredMedicalBonus
        {
            get { return StoredCartridgeBonus(RcdcDefOf.RCDC_DataCartridge_Medical); }
        }

        public IList<CompServerRack> Racks
        {
            get { return racks; }
        }

        public IList<CompNetworkCore> Cores
        {
            get { return cores; }
        }

        public IList<CompOperationsConsole> Consoles
        {
            get { return consoles; }
        }

        public static MapComponent_DataCenterNetwork For(Map map)
        {
            return map == null ? null : map.GetComponent<MapComponent_DataCenterNetwork>();
        }

        public void Register(CompServerRack rack)
        {
            if (rack != null && !racks.Contains(rack))
            {
                racks.Add(rack);
                dirty = true;
            }
        }

        public void Register(CompNetworkCore core)
        {
            if (core != null && !cores.Contains(core))
            {
                cores.Add(core);
                dirty = true;
            }
        }

        public void Register(CompOperationsConsole console)
        {
            if (console != null && !consoles.Contains(console))
            {
                consoles.Add(console);
                dirty = true;
            }
        }

        public void Unregister(CompServerRack rack)
        {
            if (racks.Remove(rack))
            {
                dirty = true;
            }
        }

        public void Unregister(CompNetworkCore core)
        {
            if (cores.Remove(core))
            {
                dirty = true;
            }
        }

        public void Unregister(CompOperationsConsole console)
        {
            if (consoles.Remove(console))
            {
                dirty = true;
            }
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        public CompNetworkCore CoreFor(CompServerRack rack, out NetworkFailure failure)
        {
            Refresh();
            failure = NetworkFailure.NoCoreInRange;
            if (rack == null)
            {
                return null;
            }
            CompNetworkCore core;
            if (rackToCore.TryGetValue(rack, out core))
            {
                failure = NetworkFailure.None;
                return core;
            }
            rackFailure.TryGetValue(rack, out failure);
            return null;
        }

        public CompNetworkCore CoreFor(CompOperationsConsole console)
        {
            Refresh();
            if (console == null)
            {
                return null;
            }
            CompNetworkCore core;
            consoleToCore.TryGetValue(console, out core);
            return core;
        }

        private void Refresh()
        {
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (dirty || now - lastComputeTick >= RefreshIntervalTicks || now < lastComputeTick)
            {
                Recompute(now);
            }
        }

        private void Recompute(int now)
        {
            dirty = false;
            lastComputeTick = now;

            racks.RemoveAll(r => r == null || r.parent == null || r.parent.Destroyed);
            cores.RemoveAll(c => c == null || c.parent == null || c.parent.Destroyed);
            consoles.RemoveAll(c => c == null || c.parent == null || c.parent.Destroyed);

            rackToCore.Clear();
            rackFailure.Clear();
            consoleToCore.Clear();
            for (int i = 0; i < cores.Count; i++)
            {
                cores[i].ClearLinks();
            }

            // Stable ordering: oldest buildings first, so an existing rack never loses its slot
            // to a newer one.
            racks.Sort((a, b) => a.parent.thingIDNumber.CompareTo(b.parent.thingIDNumber));
            cores.Sort((a, b) => a.parent.thingIDNumber.CompareTo(b.parent.thingIDNumber));

            for (int i = 0; i < racks.Count; i++)
            {
                CompServerRack rack = racks[i];
                if (!rack.parent.Spawned)
                {
                    continue;
                }
                CompNetworkCore best = null;
                float bestDist = float.MaxValue;
                bool anyInRange = false;
                for (int j = 0; j < cores.Count; j++)
                {
                    CompNetworkCore core = cores[j];
                    if (!core.parent.Spawned || !core.InRange(rack.parent))
                    {
                        continue;
                    }
                    anyInRange = true;
                    if (!core.HasFreeSlot)
                    {
                        continue;
                    }
                    float dist = (core.parent.Position - rack.parent.Position).LengthHorizontal;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = core;
                    }
                }
                if (best != null)
                {
                    best.LinkRack(rack);
                    rackToCore[rack] = best;
                }
                else
                {
                    rackFailure[rack] = anyInRange ? NetworkFailure.CoreFull : NetworkFailure.NoCoreInRange;
                }
            }

            for (int i = 0; i < consoles.Count; i++)
            {
                CompOperationsConsole console = consoles[i];
                if (!console.parent.Spawned)
                {
                    continue;
                }
                CompNetworkCore best = null;
                float bestDist = float.MaxValue;
                for (int j = 0; j < cores.Count; j++)
                {
                    CompNetworkCore core = cores[j];
                    if (!core.parent.Spawned || !core.InRange(console.parent))
                    {
                        continue;
                    }
                    float dist = (core.parent.Position - console.parent.Position).LengthHorizontal;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = core;
                    }
                }
                if (best != null)
                {
                    best.LinkConsole(console);
                    consoleToCore[console] = best;
                }
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            // Cheap heartbeat so links stay fresh even when no rack is ticking (e.g. all destroyed).
            if (Find.TickManager.TicksGame % RefreshIntervalTicks == 0)
            {
                Refresh();
            }
        }
    }
}
