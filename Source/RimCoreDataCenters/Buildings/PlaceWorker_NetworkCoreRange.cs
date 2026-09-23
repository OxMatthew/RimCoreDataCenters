using UnityEngine;
using Verse;

namespace RimCore.DataCenters
{
    /// <summary>Draws the network range while placing a Network Core, so racks can be laid out inside it.</summary>
    public class PlaceWorker_NetworkCoreRange : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            CompProperties_NetworkCore props = def.GetCompProperties<CompProperties_NetworkCore>();
            if (props != null)
            {
                GenDraw.DrawRadiusRing(center, props.range + RcdcUpgrades.Current.ExtraCoreRange);
            }
        }
    }
}
