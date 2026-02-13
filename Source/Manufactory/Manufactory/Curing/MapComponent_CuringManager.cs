using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Manufactory.Curing
{
    public class MapComponent_CuringManager : MapComponent
    {
        private Dictionary<int, int> pendingTerrainCures = new Dictionary<int, int>();
        private Dictionary<int, int> pendingThingCures = new Dictionary<int, int>();

        public MapComponent_CuringManager(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref this.pendingTerrainCures, "pendingTerrainCures", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref this.pendingThingCures, "pendingThingCures", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.pendingTerrainCures == null) this.pendingTerrainCures = new Dictionary<int, int>();
                if (this.pendingThingCures == null) this.pendingThingCures = new Dictionary<int, int>();

            }
        }

        public override void MapComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick % 250 != 0)
            {
                return;
            }

            this.ProcessTerrainCures(currentTick);
            this.ProcessThingCures(currentTick);
        }

        public void RegisterWetTerrain(Map map, IntVec3 c)
        {
            if (map == null || map != this.map || !c.InBounds(map))
            {
                return;
            }

            TerrainDef terrainDef = map.terrainGrid.TerrainAt(c);
            if (terrainDef == null || terrainDef.defName != CuringDefs.WetConcreteTerrainDefName)
            {
                return;
            }

            int cellIndex = map.cellIndices.CellToIndex(c);
            if (!this.pendingTerrainCures.ContainsKey(cellIndex))
            {
                int cureTicks = this.GetCureTicks(terrainDef);
                this.pendingTerrainCures[cellIndex] = Find.TickManager.TicksGame + cureTicks;
            }
        }

        public void RegisterWetThing(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map != this.map)
            {
                return;
            }

            if (thing.def?.defName != CuringDefs.WetConcreteWallDefName)
            {
                return;
            }

            int thingId = thing.thingIDNumber;
            if (!this.pendingThingCures.ContainsKey(thingId))
            {
                int cureTicks = this.GetCureTicks(thing.def);
                this.pendingThingCures[thingId] = Find.TickManager.TicksGame + cureTicks;
            }
        }

        public bool TryGetRemainingThingCureTicks(Thing thing, out int remainingTicks)
        {
            remainingTicks = 0;
            if (thing == null || thing.Map != this.map)
            {
                return false;
            }

            if (!this.pendingThingCures.TryGetValue(thing.thingIDNumber, out int dueTick))
            {
                return false;
            }

            remainingTicks = Math.Max(0, dueTick - Find.TickManager.TicksGame);
            return true;
        }

        private void ProcessTerrainCures(int currentTick)
        {
            if (this.pendingTerrainCures.Count == 0)
            {
                return;
            }

            TerrainDef curedTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail(CuringDefs.CuredConcreteTerrainDefName);
            if (curedTerrain == null)
            {
                Log.Warning($"[Manufactory] Missing cured terrain def '{CuringDefs.CuredConcreteTerrainDefName}'.");
                this.pendingTerrainCures.Clear();
                return;
            }

            List<int> dueCellIndices = this.pendingTerrainCures
                .Where(pair => pair.Value <= currentTick)
                .Select(pair => pair.Key)
                .ToList();

            for (int i = 0; i < dueCellIndices.Count; i++)
            {
                int cellIndex = dueCellIndices[i];
                IntVec3 cell = CellIndicesUtility.IndexToCell(cellIndex, this.map.Size.x);

                if (cell.InBounds(this.map))
                {
                    TerrainDef currentTerrain = this.map.terrainGrid.TerrainAt(cell);
                    if (currentTerrain != null && currentTerrain.defName == CuringDefs.WetConcreteTerrainDefName)
                    {
                        this.map.terrainGrid.SetTerrain(cell, curedTerrain);
                    }
                }

                this.pendingTerrainCures.Remove(cellIndex);
            }
        }

        private void ProcessThingCures(int currentTick)
        {
            if (this.pendingThingCures.Count == 0)
            {
                return;
            }

            ThingDef curedWallDef = DefDatabase<ThingDef>.GetNamedSilentFail(CuringDefs.CuredConcreteWallDefName);
            if (curedWallDef == null)
            {
                Log.Warning($"[Manufactory] Missing cured wall def '{CuringDefs.CuredConcreteWallDefName}'.");
                this.pendingThingCures.Clear();
                return;
            }

            List<int> dueThingIds = this.pendingThingCures
                .Where(pair => pair.Value <= currentTick)
                .Select(pair => pair.Key)
                .ToList();

            ThingDef wetWallDef = DefDatabase<ThingDef>.GetNamedSilentFail(CuringDefs.WetConcreteWallDefName);
            if (wetWallDef == null)
            {
                Log.Warning($"[Manufactory] Missing wet wall def '{CuringDefs.WetConcreteWallDefName}'.");
                this.pendingThingCures.Clear();
                return;
            }

            // Build an ID index from only relevant things instead of scanning all map things per due cure.
            Dictionary<int, Thing> wetWallsById = this.BuildThingIndexForDef(wetWallDef);

            for (int i = 0; i < dueThingIds.Count; i++)
            {
                int thingId = dueThingIds[i];
                wetWallsById.TryGetValue(thingId, out Thing wetWall);

                if (wetWall != null &&
                    wetWall.Spawned &&
                    wetWall.def?.defName == CuringDefs.WetConcreteWallDefName)
                {
                    this.ReplaceWetWall(wetWall, curedWallDef);
                }

                this.pendingThingCures.Remove(thingId);
            }
        }

        private Dictionary<int, Thing> BuildThingIndexForDef(ThingDef def)
        {
            Dictionary<int, Thing> result = new Dictionary<int, Thing>();
            if (def == null)
            {
                return result;
            }

            List<Thing> things = this.map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null)
                {
                    result[thing.thingIDNumber] = thing;
                }
            }

            return result;
        }

        private int GetCureTicks(Def wetDef)
        {
            CuringSettingsExtension extension = wetDef?.GetModExtension<CuringSettingsExtension>();
            if (extension != null && extension.cureTicks > 0)
            {
                return extension.cureTicks;
            }

            return CuringDefs.DefaultCureTicks;
        }

        private void ReplaceWetWall(Thing wetWall, ThingDef curedWallDef)
        {
            Thing curedWall = curedWallDef.MadeFromStuff
                ? ThingMaker.MakeThing(curedWallDef, wetWall.Stuff)
                : ThingMaker.MakeThing(curedWallDef);

            float hpPercent = wetWall.MaxHitPoints > 0
                ? (float)wetWall.HitPoints / wetWall.MaxHitPoints
                : 1f;
            curedWall.SetFaction(wetWall.Faction);
            int targetHp = GenMath.RoundRandom(hpPercent * curedWall.MaxHitPoints);
            curedWall.HitPoints = Math.Max(1, Math.Min(targetHp, curedWall.MaxHitPoints));

            CompQuality wetQuality = wetWall.TryGetComp<CompQuality>();
            CompQuality curedQuality = curedWall.TryGetComp<CompQuality>();
            if (wetQuality != null && curedQuality != null)
            {
                curedQuality.SetQuality(wetQuality.Quality, ArtGenerationContext.Outsider);
            }

            CompColorable wetColor = wetWall.TryGetComp<CompColorable>();
            CompColorable curedColor = curedWall.TryGetComp<CompColorable>();
            if (wetColor != null && curedColor != null)
            {
                curedColor.SetColor(wetColor.Color);
            }

            GenSpawn.Spawn(curedWall, wetWall.Position, this.map, wetWall.Rotation, WipeMode.Vanish);
            if (!wetWall.Destroyed)
            {
                wetWall.Destroy(DestroyMode.Vanish);
            }
        }
    }
}
