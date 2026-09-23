using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimCore.DataCenters
{
    /// <summary>Self-test sections for the AI core: definitions, behaviour, requests, glitches, voice and persistence.</summary>
    internal static partial class SelfTest
    {
        // ---- A. static definition checks ------------------------------------------------------------

        private static IEnumerable<Waiter> SectionAiDefs()
        {
            ThingDef def = RcdcDefOf.RCDC_AiCore;
            Check("def loaded: RCDC_AiCore", def != null);
            if (def != null)
            {
                List<string> errors = def.ConfigErrors().ToList();
                Check("no config errors: RCDC_AiCore", errors.Count == 0, string.Join("; ", errors.ToArray()));
                Check("texture loaded: RCDC_AiCore", def.graphic != null && def.graphic != BaseContent.BadGraphic);
                Check("AI core is 2x2 with costs, power and research gating", def.size == new IntVec2(2, 2) && def.CostList != null && def.CostList.Count == 4
                    && def.GetCompProperties<CompProperties_Power>() != null && def.researchPrerequisites != null && def.researchPrerequisites.Count == 1 && !def.IsResearchFinished);
                Check("AI core is in the data center tab", def.designationCategory != null && def.designationCategory.defName == "RCDC_DataCenter");
            }
            Check("AI command icons load", AiIcons.Directive != null && AiIcons.Report != null && AiIcons.Directive != BaseContent.BadTex && AiIcons.Report != BaseContent.BadTex);
            Check("AI request letter def exists and uses the request letter class", RcdcDefOf.RCDC_AiLetter != null && RcdcDefOf.RCDC_AiLetter.letterClass == typeof(ChoiceLetter_AiRequest));
            Check("AI chime defined", RcdcDefOf.RCDC_AiChime != null);

            ResearchProjectDef cognitive = Project("RCDC_CognitiveComputing");
            ResearchProjectDef adaptive = Project("RCDC_AdaptiveLearning");
            Check("Cognitive Computing needs Distributed Computing", cognitive != null && cognitive.prerequisites != null && cognitive.prerequisites.Count == 1 && cognitive.prerequisites[0].defName == "RCDC_DistributedComputing");
            Check("Adaptive Learning needs Cognitive Computing", adaptive != null && adaptive.prerequisites != null && adaptive.prerequisites.Count == 1 && adaptive.prerequisites[0].defName == "RCDC_CognitiveComputing");
            Check("the AI research is locked", cognitive != null && !cognitive.IsFinished && !cognitive.PrerequisitesCompleted && adaptive != null && !adaptive.IsFinished);

            List<string> missing = AiVoice.AllKeys().Where(k => !k.CanTranslate()).ToList();
            Check("every line the AI can say exists as a keyed string", missing.Count == 0 && AiVoice.AllKeys().Count() == 46, missing.Count + " missing of " + AiVoice.AllKeys().Count() + " " + string.Join(",", missing.Take(3).ToArray()));
            List<string> dynamicKeys = new List<string>();
            foreach (AiStatus s in Enum.GetValues(typeof(AiStatus))) dynamicKeys.Add("RCDC_AiStatus_" + s);
            foreach (AiDirective d in Enum.GetValues(typeof(AiDirective))) { dynamicKeys.Add("RCDC_AiDirective_" + d); dynamicKeys.Add("RCDC_AiDirectiveDesc_" + d); }
            foreach (AiBoon b in Enum.GetValues(typeof(AiBoon))) dynamicKeys.Add("RCDC_AiBoon_" + b);
            foreach (AiBoon b in new[] { AiBoon.ComputeLoan, AiBoon.Overclock, AiBoon.Diagnostics })
            {
                foreach (string part in new[] { "Label", "Text", "Accept", "Decline" }) dynamicKeys.Add("RCDC_AiReq_" + b + "_" + part);
            }
            List<string> missingDynamic = dynamicKeys.Where(k => !k.CanTranslate()).ToList();
            Check("every status, directive, arrangement and request string exists", missingDynamic.Count == 0, string.Join(",", missingDynamic.ToArray()));
            yield return null;
        }

        // ---- B. behaviour in a running data center ---------------------------------------------------

        private static Thing FindDoorOfKind(AccessDoorKind kind)
        {
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);
            foreach (Building_AccessDoor d in network.AccessDoors)
            {
                if (d.Kind == kind)
                {
                    return d;
                }
            }
            return null;
        }

        private static IEnumerable<Waiter> SectionAi()
        {
            if (layout == null)
            {
                yield break;
            }
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(Map);
            CompNetworkCore core = Core;
            CompOperationsConsole console = Console;
            Room room = DataCenterRoom;
            ThingDef aiDef = RcdcDefOf.RCDC_AiCore;

            // Research unlocks the building.
            FinishResearch("RCDC_CognitiveComputing");
            Check("Cognitive Computing unlocks the AI core", aiDef.IsResearchFinished);
            DesignationCategoryDef category = DefDatabase<DesignationCategoryDef>.GetNamed("RCDC_DataCenter");
            Check("the architect tab now offers the AI core", category.AllResolvedDesignators.OfType<Designator_Build>().Any(d => d.PlacingDef == aiDef));

            // Build it inside the data center, next to the row of conduits so it is on the grid.
            foreach (Pawn p in Colonists) { PlaceAt(p, layout.AisleCell + new IntVec3(0, 0, 1)); }
            room.Temperature = 22f;
            Thing aiThing = DemoDataCenter.Spawn(Map, aiDef, DemoDataCenter.CenterForMin(aiDef, Rot4.North, layout.Origin + new IntVec3(6, 0, 1)), Rot4.North, null);
            SealAndRebuild();
            room = DataCenterRoom;   // rebuilding the regions replaced the room object
            CompAiCore ai = aiThing.TryGetComp<CompAiCore>();
            Check("the AI core is registered with the network", network.AiCores.Count == 1 && ai != null);
            WaitUntil booted = new WaitUntil(() => ai.Status == AiStatus.Online, 3000);
            yield return booted;
            Check("the AI core comes online (powered, core in range, cool)", !booted.TimedOut, ai.Status + "; " + DescribePower(aiThing));
            yield return new WaitUntil(() => ai.HasEverBooted, 800);   // the boot greeting happens on its next rare tick
            Check("it booted and greeted the player", ai.HasEverBooted);
            Check("it draws its rated 800 W", (ai.DevPowerDraw() > 790f && ai.DevPowerDraw() < 810f), ai.DevPowerDraw().ToString("F0") + " W");
            string inspect = aiThing.GetInspectString();
            Check("AI inspect text is complete", inspect.Contains("Meridian") && inspect.Contains("Rapport") && inspect.Contains("Directive") && !inspect.Contains("RCDC_"), inspect.Replace('\n', '|'));

            // Baseline (rapport 100 so the numbers are the XML numbers).
            ai.DevClear();
            ai.DevSetRapport(100f);
            ai.SetDirective(AiDirective.Balanced);
            foreach (CompServerRack r in AllRacks()) { r.DevSetWear(0.1f); r.DevSetShutdown(false); r.DevRefresh(); }
            AiModifiers m = network.Ai;
            Check("balanced AI: monitoring and forecasts on, diagnostics wear x0.9, nothing else changes",
                m.Monitoring && m.Forecast && Near(m.Wear, 0.9f, 0.001f) && Near(m.Output, 1f, 0.001f) && Near(m.Heat, 1f, 0.001f) && Near(m.Tolerance, 0f, 0.001f));

            // The AI monitors the data center on its own.
            console.DevSetCoverage(0);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            Check("with no console coverage the AI still monitors every rack", core.IsMonitored && Rack(0).IsMonitored && !console.HasCoverage);
            Check("no operations shift is requested while the AI is watching", !console.ShiftWanted(false));

            // A reboot takes the monitoring away and gives it back.
            ai.TriggerGlitch(AiGlitch.Reboot);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            Check("a reboot glitch: the AI is rebooting and monitoring drops", ai.Status == AiStatus.Rebooting && !core.IsMonitored && network.ActiveAi == null && !network.Ai.Monitoring);
            Check("a reboot glitch changes nothing else about the racks", Near(Rack(0).UpgradeOutputFactor, RcdcUpgrades.Current.OutputMultiplier, 0.001f));
            WaitUntil rebooted = new WaitUntil(() => ai.Status == AiStatus.Online, 8000);
            yield return rebooted;
            Check("the AI reboots by itself and monitoring returns", !rebooted.TimedOut && core.IsMonitored);

            // --- Directives.
            CompServerRack rack = Rack(0);
            rack.DevSetProgress(0.1f);
            yield return new WaitTicks(2500);
            float balancedProgress = rack.Progress - 0.1f;

            ai.SetDirective(AiDirective.Efficiency);
            m = network.Ai;
            Check("efficiency directive: output x1.10, heat x1.20", Near(m.Output, 1.10f, 0.002f) && Near(m.Heat, 1.20f, 0.002f), m.Output + " / " + m.Heat);
            ai.Step();
            Check("efficiency directive: the AI core draws 25% more power (1000 W)", Near(ai.DevPowerDraw(), 1000f, 12f), ai.DevPowerDraw().ToString("F0") + " W");
            rack.DevSetProgress(0.1f);
            yield return new WaitTicks(2500);
            float efficiencyProgress = rack.Progress - 0.1f;
            Check("efficiency directive: racks really produce 10% faster", balancedProgress > 0.005f && Near(efficiencyProgress / balancedProgress, 1.10f, 0.04f),
                balancedProgress.ToString("F4") + " -> " + efficiencyProgress.ToString("F4"));

            ai.SetDirective(AiDirective.Stewardship);
            m = network.Ai;
            Check("stewardship directive: wear x0.675, +2 C tolerance, output x0.95", Near(m.Wear, 0.675f, 0.002f) && Near(m.Tolerance, 2f, 0.01f) && Near(m.Output, 0.95f, 0.002f), m.Wear + " / " + m.Tolerance + " / " + m.Output);
            Check("stewardship directive: racks throttle 2 C later", Near(rack.WarmTemperature, 32f + 4f + 2f, 0.01f), rack.WarmTemperature.ToString());
            Check("stewardship directive: the forecast counts the slower wear", rack.DaysUntilService() > 0f);

            network.InvalidateResearchCache();
            ai.SetDirective(AiDirective.Balanced);
            float balancedResearch = network.ResearchBonus;
            ai.SetDirective(AiDirective.Curiosity);
            network.InvalidateResearchCache();
            float curiousResearch = network.ResearchBonus;
            m = network.Ai;
            float equiv = network.ResearchRackEquivalents;
            Check("curiosity directive: research uplink +2% per rack (cap +10%), output x0.92",
                Near(m.ResearchPerRack, 0.02f, 0.0005f) && Near(m.ResearchCap, 0.10f, 0.0005f) && Near(m.Output, 0.92f, 0.002f)
                && curiousResearch > balancedResearch + 0.01f && Near(curiousResearch, Mathf.Min(0.55f, 0.09f * equiv), 0.003f),
                "balanced +" + (balancedResearch * 100f).ToString("F1") + "%, curious +" + (curiousResearch * 100f).ToString("F1") + "% from " + equiv.ToString("F2") + " rack equivalents");
            ai.SetDirective(AiDirective.Balanced);

            // Rapport scales the benefits.
            ai.DevSetRapport(50f);
            ai.SetDirective(AiDirective.Efficiency);
            Check("at rapport 50 the benefit is scaled to 75% (output x1.075)", Near(network.Ai.Output, 1.075f, 0.002f), network.Ai.Output.ToString("F3"));
            ai.DevSetRapport(0f);
            Check("at rapport 0 the benefit is half strength (output x1.05)", Near(network.Ai.Output, 1.05f, 0.002f), network.Ai.Output.ToString("F3"));
            ai.SetDirective(AiDirective.Balanced);
            ai.DevSetRapport(50f);

            // --- Daily mood.
            foreach (CompServerRack r in AllRacks()) { r.DevSetWear(0.1f); r.DevSetShutdown(false); r.DevRefresh(); }
            ai.DevRunDaily();
            Check("a good day with healthy racks raises rapport by 1", Near(ai.Rapport, 51f, 0.01f), ai.Rapport.ToString("F1"));
            Rack(1).DevSetWear(0.7f);
            Rack(1).DevRefresh();
            ai.DevRunDaily();
            Check("a day with a rack in trouble lowers rapport by 1", Near(ai.Rapport, 50f, 0.01f), ai.Rapport.ToString("F1"));
            Rack(1).DevSetWear(0.1f);
            Rack(1).DevRefresh();

            // --- Overheating shuts the AI down and raises the alert.
            room.Temperature = 46f;
            // The game's per-cell temperature cache refreshes over a few ticks.
            yield return new WaitUntil(() => ai.Status == AiStatus.Overheated, 600);
            ai.DevRefresh();
            Check("a too-hot room shuts the AI down", ai.Status == AiStatus.Overheated && !core.IsMonitored, ai.Status.ToString());
            Check("the AI core down alert is raised", new Alert_AiOffline().GetReport().active);
            string alertText = new Alert_AiOffline().GetExplanation().ToString();
            Check("the alert explains itself without missing strings", alertText.Contains("Meridian") && !alertText.Contains("RCDC_"), alertText.Replace('\n', '|'));
            float rapportBefore = ai.Rapport;
            ai.DevRunDaily();
            Check("a day offline costs 3 rapport", Near(ai.Rapport, rapportBefore - 3f, 0.01f), rapportBefore + " -> " + ai.Rapport);
            room.Temperature = 22f;
            ai.DevSetRapport(50f);
            yield return new WaitUntil(() => ai.Status == AiStatus.Online, 600);
            ai.DevRefresh();
            Check("cooling the room brings it back", ai.Status == AiStatus.Online && network.ActiveAi == ai);

            // --- Requests: accept, decline, ignore.
            ai.DevClear();
            ai.DevSetRapport(50f);
            ai.SendRequest(AiBoon.ComputeLoan);
            ChoiceLetter_AiRequest letter = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_AiRequest>().LastOrDefault();
            Check("the AI sends a request letter", letter != null && ai.HasPendingRequest);
            List<DiaOption> choices = letter == null ? new List<DiaOption>() : letter.Choices.ToList();
            Check("the letter offers accept, decline and decide later", choices.Count == 3);
            string letterText = letter == null ? "" : letter.Text.ToString() + " | " + letter.Label;
            Check("the letter names the AI and has no missing strings", letterText.Contains("Meridian") && !letterText.Contains("RCDC_"), letterText.Replace('\n', '|'));
            if (choices.Count == 3)
            {
                choices[0].action();
            }
            Check("accepting a compute loan starts it and raises rapport by 5", ai.ActiveBoon == AiBoon.ComputeLoan && Near(ai.Rapport, 55f, 0.01f) && !ai.HasPendingRequest && !Find.LetterStack.LettersListForReading.Contains(letter),
                ai.ActiveBoon + ", rapport " + ai.Rapport);
            m = network.Ai;
            Check("compute loan: research uplink doubles, racks produce 20% less", Near(m.ResearchMultiplier, 2f, 0.001f) && Near(m.Output, 0.8f, 0.002f), m.ResearchMultiplier + " / " + m.Output);
            Check("the AI inspect text shows the arrangement", aiThing.GetInspectString().Contains("ompute loan"), aiThing.GetInspectString().Replace('\n', '|'));

            ai.DevClear();
            ai.SendRequest(AiBoon.Overclock);
            letter = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_AiRequest>().LastOrDefault();
            float beforeDecline = ai.Rapport;
            letter.Choices.ToList()[1].action();
            Check("declining costs 2 rapport and starts nothing", Near(ai.Rapport, beforeDecline - 2f, 0.01f) && ai.ActiveBoon == AiBoon.None && !ai.HasPendingRequest, beforeDecline + " -> " + ai.Rapport);

            ai.SendRequest(AiBoon.Overclock);
            letter = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_AiRequest>().LastOrDefault();
            letter.Choices.ToList()[0].action();
            m = network.Ai;
            Check("overclock window: output x1.25, heat x1.4", ai.ActiveBoon == AiBoon.Overclock && Near(m.Output, 1.25f, 0.002f) && Near(m.Heat, 1.4f, 0.002f), m.Output + " / " + m.Heat);

            ai.DevClear();
            Rack(2).DevSetWear(0.5f);
            ai.SendRequest(AiBoon.Diagnostics);
            letter = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_AiRequest>().LastOrDefault();
            letter.Choices.ToList()[0].action();
            Check("maintenance window: every rack recovers 20% wear and output halves", Near(Rack(2).Wear, 0.3f, 0.01f) && Near(network.Ai.Output, 0.5f, 0.002f), Rack(2).Wear.ToString("F2") + ", output x" + network.Ai.Output.ToString("F2"));

            ai.DevClear();
            float beforeIgnore = ai.Rapport;
            ai.SendRequest(AiBoon.ComputeLoan);
            ai.DevExpireRequest();
            ai.Step();
            Check("an ignored request is withdrawn and costs 4 rapport", !ai.HasPendingRequest && !Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_AiRequest>().Any() && Near(ai.Rapport, beforeIgnore - 4f, 0.01f), beforeIgnore + " -> " + ai.Rapport);
            ai.DevClear();
            ai.DevSetRapport(50f);

            // --- Glitches.
            foreach (CompServerRack r in AllRacks()) r.DevSetProgress(0.5f);
            ai.TriggerGlitch(AiGlitch.CacheError);
            int reset = AllRacks().Count(r => r.Progress < 0.01f);
            Check("a cache glitch resets exactly one rack's cartridge progress", reset == 1, reset + " racks reset");
            ai.TriggerGlitch(AiGlitch.Sulk);
            foreach (CompServerRack r in AllRacks()) r.DevRefresh();
            Check("a sulk: the AI stops monitoring and helping for a day", ai.Status == AiStatus.Sulking && !core.IsMonitored && network.ActiveAi == null);
            Check("a sulk is visible in the inspect text", aiThing.GetInspectString().Contains("not speaking"), aiThing.GetInspectString().Replace('\n', '|'));
            Check("nothing is destroyed by glitches", AllRacks().All(r => r.parent.Spawned && r.parent.HitPoints == r.parent.MaxHitPoints) && aiThing.HitPoints == aiThing.MaxHitPoints);
            ai.DevClear();
            ai.DevSetRapport(100f);

            // Glitch rarity: security certification halves it, Adaptive Learning trims it further.
            Thing gate = FindDoorOfKind(AccessDoorKind.MetalDetector);
            float certifiedMean = ai.GlitchMeanTicks();
            Flick(gate, false);
            float uncertifiedMean = ai.GlitchMeanTicks();
            Check("certification makes glitches half as frequent", !network.IsCertified && Near(certifiedMean / uncertifiedMean, 2f, 0.01f) && Near(uncertifiedMean, 45f * 60000f, 1f),
                (certifiedMean / 60000f).ToString("F0") + " vs " + (uncertifiedMean / 60000f).ToString("F0") + " days");
            ai.DevSetRapport(40f);
            Check("mean days between glitches by mood: 45 calm, 25 uneasy, 12 tense", Near(uncertifiedMean / 60000f, 45f, 0.01f) && Near(ai.GlitchMeanTicks() / 60000f, 25f, 0.01f), (ai.GlitchMeanTicks() / 60000f).ToString("F1"));
            ai.DevSetRapport(10f);
            Check("a low-rapport AI glitches more often (12 days)", Near(ai.GlitchMeanTicks() / 60000f, 12f, 0.01f));
            Flick(gate, true);
            yield return new WaitUntil(() => ((Building_AccessDoor)gate).DoorPowerOn, 1200);

            // Adaptive Learning: stronger and steadier.
            ai.DevSetRapport(50f);
            ai.SetDirective(AiDirective.Efficiency);
            float outputBefore = network.Ai.Output;
            float meanBefore = ai.GlitchMeanTicks();
            FinishResearch("RCDC_AdaptiveLearning");
            ai.DevRefresh();   // the AI's cached numbers are rebuilt on the next tick in the real game
            Check("adaptive learning: benefits 15% stronger (output x1.09 at rapport 50)", Near(network.Ai.Output, 1.09f, 0.002f) && network.Ai.Output > outputBefore, outputBefore + " -> " + network.Ai.Output);
            Check("adaptive learning: glitches 30% rarer", Near(ai.GlitchMeanTicks() / meanBefore, 1f / 0.7f, 0.01f), (ai.GlitchMeanTicks() / meanBefore).ToString("F3"));
            ai.SetDirective(AiDirective.Balanced);

            // --- Voice, forecast, report, gizmos.
            ai.DevSetRapport(80f);
            foreach (CompServerRack r in AllRacks()) { r.DevSetWear(0.1f); r.DevRefresh(); }
            Rack(1).DevSetWear(0.6f);
            Rack(1).DevRefresh();
            string report = AiReport.Build(ai);
            Check("the status report is a real analysis with no missing strings", report.Contains("Meridian") && report.Contains("racks") && report.Contains("cartridges per day") && report.Contains("servicing") && !report.Contains("RCDC_"), report.Replace('\n', '|'));
            Check("the report gives at most three recommendations", report.Split('\n').Count(l => l.StartsWith("- ")) <= 3);
            Rack(1).DevSetWear(0.1f);
            Rack(1).DevRefresh();
            string cleanReport = AiReport.Build(ai);
            Check("a healthy data center gets no servicing or cooling warnings", !cleanReport.Contains("servicing") && !cleanReport.Contains("overheating") && cleanReport.Split('\n').Any(l => l.StartsWith("- ")), cleanReport.Replace('\n', '|'));
            string rackText = Rack(0).CompInspectStringExtra();
            Check("racks show the AI's maintenance forecast", rackText.Contains("forecast") && rackText.Contains("days") && !rackText.Contains("RCDC_"), rackText.Replace('\n', '|'));
            List<Gizmo> gizmos = ai.CompGetGizmosExtra().ToList();
            Check("the AI core offers the directive and report commands", gizmos.OfType<Command_Action>().Count(g => g.icon != null && !g.defaultLabel.Contains("RCDC_")) >= 2);
            Check("the AI voice picks a line for every mood", new[] { 10f, 50f, 90f }.All(r => AiVoice.Line(AiVoice.IdleTopic(r)).Length > 5 && !AiVoice.Line(AiVoice.IdleTopic(r)).Contains("RCDC_")));

            // Leave a rich state for the save/load round trip: a directive, a mood and an unanswered request.
            ai.DevSetRapport(73f);
            ai.SetDirective(AiDirective.Curiosity);
            ai.SendRequest(AiBoon.Overclock);
            Check("state prepared for the save: request pending", ai.HasPendingRequest && ai.Directive == AiDirective.Curiosity);
            yield return new WaitTicks(60);
        }
    }
}
