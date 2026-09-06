using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Bottom-bar tab: every enclosed room and every cleaning zone on the current map, in one swim lane per cleaning
    /// lane. Rooms land in a lane automatically from the room-type defaults; moving one pins it. Entries in the Custom
    /// lane expose their own filth kinds, priority (which Work column cleans them) and upkeep jobs.
    /// </summary>
    public class MainTabWindow_CleaningRooms : MainTabWindow
    {
        private class Row
        {
            public Room room;
            public Zone_Cleaning zone;
            public string name;
            public string typeLabel;
            public CleaningCategoryDef lane;
            public bool pinned;
            public bool inHome;
            public int filthCount;
            public IntVec3 anchor;

            public bool IsZone => zone != null;
            public Map Map => zone?.Map ?? room?.Map;
            public CustomCleaningConfig Custom(FilthCache cache) => zone != null ? zone.custom : cache.PinFor(room)?.custom;
        }

        private const float HintHeight = 40f;
        private const float LaneHeaderHeight = 32f;
        private const float RowHeight = 36f;
        private const float CustomRowHeight = 96f;
        private const float ArrowSize = 24f;
        private const float LaneGap = 6f;
        private const float ScrollbarWidth = 16f;
        private const int RefreshIntervalFrames = 60;

        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>Room under the mouse in the tab; outlined on the map by <see cref="HarmonyPatches.MapInterface_Update_Patches"/>.</summary>
        public static Room hoveredRoom;

        private readonly List<Row> rows = new List<Row>();
        private Vector2[] scrollPositions = new Vector2[0];
        private int lastRefreshFrame = -1;

        public override Vector2 RequestedTabSize => new Vector2(Mathf.Min(UI.screenWidth - 20f, 1400f), 680f);

        public override void PreOpen()
        {
            base.PreOpen();
            Refresh(force: true);
        }

        public override void PostClose()
        {
            base.PostClose();
            hoveredRoom = null;
        }

        private void Refresh(bool force = false)
        {
            if (!force && Time.frameCount - lastRefreshFrame < RefreshIntervalFrames)
                return;
            lastRefreshFrame = Time.frameCount;
            rows.Clear();

            var map = Find.CurrentMap;
            if (map == null)
                return;
            var cache = map.GetComponent<FilthCache>();
            var home = map.areaManager.Home;

            foreach (var room in map.regionGrid.AllRooms)
            {
                if (room == null || room.Dereferenced || room.IsDoorway || room.CellCount == 0
                    || room.PsychologicallyOutdoors || room.TouchesMapEdge)
                    continue;

                var pin = cache.PinFor(room);
                var lane = pin?.lane ?? FilthCache.AutomaticLane(room);
                if (lane == null)
                    continue;

                bool inHome = false;
                foreach (var cell in room.Cells)
                {
                    if (home[cell])
                    {
                        inHome = true;
                        break;
                    }
                }

                rows.Add(new Row
                {
                    room = room,
                    name = room.GetRoomRoleLabel().CapitalizeFirst(),
                    typeLabel = room.Role?.LabelCap ?? "",
                    lane = lane,
                    pinned = pin != null,
                    inHome = inHome,
                    filthCount = room.ContainedThings<Filth>().Count(),
                    anchor = room.Cells.First(),
                });
            }

            foreach (var zone in map.zoneManager.AllZones)
            {
                if (!(zone is Zone_Cleaning cleaningZone) || cleaningZone.lane == null || cleaningZone.cells.Count == 0)
                    continue;
                int filth = 0;
                foreach (var cell in cleaningZone.cells)
                {
                    var things = cell.GetThingList(map);
                    for (int i = 0; i < things.Count; i++)
                        if (things[i] is Filth)
                            filth++;
                }
                rows.Add(new Row
                {
                    zone = cleaningZone,
                    name = cleaningZone.label,
                    typeLabel = "CC_Tab_ZoneType".Translate(),
                    lane = cleaningZone.lane,
                    pinned = true,
                    inHome = true,
                    filthCount = filth,
                    anchor = cleaningZone.Position,
                });
            }

            // Several rooms often share a label ("Kitchen", "Storeroom"); number them in a stable map order.
            foreach (var group in rows.Where(r => !r.IsZone).GroupBy(r => r.name).Where(g => g.Count() > 1).ToList())
            {
                int i = 1;
                foreach (var row in group.OrderBy(r => r.anchor.z).ThenBy(r => r.anchor.x))
                    row.name = $"{row.name} {i++}";
            }
            rows.SortBy(r => r.IsZone ? 1 : 0, r => r.name);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Refresh();
            var lanes = CleaningCategoryDef.All;
            if (scrollPositions.Length != lanes.Count)
                scrollPositions = new Vector2[lanes.Count];
            hoveredRoom = null;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect hintRect = new Rect(inRect.x, inRect.y, inRect.width - 130f, HintHeight);
            Widgets.Label(hintRect, "CC_Tab_Hint".Translate());
            Rect defaultsRect = new Rect(inRect.xMax - 120f, inRect.y, 120f, 30f);
            if (Widgets.ButtonText(defaultsRect, "CC_Tab_Defaults".Translate()))
                Find.WindowStack.Add(new Dialog_ModSettings(CategorizedCleaning_Mod.ModSingleton));
            TooltipHandler.TipRegion(defaultsRect, "CC_Tab_DefaultsTip".Translate());

            Rect lanesRect = new Rect(inRect.x, hintRect.yMax + 4f, inRect.width, inRect.height - HintHeight - 4f);
            float laneWidth = (lanesRect.width - LaneGap * (lanes.Count - 1)) / lanes.Count;
            for (int i = 0; i < lanes.Count; i++)
            {
                Rect laneRect = new Rect(lanesRect.x + i * (laneWidth + LaneGap), lanesRect.y, laneWidth, lanesRect.height);
                DrawLane(laneRect, i, lanes);
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void DrawLane(Rect rect, int laneIndex, List<CleaningCategoryDef> lanes)
        {
            var lane = lanes[laneIndex];
            var laneRows = rows.Where(r => r.lane == lane).ToList();
            float rowHeight = lane.isCustom ? CustomRowHeight : RowHeight;

            Widgets.DrawMenuSection(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, LaneHeaderHeight);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = lane.color;
            Widgets.Label(headerRect, lane.LabelCap + " (" + laneRows.Count + ")");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            string headerTip = lane.description;
            if (lane.WorkType != null)
                headerTip += "\n\n" + "CC_Tab_WorkColumn".Translate(lane.WorkType.labelShort.CapitalizeFirst());
            if (lane.hasFilthKindFilter)
                headerTip += "\n" + "CC_Tab_LaneKinds".Translate(CategorizedCleaning_Settings.KindsFor(lane).Select(k => k.label).ToCommaList());
            TooltipHandler.TipRegion(headerRect, headerTip);
            Widgets.DrawLineHorizontal(rect.x + 4f, headerRect.yMax, rect.width - 8f);

            Rect listRect = new Rect(rect.x, headerRect.yMax + 2f, rect.width, rect.height - LaneHeaderHeight - 4f).ContractedBy(3f);
            float contentHeight = laneRows.Count * rowHeight;
            Rect viewRect = new Rect(0f, 0f, listRect.width - (contentHeight > listRect.height ? ScrollbarWidth : 0f), contentHeight);
            Widgets.BeginScrollView(listRect, ref scrollPositions[laneIndex], viewRect);
            float y = 0f;
            foreach (var row in laneRows)
            {
                DrawRow(new Rect(0f, y, viewRect.width, rowHeight), row, laneIndex, lanes);
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawRow(Rect rect, Row row, int laneIndex, List<CleaningCategoryDef> lanes)
        {
            var lane = lanes[laneIndex];
            var cache = row.Map?.GetComponent<FilthCache>();
            if (cache == null)
                return;

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                if (!row.IsZone)
                    hoveredRoom = row.room;
            }

            float buttonY = rect.y + (RowHeight - ArrowSize) / 2f;
            Rect leftRect = new Rect(rect.x + 2f, buttonY, ArrowSize, ArrowSize);
            Rect rightRect = new Rect(rect.xMax - ArrowSize - 2f, buttonY, ArrowSize, ArrowSize);
            if (laneIndex > 0 && Widgets.ButtonImageWithBG(leftRect, Textures_Custom.ArrowLeft))
                Move(row, lanes[laneIndex - 1]);
            if (laneIndex < lanes.Count - 1 && Widgets.ButtonImageWithBG(rightRect, Textures_Custom.ArrowRight))
                Move(row, lanes[laneIndex + 1]);

            Rect nameRect = new Rect(leftRect.xMax + 4f, rect.y + 2f, rightRect.x - leftRect.xMax - 8f, RowHeight - 4f);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = row.inHome || row.pinned ? Color.white : Dim;
            string title = row.IsZone ? "CC_Tab_ZoneName".Translate(row.name).ToString() : (row.pinned ? "CC_Tab_Pinned".Translate(row.name).ToString() : row.name);
            Widgets.Label(nameRect.TopHalf(), title);
            Text.Font = GameFont.Tiny;
            GUI.color = Dim;
            string detail = "CC_Tab_FilthCount".Translate(row.filthCount);
            if (!row.inHome && !row.pinned)
                detail += "  " + "CC_Tab_OutsideHome".Translate();
            Widgets.Label(nameRect.BottomHalf(), detail);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            string autoLabel = row.IsZone ? "-" : (FilthCache.AutomaticLane(row.room)?.label ?? "-");
            TooltipHandler.TipRegion(nameRect, () => "CC_Tab_RowTip".Translate(row.typeLabel,
                row.IsZone ? "CC_Tab_ZoneTip".Translate() : (row.pinned ? "CC_Tab_PinnedYes".Translate() : "CC_Tab_PinnedNo".Translate(autoLabel))).ToString(),
                (row.IsZone ? row.zone.ID : row.room.ID) ^ 0x5CC);

            if (Widgets.ButtonInvisible(nameRect))
            {
                CameraJumper.TryJump(new GlobalTargetInfo(row.anchor, row.Map));
                if (row.IsZone)
                    Find.Selector.ClearSelection();
                if (row.IsZone)
                    Find.Selector.Select(row.zone, playSound: false, forceDesignatorDeselect: false);
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(nameRect))
            {
                Event.current.Use();
                Find.WindowStack.Add(new FloatMenu(BuildRowMenu(row, lanes)));
            }

            if (lane.isCustom)
                DrawCustomControls(new Rect(rect.x + 4f, rect.y + RowHeight, rect.width - 8f, rect.height - RowHeight - 2f), row, cache);
        }

        private void DrawCustomControls(Rect rect, Row row, FilthCache cache)
        {
            var config = row.Custom(cache);
            if (config == null)
                return;

            Text.Font = GameFont.Tiny;
            var kinds = CleaningFilthKindDef.All;
            float lineHeight = 22f;
            Rect kindsLine = new Rect(rect.x, rect.y, rect.width, lineHeight);
            float kindWidth = kindsLine.width / Mathf.Max(1, kinds.Count);
            for (int i = 0; i < kinds.Count; i++)
            {
                var kind = kinds[i];
                Rect cell = new Rect(kindsLine.x + kindWidth * i, kindsLine.y, kindWidth - 2f, lineHeight);
                bool on = config.kinds.Contains(kind);
                bool before = on;
                Widgets.CheckboxLabeled(cell, kind.LabelCap, ref on);
                TooltipHandler.TipRegion(cell, kind.description);
                if (on != before)
                {
                    config.ToggleKind(kind);
                    NotifyConfigChanged(row, cache);
                }
            }

            Rect tasksLine = new Rect(rect.x, kindsLine.yMax + 2f, rect.width, lineHeight);
            float taskWidth = tasksLine.width / 3f;
            DrawTaskToggle(new Rect(tasksLine.x, tasksLine.y, taskWidth - 2f, lineHeight), "CC_Custom_CutPlants", "CC_Custom_CutPlantsDesc", ref config.cutPlants);
            DrawTaskToggle(new Rect(tasksLine.x + taskWidth, tasksLine.y, taskWidth - 2f, lineHeight), "CC_Custom_ClearSnow", "CC_Custom_ClearSnowDesc", ref config.clearSnow);
            DrawTaskToggle(new Rect(tasksLine.x + taskWidth * 2f, tasksLine.y, taskWidth - 2f, lineHeight), "CC_Custom_HaulDebris", "CC_Custom_HaulDebrisDesc", ref config.haulDebris);

            Rect priorityLine = new Rect(rect.x, tasksLine.yMax + 2f, rect.width, lineHeight);
            var column = config.WorkLane;
            string priorityLabel = "CC_Custom_PriorityOption".Translate(("CC_Priority_" + config.priority).Translate(), column?.WorkType?.labelShort ?? "-");
            if (Widgets.ButtonText(priorityLine, priorityLabel))
            {
                var options = new List<FloatMenuOption>();
                foreach (CustomCleaningPriority p in System.Enum.GetValues(typeof(CustomCleaningPriority)))
                {
                    var local = p;
                    var localColumn = CleaningCategoryDef.ForPriority(local);
                    string label = "CC_Custom_PriorityOption".Translate(("CC_Priority_" + local).Translate(), localColumn?.WorkType?.labelShort ?? "-");
                    options.Add(new FloatMenuOption(label, local == config.priority ? null : (System.Action)delegate
                    {
                        config.priority = local;
                        NotifyConfigChanged(row, cache);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(priorityLine, "CC_Custom_PriorityCommandDesc".Translate());
            Text.Font = GameFont.Small;
        }

        private static void DrawTaskToggle(Rect rect, string labelKey, string tipKey, ref bool value)
        {
            Widgets.CheckboxLabeled(rect, labelKey.Translate(), ref value);
            TooltipHandler.TipRegion(rect, tipKey.Translate());
        }

        private static void NotifyConfigChanged(Row row, FilthCache cache)
        {
            if (row.IsZone)
                row.zone.Notify_ConfigChanged();
            else
                cache.Notify_RoomChanged(row.room);
        }

        private List<FloatMenuOption> BuildRowMenu(Row row, List<CleaningCategoryDef> lanes)
        {
            var options = new List<FloatMenuOption>();
            if (!row.IsZone)
            {
                var automatic = FilthCache.AutomaticLane(row.room);
                options.Add(new FloatMenuOption("CC_RoomOverride_Auto".Translate(automatic?.label ?? "-"), row.pinned ? (System.Action)(() => Move(row, null)) : null));
            }
            foreach (var lane in lanes)
            {
                var local = lane;
                bool isCurrent = row.IsZone ? row.lane == local : (row.pinned && row.lane == local);
                options.Add(new FloatMenuOption("CC_RoomOverride_Set".Translate(local.label), isCurrent ? null : (System.Action)(() => Move(row, local))));
            }
            if (row.IsZone)
            {
                options.Add(new FloatMenuOption("CC_Tab_DeleteZone".Translate(), delegate
                {
                    row.zone.Delete();
                    Refresh(force: true);
                }));
            }
            return options;
        }

        private void Move(Row row, CleaningCategoryDef lane)
        {
            var map = row.Map;
            if (map == null)
                return;
            if (row.IsZone)
            {
                if (lane != null)
                    row.zone.SetLane(lane);
            }
            else
            {
                if (row.room.Dereferenced)
                    return;
                map.GetComponent<FilthCache>().PinRoom(row.room, lane);
            }
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            Refresh(force: true);
        }
    }
}
