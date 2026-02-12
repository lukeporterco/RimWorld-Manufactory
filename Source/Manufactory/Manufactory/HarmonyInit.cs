using HarmonyLib;
using Verse;

namespace Manufactory
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            Harmony harmony = new Harmony("Manufactory.Curing");
            harmony.PatchAll();
        }
    }
}
