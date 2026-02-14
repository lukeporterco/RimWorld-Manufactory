using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Manufactory.ConcreteMix
{
    public class CompConcreteMixerFermenter : ThingComp, IThingHolder
    {
        private ThingOwner<Thing> inputBay;
        private ThingOwner<Thing> mixStore;
        private int ticksRemaining;
        private int pendingMixOutput;
        private bool productionEnabled = true;

        public CompProperties_ConcreteMixerFermenter Props => (CompProperties_ConcreteMixerFermenter)this.props;
        public bool ProductionEnabled => this.productionEnabled;
        public int StoredMixCount => this.CountStoredMixInMixerStorage();
        public bool Active => this.ticksRemaining > 0;
        public int TotalMixEquivalent => this.StoredMixCount + this.CountMixInStore() + this.pendingMixOutput + this.GetPotentialMixFromInputs();

        IThingHolder IThingHolder.ParentHolder => (IThingHolder)this.parent;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            this.inputBay = new ThingOwner<Thing>(this);
            this.mixStore = new ThingOwner<Thing>(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref this.inputBay, "inputBay", this);
            Scribe_Deep.Look(ref this.mixStore, "mixStore", this);
            Scribe_Values.Look(ref this.ticksRemaining, "ticksRemaining", 0);
            Scribe_Values.Look(ref this.pendingMixOutput, "pendingMixOutput", 0);
            Scribe_Values.Look(ref this.productionEnabled, "productionEnabled", true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.inputBay == null)
                {
                    this.inputBay = new ThingOwner<Thing>(this);
                }

                if (this.mixStore == null)
                {
                    this.mixStore = new ThingOwner<Thing>(this);
                }

                this.ticksRemaining = Math.Max(0, this.ticksRemaining);
                this.pendingMixOutput = Math.Max(0, this.pendingMixOutput);
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!(this.parent is Building_ConcreteMixer mixer) || !mixer.IsReversalPowered() || !this.productionEnabled)
            {
                return;
            }

            if (this.ticksRemaining > 0)
            {
                this.ticksRemaining--;
                if (this.ticksRemaining <= 0 && this.pendingMixOutput > 0)
                {
                    this.CompleteBatch();
                }
            }

            if (this.ticksRemaining <= 0 && this.CanStartBatch())
            {
                this.StartBatch();
            }
        }

        public override string CompInspectStringExtra()
        {
            int chunks = this.CountInputChunks();
            int fuel = this.CountInputChemfuel();

            string progress = this.ticksRemaining > 0
                ? (1f - ((float)this.ticksRemaining / Math.Max(1, this.Props.batchDurationTicks))).ToStringPercent()
                : "idle";

            return $"Stored mix: {this.StoredMixCount} / {this.GetMaxMixCapacity()}\nInputs: chunks {chunks}/{this.GetMaxInputChunks()}, chemfuel {fuel}/{this.GetMaxInputChemfuel()}\nProgress: {progress}\nProduction: {(this.productionEnabled ? "enabled" : "disabled")}";
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Toggle
            {
                defaultLabel = "Toggle mix production",
                defaultDesc = "Enable or disable automatic concrete mix production. Mix setting reversal remains independent.",
                isActive = () => this.productionEnabled,
                toggleAction = delegate { this.productionEnabled = !this.productionEnabled; }
            };

            yield return new Command_Action
            {
                defaultLabel = "Force mix batch",
                defaultDesc = "Force one concrete mix batch to start even if mixer storage is near/full. Overflow will be ejected if needed.",
                action = this.TryForceStartBatch
            };

            if (this.inputBay != null && this.inputBay.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Extract ingredients",
                    defaultDesc = "Eject hidden ingredient stock from this mixer.",
                    action = this.ExtractIngredientsToMap
                };
            }

            if (this.CountMixInStore() > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Unload hidden mix",
                    defaultDesc = "Drop legacy hidden concrete mix near this mixer.",
                    action = this.UnloadMixToMap
                };
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (mode != DestroyMode.Vanish)
            {
                this.TryDropAllContentsToMap(map);
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            this.TryDropAllContentsToMap(previousMap);
        }

        public bool WantsIngredient(ThingDef def)
        {
            return this.NeededIngredientCount(def) > 0;
        }

        public int NeededChunkCount()
        {
            return this.CalculateNeededForChunk();
        }

        public int NeededChemfuelCount()
        {
            return this.CalculateNeededForChemfuel();
        }

        public int NeededIngredientCount(ThingDef def)
        {
            if (!this.productionEnabled || def == null)
            {
                return 0;
            }

            if (def == ThingDefOf.Chemfuel)
            {
                return this.CalculateNeededForChemfuel();
            }

            if (def.IsWithinCategory(ThingCategoryDefOf.StoneChunks))
            {
                return this.CalculateNeededForChunk();
            }

            return 0;
        }

        public bool TryAddIngredient(Thing ingredient, int count, Pawn hauler)
        {
            if (ingredient == null || ingredient.Destroyed || count <= 0 || !this.productionEnabled)
            {
                return false;
            }

            int needed = this.NeededIngredientCount(ingredient.def);
            if (needed <= 0)
            {
                return false;
            }

            int transferCount = Math.Min(count, ingredient.stackCount);
            transferCount = Math.Min(transferCount, needed);
            if (transferCount <= 0)
            {
                return false;
            }

            ThingOwner sourceContainer = ingredient.ParentHolder?.GetDirectlyHeldThings();
            if (sourceContainer != null && sourceContainer.Contains(ingredient))
            {
                int movedCount = sourceContainer.TryTransferToContainer(ingredient, this.inputBay, transferCount, false);
                return movedCount > 0;
            }

            Thing moved = transferCount == ingredient.stackCount ? ingredient : ingredient.SplitOff(transferCount);
            if (this.inputBay.TryAdd(moved))
            {
                return true;
            }

            if (!moved.Destroyed)
            {
                moved.Destroy(DestroyMode.Vanish);
            }

            return false;
        }

        public void ExtractIngredientsToMap()
        {
            if (this.inputBay == null || this.inputBay.Count == 0 || this.parent?.Map == null)
            {
                return;
            }

            this.inputBay.TryDropAll(this.GetDropCell(), this.parent.Map, ThingPlaceMode.Near);
        }

        public void UnloadMixToMap()
        {
            if (this.mixStore == null || this.mixStore.Count == 0 || this.parent?.Map == null)
            {
                return;
            }

            this.mixStore.TryDropAll(this.GetDropCell(), this.parent.Map, ThingPlaceMode.Near);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return this.inputBay;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwner direct = this.GetDirectlyHeldThings();
            if (direct != null)
            {
                ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, direct);
            }

            if (this.mixStore != null)
            {
                ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.mixStore);
            }
        }

        private bool CanStartBatch()
        {
            if (!this.productionEnabled)
            {
                return false;
            }

            int currentProduced = this.StoredMixCount + this.CountMixInStore() + this.pendingMixOutput;
            if (currentProduced + this.GetMixPerBatch() > this.GetMaxMixCapacity())
            {
                return false;
            }

            return this.CountInputChemfuel() >= this.GetChemfuelPerBatch()
                && this.CountInputChunks() >= this.GetChunkPerBatch();
        }

        private void TryForceStartBatch()
        {
            if (this.ticksRemaining > 0)
            {
                return;
            }

            if (!(this.parent is Building_ConcreteMixer mixer) || !mixer.IsReversalPowered())
            {
                return;
            }

            if (this.CountInputChemfuel() < this.GetChemfuelPerBatch() || this.CountInputChunks() < this.GetChunkPerBatch())
            {
                return;
            }

            this.StartBatch();
        }

        private void StartBatch()
        {
            int chemfuelPerBatch = this.GetChemfuelPerBatch();
            int chunkPerBatch = this.GetChunkPerBatch();

            if (!this.TryConsumeInput(ThingDefOf.Chemfuel, chemfuelPerBatch))
            {
                return;
            }

            if (!this.TryConsumeInputByCategory(ThingCategoryDefOf.StoneChunks, chunkPerBatch))
            {
                Thing chemfuelRefund = ThingMaker.MakeThing(ThingDefOf.Chemfuel);
                chemfuelRefund.stackCount = chemfuelPerBatch;
                if (!this.inputBay.TryAdd(chemfuelRefund))
                {
                    GenPlace.TryPlaceThing(chemfuelRefund, this.GetDropCell(), this.parent.Map, ThingPlaceMode.Near);
                }

                return;
            }

            this.pendingMixOutput += this.GetMixPerBatch();
            this.ticksRemaining = Math.Max(1, this.Props.batchDurationTicks);
        }

        private void CompleteBatch()
        {
            ThingDef mixDef = DefDatabase<ThingDef>.GetNamedSilentFail(this.Props.outputDefName);
            if (mixDef == null)
            {
                Log.Warning($"[Manufactory] Missing concrete mix output def '{this.Props.outputDefName}'.");
                this.pendingMixOutput = 0;
                return;
            }

            int toProduce = this.pendingMixOutput;
            this.pendingMixOutput = 0;
            if (toProduce <= 0)
            {
                return;
            }

            int overflow = this.StoreProducedMixInMixerStorage(mixDef, toProduce);
            if (overflow > 0)
            {
                if (this.parent?.Map != null)
                {
                    Thing overflowThing = ThingMaker.MakeThing(mixDef);
                    overflowThing.stackCount = overflow;
                    GenPlace.TryPlaceThing(overflowThing, this.GetDropCell(), this.parent.Map, ThingPlaceMode.Near);
                }
                else
                {
                    Thing hiddenOverflow = ThingMaker.MakeThing(mixDef);
                    hiddenOverflow.stackCount = overflow;
                    this.mixStore.TryAdd(hiddenOverflow);
                }
            }
        }

        private int StoreProducedMixInMixerStorage(ThingDef mixDef, int count)
        {
            if (count <= 0 || this.parent?.Map == null || !(this.parent is Building_Storage storage))
            {
                return count;
            }

            List<IntVec3> slotCells = storage.AllSlotCellsList();
            if (slotCells == null || slotCells.Count == 0)
            {
                return count;
            }

            int remaining = count;
            int stackLimit = Math.Max(1, mixDef.stackLimit);
            Map map = this.parent.Map;

            // Fill existing concrete mix stacks first.
            for (int i = 0; i < slotCells.Count && remaining > 0; i++)
            {
                List<Thing> things = slotCells[i].GetThingList(map);
                for (int j = 0; j < things.Count && remaining > 0; j++)
                {
                    Thing thing = things[j];
                    if (thing == null || thing.def != mixDef || thing.stackCount >= stackLimit)
                    {
                        continue;
                    }

                    int add = Math.Min(remaining, stackLimit - thing.stackCount);
                    thing.stackCount += add;
                    remaining -= add;
                }
            }

            // Then use any open storage cell for new stacks.
            for (int i = 0; i < slotCells.Count && remaining > 0; i++)
            {
                int spawnCount = Math.Min(remaining, stackLimit);
                Thing stack = ThingMaker.MakeThing(mixDef);
                stack.stackCount = spawnCount;
                if (GenPlace.TryPlaceThing(stack, slotCells[i], map, ThingPlaceMode.Direct))
                {
                    remaining -= spawnCount;
                }
                else if (!stack.Destroyed)
                {
                    stack.Destroy(DestroyMode.Vanish);
                }
            }

            return remaining;
        }

        private void TryDropAllContentsToMap(Map map)
        {
            if (map == null)
            {
                return;
            }

            IntVec3 dropCell = this.GetDropCell();
            this.inputBay?.TryDropAll(dropCell, map, ThingPlaceMode.Near);
            this.mixStore?.TryDropAll(dropCell, map, ThingPlaceMode.Near);
        }

        private IntVec3 GetDropCell()
        {
            if (this.parent == null || this.parent.Map == null)
            {
                return this.parent?.Position ?? IntVec3.Invalid;
            }

            if (this.parent is Building_ConcreteMixer mixer)
            {
                IntVec3 interaction = mixer.InteractionCell;
                if (interaction.IsValid && interaction.InBounds(this.parent.Map))
                {
                    return interaction;
                }
            }

            return this.parent.Position;
        }

        private int GetBatchesWanted()
        {
            int missing = Math.Max(0, this.GetMaxMixCapacity() - this.TotalMixEquivalent);
            if (missing <= 0)
            {
                return 0;
            }

            return Mathf.CeilToInt((float)missing / this.GetMixPerBatch());
        }

        private int GetPotentialMixFromInputs()
        {
            int chunkBatches = this.CountInputChunks() / this.GetChunkPerBatch();
            int fuelBatches = this.CountInputChemfuel() / this.GetChemfuelPerBatch();
            int possibleBatches = Math.Min(chunkBatches, fuelBatches);
            return possibleBatches * this.GetMixPerBatch();
        }

        private int CalculateNeededForChunk()
        {
            if (!this.productionEnabled)
            {
                return 0;
            }

            return Math.Max(0, this.GetMaxInputChunks() - this.CountInputChunks());
        }

        private int CalculateNeededForChemfuel()
        {
            if (!this.productionEnabled)
            {
                return 0;
            }

            return Math.Max(0, this.GetMaxInputChemfuel() - this.CountInputChemfuel());
        }

        private int CountInputChemfuel()
        {
            return this.CountByDef(this.inputBay, ThingDefOf.Chemfuel);
        }

        private int CountInputChunks()
        {
            return this.CountByCategory(this.inputBay, ThingCategoryDefOf.StoneChunks);
        }

        private int CountMixInStore()
        {
            ThingDef mixDef = DefDatabase<ThingDef>.GetNamedSilentFail(this.Props.outputDefName);
            if (mixDef == null)
            {
                return 0;
            }

            return this.CountByDef(this.mixStore, mixDef);
        }

        private int CountStoredMixInMixerStorage()
        {
            if (!(this.parent is Building_ConcreteMixer mixer) || mixer.Map == null)
            {
                return 0;
            }

            SlotGroup slotGroup = mixer.GetSlotGroup();
            if (slotGroup == null)
            {
                return 0;
            }

            ThingDef mixDef = DefDatabase<ThingDef>.GetNamedSilentFail(this.Props.outputDefName);
            if (mixDef == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Thing thing in slotGroup.HeldThings)
            {
                if (thing != null && thing.def == mixDef)
                {
                    count += thing.stackCount;
                }
            }

            return count;
        }

        private int CountByDef(ThingOwner<Thing> owner, ThingDef def)
        {
            if (owner == null || def == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < owner.Count; i++)
            {
                Thing thing = owner[i];
                if (thing != null && thing.def == def)
                {
                    count += thing.stackCount;
                }
            }

            return count;
        }

        private int CountByCategory(ThingOwner<Thing> owner, ThingCategoryDef category)
        {
            if (owner == null || category == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < owner.Count; i++)
            {
                Thing thing = owner[i];
                if (thing?.def != null && thing.def.IsWithinCategory(category))
                {
                    count += thing.stackCount;
                }
            }

            return count;
        }

        private bool TryConsumeInput(ThingDef def, int count)
        {
            return this.TryConsumeWhere(this.inputBay, count, t => t.def == def);
        }

        private bool TryConsumeInputByCategory(ThingCategoryDef category, int count)
        {
            return this.TryConsumeWhere(this.inputBay, count, t => t.def.IsWithinCategory(category));
        }

        private bool TryConsumeWhere(ThingOwner<Thing> owner, int count, Func<Thing, bool> predicate)
        {
            if (owner == null || count <= 0)
            {
                return false;
            }

            int available = 0;
            for (int i = 0; i < owner.Count; i++)
            {
                Thing thing = owner[i];
                if (thing != null && predicate(thing))
                {
                    available += thing.stackCount;
                    if (available >= count)
                    {
                        break;
                    }
                }
            }

            if (available < count)
            {
                return false;
            }

            int remaining = count;
            for (int i = owner.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Thing thing = owner[i];
                if (thing == null || !predicate(thing))
                {
                    continue;
                }

                int consume = Math.Min(remaining, thing.stackCount);
                Thing consumed = thing.SplitOff(consume);
                consumed.Destroy(DestroyMode.Vanish);
                remaining -= consume;
            }

            return remaining <= 0;
        }

        private int GetMixPerBatch()
        {
            return Math.Max(1, this.Props.mixPerBatch);
        }

        private int GetChunkPerBatch()
        {
            return Math.Max(1, this.Props.chunkPerBatch);
        }

        private int GetChemfuelPerBatch()
        {
            return Math.Max(1, this.Props.chemfuelPerBatch);
        }

        private int GetMaxInputChunks()
        {
            return Math.Max(1, this.Props.maxInputChunks);
        }

        private int GetMaxInputChemfuel()
        {
            return Math.Max(1, this.Props.maxInputChemfuel);
        }

        private int GetMaxMixCapacity()
        {
            ThingDef mixDef = DefDatabase<ThingDef>.GetNamedSilentFail(this.Props.outputDefName);
            if (mixDef == null)
            {
                return Math.Max(1, this.Props.maxMixCapacity);
            }

            if (this.parent is Building_Storage storage)
            {
                List<IntVec3> cells = storage.AllSlotCellsList();
                if (cells != null && cells.Count > 0)
                {
                    return cells.Count * Math.Max(1, mixDef.stackLimit);
                }
            }

            return Math.Max(1, this.Props.maxMixCapacity);
        }
    }
}
