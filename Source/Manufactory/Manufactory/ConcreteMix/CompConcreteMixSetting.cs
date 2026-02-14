using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Manufactory.ConcreteMix
{
    public class CompConcreteMixSetting : ThingComp
    {
        private int settingTicks;

        public CompProperties_ConcreteMixSetting Props => (CompProperties_ConcreteMixSetting)this.props;
        public int SettingTicks => this.settingTicks;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref this.settingTicks, "settingTicks", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.settingTicks = Mathf.Clamp(this.settingTicks, 0, this.GetSetTicks());
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (this.parent.Destroyed)
            {
                return;
            }

            if (this.IsStoredInMixerStorage())
            {
                if (this.settingTicks > 0)
                {
                    this.settingTicks--;
                }

                return;
            }

            if (this.settingTicks >= this.GetSetTicks())
            {
                return;
            }

            this.settingTicks++;
            if (this.settingTicks >= this.GetSetTicks())
            {
                this.ConvertIntoSlag();
            }
        }

        public override string CompInspectStringExtra()
        {
            float percent = this.GetSettingPercent();
            return "Setting: " + percent.ToStringPercent();
        }

        public void SetSettingTicks(int ticks)
        {
            this.settingTicks = Mathf.Clamp(ticks, 0, this.GetSetTicks());
        }

        // Manual test checklist:
        // 1) Spawn concrete mix and let 60,000 ticks pass -> becomes concrete slag.
        // 2) Store concrete mix inside concrete mixer -> progress pauses at current value.
        // 3) Move it into a freezer -> progress still increases.
        // 4) Split and merge stacks -> split keeps same ticks, merge keeps max ticks.
        private void ConvertIntoSlag()
        {
            ThingDef slagDef = DefDatabase<ThingDef>.GetNamedSilentFail(this.Props.slagDefName);
            if (slagDef == null)
            {
                Log.Warning($"[Manufactory] Missing concrete slag def '{this.Props.slagDefName}'.");
                return;
            }

            int stackCount = this.parent.stackCount;
            Map mapHeld = this.parent.MapHeld;
            IntVec3 positionHeld = this.parent.PositionHeld;
            Rot4 rotation = this.parent.Rotation;

            Thing slag = ThingMaker.MakeThing(slagDef);
            slag.stackCount = stackCount;

            if (this.parent.Spawned && this.parent.Map != null)
            {
                IntVec3 position = this.parent.Position;
                Map map = this.parent.Map;
                GenSpawn.Spawn(slag, position, map, rotation, WipeMode.Vanish);
                if (!this.parent.Destroyed)
                {
                    this.parent.Destroy(DestroyMode.Vanish);
                }

                return;
            }

            ThingOwner owner = this.parent.ParentHolder?.GetDirectlyHeldThings();
            if (owner != null && owner.Contains(this.parent))
            {
                owner.Remove(this.parent);

                bool converted = owner.TryAdd(slag);
                if (!converted && mapHeld != null && positionHeld.IsValid && positionHeld.InBounds(mapHeld))
                {
                    converted = GenPlace.TryPlaceThing(slag, positionHeld, mapHeld, ThingPlaceMode.Near);
                }

                if (converted)
                {
                    if (!this.parent.Destroyed)
                    {
                        this.parent.Destroy(DestroyMode.Vanish);
                    }
                }
                else
                {
                    // Roll back so mix never disappears if conversion placement fails.
                    owner.TryAdd(this.parent, true);
                    if (!slag.Destroyed)
                    {
                        slag.Destroy(DestroyMode.Vanish);
                    }
                }

                return;
            }

            if (mapHeld != null && positionHeld.IsValid && positionHeld.InBounds(mapHeld))
            {
                if (GenPlace.TryPlaceThing(slag, positionHeld, mapHeld, ThingPlaceMode.Near))
                {
                    this.parent.Destroy(DestroyMode.Vanish);
                }
                else if (!slag.Destroyed)
                {
                    slag.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private void TryDropOrDestroy(Thing thing, IntVec3 position, Map map)
        {
            if (map != null && position.IsValid && position.InBounds(map))
            {
                GenPlace.TryPlaceThing(thing, position, map, ThingPlaceMode.Near);
                return;
            }

            thing.Destroy(DestroyMode.Vanish);
        }

        private bool IsStoredInMixerStorage()
        {
            if (this.parent.Spawned && this.parent.Map != null)
            {
                SlotGroup slotGroup = this.parent.Position.GetSlotGroup(this.parent.Map);
                if (slotGroup?.parent is Building_ConcreteMixer mixer && mixer.IsReversalPowered())
                {
                    return true;
                }
            }

            // Legacy support for old mixer saves that still hold things in innerContainer.
            IThingHolder holder = this.parent.ParentHolder;
            while (holder != null)
            {
                Building_ConcreteMixer concreteMixer = holder as Building_ConcreteMixer;
                if (concreteMixer != null &&
                    concreteMixer.def != null &&
                    concreteMixer.def.defName == this.Props.mixerDefName &&
                    concreteMixer.IsReversalPowered())
                {
                    return true;
                }

                holder = holder.ParentHolder;
            }

            return false;
        }

        private float GetSettingPercent()
        {
            int totalTicks = this.GetSetTicks();
            if (totalTicks <= 0)
            {
                return 1f;
            }

            return Mathf.Clamp01((float)this.settingTicks / totalTicks);
        }

        private int GetSetTicks()
        {
            return Math.Max(1, this.Props.setTicks);
        }
    }
}
