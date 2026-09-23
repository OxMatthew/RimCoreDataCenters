using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>
    /// A compact UPS. It is an ordinary battery on the power grid (the game's power net drains it
    /// automatically when supply falls short); this class only adds a charge bar and a live
    /// "runtime at current load" readout so the player can see how long it will hold the room up.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_UpsUnit : Building
    {
        private static readonly Vector2 BarSize = new Vector2(0.8f, 0.14f);
        private static readonly Material BarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.35f, 0.95f, 0.55f));
        private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.16f, 0.17f, 0.19f));

        private CompPowerBattery Battery
        {
            get { return GetComp<CompPowerBattery>(); }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            CompPowerBattery battery = Battery;
            if (battery == null || !Spawned)
            {
                return;
            }
            GenDraw.FillableBarRequest request = default(GenDraw.FillableBarRequest);
            request.center = drawLoc + Vector3.up * 0.1f + new Vector3(0f, 0f, -0.32f);
            request.size = BarSize;
            request.fillPercent = battery.Props.storedEnergyMax > 0f ? battery.StoredEnergy / battery.Props.storedEnergyMax : 0f;
            request.filledMat = BarFilledMat;
            request.unfilledMat = BarUnfilledMat;
            request.margin = 0.05f;
            request.rotation = Rot4.North;
            GenDraw.DrawFillableBar(request);
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder(base.GetInspectString());
            CompPowerBattery battery = Battery;
            if (battery != null && Spawned && battery.PowerNet != null)
            {
                float gainPerTick = battery.PowerNet.CurrentEnergyGainRate();
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                if (gainPerTick < -0.000001f && battery.StoredEnergy > 0f)
                {
                    float hours = battery.StoredEnergy / -gainPerTick / 2500f;
                    sb.Append("RCDC_UpsRuntime".Translate(hours.ToString("F1")));
                }
                else if (gainPerTick > 0.000001f && battery.StoredEnergyPct < 0.999f)
                {
                    sb.Append("RCDC_UpsCharging".Translate());
                }
                else if (battery.StoredEnergyPct >= 0.999f)
                {
                    sb.Append("RCDC_UpsFull".Translate());
                }
                else
                {
                    sb.Append("RCDC_UpsIdle".Translate());
                }
            }
            return sb.ToString();
        }
    }
}
