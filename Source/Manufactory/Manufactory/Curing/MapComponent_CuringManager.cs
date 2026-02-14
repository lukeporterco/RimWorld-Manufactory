using System;
using System.Collections.Generic;
using Manufactory.Diagnostics;
using RimWorld;
using Verse;

namespace Manufactory.Curing
{
    public class MapComponent_CuringManager : MapComponent
    {
        private readonly List<int> dueTerrainBuffer = new List<int>();
        private readonly List<int> dueThingBuffer = new List<int>();
        private readonly Dictionary<int, Thing> wetWallsById = new Dictionary<int, Thing>();
        private readonly Dictionary<string, TerrainDef> curedTerrainCache = new Dictionary<string, TerrainDef>();

        private Dictionary<int, int> pendingTerrainCures = new Dictionary<int, int>();
        private Dictionary<int, int> pendingThingCures = new Dictionary<int, int>();
        private ThingDef wetWallDefCached;
        private ThingDef curedWallDefCached;

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
                if (this.pendingTerrainCures == null)
                {
                    this.pendingTerrainCures = new Dictionary<int, int>();
                }

                if (this.pendingThingCures == null)
                {
                    this.pendingThingCures = new Dictionary<int, int>();
                }

                this.ReconcileLoadedWetWalls();
            }
        }

        public override void MapComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            if ((currentTick + this.map.uniqueID) % 250 != 0)
            {
                return;
            }

            this.ProcessTerrainCures(currentTick);
            this.ProcessThingCures(currentTick);
            ManufactoryPerf.ReportIfDue(currentTick);
        }

        public void RegisterWetTerrain(Map map, IntVec3 c)
        {
            if (map == null || map != this.map || !c.InBounds(map))
            {
                return;
            }

            TerrainDef terrainDef = map.terrainGrid.TerrainAt(c);
            CuringSettingsExtension curingSettings = terrainDef?.GetModExtension<CuringSettingsExtension>();
            if (curingSettings == null || string.IsNullOrEmpty(curingSettings.curedTerrainDefName))
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

            ThingDef wetWallDef = this.GetWetWallDef();
            if (wetWallDef == null || thing.def != wetWallDef)
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
            long perfStamp = ManufactoryPerf.Begin();
            try
            {
                if (this.pendingTerrainCures.Count == 0)
                {
                    return;
                }

                this.dueTerrainBuffer.Clear();
                foreach (KeyValuePair<int, int> pair in this.pendingTerrainCures)
                {
                    if (pair.Value <= currentTick)
                    {
                        this.dueTerrainBuffer.Add(pair.Key);
                    }
                }

                for (int i = 0; i < this.dueTerrainBuffer.Count; i++)
                {
                    int cellIndex = this.dueTerrainBuffer[i];
                    IntVec3 cell = CellIndicesUtility.IndexToCell(cellIndex, this.map.Size.x);

                    if (cell.InBounds(this.map))
                    {
                        TerrainDef currentTerrain = this.map.terrainGrid.TerrainAt(cell);
                        CuringSettingsExtension curingSettings = currentTerrain?.GetModExtension<CuringSettingsExtension>();
                        if (curingSettings != null && !string.IsNullOrEmpty(curingSettings.curedTerrainDefName))
                        {
                            TerrainDef curedTerrain = this.GetCuredTerrainDef(curingSettings.curedTerrainDefName);
                            if (curedTerrain == null)
                            {
                                Log.Warning($"[Manufactory] Missing cured terrain def '{curingSettings.curedTerrainDefName}' for wet terrain '{currentTerrain.defName}'.");
                            }
                            else
                            {
                                this.map.terrainGrid.SetTerrain(cell, curedTerrain);
                            }
                        }
                    }

                    this.pendingTerrainCures.Remove(cellIndex);
                }
            }
            finally
            {
                ManufactoryPerf.End("MapComponent_CuringManager.ProcessTerrainCures", perfStamp);
            }
        }

        private void ProcessThingCures(int currentTick)
        {
            long perfStamp = ManufactoryPerf.Begin();
            try
            {
                if (this.pendingThingCures.Count == 0)
                {
                    return;
                }

                ThingDef curedWallDef = this.GetCuredWallDef();
                if (curedWallDef == null)
                {
                    Log.Warning($"[Manufactory] Missing cured wall def '{CuringDefs.CuredConcreteWallDefName}'.");
                    this.pendingThingCures.Clear();
                    return;
                }

                this.dueThingBuffer.Clear();
                foreach (KeyValuePair<int, int> pair in this.pendingThingCures)
                {
                    if (pair.Value <= currentTick)
                    {
                        this.dueThingBuffer.Add(pair.Key);
                    }
                }

                if (this.dueThingBuffer.Count == 0)
                {
                    return;
                }

                ThingDef wetWallDef = this.GetWetWallDef();
                if (wetWallDef == null)
                {
                    Log.Warning($"[Manufactory] Missing wet wall def '{CuringDefs.WetConcreteWallDefName}'.");
                    this.pendingThingCures.Clear();
                    return;
                }

                this.wetWallsById.Clear();
                List<Thing> wetWalls = this.map.listerThings.ThingsOfDef(wetWallDef);
                for (int i = 0; i < wetWalls.Count; i++)
                {
                    Thing wetWall = wetWalls[i];
                    if (wetWall != null)
                    {
                        this.wetWallsById[wetWall.thingIDNumber] = wetWall;
                    }
                }

                for (int i = 0; i < this.dueThingBuffer.Count; i++)
                {
                    int thingId = this.dueThingBuffer[i];
                    this.wetWallsById.TryGetValue(thingId, out Thing wetWall);

                    if (wetWall != null && wetWall.Spawned && wetWall.def == wetWallDef)
                    {
                        this.ReplaceWetWall(wetWall, curedWallDef);
                    }

                    this.pendingThingCures.Remove(thingId);
                }
            }
            finally
            {
                ManufactoryPerf.End("MapComponent_CuringManager.ProcessThingCures", perfStamp);
            }
        }

        private void ReconcileLoadedWetWalls()
        {
            ThingDef wetWallDef = this.GetWetWallDef();
            if (wetWallDef == null)
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int dueTick = currentTick + this.GetCureTicks(wetWallDef);
            List<Thing> wetWalls = this.map.listerThings.ThingsOfDef(wetWallDef);
            for (int i = 0; i < wetWalls.Count; i++)
            {
                Thing wetWall = wetWalls[i];
                if (wetWall == null)
                {
                    continue;
                }

                int thingId = wetWall.thingIDNumber;
                if (!this.pendingThingCures.ContainsKey(thingId))
                {
                    this.pendingThingCures[thingId] = dueTick;
                }
            }
        }

        private TerrainDef GetCuredTerrainDef(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }

            if (this.curedTerrainCache.TryGetValue(defName, out TerrainDef cached))
            {
                return cached;
            }

            TerrainDef terrainDef = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
            this.curedTerrainCache[defName] = terrainDef;
            return terrainDef;
        }

        private ThingDef GetWetWallDef()
        {
            if (this.wetWallDefCached == null)
            {
                this.wetWallDefCached = DefDatabase<ThingDef>.GetNamedSilentFail(CuringDefs.WetConcreteWallDefName);
            }

            return this.wetWallDefCached;
        }

        private ThingDef GetCuredWallDef()
        {
            if (this.curedWallDefCached == null)
            {
                this.curedWallDefCached = DefDatabase<ThingDef>.GetNamedSilentFail(CuringDefs.CuredConcreteWallDefName);
            }

            return this.curedWallDefCached;
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
