using HarmonyLib;
using RimWorld;
using Verse;
using static HarmonyLib.AccessTools;

namespace PeteTimesSix.CategorizedCleaning.HarmonyPatches
{
    /// <summary>
    /// Filth spawn/despawn and the map-load rebuild are only announced to ListerFilthInHomeArea, so we ride along.
    /// Notify_FilthSpawned fires for every filth regardless of area; the Home check happens inside the lister.
    /// Home area edits are covered by <see cref="Area_Set_Patches"/> instead of Notify_HomeAreaChanged.
    /// </summary>
    public static class ListerFilthInHomeArea_MapAccess
    {
        public static FieldRef<ListerFilthInHomeArea, Map> field_map;
        static ListerFilthInHomeArea_MapAccess()
        {
            field_map = FieldRefAccess<ListerFilthInHomeArea, Map>("map");
        }

        public static Map GetMap(this ListerFilthInHomeArea lister) => field_map(lister);
    }

    [HarmonyPatch(typeof(ListerFilthInHomeArea), nameof(ListerFilthInHomeArea.RebuildAll))]
    public static class ListerFilthInHomeArea_RebuildAll_Patches
    {
        [HarmonyPostfix]
        public static void ListerFilthInHomeArea_RebuildAll_Postfix(ListerFilthInHomeArea __instance)
        {
            __instance.GetMap()?.GetComponent<FilthCache>()?.RebuildAll();
        }
    }

    [HarmonyPatch(typeof(ListerFilthInHomeArea), nameof(ListerFilthInHomeArea.Notify_FilthSpawned))]
    public static class ListerFilthInHomeArea_Notify_FilthSpawned_Patches
    {
        [HarmonyPostfix]
        public static void ListerFilthInHomeArea_Notify_FilthSpawned_Postfix(ListerFilthInHomeArea __instance, Filth f)
        {
            __instance.GetMap()?.GetComponent<FilthCache>()?.Notify_FilthSpawned(f);
        }
    }

    [HarmonyPatch(typeof(ListerFilthInHomeArea), nameof(ListerFilthInHomeArea.Notify_FilthDespawned))]
    public static class ListerFilthInHomeArea_Notify_FilthDespawned_Patches
    {
        [HarmonyPostfix]
        public static void ListerFilthInHomeArea_Notify_FilthDespawned_Postfix(ListerFilthInHomeArea __instance, Filth f)
        {
            __instance.GetMap()?.GetComponent<FilthCache>()?.Notify_FilthDespawned(f);
        }
    }
}
