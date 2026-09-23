using RimWorld;
using Verse;
using Verse.AI;

namespace RimCore.DataCenters
{
    /// <summary>Offers the recurring "Data Center Operations" shift at an Operations Console.</summary>
    public class WorkGiver_DataCenterOperations : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get { return ThingRequest.ForDef(RcdcDefOf.RCDC_OperationsConsole); }
        }

        public override PathEndMode PathEndMode
        {
            get { return PathEndMode.InteractionCell; }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || t.def != RcdcDefOf.RCDC_OperationsConsole)
            {
                return false;
            }
            CompOperationsConsole console = t.TryGetComp<CompOperationsConsole>();
            if (console == null)
            {
                return false;
            }
            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            string reason;
            if (!console.CanOperate(out reason))
            {
                if (forced && reason != null)
                {
                    JobFailReason.Is(reason.Translate());
                }
                return false;
            }
            if (!console.ShiftWanted(forced))
            {
                if (forced)
                {
                    JobFailReason.Is("RCDC_ReasonNoShiftNeeded".Translate());
                }
                return false;
            }
            if (t.def.hasInteractionCell && !pawn.CanReserveSittableOrSpot(t.InteractionCell, forced))
            {
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(RcdcDefOf.RCDC_DataCenterOperations, t);
        }
    }
}
