using HarmonyLib;
using System;
using RimWorld;
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

    [HarmonyPatch(typeof(Thing), nameof(Thing.GetInspectString))]
    public static class Patch_Thing_GetInspectString
    {
        public static void Postfix(Thing __instance, ref string __result)
        {
            if (__instance?.def?.defName != CuringDefs.WetConcreteWallDefName)
            {
                return;
            }

            if (!__instance.Spawned || __instance.Map == null)
            {
                return;
            }

            MapComponent_CuringManager manager = __instance.Map.GetComponent<MapComponent_CuringManager>();
            if (manager == null || !manager.TryGetRemainingThingCureTicks(__instance, out int remainingTicks))
            {
                return;
            }

            string cureText = "Cures in: " + remainingTicks.ToStringTicksToPeriod();
            __result = string.IsNullOrEmpty(__result)
                ? cureText
                : __result + Environment.NewLine + cureText;
        }
    }
}
