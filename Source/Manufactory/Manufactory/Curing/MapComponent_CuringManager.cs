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
                this.pendingTerrainCures[cellIndex] = Find.TickManager.TicksGame + CuringDefs.CURE_TICKS;
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
                this.pendingThingCures[thingId] = Find.TickManager.TicksGame + CuringDefs.CURE_TICKS;
            }
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

            for (int i = 0; i < dueThingIds.Count; i++)
            {
                int thingId = dueThingIds[i];
                Thing wetWall = this.FindThingById(thingId);

                if (wetWall != null &&
                    wetWall.Spawned &&
                    wetWall.def?.defName == CuringDefs.WetConcreteWallDefName)
                {
                    this.ReplaceWetWall(wetWall, curedWallDef);
                }

                this.pendingThingCures.Remove(thingId);
            }
        }

        private Thing FindThingById(int thingId)
        {
            List<Thing> allThings = this.map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing != null && thing.thingIDNumber == thingId)
                {
                    return thing;
                }
            }

            return null;
        }

        private void ReplaceWetWall(Thing wetWall, ThingDef curedWallDef)
        {
            Thing curedWall = ThingMaker.MakeThing(curedWallDef, wetWall.Stuff);
            curedWall.SetFaction(wetWall.Faction);
            curedWall.HitPoints = Math.Min(wetWall.HitPoints, curedWall.MaxHitPoints);

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
