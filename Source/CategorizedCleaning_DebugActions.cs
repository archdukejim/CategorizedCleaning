using LudeonTK;
using RimWorld;
using System.Linq;
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
            sb.AppendLine($"CC: filth by lane on map {map.uniqueID}:");
            foreach (var def in CleaningCategoryDef.All)
            {
                sb.AppendLine($"  {def.defName} ({def.label}) rooms={CategorizedCleaning_Settings.RolesFor(def).Count} kinds={CategorizedCleaning_Settings.KindsFor(def).Count}: {cache.CountFor(def)} filth; workType={def.WorkType?.defName ?? "-"} giver={def.workGiver?.defName ?? "-"}");
            }
            sb.AppendLine($"  pins={cache.Pins.Count} cleaning zones={map.zoneManager.AllZones.OfType<Zone_Cleaning>().Count()}");
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
            var zone = map.zoneManager.ZoneAt(cell) as Zone_Cleaning;
            var sb = new StringBuilder();
            sb.Append($"CC: cell {cell} room={(room == null ? "none" : room.Role?.defName ?? "?")} outdoors={(room?.PsychologicallyOutdoors ?? true)} lane={(cache.LaneFor(room)?.defName ?? "-")} pin={(cache.PinFor(room)?.lane?.defName ?? "no")} zone={(zone?.label ?? "no")}");
            foreach (var filth in cell.GetThingList(map).OfType<Filth>())
                sb.Append($"\n   {filth.def.defName} [{CleaningFilthKindDef.KindOf(filth.def)?.defName}] -> {(cache.Classify(filth)?.defName ?? "unclaimed")}");
            Log.Message(sb.ToString());
        }
    }
}
