using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCore.DataCenters
{
    /// <summary>
    /// A colonist fetches a replacement part (target B, optional), then services a worn server
    /// rack (target A). Progress is saved so an interrupted job survives save/load.
    /// </summary>
    public class JobDriver_ServiceRack : JobDriver
    {
        private float workDone;

        private Thing RackThing
        {
            get { return job.GetTarget(TargetIndex.A).Thing; }
        }

        private CompServerRack Rack
        {
            get { return RackThing == null ? null : RackThing.TryGetComp<CompServerRack>(); }
        }

        private bool NeedsPart
        {
            get { return job.targetB.IsValid && job.targetB.HasThing; }
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
            if (NeedsPart && !pawn.Reserve(job.targetB, job, 1, job.count, null, errorOnFailed))
            {
                return false;
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(delegate
            {
                CompServerRack rack = Rack;
                return rack == null || !rack.NeedsService(job.playerForced);
            });

            if (NeedsPart)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                    .FailOnDespawnedNullOrForbidden(TargetIndex.B);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B)
                    .FailOnDestroyedNullOrForbidden(TargetIndex.B);
            }

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil work = ToilMaker.MakeToil("RCDC_ServiceWork");
            work.initAction = delegate { workDone = 0f; };
            work.tickAction = delegate
            {
                Pawn actor = work.actor;
                CompServerRack rack = Rack;
                if (rack == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                actor.rotationTracker.FaceTarget(RackThing);
                workDone += DataCenterWork.ServiceSpeed(actor);
                if (actor.skills != null)
                {
                    actor.skills.Learn(SkillDefOf.Construction, 0.08f);
                    actor.skills.Learn(SkillDefOf.Intellectual, 0.03f);
                }
                if (workDone >= rack.Props.serviceWorkTicks)
                {
                    ReadyForNextToil();
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            work.WithEffect(EffecterDefOf.ConstructMetal, TargetIndex.A);
            work.WithProgressBar(TargetIndex.A, delegate
            {
                CompServerRack rack = Rack;
                return rack == null ? 0f : workDone / rack.Props.serviceWorkTicks;
            });
            work.activeSkill = () => SkillDefOf.Construction;
            work.handlingFacing = true;
            yield return work;

            Toil finish = ToilMaker.MakeToil("RCDC_ServiceFinish");
            finish.initAction = delegate
            {
                Pawn actor = finish.actor;
                CompServerRack rack = Rack;
                if (rack == null)
                {
                    return;
                }
                if (NeedsPart && actor.carryTracker.CarriedThing != null)
                {
                    actor.carryTracker.DestroyCarriedThing();
                }
                rack.CompleteService(actor);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
