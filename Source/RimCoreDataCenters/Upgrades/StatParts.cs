using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Adds the data center's "compute grid" bonus to a colonist's Research Speed. Attached to the vanilla
    /// ResearchSpeed stat by Patches/RCDC_Stats.xml (no code patching involved). It only reads a value the
    /// map's network component refreshes about once a second, so it is cheap.
    /// </summary>
    public class StatPart_DataCenterResearch : StatPart
    {
        private static float BonusFor(StatRequest req)
        {
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null || pawn.Map == null || pawn.Faction != Faction.OfPlayer)
            {
                return 0f;
            }
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(pawn.Map);
            return network == null ? 0f : network.ResearchBonus;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            float bonus = BonusFor(req);
            if (bonus > 0f)
            {
                val *= 1f + bonus;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            float bonus = BonusFor(req);
            if (bonus <= 0f)
            {
                return null;
            }
            return "RCDC_StatResearchGrid".Translate() + ": x" + (1f + bonus).ToString("F2");
        }
    }

    /// <summary>
    /// The colony-wide immunity gain speed bonus from stored Medical Data Cartridges. Attached to the vanilla
    /// ImmunityGainSpeed stat by Patches/RCDC_Stats.xml. Mirrors <see cref="StatPart_DataCenterResearch"/>.
    /// </summary>
    public class StatPart_DataCenterMedical : StatPart
    {
        private static float BonusFor(StatRequest req)
        {
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null || pawn.Map == null)
            {
                return 0f;
            }
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(pawn.Map);
            return network == null ? 0f : network.StoredMedicalBonus;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            float bonus = BonusFor(req);
            if (bonus > 0f)
            {
                val *= 1f + bonus;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            float bonus = BonusFor(req);
            if (bonus <= 0f)
            {
                return null;
            }
            return "RCDC_StatMedicalArchive".Translate() + ": x" + (1f + bonus).ToString("F2");
        }
    }

    /// <summary>
    /// The security-certified price premium for data cartridges: while the colony's data center has a working
    /// biometric door and metal detector gate (and the certification research is done), buyers pay more for
    /// every data type.
    /// Attached to the vanilla MarketValue stat by Patches/RCDC_Stats.xml.
    /// </summary>
    public class StatPart_CertifiedData : StatPart
    {
        private static bool AppliesTo(StatRequest req)
        {
            return req.Def != null && RcdcDefOf.IsDataCartridge(req.Def as ThingDef);
        }

        private static float BonusFor(StatRequest req)
        {
            if (!AppliesTo(req) || Current.ProgramState != ProgramState.Playing)
            {
                return 0f;
            }
            float bonus = RcdcUpgrades.Current.CertifiedPriceBonus;
            if (bonus <= 0f)
            {
                return 0f;
            }
            Map map = req.HasThing ? req.Thing.MapHeld : null;
            if (map == null)
            {
                map = Find.AnyPlayerHomeMap;
            }
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            return network != null && network.IsCertified ? bonus : 0f;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            float bonus = BonusFor(req);
            if (bonus > 0f)
            {
                val *= 1f + bonus;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            float bonus = BonusFor(req);
            if (bonus <= 0f)
            {
                return null;
            }
            return "RCDC_StatCertified".Translate() + ": x" + (1f + bonus).ToString("F2");
        }
    }

    /// <summary>
    /// Cartridge prices drift slowly over time, and occasionally a market event (a rival buyer, a glut or a
    /// shortage) pushes one type further up or down for a while.
    /// Attached to the vanilla MarketValue stat by Patches/RCDC_Stats.xml.
    /// </summary>
    public class StatPart_MarketDynamics : StatPart
    {
        private static bool AppliesTo(StatRequest req)
        {
            return req.Def != null && RcdcDefOf.IsDataCartridge(req.Def as ThingDef);
        }

        private static float MultiplierFor(StatRequest req)
        {
            if (!AppliesTo(req) || Current.ProgramState != ProgramState.Playing)
            {
                return 1f;
            }
            Map map = req.HasThing ? req.Thing.MapHeld : null;
            if (map == null)
            {
                map = Find.AnyPlayerHomeMap;
            }
            MapComponent_MarketDynamics market = MapComponent_MarketDynamics.For(map);
            return market == null ? 1f : market.PriceMultiplier(req.Def as ThingDef);
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            float multiplier = MultiplierFor(req);
            if (!Near(multiplier, 1f))
            {
                val *= multiplier;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            float multiplier = MultiplierFor(req);
            if (Near(multiplier, 1f))
            {
                return null;
            }
            return "RCDC_StatMarketDynamics".Translate() + ": x" + multiplier.ToString("F2");
        }

        private static bool Near(float a, float b)
        {
            return System.Math.Abs(a - b) < 0.005f;
        }
    }
}
