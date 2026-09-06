using HarmonyLib;
using System;
using Verse;

namespace PeteTimesSix.CategorizedCleaning.HarmonyPatches
{
    /// <summary>
    /// Only the Home area notifies anyone when a cell is painted. Our own painted areas matter too, so every
    /// area edit reclassifies the filth in the touched cell (cheap: one cell's thing list).
    /// </summary>
    [HarmonyPatch(typeof(Area), "Set", new Type[] { typeof(IntVec3), typeof(bool) })]
    public static class Area_Set_Patches
    {
        [HarmonyPostfix]
        public static void Area_Set_Postfix(Area __instance, IntVec3 c)
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;
            var map = __instance.areaManager?.map;
            if (map == null || map.thingGrid == null)
                return;
            map.GetComponent<FilthCache>()?.Notify_CellChanged(c);
        }
    }

    [HarmonyPatch(typeof(Area), nameof(Area.Invert))]
    public static class Area_Invert_Patches
    {
        [HarmonyPostfix]
        public static void Area_Invert_Postfix(Area __instance)
        {
            Area_Patches_Shared.RebuildFor(__instance);
        }
    }

    [HarmonyPatch(typeof(Area), nameof(Area.Clear))]
    public static class Area_Clear_Patches
    {
        [HarmonyPostfix]
        public static void Area_Clear_Postfix(Area __instance)
        {
            Area_Patches_Shared.RebuildFor(__instance);
        }
    }

    public static class Area_Patches_Shared
    {
        public static void RebuildFor(Area area)
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;
            var map = area.areaManager?.map;
            if (map == null || map.thingGrid == null)
                return;
            map.GetComponent<FilthCache>()?.RebuildAll();
        }
    }
}
