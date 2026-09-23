using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// The one place that turns "which upgrade projects are finished" into numbers. Everything else in the
    /// mod asks <see cref="Current"/>. The result is cached and rebuilt automatically whenever the set of
    /// finished projects changes, so there is no event to hook and no saved state: after loading a game the
    /// research manager already knows what is finished and the numbers follow.
    /// </summary>
    public static class RcdcUpgrades
    {
        private static List<ResearchProjectDef> holders;
        private static readonly UpgradeTotals Neutral = new UpgradeTotals();
        private static UpgradeTotals cached = Neutral;
        private static ulong cachedMask = ulong.MaxValue;

        // Vanilla values of the UPS, captured before any upgrade touches them (the battery properties are
        // shared by every UPS unit, so upgrades are applied to them once, not per building).
        private static bool upsBaseCaptured;
        private static float upsBaseCapacity;
        private static float upsBaseEfficiency;

        /// <summary>Every research project that carries <see cref="UpgradeEffects"/>.</summary>
        public static IList<ResearchProjectDef> Projects
        {
            get
            {
                if (holders == null)
                {
                    holders = new List<ResearchProjectDef>();
                    List<ResearchProjectDef> all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (all[i].HasModExtension<UpgradeEffects>())
                        {
                            holders.Add(all[i]);
                        }
                    }
                }
                return holders;
            }
        }

        /// <summary>The summed-up effect of every finished upgrade (neutral values outside a running game).</summary>
        public static UpgradeTotals Current
        {
            get
            {
                if (GameIsRunning)
                {
                    Refresh(false);
                }
                return cached;
            }
        }

        private static bool GameIsRunning
        {
            get { return Verse.Current.ProgramState == ProgramState.Playing && Find.ResearchManager != null; }
        }

        /// <summary>Recomputes the totals if the set of finished projects changed (or when forced).</summary>
        public static void Refresh(bool force)
        {
            if (!GameIsRunning)
            {
                cached = Neutral;
                cachedMask = ulong.MaxValue;
                return;
            }
            IList<ResearchProjectDef> list = Projects;
            ulong mask = 0UL;
            for (int i = 0; i < list.Count && i < 63; i++)
            {
                if (list[i].IsFinished)
                {
                    mask |= 1UL << i;
                }
            }
            if (!force && mask == cachedMask)
            {
                return;
            }
            cachedMask = mask;
            cached = Build(list);
            ApplyToBatteries();
        }

        private static UpgradeTotals Build(IList<ResearchProjectDef> list)
        {
            UpgradeTotals t = new UpgradeTotals();
            t.Total = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                ResearchProjectDef project = list[i];
                if (!project.IsFinished)
                {
                    continue;
                }
                UpgradeEffects e = project.GetModExtension<UpgradeEffects>();
                t.Finished++;
                t.OutputMultiplier *= e.outputMultiplier;
                t.HeatMultiplier *= e.heatMultiplier;
                t.TemperatureTolerance += e.temperatureTolerance;
                t.WearRateMultiplier *= e.wearRateMultiplier;
                t.UnmonitoredOutputBonus += e.unmonitoredOutputBonus;
                t.UnmonitoredWearReduction += e.unmonitoredWearReduction;
                t.ExtraRacksPerCore += e.extraRacksPerCore;
                t.ExtraCoreRange += e.extraCoreRange;
                t.UpsCapacityMultiplier *= e.upsCapacityMultiplier;
                t.UpsEfficiencyBonus += e.upsEfficiencyBonus;
                t.ResearchBonusPerRack += e.researchBonusPerRack;
                t.ResearchBonusCap += e.researchBonusCap;
                t.CertifiedPriceBonus += e.certifiedPriceBonus;
                t.AiBenefitBonus += e.aiBenefitBonus;
                t.AiGlitchReduction += e.aiGlitchReduction;
            }
            return t;
        }

        /// <summary>
        /// Writes the UPS upgrade into the shared battery properties. Always derived from the vanilla values,
        /// so loading a different save (with different research) never stacks or leaks an upgrade.
        /// </summary>
        private static void ApplyToBatteries()
        {
            ThingDef ups = RcdcDefOf.RCDC_UpsUnit;
            CompProperties_Battery battery = ups == null ? null : ups.GetCompProperties<CompProperties_Battery>();
            if (battery == null)
            {
                return;
            }
            if (!upsBaseCaptured)
            {
                upsBaseCaptured = true;
                upsBaseCapacity = battery.storedEnergyMax;
                upsBaseEfficiency = battery.efficiency;
            }
            battery.storedEnergyMax = upsBaseCapacity * cached.UpsCapacityMultiplier;
            battery.efficiency = System.Math.Min(1f, upsBaseEfficiency + cached.UpsEfficiencyBonus);
        }

        /// <summary>Vanilla (un-upgraded) UPS capacity in watt-days, for tests and tooltips.</summary>
        public static float BaseUpsCapacity
        {
            get { return upsBaseCaptured ? upsBaseCapacity : 0f; }
        }
    }

    /// <summary>Keeps <see cref="RcdcUpgrades"/> current: rebuilds it on load or new game, and about once a second.</summary>
    public class GameComponent_RcdcUpgrades : GameComponent
    {
        public GameComponent_RcdcUpgrades(Game game)
        {
        }

        public override void FinalizeInit()
        {
            RcdcUpgrades.Refresh(true);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                RcdcUpgrades.Refresh(false);
            }
        }
    }
}
