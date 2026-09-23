using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimCore.DataCenters
{
    /// <summary>Self-test sections for the upgrade research tree, the research uplink and the security doors.</summary>
    internal static partial class SelfTest
    {
        // What a cartridge sold for before the certification research, so the premium can be measured.
        private static float sellPriceBeforeCertification;

        private static bool Near(float a, float b, float tolerance)
        {
            return Mathf.Abs(a - b) <= tolerance;
        }

        private static void FinishResearch(string defName)
        {
            ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamed(defName);
            RcdcDebugActions.FinishWithPrerequisites(project);
            RcdcUpgrades.Refresh(true);
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);
            if (network != null)
            {
                network.MarkDirty();
                network.InvalidateResearchCache();
            }
        }

        private static ResearchProjectDef Project(string defName)
        {
            return DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
        }

        // ---- A. the research tree and the new defs (static checks) --------------------------------

        private static IEnumerable<Waiter> SectionUpgradeDefs()
        {
            // name -> the prerequisites the tree is supposed to have
            Dictionary<string, string[]> tree = new Dictionary<string, string[]>
            {
                { "RCDC_OptimizedFirmware", new[] { "RCDC_DataCenterInfrastructure" } },
                { "RCDC_ImmersionCooling", new[] { "RCDC_DataCenterInfrastructure" } },
                { "RCDC_PredictiveMaintenance", new[] { "RCDC_DataCenterInfrastructure" } },
                { "RCDC_NetworkFabric", new[] { "RCDC_DataCenterInfrastructure" } },
                { "RCDC_RedundantPower", new[] { "RCDC_DataCenterInfrastructure" } },
                { "RCDC_AccessControl", new[] { "RCDC_DataCenterInfrastructure" } },
                { "RCDC_ThreatScreening", new[] { "RCDC_AccessControl" } },
                { "RCDC_SecureCertification", new[] { "RCDC_ThreatScreening" } },
                { "RCDC_DistributedComputing", new[] { "RCDC_OptimizedFirmware", "RCDC_NetworkFabric" } },
                { "RCDC_AutonomousOperations", new[] { "RCDC_PredictiveMaintenance", "RCDC_DistributedComputing" } }
            };
            foreach (KeyValuePair<string, string[]> entry in tree)
            {
                ResearchProjectDef project = Project(entry.Key);
                Check("research project defined and not yet finished: " + entry.Key, project != null && !project.IsFinished);
                if (project == null)
                {
                    continue;
                }
                List<string> actual = (project.prerequisites ?? new List<ResearchProjectDef>()).Select(p => p.defName).OrderBy(n => n).ToList();
                List<string> wanted = entry.Value.OrderBy(n => n).ToList();
                Check("prerequisites are right: " + entry.Key, actual.SequenceEqual(wanted), string.Join(",", actual.ToArray()));
                Check("needs a hi-tech research bench: " + entry.Key, project.requiredResearchBuilding != null && project.requiredResearchBuilding.defName == "HiTechResearchBench");
                Check("has a research description: " + entry.Key, !project.description.NullOrEmpty() && !project.description.Contains("RCDC_"));
            }
            Check("Optimized Firmware is researchable right after the root", Project("RCDC_OptimizedFirmware").PrerequisitesCompleted);
            Check("Distributed Computing stays locked until Firmware and Fabric are done", !Project("RCDC_DistributedComputing").PrerequisitesCompleted);
            Check("Autonomous Operations stays locked until Maintenance and Distributed Computing are done", !Project("RCDC_AutonomousOperations").PrerequisitesCompleted);
            Check("Threat Screening stays locked until Access Control is done", !Project("RCDC_ThreatScreening").PrerequisitesCompleted);
            Check("Secure Certification stays locked until Threat Screening is done", !Project("RCDC_SecureCertification").PrerequisitesCompleted);
            Check("the ten upgrade projects carry effects (root and Adaptive Learning included)", RcdcUpgrades.Projects.Count == 10, RcdcUpgrades.Projects.Count + " projects");

            // Nodes must not sit on top of each other in the research tab.
            List<ResearchProjectDef> ours = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(p => p.defName.StartsWith("RCDC_")).ToList();
            List<string> overlaps = new List<string>();
            foreach (ResearchProjectDef a in ours)
            {
                foreach (ResearchProjectDef b in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
                {
                    if (a == b || (b.defName.StartsWith("RCDC_") && string.CompareOrdinal(a.defName, b.defName) > 0))
                    {
                        continue;
                    }
                    if (Mathf.Abs(a.researchViewX - b.researchViewX) < 0.9f && Mathf.Abs(a.researchViewY - b.researchViewY) < 0.5f)
                    {
                        overlaps.Add(a.defName + "/" + b.defName);
                    }
                }
            }
            Check("no research node overlaps another in the tab", overlaps.Count == 0, string.Join(", ", overlaps.ToArray()));

            // The root gives the base research uplink and nothing else yet.
            UpgradeTotals t = RcdcUpgrades.Current;
            Check("only the root research is finished so far", t.Finished == 1, t.Finished + " of " + t.Total);
            Check("root research: uplink +4% per rack, max +20%", Near(t.ResearchBonusPerRack, 0.04f, 0.0001f) && Near(t.ResearchBonusCap, 0.20f, 0.0001f));
            Check("no other upgrade is active yet", Near(t.OutputMultiplier, 1f, 0.0001f) && Near(t.HeatMultiplier, 1f, 0.0001f) && Near(t.WearRateMultiplier, 1f, 0.0001f)
                && t.ExtraRacksPerCore == 0 && Near(t.UpsCapacityMultiplier, 1f, 0.0001f) && Near(t.CertifiedPriceBonus, 0f, 0.0001f));

            // The security doors.
            foreach (ThingDef door in new[] { RcdcDefOf.RCDC_BiometricDoor, RcdcDefOf.RCDC_MetalDetectorGate })
            {
                Check("def loaded: " + (door == null ? "?" : door.defName), door != null);
                if (door == null)
                {
                    continue;
                }
                List<string> errors = door.ConfigErrors().ToList();
                Check("no config errors: " + door.defName, errors.Count == 0, string.Join("; ", errors.ToArray()));
                Check("texture loaded: " + door.defName, door.graphic != null && door.graphic != BaseContent.BadGraphic);
                Check("menu icon loaded: " + door.defName, door.uiIcon != null && door.uiIcon != BaseContent.BadTex);
                Check("costs, work, power and description set: " + door.defName, door.CostList != null && door.CostList.Count > 0 && door.costStuffCount > 0
                    && door.GetStatValueAbstract(StatDefOf.WorkToBuild) > 0f && door.GetCompProperties<CompProperties_Power>() != null && !door.description.NullOrEmpty());
                Check("is a security door in the data center tab: " + door.defName, door.thingClass == typeof(Building_AccessDoor)
                    && door.HasModExtension<AccessDoorExtension>() && door.designationCategory != null && door.designationCategory.defName == "RCDC_DataCenter");
                Check("architect entry is locked behind research: " + door.defName, door.researchPrerequisites != null && door.researchPrerequisites.Count == 1 && !door.IsResearchFinished);
            }
            Check("biometric door is the biometric kind, the gate is the metal detector kind",
                RcdcDefOf.RCDC_BiometricDoor.GetModExtension<AccessDoorExtension>().kind == AccessDoorKind.Biometric
                && RcdcDefOf.RCDC_MetalDetectorGate.GetModExtension<AccessDoorExtension>().kind == AccessDoorKind.MetalDetector);
            Check("both stat parts are attached to the vanilla stats",
                StatDefOf.ResearchSpeed.parts != null && StatDefOf.ResearchSpeed.parts.Any(p => p is StatPart_DataCenterResearch)
                && StatDefOf.MarketValue.parts != null && StatDefOf.MarketValue.parts.Any(p => p is StatPart_CertifiedData));
            yield return null;
        }

        // ---- B. the upgrades change real behaviour -------------------------------------------------

        private static IEnumerable<Waiter> SectionUpgrades()
        {
            if (layout == null)
            {
                yield break;
            }
            SetWorkPriority(0);
            CompServerRack rack = Rack(0);
            CompNetworkCore core = Core;
            CompOperationsConsole console = Console;
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);
            CompPowerBattery ups = layout.Ups[0].TryGetComp<CompPowerBattery>();
            Room room = DataCenterRoom;
            Flick(layout.Cooler, true);
            // The UPS section just ended a blackout. The power net switches consumers back on one at a time, so
            // wait until the core, the console and every rack are actually powered before measuring anything.
            yield return new WaitUntil(() => core.IsOnline && console.PowerOn && layout.Racks.All(r => r.TryGetComp<CompPowerTrader>().PowerOn), 4000);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            room.Temperature = 22f;
            console.DevSetCoverage(60000);
            rack.DevSetWear(0.1f);
            rack.DevRefresh();
            network.InvalidateResearchCache();

            // Baseline: exactly the XML numbers.
            Check("baseline: rack temperatures are the XML numbers (32 / 50 / 40 C)",
                Near(rack.WarmTemperature, 32f, 0.01f) && Near(rack.ShutdownTemperature, 50f, 0.01f) && Near(rack.RestartTemperature, 40f, 0.01f));
            Check("baseline: core serves 6 racks within 12.9 cells", core.Capacity == 6 && Near(core.Range, 12.9f, 0.01f));
            Check("baseline: UPS stores 100 Wd at 70%", Near(ups.Props.storedEnergyMax, 100f, 0.01f) && Near(ups.Props.efficiency, 0.7f, 0.001f));
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            network.InvalidateResearchCache();
            float baseBonus = network.ResearchBonus;
            float baseEquivalents = network.ResearchRackEquivalents;
            Check("baseline: the root research already gives a research uplink of +4% per running rack (max +20%)",
                baseEquivalents >= 1f && baseBonus >= 0.039f && Near(baseBonus, Mathf.Min(0.20f, 0.04f * baseEquivalents), 0.0005f),
                "+" + (baseBonus * 100f).ToString("F1") + "% from " + baseEquivalents.ToString("F2") + " rack equivalents; " + DescribeRacks());

            // --- Optimized Firmware: 15% faster production.
            rack.DevSetProgress(0.1f);
            yield return new WaitTicks(2500);
            float before = rack.Progress - 0.1f;
            FinishResearch("RCDC_OptimizedFirmware");
            rack.DevRefresh();
            Check("firmware: upgrade is active (x1.15)", Near(rack.UpgradeOutputFactor, 1.15f, 0.001f));
            rack.DevSetProgress(0.1f);
            yield return new WaitTicks(2500);
            float after = rack.Progress - 0.1f;
            Check("firmware: a rack really produces 15% faster", before > 0.005f && Near(after / before, 1.15f, 0.04f),
                "progress per 2500 ticks " + before.ToString("F4") + " -> " + after.ToString("F4") + " (x" + (after / Mathf.Max(0.0001f, before)).ToString("F3") + ")");

            // --- Immersion Cooling: hotter thresholds and less heat.
            FinishResearch("RCDC_ImmersionCooling");
            Check("cooling: throttle, shutdown and restart temperatures rise by 4 C",
                Near(rack.WarmTemperature, 36f, 0.01f) && Near(rack.ShutdownTemperature, 54f, 0.01f) && Near(rack.RestartTemperature, 44f, 0.01f),
                rack.WarmTemperature + " / " + rack.ShutdownTemperature + " / " + rack.RestartTemperature);
            Check("cooling: racks give off 30% less heat", Near(RcdcUpgrades.Current.HeatMultiplier, 0.7f, 0.001f));
            Flick(layout.Cooler, false);
            room.Temperature = 34f;
            rack.DevRefresh();
            Check("cooling: 34 C no longer throttles a rack", !rack.IsThrottled && rack.Status == RackStatus.Operational, rack.DevSummary());
            room.Temperature = 38f;
            rack.DevRefresh();
            Check("cooling: 38 C throttles it", rack.IsThrottled, rack.DevSummary());
            room.Temperature = 52f;
            rack.DevStep();
            Check("cooling: 52 C is not a shutdown any more", !rack.IsShutdown, rack.DevSummary());
            room.Temperature = 55f;
            rack.DevStep();
            Check("cooling: 55 C shuts the rack down", rack.IsShutdown, rack.DevSummary());
            room.Temperature = 46f;
            rack.DevStep();
            Check("cooling: stays shut down at 46 C (restart is 44 C)", rack.IsShutdown);
            room.Temperature = 43f;
            rack.DevStep();
            Check("cooling: restarts at 43 C", !rack.IsShutdown, rack.DevSummary());
            Flick(layout.Cooler, true);
            room.Temperature = 22f;
            rack.DevRefresh();
            string text = rack.CompInspectStringExtra();
            Check("cooling: the rack inspect text lists the active upgrades", text.Contains("Upgrades:") && text.Contains("heat x0.70") && !text.Contains("RCDC_"), text.Replace('\n', '|'));

            // --- Predictive Maintenance: 35% slower wear.
            FinishResearch("RCDC_PredictiveMaintenance");
            console.DevSetCoverage(60000);
            rack.DevSetWear(0.10f);
            rack.DevRefresh();
            bool monitored = rack.IsMonitored;
            for (int i = 0; i < 20; i++)
            {
                rack.DevStep();
            }
            float wearDelta = rack.Wear - 0.10f;
            float expectedWear = 20f * 250f / (rack.Props.daysToFullWear * 60000f) * 0.65f * (monitored ? 1f : rack.UnmonitoredWearFactor) * (rack.IsThrottled ? rack.Props.throttledWearFactor : 1f);
            Check("maintenance: wear accrues 35% slower", Near(wearDelta, expectedWear, expectedWear * 0.06f), "measured " + wearDelta.ToString("F5") + ", expected " + expectedWear.ToString("F5"));
            rack.DevSetWear(0.1f);

            // --- Network Fabric: 10 racks per core, longer reach.
            FinishResearch("RCDC_NetworkFabric");
            Check("fabric: a core now serves 10 racks and reaches 16.9 cells", core.Capacity == 10 && Near(core.Range, 16.9f, 0.01f), core.Capacity + " racks, range " + core.Range);
            List<Thing> extras = new List<Thing>();
            IntVec3[] cells =
            {
                layout.Origin + new IntVec3(4, 0, 5), layout.Origin + new IntVec3(5, 0, 5),
                layout.Origin + new IntVec3(4, 0, 1), layout.Origin + new IntVec3(5, 0, 1)
            };
            for (int i = 0; i < cells.Length; i++)
            {
                extras.Add(DemoDataCenter.Spawn(Map, RcdcDefOf.RCDC_ServerRack, cells[i], i < 2 ? Rot4.North : Rot4.South, null));
            }
            Thing far = DemoDataCenter.Spawn(Map, RcdcDefOf.RCDC_ServerRack, layout.Origin + new IntVec3(1, 0, -8), Rot4.North, null);
            extras.Add(far);
            yield return new WaitTicks(300);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            Check("fabric: the core links 9 racks (it stopped at 6 before)", core.ConnectedCount == 9, core.ConnectedCount + "/" + core.Capacity + "; " + DescribeRacks());
            Check("fabric: a rack 15 cells away, out of range before, is now linked", far.TryGetComp<CompServerRack>().Core == core);
            Check("fabric: the core inspect text shows the new capacity", core.parent.GetInspectString().Contains("9 / 10"), core.parent.GetInspectString().Replace('\n', '|'));
            foreach (Thing extra in extras)
            {
                if (extra.Spawned)
                {
                    extra.Destroy(DestroyMode.Vanish);
                }
            }
            yield return new WaitTicks(300);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            Check("fabric cleanup: back to four linked racks", core.ConnectedCount == 4, core.ConnectedCount.ToString());

            // --- Redundant Power: a bigger, more efficient UPS.
            FinishResearch("RCDC_RedundantPower");
            Check("power: the UPS stores 200 Wd at 85%", Near(ups.Props.storedEnergyMax, 200f, 0.01f) && Near(ups.Props.efficiency, 0.85f, 0.001f), ups.Props.storedEnergyMax + " Wd, " + ups.Props.efficiency);
            ups.SetStoredEnergyPct(1f);
            Check("power: a full UPS really holds 200 Wd", Near(ups.StoredEnergy, 200f, 0.5f), ups.StoredEnergy.ToString("F1"));
            Check("power: the UPS inspect text is complete", !layout.Ups[0].GetInspectString().Contains("RCDC_"));

            // --- Distributed Computing and Autonomous Operations.
            FinishResearch("RCDC_DistributedComputing");
            UpgradeTotals t = RcdcUpgrades.Current;
            Check("distributed computing: uplink grows to +7% per rack, max +45%", Near(t.ResearchBonusPerRack, 0.07f, 0.0001f) && Near(t.ResearchBonusCap, 0.45f, 0.0001f));
            FinishResearch("RCDC_AutonomousOperations");
            console.DevSetCoverage(0);
            rack.DevSetWear(0.1f);
            room.Temperature = 22f;
            rack.DevRefresh();
            Check("autonomous: an unmonitored rack runs at 90% output (was 65%)", !rack.IsMonitored && Near(rack.Efficiency, 0.90f, 0.03f), rack.DevSummary());
            Check("autonomous: the unmonitored wear penalty is 10% (was 35%)", Near(rack.UnmonitoredWearFactor, 1.10f, 0.001f));
            console.DevSetCoverage(60000);
            rack.DevRefresh();
            Check("eight upgrade projects are finished so far", RcdcUpgrades.Current.Finished == 8, RcdcUpgrades.Current.Finished + " of " + RcdcUpgrades.Current.Total);
            yield return null;
        }

        /// <summary>The factor a stat part currently applies to a pawn's stat: value with the part divided by value without it.</summary>
        private static float uplinkPartFactor(StatDef stat, Pawn pawn, StatPart part)
        {
            float with = stat.Worker.GetValue(StatRequest.For(pawn), true);
            stat.parts.Remove(part);
            float without;
            try
            {
                without = stat.Worker.GetValue(StatRequest.For(pawn), true);
            }
            finally
            {
                stat.parts.Add(part);
            }
            return without > 0f ? with / without : 0f;
        }

        // ---- C. the research uplink ------------------------------------------------------------------

        private static IEnumerable<Waiter> SectionResearchBuff()
        {
            if (layout == null)
            {
                yield break;
            }
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);
            Console.DevSetCoverage(60000);
            foreach (CompServerRack r in AllRacks()) { r.DevSetWear(0.1f); r.DevSetShutdown(false); r.DevRefresh(); }
            DataCenterRoom.Temperature = 22f;
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            network.InvalidateResearchCache();

            UpgradeTotals t = RcdcUpgrades.Current;
            float equivalents = network.ResearchRackEquivalents;
            float bonus = network.ResearchBonus;
            Check("uplink: running racks count toward the bonus", equivalents > 2.5f && equivalents <= 4.01f, equivalents.ToString("F2") + " rack equivalents; " + DescribeRacks());
            Check("uplink: the bonus is per-rack times racks, capped", Near(bonus, Mathf.Min(t.ResearchBonusCap, t.ResearchBonusPerRack * equivalents), 0.0005f) && bonus <= t.ResearchBonusCap + 0.0001f,
                "+" + (bonus * 100f).ToString("F1") + "%");

            StatDef stat = StatDefOf.ResearchSpeed;
            // A colonist who is incapable of research cannot use the stat at all (the game logs an error if asked).
            Pawn colonist = Colonists.FirstOrDefault(p => !stat.Worker.IsDisabledFor(p));
            if (colonist == null)
            {
                Info("no colonist on this map can research; skipped the research-speed stat checks");
                yield break;
            }
            float withUplink = stat.Worker.GetValue(StatRequest.For(colonist), true);
            StatRequest colonistRequest = StatRequest.For(colonist);
            StatPart uplinkPart = stat.parts.First(p => p is StatPart_DataCenterResearch);
            // Measure the part's effect on the real stat in one instant (with it and with it briefly removed), so
            // nothing else about the colonist (mood, inspiration) can differ between the two numbers.
            float factorNow = uplinkPartFactor(stat, colonist, uplinkPart);
            Check("uplink: the colonist's research speed is multiplied by exactly the bonus factor", Near(factorNow, 1f + bonus, 0.005f),
                "x" + factorNow.ToString("F3") + ", expected x" + (1f + bonus).ToString("F3"));
            string partText = uplinkPart.ExplanationPart(colonistRequest);
            Check("uplink: the stat part explains itself", !string.IsNullOrEmpty(partText) && partText.Contains("Data center research uplink") && !partText.Contains("RCDC_"), partText ?? "null");
            string explanation = stat.Worker.GetExplanationUnfinalized(colonistRequest, stat.toStringNumberSense);
            string fullExplanation = stat.Worker.GetExplanationFull(colonistRequest, stat.toStringNumberSense, withUplink);
            Check("uplink: the research speed breakdown names the data center", explanation.Contains("Data center research uplink") || fullExplanation.Contains("Data center research uplink"),
                "unfinalized: " + explanation.Replace('\n', '|') + " || full: " + fullExplanation.Replace('\n', '|'));
            string consoleText = Console.parent.GetInspectString();
            Check("uplink: the console inspect text shows the current bonus", consoleText.Contains("Research uplink") && !consoleText.Contains("RCDC_"), consoleText.Replace('\n', '|'));

            // Switch every rack off: the bonus must vanish and the colonist's research speed drop by exactly that factor.
            List<Thing> rackThings = AllRacks().Select(r => (Thing)r.parent).ToList();
            FlickAll(rackThings, false);
            yield return new WaitTicks(30);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            network.InvalidateResearchCache();
            Check("uplink: with every rack off there is no bonus", Near(network.ResearchBonus, 0f, 0.0001f), network.ResearchBonus.ToString("F3"));
            Check("uplink: with every rack off the stat part changes nothing", Near(uplinkPartFactor(stat, colonist, uplinkPart), 1f, 0.0001f));
            FlickAll(rackThings, true);
            // The power net switches consumers back on one at a time, so wait for every rack.
            yield return new WaitUntil(() => AllRacks().All(r => r.parent.GetComp<CompPowerTrader>().PowerOn), 2000);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            network.InvalidateResearchCache();
            Check("uplink: the bonus returns when the racks come back", network.ResearchBonus > 0.05f, network.ResearchBonus.ToString("F3"));
        }

        // ---- D. security doors -------------------------------------------------------------------------

        internal static Faction FindVisitorFaction()
        {
            foreach (Faction f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f.IsPlayer || f.def.hidden || !f.def.humanlikeFaction || f.def.permanentEnemy)
                {
                    continue;
                }
                if (f.HostileTo(Faction.OfPlayer))
                {
                    f.TryAffectGoodwillWith(Faction.OfPlayer, 200, false, false, null, null);
                }
                if (!f.HostileTo(Faction.OfPlayer))
                {
                    return f;
                }
            }
            return null;
        }

        internal static Pawn SpawnActor(Faction faction, IntVec3 cell, bool armed)
        {
            // No generated relatives: they would be created and then discarded, leaving dangling references in the save.
            PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.Villager, faction, PawnGenerationContext.NonPlayer, -1,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, allowGay: false, allowFood: false, allowAddictions: false);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, cell, Map);
            pawn.equipment.DestroyAllEquipment();
            pawn.inventory.DestroyAll();
            if (armed)
            {
                pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Revolver")));
            }
            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = 30000;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            return pawn;
        }

        /// <summary>
        /// Sends a test visitor far from the data center instead of destroying it. A destroyed pawn that a colonist
        /// has already talked to would leave a dangling reference (RimWorld warns about it when saving), and a
        /// visitor left beside a door would keep being logged. A live pawn is saved and loaded like any other.
        /// </summary>
        private static void ParkActor(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || !pawn.Spawned)
            {
                return;
            }
            IntVec3 far = layout.Origin;
            foreach (IntVec3 c in Map.AllCells)
            {
                if ((c - layout.Origin).LengthHorizontal > 45f && c.Standable(Map) && !c.Fogged(Map))
                {
                    far = c;
                    break;
                }
            }
            pawn.jobs.StopAll();
            PlaceAt(pawn, far);
        }

        private static bool CanReachInside(Pawn pawn, IntVec3 inside)
        {
            return Map.reachability.CanReach(pawn.Position, inside, PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false));
        }

        private static void PlaceAt(Pawn pawn, IntVec3 cell)
        {
            pawn.Position = cell;
            pawn.Notify_Teleported(true, true);
        }

        /// <summary>Power-net state of a building, attached to failures so a wiring problem is easy to diagnose.</summary>
        private static string DescribePower(Thing t)
        {
            CompPowerTrader trader = t.TryGetComp<CompPowerTrader>();
            if (trader == null)
            {
                return t.def.defName + " has no power trader";
            }
            List<string> around = new List<string>();
            foreach (IntVec3 c in GenAdj.CellsAdjacentCardinal(t))
            {
                around.Add(c + ":" + string.Join("+", c.GetThingList(Map).Select(x => x.def.defName).ToArray()));
            }
            return t.def.defName + " @" + t.Position + " powerOn " + trader.PowerOn + ", net " + (trader.PowerNet == null ? "none" : "gain " + trader.PowerNet.CurrentEnergyGainRate().ToString("F2"))
                + ", connectParent " + (trader.connectParent == null ? "none" : trader.connectParent.parent.def.defName) + ", switch " + (t.TryGetComp<CompFlickable>() == null ? "n/a" : t.TryGetComp<CompFlickable>().SwitchIsOn.ToString())
                + ", output " + trader.PowerOutput.ToString("F1") + " W, neighbours [" + string.Join(" ", around.ToArray()) + "]";
        }

        private static void SealAndRebuild()
        {
            Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            Map.reachability.ClearCache();
        }

        private static IEnumerable<Waiter> SectionSecurity()
        {
            if (layout == null)
            {
                yield break;
            }
            IntVec3 o = layout.Origin;
            IntVec3 northDoor = o + new IntVec3(6, 0, DemoDataCenter.InteriorHeight);
            IntVec3 southDoor = o + new IntVec3(6, 0, -1);
            IntVec3 northOutside = northDoor + new IntVec3(0, 0, 1);
            IntVec3 southOutside = southDoor + new IntVec3(0, 0, -1);
            IntVec3 inside = layout.AisleCell;
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);

            // Research gates the buildings.
            Check("security doors are locked before their research", !RcdcDefOf.RCDC_BiometricDoor.IsResearchFinished && !RcdcDefOf.RCDC_MetalDetectorGate.IsResearchFinished);
            FinishResearch("RCDC_AccessControl");
            Check("Access Control unlocks the biometric door only", RcdcDefOf.RCDC_BiometricDoor.IsResearchFinished && !RcdcDefOf.RCDC_MetalDetectorGate.IsResearchFinished);
            FinishResearch("RCDC_ThreatScreening");
            Check("Threat Screening unlocks the metal detector gate", RcdcDefOf.RCDC_MetalDetectorGate.IsResearchFinished);
            DesignationCategoryDef category = DefDatabase<DesignationCategoryDef>.GetNamed("RCDC_DataCenter");
            int designators = category.AllResolvedDesignators.OfType<Designator_Build>().Count();
            Check("the architect tab now offers both doors", designators >= 7, designators + " build designators");

            Faction faction = FindVisitorFaction();
            Check("a non-hostile faction is available to supply test visitors", faction != null);
            if (faction == null)
            {
                yield break;
            }

            // Seal the room's ordinary door so the scanners are the only way in.
            DemoDataCenter.Spawn(Map, ThingDefOf.Wall, layout.DoorCell, Rot4.North, ThingDefOf.Steel);
            SealAndRebuild();

            Pawn colonist = Colonists.First();
            PlaceAt(colonist, o + new IntVec3(5, 0, 8));
            Pawn visitor = SpawnActor(faction, northOutside, false);
            Pawn armed = SpawnActor(faction, o + new IntVec3(8, 0, 8), true);
            Check("test visitors are unarmed / armed as intended", Building_AccessDoor.FindWeapon(visitor) == null && Building_AccessDoor.FindWeapon(armed) != null);

            // Control: an ordinary door lets a friendly visitor in, so any refusal below is the scanner's doing.
            DemoDataCenter.Spawn(Map, ThingDefOf.Door, northDoor, Rot4.North, ThingDefOf.Steel);
            SealAndRebuild();
            bool controlOpen = CanReachInside(visitor, inside);
            Check("control: an ordinary door lets the friendly visitor in", controlOpen, "faction " + faction.Name);

            // --- Biometric door.
            Thing bio = DemoDataCenter.Spawn(Map, RcdcDefOf.RCDC_BiometricDoor, northDoor, Rot4.North, ThingDefOf.Steel);
            SealAndRebuild();
            Building_AccessDoor bioDoor = (Building_AccessDoor)bio;
            yield return PoweredOn(bioDoor);
            Check("the biometric door is powered by the grid", bioDoor.DoorPowerOn, DescribePower(bio));
            Check("colonist can reach the room through the biometric door", CanReachInside(colonist, inside));
            Check("an unarmed visitor cannot", !CanReachInside(visitor, inside));
            Check("an armed visitor cannot", !CanReachInside(armed, inside));
            string reasonKey;
            string reasonArg;
            Check("the door's own screening agrees (colonist yes, visitor no)", bioDoor.Screen(colonist, out reasonKey, out reasonArg) && !bioDoor.Screen(visitor, out reasonKey, out reasonArg) && reasonKey == "RCDC_DenyNotStaff");

            WaitUntil walked = WalkTo(colonist, inside, 1500);
            yield return walked;
            Check("a colonist really walks through the biometric door", !walked.TimedOut && colonist.Position == inside, colonist.Position.ToString());

            // The visitor standing at the door is logged, once, and raises the alert.
            yield return new WaitTicks(90);
            Check("the door logged the turned-away visitor", bioDoor.DeniedCount >= 1 && bioDoor.LastDeniedWho == visitor.LabelShortCap, bioDoor.DeniedCount + " denied, last " + bioDoor.LastDeniedWho);
            Check("the access-denied alert is raised", new Alert_UnauthorizedAccess().GetReport().active);
            string alertText = new Alert_UnauthorizedAccess().GetExplanation().ToString();
            Check("the alert names the door and the visitor without missing strings", alertText.Contains(visitor.LabelShortCap) && !alertText.Contains("RCDC_"), alertText.Replace('\n', '|'));
            string doorText = bioDoor.GetInspectString();
            Check("the door inspect text is complete", doorText.Contains("Access:") && doorText.Contains("Scanner: online") && doorText.Contains("Turned away") && !doorText.Contains("RCDC_"), doorText.Replace('\n', '|'));
            int countAfterFirst = bioDoor.DeniedCount;
            yield return new WaitTicks(200);
            Check("one visitor is not logged over and over", bioDoor.DeniedCount == countAfterFirst, countAfterFirst + " -> " + bioDoor.DeniedCount);

            // Without power the scanner fails secure: colonists in, visitors out.
            Flick(bio, false);
            yield return new WaitTicks(30);
            Check("the switched-off door has no power", !bioDoor.DoorPowerOn);
            Check("unpowered biometric door still admits a colonist", CanReachInside(colonist, inside));
            Check("unpowered biometric door still refuses a visitor", !CanReachInside(visitor, inside));
            Flick(bio, true);
            yield return PoweredOn(bioDoor);

            // Visitors' research speed is not boosted by the colony's data center.
            if (!StatDefOf.ResearchSpeed.Worker.IsDisabledFor(visitor))
            {
                string visitorExplanation = StatDefOf.ResearchSpeed.Worker.GetExplanationUnfinalized(StatRequest.For(visitor), StatDefOf.ResearchSpeed.toStringNumberSense);
                Check("the research uplink only helps colonists", !visitorExplanation.Contains("Data center research uplink"));
                float visitorSpeed = StatDefOf.ResearchSpeed.Worker.GetValue(StatRequest.For(visitor), true);
                Info("visitor research speed " + visitorSpeed.ToString("F3") + " (no uplink applied)");
            }

            // --- Metal detector gate (replace the biometric door with a wall, put the gate on the south side).
            PlaceAt(colonist, southOutside);
            DemoDataCenter.Spawn(Map, ThingDefOf.Wall, northDoor, Rot4.North, ThingDefOf.Steel);
            Thing det = DemoDataCenter.Spawn(Map, RcdcDefOf.RCDC_MetalDetectorGate, southDoor, Rot4.North, ThingDefOf.Steel);
            SealAndRebuild();
            Building_AccessDoor gate = (Building_AccessDoor)det;
            // Wait for the gate to power up BEFORE anyone stands at it: an unpowered gate would (correctly) log
            // "scanner offline" for whoever is there and the per-pawn cooldown would hide the weapon reason.
            yield return PoweredOn(gate);
            PlaceAt(visitor, southOutside + new IntVec3(1, 0, 0));
            PlaceAt(armed, southOutside);
            PlaceAt(colonist, southOutside + new IntVec3(-1, 0, 0));
            yield return new WaitTicks(5);
            Check("the metal detector gate is powered by the grid", gate.DoorPowerOn, DescribePower(det));
            Check("colonist can reach the room through the gate", CanReachInside(colonist, inside));
            Check("an unarmed visitor is let through", CanReachInside(visitor, inside));
            Check("an armed visitor is turned away", !CanReachInside(armed, inside));
            Check("the gate's own screening names the weapon",
                !gate.Screen(armed, out reasonKey, out reasonArg) && reasonKey == "RCDC_DenyArmed" && !string.IsNullOrEmpty(reasonArg), reasonKey + " / " + reasonArg);
            yield return new WaitTicks(90);
            Check("the gate logged the armed visitor, not the unarmed one", gate.DeniedCount >= 1 && gate.LastDeniedWho == armed.LabelShortCap && gate.LastDeniedReason.Contains("carrying"),
                gate.DeniedCount + " denied, last " + gate.LastDeniedWho + " (" + gate.LastDeniedReason + ")");
            Check("the gate inspect text is complete", !gate.GetInspectString().Contains("RCDC_") && gate.GetInspectString().Contains("weapon"), gate.GetInspectString().Replace('\n', '|'));

            Flick(det, false);
            yield return new WaitTicks(30);
            Check("unpowered gate cannot scan: unarmed visitor is refused (fail secure)", !CanReachInside(visitor, inside));
            Check("unpowered gate still admits a colonist", CanReachInside(colonist, inside));
            WaitUntil walked2 = WalkTo(colonist, inside, 2000);
            yield return walked2;
            Check("a colonist walks through the unpowered gate by hand", !walked2.TimedOut && colonist.Position == inside, colonist.Position.ToString());
            Flick(det, true);
            yield return PoweredOn(gate);
            yield return new WaitTicks(5);   // the door clears pathing's cache on its next tick
            Check("with power back the unarmed visitor is let through again", CanReachInside(visitor, inside));

            // --- Certification: both kinds of door working at once.
            Building_AccessDoor secondBio = (Building_AccessDoor)DemoDataCenter.Spawn(Map, RcdcDefOf.RCDC_BiometricDoor, northDoor, Rot4.North, ThingDefOf.Steel);
            SealAndRebuild();
            yield return PoweredOn(secondBio);
            Check("the map now has a working door of each kind", network.HasWorkingDoor(AccessDoorKind.Biometric) && network.HasWorkingDoor(AccessDoorKind.MetalDetector) && network.IsCertified);
            ThingDef cartridge = RcdcDefOf.RCDC_DataCartridge;
            const float basePrice = 80f;   // the XML value (ThingDef.BaseMarketValue is cached and includes stat parts)
            float priceBefore = cartridge.GetStatValueAbstract(StatDefOf.MarketValue);
            Check("before the certification research cartridges are worth the base 80", Near(priceBefore, basePrice, 0.01f), priceBefore.ToString("F2"));
            FinishResearch("RCDC_SecureCertification");
            float certified = cartridge.GetStatValueAbstract(StatDefOf.MarketValue);
            Check("certified: cartridges are worth 20% more (96)", Near(certified, basePrice * 1.2f, 0.05f), certified.ToString("F2"));
            Thing stackThing = Map.listerThings.ThingsOfDef(cartridge).FirstOrDefault();
            if (stackThing != null)
            {
                float thingValue = stackThing.MarketValue;
                Check("a real stack of cartridges reports the premium too", Near(thingValue, basePrice * 1.2f, 0.05f), thingValue.ToString("F2"));
                StatRequest stackRequest = StatRequest.For(stackThing);
                StatPart certPart = StatDefOf.MarketValue.parts.First(p => p is StatPart_CertifiedData);
                string certText = certPart.ExplanationPart(stackRequest);
                Check("the certification stat part explains itself", !string.IsNullOrEmpty(certText) && certText.Contains("Security-certified data") && !certText.Contains("RCDC_"), certText ?? "null");
                string valueExplanation = StatDefOf.MarketValue.Worker.GetExplanationUnfinalized(stackRequest, StatDefOf.MarketValue.toStringNumberSense)
                    + " || " + StatDefOf.MarketValue.Worker.GetExplanationFull(stackRequest, StatDefOf.MarketValue.toStringNumberSense, thingValue);
                Check("the price breakdown names the certification", valueExplanation.Contains("Security-certified data"), valueExplanation.Replace('\n', '|'));
            }
            Check("the console reports the certification", Console.parent.GetInspectString().Contains("ertified (cartridges sell for +20%)"), Console.parent.GetInspectString().Replace('\n', '|'));

            float quote = QuoteCartridgeSellPrice();
            if (quote > 0f && sellPriceBeforeCertification > 0f)
            {
                Check("a trader really pays 20% more for certified cartridges", Near(quote / sellPriceBeforeCertification, 1.2f, 0.05f),
                    sellPriceBeforeCertification.ToString("F1") + " -> " + quote.ToString("F1") + " silver each");
            }
            else
            {
                Info("trade quote unavailable (no negotiator or no cartridges near a beacon); skipped the trader price check");
            }

            Flick(det, false);
            yield return new WaitTicks(30);
            float lost = cartridge.GetStatValueAbstract(StatDefOf.MarketValue);
            Check("a dead scanner loses the certification (back to 80)", !network.IsCertified && Near(lost, basePrice, 0.01f), lost.ToString("F2"));
            Check("the console says the data center is not certified", Console.parent.GetInspectString().Contains("not certified"), Console.parent.GetInspectString().Replace('\n', '|'));
            Flick(det, true);
            yield return PoweredOn(gate);
            Check("certification returns with the scanner", network.IsCertified && Near(cartridge.GetStatValueAbstract(StatDefOf.MarketValue), basePrice * 1.2f, 0.05f));

            // Keep the doors (they are part of the save/load round trip); clean up the visitors.
            ParkActor(visitor);
            ParkActor(armed);
            PlaceAt(colonist, inside);
            Check("nine upgrade projects are finished before the AI research", RcdcUpgrades.Current.Finished == 9, RcdcUpgrades.Current.Finished + " of " + RcdcUpgrades.Current.Total);
            yield return new WaitTicks(60);
        }

        /// <summary>
        /// Waits for a door to be powered. RimWorld's power net switches new or re-enabled consumers on one at
        /// a time (a lone consumer can wait up to 200 ticks), so a fixed short wait is not enough.
        /// </summary>
        private static WaitUntil PoweredOn(Building_AccessDoor door)
        {
            return new WaitUntil(() => door.DoorPowerOn, 1200);
        }

        private static WaitUntil WalkTo(Pawn pawn, IntVec3 cell, int timeoutTicks)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            return new WaitUntil(() => pawn.Position == cell, timeoutTicks);
        }

        /// <summary>The price a trader would pay per cartridge right now, or -1 if no trade could be set up.</summary>
        private static float QuoteCartridgeSellPrice()
        {
            Pawn negotiator = Colonists.FirstOrDefault(p => !StatDefOf.TradePriceImprovement.Worker.IsDisabledFor(p));
            if (negotiator == null)
            {
                return -1f;
            }
            float price = -1f;
            try
            {
                TradeShip ship = new TradeShip(DefDatabase<TraderKindDef>.GetNamed("Orbital_BulkGoods"));
                Map.passingShipManager.AddShip(ship);
                ship.GenerateThings();
                TradeSession.SetupWith(ship, negotiator, false);
                Tradeable tradeable = TradeSession.deal.AllTradeables.FirstOrDefault(t => t.ThingDef == RcdcDefOf.RCDC_DataCartridge);
                if (tradeable != null)
                {
                    price = tradeable.GetPriceFor(TradeAction.PlayerSells);
                }
                TradeSession.Close();
            }
            catch (Exception ex)
            {
                Check("price quote ran without exceptions", false, ex.ToString());
            }
            return price;
        }
    }
}
