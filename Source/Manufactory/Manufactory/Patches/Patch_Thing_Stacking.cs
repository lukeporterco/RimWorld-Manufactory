using System;
using HarmonyLib;
using Verse;
using Manufactory.ConcreteMix;

namespace Manufactory.Patches
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    public static class Patch_Thing_SplitOff
    {
        public static void Postfix(Thing __instance, Thing __result)
        {
            if (__instance == null || __result == null)
            {
                return;
            }

            CompConcreteMixSetting sourceComp = __instance.TryGetComp<CompConcreteMixSetting>();
            CompConcreteMixSetting splitComp = __result.TryGetComp<CompConcreteMixSetting>();
            if (sourceComp == null || splitComp == null)
            {
                return;
            }

            splitComp.SetSettingTicks(sourceComp.SettingTicks);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    public static class Patch_Thing_TryAbsorbStack
    {
        public static void Postfix(Thing __instance, Thing other, bool __result)
        {
            if (__instance == null || other == null || !__result)
            {
                return;
            }

            CompConcreteMixSetting targetComp = __instance.TryGetComp<CompConcreteMixSetting>();
            CompConcreteMixSetting sourceComp = other.TryGetComp<CompConcreteMixSetting>();
            if (targetComp == null || sourceComp == null)
            {
                return;
            }

            targetComp.SetSettingTicks(Math.Max(targetComp.SettingTicks, sourceComp.SettingTicks));
        }
    }
}
