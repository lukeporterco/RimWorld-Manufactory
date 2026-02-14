using System;
using Manufactory.Curing;
using RimWorld;
using Verse;
using Verse.AI;

namespace Manufactory.ConcreteMix
{
    public class WorkGiver_HaulToConcreteMixerInput : WorkGiver_Scanner
    {
        private static ThingDef mixerDef;
        private static JobDef haulJobDef;

        public override ThingRequest PotentialWorkThingRequest => MixerDef != null
            ? ThingRequest.ForDef(MixerDef)
            : ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_ConcreteMixer mixer)
                || mixer.IsForbidden(pawn)
                || !pawn.CanReserve(mixer, 1, -1, null, forced)
                || !pawn.CanReach(mixer, PathEndMode.ClosestTouch, pawn.NormalMaxDanger()))
            {
                return false;
            }

            CompConcreteMixerFermenter fermenter = mixer.GetComp<CompConcreteMixerFermenter>();
            if (fermenter == null || !fermenter.ProductionEnabled)
            {
                return false;
            }

            return this.FindIngredientForMixer(pawn, mixer, fermenter, forced, out _, out _);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_ConcreteMixer mixer))
            {
                return null;
            }

            CompConcreteMixerFermenter fermenter = mixer.GetComp<CompConcreteMixerFermenter>();
            if (fermenter == null || !fermenter.ProductionEnabled)
            {
                return null;
            }

            if (!this.FindIngredientForMixer(pawn, mixer, fermenter, forced, out Thing ingredient, out int count))
            {
                return null;
            }

            JobDef jobDef = HaulJobDef;
            if (jobDef == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(jobDef, ingredient, mixer);
            job.count = Math.Max(1, Math.Min(ingredient.stackCount, count));
            return job;
        }

        private static ThingDef MixerDef
        {
            get
            {
                if (mixerDef == null)
                {
                    mixerDef = DefDatabase<ThingDef>.GetNamedSilentFail(CuringDefs.ConcreteMixerDefName);
                }

                return mixerDef;
            }
        }

        private static JobDef HaulJobDef
        {
            get
            {
                if (haulJobDef == null)
                {
                    haulJobDef = DefDatabase<JobDef>.GetNamedSilentFail(CuringDefs.ConcreteMixerInputHaulJobDefName);
                }

                return haulJobDef;
            }
        }

        private bool FindIngredientForMixer(
            Pawn pawn,
            Building_ConcreteMixer mixer,
            CompConcreteMixerFermenter fermenter,
            bool forced,
            out Thing ingredient,
            out int count)
        {
            ingredient = null;
            count = 0;

            Thing best = null;
            int bestCount = 0;
            float bestDistance = float.MaxValue;

            int neededFuel = fermenter.NeededChemfuelCount();
            if (neededFuel > 0)
            {
                Thing fuel = this.FindClosestIngredient(pawn, mixer.Position, ThingRequest.ForDef(ThingDefOf.Chemfuel), forced);
                if (fuel != null)
                {
                    best = fuel;
                    bestCount = Math.Min(neededFuel, fuel.stackCount);
                    bestDistance = (fuel.Position - pawn.Position).LengthHorizontalSquared;
                }
            }

            int neededChunks = fermenter.NeededChunkCount();
            if (neededChunks > 0)
            {
                Thing chunk = this.FindClosestIngredient(pawn, mixer.Position, ThingRequest.ForGroup(ThingRequestGroup.Chunk), forced);
                if (chunk != null && chunk.def.IsWithinCategory(ThingCategoryDefOf.StoneChunks))
                {
                    float chunkDistance = (chunk.Position - pawn.Position).LengthHorizontalSquared;
                    if (best == null || chunkDistance < bestDistance)
                    {
                        best = chunk;
                        bestCount = Math.Min(neededChunks, chunk.stackCount);
                        bestDistance = chunkDistance;
                    }
                }
            }

            if (best == null || bestCount <= 0)
            {
                return false;
            }

            ingredient = best;
            count = bestCount;
            return true;
        }

        private Thing FindClosestIngredient(Pawn pawn, IntVec3 mixerPos, ThingRequest request, bool forced)
        {
            return GenClosest.ClosestThingReachable(
                mixerPos,
                pawn.Map,
                request,
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, pawn.NormalMaxDanger()),
                9999f,
                thing => this.IsValidIngredientForPawn(pawn, thing, forced));
        }

        private bool IsValidIngredientForPawn(Pawn pawn, Thing thing, bool forced)
        {
            return thing != null
                && !thing.IsForbidden(pawn)
                && pawn.CanReserve(thing, 1, -1, null, forced)
                && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, forced);
        }
    }
}
