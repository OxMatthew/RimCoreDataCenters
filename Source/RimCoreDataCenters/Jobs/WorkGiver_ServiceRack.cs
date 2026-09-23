using RimWorld;
using Verse;
using Verse.AI;

namespace RimCore.DataCenters
{
    /// <summary>Offers rack maintenance: fetch a replacement part and service a worn rack.</summary>
    public class WorkGiver_ServiceRack : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get { return ThingRequest.ForDef(RcdcDefOf.RCDC_ServerRack); }
        }

        public override PathEndMode PathEndMode
        {
            get { return PathEndMode.InteractionCell; }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return CanService(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompServerRack rack = t == null ? null : t.TryGetComp<CompServerRack>();
            if (rack == null)
            {
                return null;
            }
            ThingDef partDef = rack.Props.serviceItem;
            if (partDef == null || rack.Props.serviceItemCount <= 0)
            {
                return JobMaker.MakeJob(RcdcDefOf.RCDC_ServiceRack, t);
            }
            Thing part = FindPart(pawn, rack);
            if (part == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(RcdcDefOf.RCDC_ServiceRack, t, part);
            job.count = rack.Props.serviceItemCount;
            return job;
        }

        private static bool CanService(Pawn pawn, Thing t, bool forced)
        {
            if (t == null || t.def != RcdcDefOf.RCDC_ServerRack)
            {
                return false;
            }
            CompServerRack rack = t.TryGetComp<CompServerRack>();
            if (rack == null || !rack.NeedsService(forced))
            {
                return false;
            }
            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            if (t.def.hasInteractionCell && !pawn.CanReserve(t.InteractionCell, 1, -1, null, forced))
            {
                return false;
            }
            ThingDef partDef = rack.Props.serviceItem;
            if (partDef != null && rack.Props.serviceItemCount > 0)
            {
                Thing part = FindPart(pawn, rack);
                if (part == null)
                {
                    if (forced)
                    {
                        JobFailReason.Is("RCDC_ReasonNoServiceParts".Translate(partDef.label, rack.Props.serviceItemCount));
                    }
                    return false;
                }
            }
            return true;
        }

        private static Thing FindPart(Pawn pawn, CompServerRack rack)
        {
            ThingDef partDef = rack.Props.serviceItem;
            int needed = rack.Props.serviceItemCount;
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(partDef),
                PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f,
                x => !x.IsForbidden(pawn) && x.stackCount >= 1 && pawn.CanReserve(x, 1, needed));
        }
    }
}
