using System.Collections.Generic;
using System.Text;
using Manufactory.Curing;
using RimWorld;
using Verse;

namespace Manufactory.ConcreteMix
{
    public class Building_ConcreteMixer : Building_Storage, IThingHolder
    {
        // Legacy save container from previous mixer implementation.
        // Items are migrated to map storage slots when the building loads/spawns.
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

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder(base.GetInspectString());
            SlotGroup slotGroup = this.GetSlotGroup();
            if (slotGroup != null)
            {
                int concreteMixCount = 0;
                foreach (Thing thing in slotGroup.HeldThings)
                {
                    if (thing != null && thing.def != null && thing.def.defName == CuringDefs.ConcreteMixDefName)
                    {
                        concreteMixCount += thing.stackCount;
                    }
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append("Stored concrete mix: ").Append(concreteMixCount);
            }

            return sb.ToString();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.TryMigrateLegacyContents();
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return this.innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
        }

        private void TryMigrateLegacyContents()
        {
            if (this.innerContainer == null || this.innerContainer.Count == 0 || this.Map == null)
            {
                return;
            }

            IntVec3 dropCell = this.InteractionCell;
            if (!dropCell.IsValid || !dropCell.InBounds(this.Map))
            {
                dropCell = this.Position;
            }

            this.innerContainer.TryDropAll(dropCell, this.Map, ThingPlaceMode.Near);
        }
    }
}
