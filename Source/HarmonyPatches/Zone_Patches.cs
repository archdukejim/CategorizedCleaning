using HarmonyLib;
using Verse;

namespace PeteTimesSix.CategorizedCleaning.HarmonyPatches
{
    /// <summary>Cleaning zones route filth by cell, so any cell entering or leaving one is reclassified.</summary>
    [HarmonyPatch(typeof(Zone), nameof(Zone.AddCell))]
    public static class Zone_AddCell_Patches
    {
        [HarmonyPostfix]
        public static void Zone_AddCell_Postfix(Zone __instance, IntVec3 c)
        {
            if (__instance is Zone_Cleaning && Current.ProgramState == ProgramState.Playing)
                __instance.Map?.GetComponent<FilthCache>()?.Notify_CellChanged(c);
        }
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.RemoveCell))]
    public static class Zone_RemoveCell_Patches
    {
        [HarmonyPostfix]
        public static void Zone_RemoveCell_Postfix(Zone __instance, IntVec3 c)
        {
            if (__instance is Zone_Cleaning && Current.ProgramState == ProgramState.Playing)
                __instance.Map?.GetComponent<FilthCache>()?.Notify_CellChanged(c);
        }
    }
}
