using LudeonTK;
using System.Text;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    public static partial class DebugMenuEntries
    {
        [DebugAction(category = CATEGORY, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        static void LogFilthCategoryCounts()
        {
            var map = Find.CurrentMap;
            var cache = map.GetComponent<FilthCache>();
            var sb = new StringBuilder();
            sb.AppendLine($"CC: filth by category on map {map.uniqueID}:");
            foreach (var def in CleaningCategoryDef.All)
            {
                var area = cache.GetArea(def);
                sb.AppendLine($"  {def.defName} ({def.label}) rooms={CategorizedCleaning_Settings.RolesFor(def).Count} painted={(area == null ? 0 : area.TrueCount)} cells: {cache.CountFor(def)} filth; workType={def.WorkType?.defName} giver={def.workGiver?.defName}");
            }
            sb.AppendLine($"  total filth on map: {map.listerThings.ThingsInGroup(ThingRequestGroup.Filth).Count}, in home area (vanilla lister): {map.listerFilthInHomeArea.FilthInHomeArea.Count}");
            Log.Message(sb.ToString());
        }

        [DebugAction(category = CATEGORY, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        static void ClassifyCellFilth()
        {
            var map = Find.CurrentMap;
            var cell = UI.MouseCell();
            var cache = map.GetComponent<FilthCache>();
            var room = cell.GetRoom(map);
            var category = cache.Classify(cell, room);
            Log.Message($"CC: cell {cell} room={(room == null ? "none" : room.Role?.defName ?? "?")} outdoors={(room?.PsychologicallyOutdoors ?? true)} painted={(cache.PaintedCategoryAt(cell)?.defName ?? "no")} -> {(category?.defName ?? "unclaimed")}");
        }
    }
}
