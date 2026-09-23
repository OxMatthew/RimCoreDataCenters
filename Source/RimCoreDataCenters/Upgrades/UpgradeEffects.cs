using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Put this mod extension on a <see cref="ResearchProjectDef"/> to make finishing that project change
    /// how data centers behave. Every project that carries it is summed up by <see cref="RcdcUpgrades"/>:
    /// multipliers multiply, additive values add. All numbers are XML, so the whole upgrade tree can be
    /// rebalanced without touching code (see Defs/RCDC_Research.xml).
    /// </summary>
    public class UpgradeEffects : DefModExtension
    {
        // ---- server racks -----------------------------------------------------------------------
        /// <summary>Multiplies production speed (1.15 = 15% more cartridges).</summary>
        public float outputMultiplier = 1f;

        /// <summary>Multiplies the waste heat a running rack gives off (0.7 = 30% less heat).</summary>
        public float heatMultiplier = 1f;

        /// <summary>Degrees Celsius added to the throttle, shutdown and restart temperatures.</summary>
        public float temperatureTolerance;

        /// <summary>Multiplies how fast racks wear (0.65 = 35% slower).</summary>
        public float wearRateMultiplier = 1f;

        /// <summary>Added to the output factor of a rack with no operations coverage.</summary>
        public float unmonitoredOutputBonus;

        /// <summary>Subtracted from the extra wear factor of a rack with no operations coverage.</summary>
        public float unmonitoredWearReduction;

        // ---- network core -----------------------------------------------------------------------
        public int extraRacksPerCore;

        public float extraCoreRange;

        // ---- UPS --------------------------------------------------------------------------------
        public float upsCapacityMultiplier = 1f;

        public float upsEfficiencyBonus;

        // ---- research uplink and certification --------------------------------------------------
        /// <summary>Research speed bonus per running rack (0.04 = +4% each).</summary>
        public float researchBonusPerRack;

        /// <summary>Maximum total research speed bonus from the data center.</summary>
        public float researchBonusCap;

        /// <summary>Extra price paid for data cartridges while the data center is security certified.</summary>
        public float certifiedPriceBonus;

        // ---- AI core ----------------------------------------------------------------------------
        /// <summary>Added to how strongly the AI core's directive and diagnostics work (1.0 is full strength at maximum rapport).</summary>
        public float aiBenefitBonus;

        /// <summary>Fraction by which AI glitches become rarer (0.3 = 30% fewer).</summary>
        public float aiGlitchReduction;
    }

    /// <summary>The summed-up effect of every finished upgrade project.</summary>
    public sealed class UpgradeTotals
    {
        public float OutputMultiplier = 1f;
        public float HeatMultiplier = 1f;
        public float TemperatureTolerance;
        public float WearRateMultiplier = 1f;
        public float UnmonitoredOutputBonus;
        public float UnmonitoredWearReduction;
        public int ExtraRacksPerCore;
        public float ExtraCoreRange;
        public float UpsCapacityMultiplier = 1f;
        public float UpsEfficiencyBonus;
        public float ResearchBonusPerRack;
        public float ResearchBonusCap;
        public float CertifiedPriceBonus;
        public float AiBenefitBonus;
        public float AiGlitchReduction;

        /// <summary>How many upgrade projects are finished.</summary>
        public int Finished;

        /// <summary>How many upgrade projects exist.</summary>
        public int Total;

        /// <summary>A short, translated list of the rack-related upgrades that are active, or null if none.</summary>
        public string RackSummary()
        {
            List<string> parts = new List<string>();
            if (OutputMultiplier > 1.0001f || OutputMultiplier < 0.9999f)
            {
                parts.Add("RCDC_UpFragOutput".Translate(OutputMultiplier.ToString("F2")));
            }
            if (HeatMultiplier > 1.0001f || HeatMultiplier < 0.9999f)
            {
                parts.Add("RCDC_UpFragHeat".Translate(HeatMultiplier.ToString("F2")));
            }
            if (TemperatureTolerance > 0.001f)
            {
                parts.Add("RCDC_UpFragTolerance".Translate(TemperatureTolerance.ToString("F0")));
            }
            if (WearRateMultiplier > 1.0001f || WearRateMultiplier < 0.9999f)
            {
                parts.Add("RCDC_UpFragWear".Translate(WearRateMultiplier.ToString("F2")));
            }
            if (UnmonitoredOutputBonus > 0.001f || UnmonitoredWearReduction > 0.001f)
            {
                parts.Add("RCDC_UpFragAutonomy".Translate());
            }
            if (parts.Count == 0)
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }
    }
}
