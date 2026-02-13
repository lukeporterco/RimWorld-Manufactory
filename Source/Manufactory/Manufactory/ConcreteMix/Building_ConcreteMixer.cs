using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;
using Manufactory.Curing;

namespace Manufactory.ConcreteMix
{
    public class Building_ConcreteMixer : Building, IThingHolder
    {
        private ThingOwner<Thing> innerContainer;

        public Building_ConcreteMixer()
        {
            this.innerContainer = new ThingOwner<Thing>(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref this.innerContainer, "innerContainer", this);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (this.innerContainer != null && this.innerContainer.Count > 0 && this.MapHeld != null)
            {
                this.innerContainer.TryDropAll(this.InteractionCell, this.MapHeld, ThingPlaceMode.Near);
            }

            base.Destroy(mode);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "Load nearby mix",
                defaultDesc = "Move nearby concrete mix stacks into the mixer.",
                icon = BaseContent.BadTex,
                action = this.LoadNearbyConcreteMix
            };

            yield return new Command_Action
            {
                defaultLabel = "Unload mixer",
                defaultDesc = "Drop all stored concrete mix stacks.",
                icon = BaseContent.BadTex,
                action = this.UnloadAllConcreteMix
            };
        }

        public override string GetInspectString()
        {
            string inspect = base.GetInspectString();
            int storedCount = this.CountStoredConcreteMix();
            string mixerText = "Stored mix: " + storedCount;

            return string.IsNullOrEmpty(inspect)
                ? mixerText
                : inspect + "\n" + mixerText;
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return this.innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
        }

        public bool Holds(Thing thing)
        {
            return thing != null && this.innerContainer != null && this.innerContainer.Contains(thing);
        }

        private void LoadNearbyConcreteMix()
        {
            ThingDef concreteMixDef = DefDatabase<ThingDef>.GetNamedSilentFail(CuringDefs.ConcreteMixDefName);
            if (concreteMixDef == null || this.Map == null)
            {
                Messages.Message("Concrete mix def is missing.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            CellRect scanRect = this.OccupiedRect().ExpandedBy(1).ClipInsideMap(this.Map);
            List<Thing> nearbyMixStacks = this.Map.listerThings.ThingsOfDef(concreteMixDef)
                .Where(thing => thing != null && thing.Spawned && scanRect.Contains(thing.Position))
                .ToList();

            int movedCount = 0;
            for (int i = 0; i < nearbyMixStacks.Count; i++)
            {
                Thing thing = nearbyMixStacks[i];
                int stackCount = thing.stackCount;
                IntVec3 originalPos = thing.Position;
                Rot4 originalRot = thing.Rotation;

                thing.DeSpawn(DestroyMode.Vanish);
                if (this.innerContainer.TryAdd(thing, true))
                {
                    movedCount += stackCount;
                }
                else
                {
                    GenSpawn.Spawn(thing, originalPos, this.Map, originalRot);
                }
            }

            if (movedCount > 0)
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                Messages.Message($"Loaded {movedCount} concrete mix.", MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Messages.Message("No nearby concrete mix found.", MessageTypeDefOf.RejectInput, false);
            }
        }

        private void UnloadAllConcreteMix()
        {
            if (this.innerContainer == null || this.innerContainer.Count == 0 || this.Map == null)
            {
                Messages.Message("Mixer is empty.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            this.innerContainer.TryDropAll(this.InteractionCell, this.Map, ThingPlaceMode.Near);
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }

        private int CountStoredConcreteMix()
        {
            if (this.innerContainer == null)
            {
                return 0;
            }

            ThingDef concreteMixDef = DefDatabase<ThingDef>.GetNamedSilentFail(CuringDefs.ConcreteMixDefName);
            if (concreteMixDef == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < this.innerContainer.Count; i++)
            {
                Thing thing = this.innerContainer[i];
                if (thing.def == concreteMixDef)
                {
                    count += thing.stackCount;
                }
            }

            return count;
        }
    }
}
