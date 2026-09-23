using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>Shared logic for the data center alerts: they list problem racks across all player maps.</summary>
    public abstract class Alert_DataCenterBase : Alert
    {
        private readonly List<Thing> culprits = new List<Thing>();

        protected abstract bool IsProblem(CompServerRack rack);

        protected abstract string ExplanationKey { get; }

        protected List<Thing> Culprits
        {
            get
            {
                culprits.Clear();
                List<Map> maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    Map map = maps[i];
                    if (map == null || !map.IsPlayerHome)
                    {
                        continue;
                    }
                    MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
                    if (network == null)
                    {
                        continue;
                    }
                    IList<CompServerRack> racks = network.Racks;
                    for (int j = 0; j < racks.Count; j++)
                    {
                        CompServerRack rack = racks[j];
                        if (rack != null && rack.parent != null && rack.parent.Spawned && IsProblem(rack))
                        {
                            culprits.Add(rack.parent);
                        }
                    }
                }
                return culprits;
            }
        }

        public override AlertReport GetReport()
        {
            return AlertReport.CulpritsAre(Culprits);
        }

        public override TaggedString GetExplanation()
        {
            StringBuilder sb = new StringBuilder();
            List<Thing> list = Culprits;
            for (int i = 0; i < list.Count; i++)
            {
                CompServerRack rack = list[i].TryGetComp<CompServerRack>();
                if (rack == null)
                {
                    continue;
                }
                sb.Append("  - ").Append(list[i].LabelShort).Append(": ").Append(rack.StatusLabel).AppendLine();
            }
            return ExplanationKey.Translate(sb.ToString().TrimEnd());
        }
    }

    /// <summary>Racks that are throttled or shut down because the room is too hot.</summary>
    public class Alert_DataCenterOverheating : Alert_DataCenterBase
    {
        public Alert_DataCenterOverheating()
        {
            defaultLabel = "RCDC_AlertOverheating".Translate();
            defaultPriority = AlertPriority.High;
        }

        protected override string ExplanationKey
        {
            get { return "RCDC_AlertOverheatingDesc"; }
        }

        protected override bool IsProblem(CompServerRack rack)
        {
            return rack.Status == RackStatus.TooHot;
        }
    }

    /// <summary>Racks that are worn and need a colonist to service them, or have halted.</summary>
    public class Alert_DataCenterMaintenance : Alert_DataCenterBase
    {
        public Alert_DataCenterMaintenance()
        {
            defaultLabel = "RCDC_AlertMaintenance".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        protected override string ExplanationKey
        {
            get { return "RCDC_AlertMaintenanceDesc"; }
        }

        protected override bool IsProblem(CompServerRack rack)
        {
            return rack.Status == RackStatus.MaintenanceRequired || (rack.IsHalted && rack.Status != RackStatus.NoPower);
        }
    }

    /// <summary>Security doors that turned someone away recently (the last two hours).</summary>
    public class Alert_UnauthorizedAccess : Alert
    {
        private const int RecentTicks = 5000;
        private readonly List<Thing> culprits = new List<Thing>();

        public Alert_UnauthorizedAccess()
        {
            defaultLabel = "RCDC_AlertAccess".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        private List<Thing> Culprits
        {
            get
            {
                culprits.Clear();
                List<Map> maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    Map map = maps[i];
                    if (map == null || !map.IsPlayerHome)
                    {
                        continue;
                    }
                    MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
                    if (network == null)
                    {
                        continue;
                    }
                    IList<Building_AccessDoor> doors = network.AccessDoors;
                    for (int j = 0; j < doors.Count; j++)
                    {
                        Building_AccessDoor door = doors[j];
                        if (door != null && door.Spawned && door.DeniedRecently(RecentTicks))
                        {
                            culprits.Add(door);
                        }
                    }
                }
                return culprits;
            }
        }

        public override AlertReport GetReport()
        {
            return AlertReport.CulpritsAre(Culprits);
        }

        public override TaggedString GetExplanation()
        {
            StringBuilder sb = new StringBuilder();
            List<Thing> list = Culprits;
            for (int i = 0; i < list.Count; i++)
            {
                Building_AccessDoor door = list[i] as Building_AccessDoor;
                if (door == null)
                {
                    continue;
                }
                sb.Append("  - ").Append(door.LabelShort).Append(": ").Append(door.LastDeniedWho).Append(" (").Append(door.LastDeniedReason).Append(")").AppendLine();
            }
            return "RCDC_AlertAccessDesc".Translate(sb.ToString().TrimEnd());
        }
    }

    /// <summary>An AI core that has been running but is now unpowered, unlinked or too hot to work.</summary>
    public class Alert_AiOffline : Alert
    {
        private readonly List<Thing> culprits = new List<Thing>();

        public Alert_AiOffline()
        {
            defaultLabel = "RCDC_AlertAi".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        private List<Thing> Culprits
        {
            get
            {
                culprits.Clear();
                List<Map> maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    MapComponent_DataCenterNetwork network = maps[i] == null || !maps[i].IsPlayerHome ? null : MapComponent_DataCenterNetwork.For(maps[i]);
                    if (network == null)
                    {
                        continue;
                    }
                    for (int j = 0; j < network.AiCores.Count; j++)
                    {
                        CompAiCore ai = network.AiCores[j];
                        if (ai != null && ai.parent != null && ai.parent.Spawned && ai.HasEverBooted
                            && (ai.Status == AiStatus.Offline || ai.Status == AiStatus.Overheated)
                            && !(ai.parent.TryGetComp<CompFlickable>() != null && !ai.parent.TryGetComp<CompFlickable>().SwitchIsOn))
                        {
                            culprits.Add(ai.parent);
                        }
                    }
                }
                return culprits;
            }
        }

        public override AlertReport GetReport()
        {
            return AlertReport.CulpritsAre(Culprits);
        }

        public override TaggedString GetExplanation()
        {
            StringBuilder sb = new StringBuilder();
            List<Thing> list = Culprits;
            for (int i = 0; i < list.Count; i++)
            {
                CompAiCore ai = list[i].TryGetComp<CompAiCore>();
                if (ai != null)
                {
                    sb.Append("  - ").Append(list[i].LabelShort).Append(": ").Append(("RCDC_AiStatus_" + ai.Status).Translate()).AppendLine();
                }
            }
            return "RCDC_AlertAiDesc".Translate(AiVoice.Name, sb.ToString().TrimEnd());
        }
    }

    /// <summary>Racks that have no power or no network link (unless the player switched them off on purpose).</summary>
    public class Alert_DataCenterOffline : Alert_DataCenterBase
    {
        public Alert_DataCenterOffline()
        {
            defaultLabel = "RCDC_AlertOffline".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        protected override string ExplanationKey
        {
            get { return "RCDC_AlertOfflineDesc"; }
        }

        protected override bool IsProblem(CompServerRack rack)
        {
            RackStatus status = rack.Status;
            if (status == RackStatus.NoNetwork)
            {
                return true;
            }
            return status == RackStatus.NoPower && !rack.SwitchedOff;
        }
    }
}
