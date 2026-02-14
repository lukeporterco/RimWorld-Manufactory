using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Manufactory.ConcreteMix
{
    public class JobDriver_HaulToConcreteMixerInput : JobDriver
    {
        private const TargetIndex IngredientInd = TargetIndex.A;
        private const TargetIndex MixerInd = TargetIndex.B;

        private Thing Ingredient => this.job.GetTarget(IngredientInd).Thing;
        private Building_ConcreteMixer Mixer => this.job.GetTarget(MixerInd).Thing as Building_ConcreteMixer;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.Ingredient, this.job, 1, -1, null, errorOnFailed)
                && this.pawn.Reserve(this.Mixer, this.job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(IngredientInd);
            this.FailOnDestroyedNullOrForbidden(MixerInd);
            this.FailOn(() => this.Mixer == null || this.Mixer.GetComp<CompConcreteMixerFermenter>() == null);

            yield return Toils_Goto.GotoThing(IngredientInd, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(IngredientInd);
            yield return Toils_Goto.GotoThing(MixerInd, PathEndMode.ClosestTouch);

            Toil placeInInputBay = ToilMaker.MakeToil("PlaceIngredientInMixerInputBay");
            placeInInputBay.initAction = delegate
            {
                Pawn actor = placeInInputBay.actor;
                CompConcreteMixerFermenter fermenter = this.Mixer?.GetComp<CompConcreteMixerFermenter>();
                Thing carried = actor.carryTracker?.CarriedThing;
                if (fermenter == null || carried == null)
                {
                    return;
                }

                int needed = fermenter.NeededIngredientCount(carried.def);
                if (needed <= 0)
                {
                    return;
                }

                int toTransfer = carried.stackCount;
                if (this.job.count > 0)
                {
                    toTransfer = System.Math.Min(toTransfer, this.job.count);
                }

                toTransfer = System.Math.Min(toTransfer, needed);
                if (toTransfer <= 0)
                {
                    return;
                }

                if (!fermenter.TryAddIngredient(carried, toTransfer, actor))
                {
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
            };
            placeInInputBay.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return placeInInputBay;
        }
    }
}
