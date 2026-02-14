using RimWorld;
using UnityEngine;
using Verse;

namespace Manufactory.ConcreteMix
{
    public class Thing_ConcreteMix : ThingWithComps
    {
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (this.ShouldRender())
            {
                base.DrawAt(drawLoc, flip);
            }
        }

        public override void Print(SectionLayer layer)
        {
            if (this.ShouldRender())
            {
                base.Print(layer);
            }
        }

        public override void DrawGUIOverlay()
        {
            if (this.ShouldRender())
            {
                base.DrawGUIOverlay();
            }
        }

        private bool ShouldRender()
        {
            if (!this.Spawned || this.Map == null)
            {
                return true;
            }

            SlotGroup slotGroup = this.Position.GetSlotGroup(this.Map);
            if (slotGroup?.parent is Building_ConcreteMixer)
            {
                return false;
            }

            var thingsAtCell = this.Position.GetThingList(this.Map);
            for (int i = 0; i < thingsAtCell.Count; i++)
            {
                if (thingsAtCell[i] is Building_ConcreteMixer)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
