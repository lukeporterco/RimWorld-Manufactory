using HarmonyLib;
using System.Linq;
using System.Reflection;
using Verse;
using Manufactory.Curing;

namespace Manufactory.Patches
{
    [HarmonyPatch]
    public static class Patch_TerrainGrid_SetTerrain
    {
        public static MethodBase TargetMethod()
        {
            MethodInfo[] methods = AccessTools.GetDeclaredMethods(typeof(TerrainGrid))
                .Where(m => m.Name == nameof(TerrainGrid.SetTerrain))
                .ToArray();

            return methods.FirstOrDefault(m =>
            {
                ParameterInfo[] parameters = m.GetParameters();
                return parameters.Length >= 2 &&
                       parameters[0].ParameterType == typeof(IntVec3) &&
                       parameters[1].ParameterType == typeof(TerrainDef);
            });
        }

        public static void Postfix(TerrainGrid __instance, IntVec3 c, TerrainDef newTerr)
        {
            CuringSettingsExtension curingSettings = newTerr?.GetModExtension<CuringSettingsExtension>();
            if (curingSettings == null || string.IsNullOrEmpty(curingSettings.curedTerrainDefName))
            {
                return;
            }

            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                return;
            }

            Traverse terrainGridTraverse = Traverse.Create(__instance);
            Map map = terrainGridTraverse.Property("Map").GetValue<Map>() ??
                      terrainGridTraverse.Field("map").GetValue<Map>();
            if (map == null)
            {
                return;
            }

            MapComponent_CuringManager manager = map.GetComponent<MapComponent_CuringManager>();
            manager?.RegisterWetTerrain(map, c);
        }
    }
}
