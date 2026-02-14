using Manufactory.ConcreteMix;
using Manufactory.Curing;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Manufactory.Patches
{
    internal static class ConcreteMixerRenderHider
    {
        private static ThingDef concreteMixDef;

        private static ThingDef ConcreteMixDef
        {
            get
            {
                if (concreteMixDef == null)
                {
                    concreteMixDef = DefDatabase<ThingDef>.GetNamedSilentFail(CuringDefs.ConcreteMixDefName);
                }

                return concreteMixDef;
            }
        }

        internal static bool ShouldRender(Thing thing)
        {
            if (thing == null)
            {
                return true;
            }

            ThingDef mixDef = ConcreteMixDef;
            if (mixDef == null || thing.def != mixDef)
            {
                return true;
            }

            if (!thing.Spawned || thing.Map == null)
            {
                return true;
            }

            SlotGroup slotGroup = thing.Position.GetSlotGroup(thing.Map);
            if (slotGroup?.parent is Building_ConcreteMixer)
            {
                return false;
            }

            var thingsAtCell = thing.Position.GetThingList(thing.Map);
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

    [HarmonyPatch(typeof(Thing), nameof(Thing.DrawNowAt))]
    public static class Patch_Thing_DrawNowAt
    {
        [HarmonyPrefix]
        public static bool Prefix(Thing __instance)
        {
            return ConcreteMixerRenderHider.ShouldRender(__instance);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
    public static class Patch_Thing_Print
    {
        [HarmonyPrefix]
        public static bool Prefix(Thing __instance)
        {
            return ConcreteMixerRenderHider.ShouldRender(__instance);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DrawGUIOverlay))]
    public static class Patch_Thing_DrawGUIOverlay
    {
        [HarmonyPrefix]
        public static bool Prefix(Thing __instance)
        {
            return ConcreteMixerRenderHider.ShouldRender(__instance);
        }
    }

}
