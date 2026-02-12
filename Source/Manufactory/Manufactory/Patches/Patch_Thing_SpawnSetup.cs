using HarmonyLib;
using Verse;
using Manufactory.Curing;

namespace Manufactory.Patches
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    public static class Patch_Thing_SpawnSetup
    {
        public static void Postfix(Thing __instance, Map map, bool respawningAfterLoad)
        {
            if (__instance?.def?.defName != CuringDefs.WetConcreteWallDefName)
            {
                return;
            }

            if (respawningAfterLoad || Scribe.mode != LoadSaveMode.Inactive)
            {
                return;
            }

            MapComponent_CuringManager manager = map?.GetComponent<MapComponent_CuringManager>();
            manager?.RegisterWetThing(__instance);
        }
    }
}
