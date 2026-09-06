using HarmonyLib;
using RimWorld;
using Verse;

namespace PeteTimesSix.CategorizedCleaning.HarmonyPatches
{
    /// <summary>Outlines the room hovered in the cleaning tab on the map, the way the inspect pane outlines a selected room.</summary>
    [HarmonyPatch(typeof(MapInterface), nameof(MapInterface.MapInterfaceUpdate))]
    public static class MapInterface_Update_Patches
    {
        [HarmonyPostfix]
        public static void MapInterface_Update_Postfix()
        {
            var room = MainTabWindow_CleaningRooms.hoveredRoom;
            if (room == null || room.Dereferenced || room.Map != Find.CurrentMap)
                return;
            if (!Find.WindowStack.IsOpen<MainTabWindow_CleaningRooms>())
                return;
            room.DrawFieldEdges();
        }
    }
}
