using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// The AI's status report: a real analysis of the racks' current state (not canned text). It counts racks by
    /// status, estimates output, finds the rack that will need service soonest, checks temperature headroom and
    /// security certification, and turns those into at most three recommendations.
    /// </summary>
    public static class AiReport
    {
        public static string Build(CompAiCore ai)
        {
            Map map = ai.parent.Map;
            MapComponent_DataCenterNetwork network = MapComponent_DataCenterNetwork.For(map);
            List<CompServerRack> racks = new List<CompServerRack>();
            if (network != null)
            {
                for (int i = 0; i < network.Racks.Count; i++)
                {
                    if (network.Racks[i].parent != null && network.Racks[i].parent.Spawned)
                    {
                        racks.Add(network.Racks[i]);
                    }
                }
            }
            int operational = 0, hot = 0, service = 0, offline = 0, full = 0;
            float perDay = 0f;
            float minMargin = float.MaxValue;
            CompServerRack soonest = null;
            float soonestDays = float.MaxValue;
            foreach (CompServerRack rack in racks)
            {
                switch (rack.Status)
                {
                    case RackStatus.Operational: operational++; break;
                    case RackStatus.TooHot: hot++; break;
                    case RackStatus.MaintenanceRequired: service++; break;
                    case RackStatus.OutputFull: full++; break;
                    default: offline++; break;
                }
                if (rack.IsRunning)
                {
                    perDay += 60000f / rack.Props.ticksPerCartridge * rack.Props.cartridgesPerCycle * rack.Efficiency * rack.UpgradeOutputFactor;
                    minMargin = Mathf.Min(minMargin, rack.WarmTemperature - rack.Temperature);
                    float days = rack.DaysUntilService();
                    if (days >= 0f && days < soonestDays)
                    {
                        soonestDays = days;
                        soonest = rack;
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("RCDC_AiReportHeader".Translate(AiVoice.Name, ("RCDC_AiDirective_" + ai.Directive).Translate()));
            sb.AppendLine("RCDC_AiReportRacks".Translate(racks.Count, operational, hot, service, offline + full));
            sb.AppendLine("RCDC_AiReportOutput".Translate(perDay.ToString("F1")));
            if (network != null)
            {
                float bonus = network.ResearchBonus;
                if (bonus > 0f)
                {
                    sb.AppendLine("RCDC_AiReportResearch".Translate((bonus * 100f).ToString("F0")));
                }
            }
            if (soonest != null)
            {
                sb.AppendLine("RCDC_AiReportForecast".Translate(soonest.parent.LabelShort, soonestDays.ToString("F1")));
            }
            sb.AppendLine("RCDC_AiReportMood".Translate(ai.MoodLabel, Mathf.RoundToInt(ai.Rapport)));
            if (ai.Trait != AiTrait.Developing)
            {
                sb.AppendLine("RCDC_AiReportTrait".Translate(("RCDC_AiTrait_" + ai.Trait).Translate()));
            }
            sb.AppendLine();

            // Recommendations, most urgent first, at most three.
            List<string> recs = new List<string>();
            if (hot > 0)
            {
                recs.Add("RCDC_AiRec_Cooling".Translate());
            }
            if (service > 0)
            {
                recs.Add("RCDC_AiRec_Service".Translate());
            }
            if (offline > 0)
            {
                recs.Add("RCDC_AiRec_Network".Translate());
            }
            if (full > 0)
            {
                recs.Add("RCDC_AiRec_Haul".Translate());
            }
            if (hot == 0 && minMargin < 4f && minMargin != float.MaxValue)
            {
                recs.Add("RCDC_AiRec_Headroom".Translate(minMargin.ToString("F1")));
            }
            if (soonest != null && soonestDays < 1.5f && service == 0)
            {
                recs.Add("RCDC_AiRec_Soon".Translate(soonest.parent.LabelShort));
            }
            if (network != null && RcdcUpgrades.Current.CertifiedPriceBonus > 0f && !network.IsCertified)
            {
                recs.Add("RCDC_AiRec_Certify".Translate());
            }
            if (ai.Rapport < 35f)
            {
                recs.Add("RCDC_AiRec_Rapport".Translate());
            }
            if (recs.Count == 0)
            {
                recs.Add("RCDC_AiRec_AllGood".Translate());
            }
            for (int i = 0; i < recs.Count && i < 3; i++)
            {
                sb.AppendLine("- " + recs[i]);
            }
            return sb.ToString().TrimEnd();
        }

        public static void Show(CompAiCore ai)
        {
            Find.WindowStack.Add(new Dialog_MessageBox(Build(ai), "OK".Translate(), null, null, null, "RCDC_AiReportTitle".Translate(AiVoice.Name)));
        }
    }
}
