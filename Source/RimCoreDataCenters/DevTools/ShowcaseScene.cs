using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Opt-in (launch argument -rcdc-scene) setup used to take clean screenshots: builds a working demo
    /// data center in the test colony, gets the colonists working in it, and frames the camera. Developer
    /// tooling only; it does nothing unless that launch argument is present.
    /// </summary>
    internal static class ShowcaseScene
    {
        private const string LaunchArg = "rcdc-scene";
        private static bool started;
        private static bool ready;
        private static int readyTick;
        private static DemoLayout layout;

        public static bool Wanted
        {
            get { return GenCommandLine.CommandLineArgPassed(LaunchArg); }
        }

        public static void Tick()
        {
            if (!Wanted || Find.CurrentMap == null || Find.TickManager == null)
            {
                return;
            }
            if (!started)
            {
                if (Find.TickManager.TicksGame < 600 || Find.CurrentMap.mapPawns.FreeColonistsSpawnedCount == 0)
                {
                    return;
                }
                started = true;
                Setup();
                return;
            }
            // Once the doors have powered up, two visitors turn up at them so the access log, alert and message exist.
            if (ready && Find.TickManager.TicksGame == readyTick + 900)
            {
                SpawnVisitors();
            }
            if (ready && Find.TickManager.TicksGame == readyTick + 1200)
            {
                RcdcLog.Test("[SCENE] settled: racks " + MapComponent_DataCenterNetwork.For(Find.CurrentMap).Racks.Count);
            }
        }

        private static void SpawnVisitors()
        {
            Map map = Find.CurrentMap;
            Faction faction = SelfTest.FindVisitorFaction();
            if (faction == null || layout == null)
            {
                RcdcLog.Test("[SCENE] no friendly faction available for the visitors");
                return;
            }
            IntVec3 o = layout.Origin;
            SelfTest.SpawnActor(faction, o + new IntVec3(9, 0, 8), false);
            SelfTest.SpawnActor(faction, o + new IntVec3(10, 0, 8), true);
            RcdcLog.Test("[SCENE] visitors placed at the security doors");

            // The AI has settled in: a warm mood, a directive, and an unanswered request in the letter stack.
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            if (network.AiCores.Count > 0)
            {
                CompAiCore ai = network.AiCores[0];
                ai.DevSetRapport(72f);
                ai.SetDirective(AiDirective.Curiosity);
                ai.SendRequest(AiBoon.ComputeLoan);
                RcdcLog.Test("[SCENE] AI core status " + ai.Status + ", request sent");
            }
        }

        private static void Setup()
        {
            Map map = Find.CurrentMap;
            DebugSettings.enableStoryteller = false;
            RcdcDebugActions.CompleteResearch();
            // A partly finished tree makes a better picture than a fully green one: some done, some available, some locked.
            foreach (string name in new[] { "RCDC_OptimizedFirmware", "RCDC_ImmersionCooling", "RCDC_PredictiveMaintenance", "RCDC_NetworkFabric",
                "RCDC_AccessControl", "RCDC_ThreatScreening", "RCDC_SecureCertification", "RCDC_CognitiveComputing", "RCDC_DataClassification" })
            {
                RcdcDebugActions.FinishWithPrerequisites(DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name));
            }
            RcdcUpgrades.Refresh(true);

            IntVec3 origin;
            if (!DemoDataCenter.TryFindSite(map, out origin))
            {
                RcdcLog.Test("[SCENE] no site found");
                return;
            }
            layout = DemoDataCenter.Build(map, origin, true, false);

            // A fuller room: two more racks and a second cooler so six racks run comfortably.
            DemoDataCenter.Spawn(map, RcdcDefOf.RCDC_ServerRack, origin + new IntVec3(4, 0, 5), Rot4.North, null);
            DemoDataCenter.Spawn(map, RcdcDefOf.RCDC_ServerRack, origin + new IntVec3(4, 0, 1), Rot4.South, null);
            ThingDef coolerDef = RcdcDefOf.RCDC_PrecisionCoolingUnit;
            DemoDataCenter.Spawn(map, coolerDef, DemoDataCenter.CenterForMin(coolerDef, Rot4.North, origin + new IntVec3(6, 0, DemoDataCenter.InteriorHeight)), Rot4.North, null);
            // The security checkpoint: a biometric door and a metal detector gate side by side in the north wall.
            DemoDataCenter.Spawn(map, RcdcDefOf.RCDC_BiometricDoor, origin + new IntVec3(9, 0, DemoDataCenter.InteriorHeight), Rot4.North, null);
            DemoDataCenter.Spawn(map, RcdcDefOf.RCDC_MetalDetectorGate, origin + new IntVec3(10, 0, DemoDataCenter.InteriorHeight), Rot4.North, null);
            // The AI core in the middle of the room, on the conduit row.
            ThingDef aiDef = RcdcDefOf.RCDC_AiCore;
            DemoDataCenter.Spawn(map, aiDef, DemoDataCenter.CenterForMin(aiDef, Rot4.North, origin + new IntVec3(6, 0, 1)), Rot4.North, null);
            map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();

            // Let a few racks show different states, and different specializations, for the screenshots.
            System.Collections.Generic.List<CompServerRack> racks = MapComponent_DataCenterNetwork.For(map).Racks.ToList();
            ThingDef[] specializations = { RcdcDefOf.RCDC_DataCartridge, RcdcDefOf.RCDC_DataCartridge_Research, RcdcDefOf.RCDC_DataCartridge_Financial, RcdcDefOf.RCDC_DataCartridge_Medical };
            for (int i = 0; i < racks.Count; i++)
            {
                racks[i].DevSetProgress(0.55f);
                racks[i].SetOutput(specializations[i % specializations.Length]);
            }

            int spread = 0;
            foreach (Pawn pawn in map.mapPawns.FreeColonists.ToList())
            {
                if (pawn.skills != null)
                {
                    pawn.skills.GetSkill(SkillDefOf.Construction).Level = 9;
                    pawn.skills.GetSkill(SkillDefOf.Intellectual).Level = 9;
                }
                pawn.workSettings.EnableAndInitialize();
                SelfTest.TrySetPriority(pawn, RcdcDefOf.RCDC_DataCenter, 1);
                pawn.Position = layout.AisleCell + new IntVec3(spread++, 0, 0);
                pawn.Notify_Teleported(true, true);
            }

            Room room = layout.Racks[0].GetRoom();
            if (room != null)
            {
                room.Temperature = 24f;
            }

            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            IntVec3 focus = origin + new IntVec3(6, 0, 3);
            Find.CameraDriver.SetRootPosAndSize(focus.ToVector3Shifted(), 13f);
            Find.Selector.ClearSelection();
            Find.Selector.Select(layout.Racks[1]);
            ready = true;
            readyTick = Find.TickManager.TicksGame;
            RcdcLog.Test("[SCENE] ready at " + origin);
        }
    }
}
