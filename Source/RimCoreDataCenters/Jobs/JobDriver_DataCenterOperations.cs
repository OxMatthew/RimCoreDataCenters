using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCore.DataCenters
{
    /// <summary>A colonist works an operations shift at the console: monitoring and diagnostics for the whole network.</summary>
    public class JobDriver_DataCenterOperations : JobDriver
    {
        private float workDone;

        private Thing ConsoleThing
        {
            get { return job.GetTarget(TargetIndex.A).Thing; }
        }

        private CompOperationsConsole Console
        {
            get { return ConsoleThing == null ? null : ConsoleThing.TryGetComp<CompOperationsConsole>(); }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workDone, "rcdcWorkDone", 0f);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            Thing t = ConsoleThing;
            if (t != null && t.def.hasInteractionCell)
            {
                return pawn.ReserveSittableOrSpot(t.InteractionCell, job, errorOnFailed);
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(delegate
            {
                CompOperationsConsole console = Console;
                string reason;
                return console == null || !console.CanOperate(out reason);
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil work = ToilMaker.MakeToil("RCDC_OperationsWork");
            work.initAction = delegate { workDone = 0f; };
            work.tickAction = delegate
            {
                Pawn actor = work.actor;
                CompOperationsConsole console = Console;
                if (console == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                workDone += DataCenterWork.OperationsSpeed(actor);
                if (actor.skills != null)
                {
                    actor.skills.Learn(SkillDefOf.Intellectual, 0.09f);
                    actor.skills.Learn(SkillDefOf.Construction, 0.02f);
                }
#if RIMWORLD_1_6
                actor.GainComfortFromCellIfPossible(1, true);
#else
                actor.GainComfortFromCellIfPossible(true);
#endif
                if (workDone >= console.Props.shiftWorkAmount)
                {
                    console.CompleteShift(actor);
                    ReadyForNextToil();
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            work.WithEffect(EffecterDefOf.Research, TargetIndex.A);
            work.WithProgressBar(TargetIndex.A, delegate
            {
                CompOperationsConsole console = Console;
                return console == null ? 0f : workDone / console.Props.shiftWorkAmount;
            });
            work.activeSkill = () => SkillDefOf.Intellectual;
            yield return work;
        }
    }
}
