using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// Developer-mode debug actions (Debug actions menu > "RimCore Data Centers"). They only exist for
    /// people who turn on developer mode; normal play never touches them.
    /// </summary>
    public static class RcdcDebugActions
    {
        private const string Category = "RimCore Data Centers";

        [DebugAction(Category, "Complete data center research", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void CompleteResearch()
        {
            ResearchProjectDef project = RcdcDefOf.RCDC_DataCenterInfrastructure;
            if (project == null)
            {
                return;
            }
            foreach (ResearchProjectDef prerequisite in project.prerequisites ?? new System.Collections.Generic.List<ResearchProjectDef>())
            {
                if (!prerequisite.IsFinished)
                {
                    Find.ResearchManager.FinishProject(prerequisite, false, null, false);
                }
            }
            Find.ResearchManager.FinishProject(project, false, null, false);
            Messages.Message("Data center infrastructure research completed.", MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Complete ALL data center research (upgrades + security)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void CompleteAllResearch()
        {
            FinishWithPrerequisites(RcdcDefOf.RCDC_DataCenterInfrastructure);
            foreach (ResearchProjectDef project in RcdcUpgrades.Projects)
            {
                FinishWithPrerequisites(project);
            }
            foreach (string name in new[] { "RCDC_AccessControl", "RCDC_ThreatScreening", "RCDC_SecureCertification", "RCDC_DataClassification" })
            {
                FinishWithPrerequisites(DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name));
            }
            RcdcUpgrades.Refresh(true);
            Messages.Message("All data center research completed.", MessageTypeDefOf.TaskCompletion, false);
        }

        /// <summary>Finishes a project after finishing everything it depends on (developer tooling).</summary>
        internal static void FinishWithPrerequisites(ResearchProjectDef project)
        {
            if (project == null || project.IsFinished)
            {
                return;
            }
            if (project.prerequisites != null)
            {
                foreach (ResearchProjectDef prerequisite in project.prerequisites)
                {
                    FinishWithPrerequisites(prerequisite);
                }
            }
            Find.ResearchManager.FinishProject(project, false, null, false);
        }

        [DebugAction(Category, "Spawn demo data center (powered)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SpawnDemoDataCenter()
        {
            Map map = Find.CurrentMap;
            IntVec3 origin = UI.MouseCell();
            if (map == null || !origin.InBounds(map))
            {
                return;
            }
            if (!DemoDataCenter.SiteFitsAt(map, origin))
            {
                Messages.Message("The demo data center needs a 21x12 cell area inside the map here.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            DemoLayout layout = DemoDataCenter.Build(map, origin, true, false);
            Messages.Message("Demo data center spawned (" + layout.Racks.Count + " racks). Generators hold a limited amount of wood.",
                new TargetInfo(layout.Core.Position, map), MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Rack: set wear 100%", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RackFullWear()
        {
            ForRackAtMouse(rack => rack.DevSetWear(1f));
        }

        [DebugAction(Category, "Rack: add 60% wear", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RackAddWear()
        {
            ForRackAtMouse(rack => rack.DevSetWear(rack.Wear + 0.6f));
        }

        [DebugAction(Category, "Rack: reset wear", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RackResetWear()
        {
            ForRackAtMouse(rack => rack.DevSetWear(0f));
        }

        [DebugAction(Category, "Rack: fill cartridge progress", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RackFillProgress()
        {
            ForRackAtMouse(rack => rack.DevSetProgress(1f));
        }

        [DebugAction(Category, "Rack: force emergency shutdown", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RackShutdown()
        {
            ForRackAtMouse(rack => rack.DevSetShutdown(true));
        }

        [DebugAction(Category, "Room: heat +15 C", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RoomHeat()
        {
            ChangeRoomTemperature(15f);
        }

        [DebugAction(Category, "Room: cool -15 C", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RoomCool()
        {
            ChangeRoomTemperature(-15f);
        }

        [DebugAction(Category, "Spawn 20 data cartridges", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SpawnCartridges()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map))
            {
                return;
            }
            Thing cartridges = ThingMaker.MakeThing(RcdcDefOf.RCDC_DataCartridge);
            cartridges.stackCount = 20;
            GenPlace.TryPlaceThing(cartridges, cell, map, ThingPlaceMode.Near);
        }

        [DebugAction(Category, "Trigger espionage incident now", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TriggerEspionage()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatSmall, map);
            if (!RcdcDefOf.RCDC_Espionage.Worker.TryExecute(parms))
            {
                Messages.Message("Espionage incident did not fire (no hostile faction available, or no data center on this map).", MessageTypeDefOf.RejectInput, false);
            }
        }

        [DebugAction(Category, "Force a data contract offer", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ForceContractOffer()
        {
            MapComponent_DataContracts contracts = MapComponent_DataContracts.For(Find.CurrentMap);
            if (contracts == null)
            {
                return;
            }
            contracts.DevForceOffer();
        }

        [DebugAction(Category, "Print data center report", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void PrintReport()
        {
            Map map = Find.CurrentMap;
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            if (network == null)
            {
                Log.Message("[RimCore Data Centers] No network data for this map.");
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[RimCore Data Centers] Report: " + network.Racks.Count + " racks, " + network.Cores.Count
                + " cores, " + network.Consoles.Count + " consoles");
            foreach (CompNetworkCore core in network.Cores)
            {
                sb.AppendLine("  core @" + core.parent.Position + " online " + core.IsOnline + ", racks "
                    + core.ConnectedCount + "/" + core.Capacity + ", monitored " + core.IsMonitored);
            }
            foreach (CompServerRack rack in network.Racks)
            {
                sb.AppendLine("  " + rack.DevSummary());
            }
            Log.Message(sb.ToString());
        }

        [DebugAction(Category, "Toggle verbose logging", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void ToggleVerbose()
        {
            RcdcLog.Verbose = !RcdcLog.Verbose;
            Messages.Message("RimCore Data Centers verbose logging: " + (RcdcLog.Verbose ? "on" : "off"), MessageTypeDefOf.SilentInput, false);
        }

        [DebugAction(Category, "Run self-test (takes several minutes)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunSelfTest()
        {
            SelfTest.Start(false);
        }

        private static void ForRackAtMouse(System.Action<CompServerRack> action)
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map))
            {
                return;
            }
            foreach (Thing thing in cell.GetThingList(map))
            {
                CompServerRack rack = thing.TryGetComp<CompServerRack>();
                if (rack != null)
                {
                    action(rack);
                    rack.DevRefresh();
                }
            }
        }

        private static void ChangeRoomTemperature(float delta)
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map))
            {
                return;
            }
            Room room = cell.GetRoom(map);
            if (room != null && !room.UsesOutdoorTemperature)
            {
                room.Temperature = Mathf.Clamp(room.Temperature + delta, -100f, 200f);
            }
        }
    }
}
