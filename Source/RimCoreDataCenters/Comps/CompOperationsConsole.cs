using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCore.DataCenters
{
    public class CompProperties_OperationsConsole : CompProperties
    {
        /// <summary>Work units for one operations shift (1 unit per tick at speed 1.0, i.e. 2500 = one hour).</summary>
        public int shiftWorkAmount = 2400;

        /// <summary>How long a completed shift keeps the network monitored, in ticks (60000 = one day).</summary>
        public int coverageTicks = 60000;

        /// <summary>A new shift is requested automatically once remaining coverage drops to this many ticks.</summary>
        public int shiftDueBelowTicks = 15000;

        /// <summary>Wear removed from every connected rack by a completed shift (0.05 = 5%).</summary>
        public float wearReliefPerShift = 0.05f;

        /// <summary>Waste heat while powered, in heat units per second.</summary>
        public float heatPerSecond = 1.5f;

        public CompProperties_OperationsConsole()
        {
            compClass = typeof(CompOperationsConsole);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            if (shiftWorkAmount < 1)
            {
                yield return "CompProperties_OperationsConsole.shiftWorkAmount must be positive.";
            }
            if (coverageTicks < shiftDueBelowTicks)
            {
                yield return "CompProperties_OperationsConsole.coverageTicks must be >= shiftDueBelowTicks.";
            }
            if (parentDef.GetCompProperties<CompProperties_Power>() == null)
            {
                yield return "Operations Console requires CompProperties_Power.";
            }
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
            {
                yield return entry;
            }
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "RCDC_StatShiftLength".Translate(),
                "RCDC_HoursValue".Translate((shiftWorkAmount / 2500f).ToString("F1")), "RCDC_StatShiftLengthDesc".Translate(), 4300);
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "RCDC_StatCoverage".Translate(),
                "RCDC_DaysValue".Translate((coverageTicks / 60000f).ToString("F1")), "RCDC_StatCoverageDesc".Translate(), 4290);
        }
    }

    /// <summary>
    /// The Operations Console is where colonists perform the recurring "Data Center Operations"
    /// job: a monitoring and diagnostics shift for the whole network. A finished shift gives the
    /// linked Network Core a period of monitoring coverage (racks run at full output and wear
    /// normally) and performs light diagnostics that trim wear on every connected rack.
    /// </summary>
    public class CompOperationsConsole : ThingComp
    {
        private int coverageTicksLeft;
        private int shiftsCompleted;
        private CompPowerTrader power;

        public CompProperties_OperationsConsole Props
        {
            get { return (CompProperties_OperationsConsole)props; }
        }

        public bool HasCoverage
        {
            get { return coverageTicksLeft > 0; }
        }

        public int CoverageTicksLeft
        {
            get { return coverageTicksLeft; }
        }

        /// <summary>Developer/self-test hook: set the remaining monitoring coverage directly.</summary>
        internal void DevSetCoverage(int ticks)
        {
            coverageTicksLeft = Mathf.Clamp(ticks, 0, Props.coverageTicks);
        }

        public int ShiftsCompleted
        {
            get { return shiftsCompleted; }
        }

        public CompNetworkCore Core
        {
            get
            {
                MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
                return network == null ? null : network.CoreFor(this);
            }
        }

        public bool PowerOn
        {
            get { return power != null && power.PowerOn; }
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
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref coverageTicksLeft, "rcdcCoverageTicksLeft", 0);
            Scribe_Values.Look(ref shiftsCompleted, "rcdcShiftsCompleted", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                coverageTicksLeft = Mathf.Clamp(coverageTicksLeft, 0, Props.coverageTicks);
                if (shiftsCompleted < 0)
                {
                    shiftsCompleted = 0;
                }
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!parent.Spawned)
            {
                return;
            }
            if (coverageTicksLeft > 0)
            {
                coverageTicksLeft = Mathf.Max(0, coverageTicksLeft - 250);
            }
            if (PowerOn && Props.heatPerSecond > 0f)
            {
                GenTemperature.PushHeat(parent, Props.heatPerSecond * 4.1666665f);
            }
        }

        /// <summary>Whether a colonist could operate the console right now (ignores whether a shift is due).</summary>
        public bool CanOperate(out string reasonKey)
        {
            reasonKey = null;
            if (!parent.Spawned)
            {
                reasonKey = "RCDC_ReasonNotSpawned";
                return false;
            }
            if (!PowerOn)
            {
                reasonKey = "RCDC_ReasonNoPower";
                return false;
            }
            CompNetworkCore core = Core;
            if (core == null)
            {
                reasonKey = "RCDC_ReasonNoNetwork";
                return false;
            }
            if (!core.IsOnline)
            {
                reasonKey = "RCDC_ReasonCoreOffline";
                return false;
            }
            if (core.ConnectedCount <= 0)
            {
                reasonKey = "RCDC_ReasonNoRacks";
                return false;
            }
            return true;
        }

        /// <summary>Whether a shift should be requested. Forced (player-ordered) shifts are allowed earlier.</summary>
        public bool ShiftWanted(bool forced)
        {
            string reason;
            if (!CanOperate(out reason))
            {
                return false;
            }
            if (!forced)
            {
                // While an AI core is watching the data center nobody needs to be sent to run a shift.
                MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
                if (network != null && network.Ai.Monitoring)
                {
                    return false;
                }
            }
            int threshold = forced ? (int)(Props.coverageTicks * 0.9f) : Props.shiftDueBelowTicks;
            return coverageTicksLeft <= threshold;
        }

        /// <summary>Called by the job driver when a colonist finishes a shift.</summary>
        public void CompleteShift(Pawn worker)
        {
            coverageTicksLeft = Props.coverageTicks;
            shiftsCompleted++;
            int reviewed = 0;
            CompNetworkCore core = Core;
            if (core != null)
            {
                IList<CompServerRack> racks = core.ConnectedRacks;
                for (int i = 0; i < racks.Count; i++)
                {
                    if (racks[i] != null)
                    {
                        racks[i].ApplyWearRelief(Props.wearReliefPerShift);
                        reviewed++;
                    }
                }
            }
            if (worker != null && parent.Spawned)
            {
                Messages.Message("RCDC_MsgShiftComplete".Translate(worker.LabelShort, reviewed,
                    (Props.coverageTicks / 60000f).ToString("F1")), parent, MessageTypeDefOf.TaskCompletion, false);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            string reason;
            if (!CanOperate(out reason))
            {
                sb.Append("RCDC_ConsoleStatus".Translate()).Append(": ").Append(reason.Translate());
            }
            else
            {
                CompNetworkCore core = Core;
                sb.Append("RCDC_ConsoleStatus".Translate()).Append(": ").Append("RCDC_ConsoleReady".Translate());
                sb.Append('\n').Append("RCDC_ConsoleNetwork".Translate(core.ConnectedCount, core.Capacity));
            }
            if (HasCoverage)
            {
                sb.Append('\n').Append("RCDC_ConsoleCoverage".Translate((coverageTicksLeft / 60000f).ToString("F2")));
            }
            else
            {
                sb.Append('\n').Append("RCDC_ConsoleNoCoverage".Translate());
            }

            UpgradeTotals upgrades = RcdcUpgrades.Current;
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(parent.Map);
            if (network != null)
            {
                if (upgrades.ResearchBonusCap > 0f)
                {
                    sb.Append('\n').Append("RCDC_ConsoleResearchGrid".Translate((network.ResearchBonus * 100f).ToString("F0"),
                        (upgrades.ResearchBonusCap * 100f).ToString("F0")));
                }
                if (upgrades.CertifiedPriceBonus > 0f)
                {
                    sb.Append('\n');
                    if (network.IsCertified)
                    {
                        sb.Append("RCDC_ConsoleCertified".Translate((upgrades.CertifiedPriceBonus * 100f).ToString("F0")));
                    }
                    else
                    {
                        sb.Append("RCDC_ConsoleNotCertified".Translate());
                    }
                }
            }
            MapComponent_MarketDynamics market = MapComponent_MarketDynamics.For(parent.Map);
            if (market != null)
            {
                sb.Append('\n').Append("RCDC_ConsoleMarket".Translate(market.Describe()));
            }
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    icon = TexButton.Add,
                    defaultLabel = "DEV: expire coverage",
                    action = delegate { coverageTicksLeft = 0; }
                };
                yield return new Command_Action
                {
                    icon = TexButton.Add,
                    defaultLabel = "DEV: full coverage",
                    action = delegate { coverageTicksLeft = Props.coverageTicks; }
                };
            }
        }
    }
}
