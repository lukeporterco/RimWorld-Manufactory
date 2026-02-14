using HarmonyLib;
using RimWorld;
using Verse;

namespace Manufactory.Patches
{
    [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.CopyFrom))]
    public static class Patch_StorageSettings_CopyFrom
    {
        [HarmonyPrefix]
        public static bool Prefix(StorageSettings __instance, StorageSettings other)
        {
            if (other != null)
            {
                return true;
            }

            Log.Warning("[Manufactory] Skipped StorageSettings.CopyFrom because source settings were null.");
            return false;
        }
    }
}
