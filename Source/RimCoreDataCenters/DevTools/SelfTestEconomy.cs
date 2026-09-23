using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimCore.DataCenters
{
    /// <summary>Self-test sections for cartridge specialization and the espionage incident.</summary>
    internal static partial class SelfTest
    {
        // ---- cartridge specialization ------------------------------------------------------------------

        private static Thing SpawnCartridges(ThingDef def, int count, IntVec3 cell)
        {
            Thing t = ThingMaker.MakeThing(def);
            t.stackCount = count;
            Thing placed;
            GenPlace.TryPlaceThing(t, cell, Map, ThingPlaceMode.Near, out placed);
            return placed;
        }

        private static IEnumerable<Waiter> SectionSpecialization()
        {
            if (layout == null)
            {
                yield break;
            }
            ThingDef standard = RcdcDefOf.RCDC_DataCartridge;
            ThingDef research = RcdcDefOf.RCDC_DataCartridge_Research;
            ThingDef financial = RcdcDefOf.RCDC_DataCartridge_Financial;
            ThingDef medical = RcdcDefOf.RCDC_DataCartridge_Medical;

            Check("data classification research is locked before its prerequisite is done",
                !research.IsResearchFinished && !financial.IsResearchFinished && !medical.IsResearchFinished);

            CompServerRack rack = Rack(0);
            Check("a rack defaults to the standard data cartridge", rack.OutputThing == standard);
            Check("a rack advertises a specialization choice", rack.HasSpecializationChoice);
            string textBefore = rack.parent.GetInspectString();
            Check("rack inspect text names what it is producing", textBefore.Contains("Producing:") && textBefore.Contains(standard.LabelCap) && !textBefore.Contains("RCDC_"), textBefore.Replace('\n', '|'));

            // Switching keeps the rack's progress toward its next cartridge.
            rack.DevSetProgress(0.42f);
            rack.SetOutput(financial);
            Check("a rack can be switched even before research (the gate is only in the menu UI)", rack.OutputThing == financial);
            Check("switching specialization keeps production progress", Near(rack.Progress, 0.42f, 0.001f), rack.Progress.ToString("F3"));

            FinishResearch("RCDC_DataClassification");
            Check("data classification unlocks all three specialized cartridges", research.IsResearchFinished && financial.IsResearchFinished && medical.IsResearchFinished);
            DesignationCategoryDef category = DefDatabase<DesignationCategoryDef>.GetNamed("RCDC_DataCenter");
            Check("the architect tab is unaffected by cartridge research (cartridges are not buildings)", category.AllResolvedDesignators.OfType<Designator_Build>().Any(d => d.PlacingDef == RcdcDefOf.RCDC_ServerRack));

            // A rack set to Financial really outputs a Financial cartridge.
            IntVec3 outCell = rack.parent.InteractionCell;
            rack.DevSetProgress(0.999f);
            rack.DevStep();
            rack.DevStep();
            Thing produced = outCell.GetThingList(Map).FirstOrDefault(t => t.def == financial);
            Check("a rack set to Financial actually outputs a financial data cartridge", produced != null, produced == null ? "none" : produced.def.defName);
            if (produced != null)
            {
                produced.Destroy(DestroyMode.Vanish);
            }
            // Security certification (from the section run just before this one) may already be lifting every
            // price, so compare ratios rather than assuming an un-certified baseline.
            float financialNow = financial.GetStatValueAbstract(StatDefOf.MarketValue);
            float standardNow = standard.GetStatValueAbstract(StatDefOf.MarketValue);
            float medicalNow = medical.GetStatValueAbstract(StatDefOf.MarketValue);
            Check("financial data is worth 37.5% more than standard data, medical data is not",
                Near(financialNow / standardNow, 110f / 80f, 0.01f) && Near(medicalNow / standardNow, 1f, 0.01f),
                financialNow + " / " + standardNow + " / " + medicalNow);

            // Stockpiled Research Data Cartridges add to the research uplink, capped.
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);
            IntVec3 stockCell = layout.AisleCell + new IntVec3(-2, 0, 2);
            network.InvalidateResearchCache();
            float bonusBefore = network.ResearchBonus;
            SpawnCartridges(research, 10, stockCell);
            float bonusWith10 = network.ResearchBonus;
            Check("10 stored research cartridges add 0.04 to the research uplink (0.004 each)",
                Near(bonusWith10 - bonusBefore, 0.04f, 0.002f), (bonusWith10 - bonusBefore).ToString("F4"));
            SpawnCartridges(research, 30, stockCell + new IntVec3(1, 0, 0));
            float bonusCapped = network.ResearchBonus;
            Check("the stored-research bonus caps at 0.08 however many are stored",
                Near(bonusCapped - bonusBefore, 0.08f, 0.002f), (bonusCapped - bonusBefore).ToString("F4"));

            // Stockpiled Medical Data Cartridges raise immunity gain speed, capped, for every colonist on the map.
            Pawn patient = Colonists.First();
            StatDef immunity = StatDefOf.ImmunityGainSpeed;
            StatPart medicalPart = immunity.parts == null ? null : immunity.parts.FirstOrDefault(p => p is StatPart_DataCenterMedical);
            Check("the medical archive stat part is attached to Immunity Gain Speed", medicalPart != null);
            float factorBefore = medicalPart == null ? 1f : uplinkPartFactor(immunity, patient, medicalPart);
            Check("no medical cartridges stored yet: no bonus", Near(factorBefore, 1f, 0.002f), factorBefore.ToString("F3"));
            SpawnCartridges(medical, 10, stockCell + new IntVec3(0, 0, 1));
            float factorWith10 = medicalPart == null ? 1f : uplinkPartFactor(immunity, patient, medicalPart);
            Check("10 stored medical cartridges give +10% immunity gain speed (1% each)", Near(factorWith10, 1.10f, 0.01f), factorWith10.ToString("F3"));
            SpawnCartridges(medical, 20, stockCell + new IntVec3(1, 0, 1));
            float factorCapped = medicalPart == null ? 1f : uplinkPartFactor(immunity, patient, medicalPart);
            Check("the stored-medical bonus caps at +15% however many are stored", Near(factorCapped, 1.15f, 0.01f), factorCapped.ToString("F3"));

            // Certification (from the security section, already active) lifts every cartridge type's price together.
            float financialCertified = financial.GetStatValueAbstract(StatDefOf.MarketValue);
            float standardCertified = standard.GetStatValueAbstract(StatDefOf.MarketValue);
            float medicalCertified = medical.GetStatValueAbstract(StatDefOf.MarketValue);
            Check("certification lifts every specialization's price together",
                Near(financialCertified, 132f, 1f) && Near(standardCertified, 96f, 1f) && Near(medicalCertified, 96f, 1f),
                financialCertified + " / " + standardCertified + " / " + medicalCertified);

            rack.SetOutput(standard);
            // The stockpiled research/medical cartridges above were only for this section's checks: clear them so
            // they do not silently add to the research uplink or immunity bonus for every later section.
            foreach (ThingDef spawnedDef in new[] { research, medical })
            {
                foreach (Thing t in Map.listerThings.ThingsOfDef(spawnedDef).ToList())
                {
                    t.Destroy(DestroyMode.Vanish);
                }
            }
            network.InvalidateResearchCache();
            yield return null;
        }

        // ---- espionage ------------------------------------------------------------------------------------

        /// <summary>Pushes a faction hostile if it is not already, for a deterministic test. Returns null if none exist.</summary>
        private static Faction FindOrMakeHostileFaction()
        {
            foreach (Faction f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f.IsPlayer || f.def.hidden || !f.def.humanlikeFaction || f.defeated)
                {
                    continue;
                }
                if (!f.HostileTo(Faction.OfPlayer))
                {
                    f.TryAffectGoodwillWith(Faction.OfPlayer, -200, false, false, null, null);
                }
                if (f.HostileTo(Faction.OfPlayer))
                {
                    return f;
                }
            }
            return null;
        }

        private static IEnumerable<Waiter> SectionEspionage()
        {
            if (layout == null)
            {
                yield break;
            }
            IncidentDef incident = RcdcDefOf.RCDC_Espionage;
            Check("the espionage incident targets player-home maps as a small threat",
                incident.targetTags != null && incident.targetTags.Contains(IncidentTargetTagDefOf.Map_PlayerHome) && incident.category == IncidentCategoryDefOf.ThreatSmall);
            Check("the espionage worker is our own class", incident.workerClass == typeof(IncidentWorker_Espionage));

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatSmall, Map);
            Check("the espionage incident can fire now that a rack exists", incident.Worker.CanFireNow(parms));

            Faction hostile = FindOrMakeHostileFaction();
            if (hostile == null)
            {
                Info("no faction on this map can be made hostile; skipping the espionage execution check");
                yield break;
            }
            int lettersBefore = Find.LetterStack.LettersListForReading.Count;
            int lordsBefore = Map.lordManager.lords.Count;
            bool fired = incident.Worker.TryExecute(parms);
            Check("the espionage incident executes with a hostile faction available", fired);
            if (!fired)
            {
                yield break;
            }
            yield return new WaitTicks(30);

            // The incident picks its own faction (any hostile one able to field a combat group), which need not be
            // the specific one this test forced hostile, so look for the lord job rather than that exact faction.
            List<Pawn> raiders = Map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && p.Faction != Faction.OfPlayer
                && p.GetLord() != null && p.GetLord().LordJob is LordJob_AssaultColony).ToList();
            Check("the incident spawns a small raiding party with a real assault lord job", raiders.Count >= 1 && raiders.Count <= 8,
                raiders.Count + " raiders" + (raiders.Count == 0 ? "; " + string.Join(", ", Map.mapPawns.AllPawnsSpawned.Select(p => p.LabelShort + "@" + (p.Faction == null ? "none" : p.Faction.Name)).ToArray()) : ""));
            Check("the espionage letter was sent", Find.LetterStack.LettersListForReading.Count > lettersBefore
                && Find.LetterStack.LettersListForReading.Any(l => l.def == RcdcDefOf.RCDC_EspionageLetter));
            Check("a lord was created for the raiders", Map.lordManager.lords.Count > lordsBefore);

            // A locked security door is just another obstacle to a hostile, non-colony pawn: it cannot open one.
            Building_AccessDoor door = MapComponent_DataCenterNetwork.For(Map).AccessDoors.FirstOrDefault();
            if (door != null && raiders.Count > 0)
            {
                Check("a locked security door refuses a raider exactly like any other hostile", !door.PawnCanOpen(raiders[0]));
            }

            // Clean up: a normal death (not despawn), so nothing about it looks like a dangling save reference.
            foreach (Pawn raider in raiders)
            {
                if (!raider.Dead)
                {
                    raider.Kill(null);
                }
                if (raider.Corpse != null && !raider.Corpse.Destroyed)
                {
                    raider.Corpse.Destroy(DestroyMode.Vanish);
                }
            }
            yield return new WaitTicks(30);
        }
    }
}
