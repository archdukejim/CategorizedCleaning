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
    /// Bottom-bar tab listing every enclosed room on the current map in one swim lane per cleaning column.
    /// Rooms land in a lane automatically (room type defaults from the mod settings); moving a room with the
    /// arrows or the right-click menu pins it to a lane, which is the same per-room override the right-click
    /// menu on the map creates. Names come from the game's own room label, so bedrooms read "Kim's bedroom".
    /// </summary>
    public class MainTabWindow_CleaningRooms : MainTabWindow
    {
        private class RoomRow
        {
            public Room room;
            public string name;
            public string typeLabel;
            public CleaningCategoryDef lane;
            public bool pinned;
            public bool inHome;
            public int filthCount;
            public IntVec3 anchor;
        }

        private const float HintHeight = 40f;
        private const float LaneHeaderHeight = 32f;
        private const float RowHeight = 36f;
        private const float ArrowSize = 24f;
        private const float LaneGap = 6f;
        private const float ScrollbarWidth = 16f;
        private const int RefreshIntervalFrames = 60;

        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>Room under the mouse in the tab; drawn on the map by <see cref="HarmonyPatches.MapInterface_Update_Patches"/>.</summary>
        public static Room hoveredRoom;

        private readonly List<RoomRow> rows = new List<RoomRow>();
        private Vector2[] scrollPositions = new Vector2[0];
        private int lastRefreshFrame = -1;

        public override Vector2 RequestedTabSize => new Vector2(Mathf.Min(UI.screenWidth - 20f, 1100f), 640f);

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

        /// <summary>Where a room's filth goes without an override: its room type's column, else the indoor default.</summary>
        public static CleaningCategoryDef AutomaticLane(Room room)
        {
            var role = room.Role;
            if (role != null && role != RoomRoleDefOf.None)
            {
                var byRole = CategorizedCleaning_Settings.CategoryForRole(role);
                if (byRole != null)
                    return byRole;
            }
            return room.PsychologicallyOutdoors ? CleaningCategoryDef.OutdoorDefault : CleaningCategoryDef.IndoorDefault;
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

                var pinnedTo = cache.OverrideFor(room);
                var lane = pinnedTo ?? AutomaticLane(room);
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

                rows.Add(new RoomRow
                {
                    room = room,
                    name = room.GetRoomRoleLabel().CapitalizeFirst(),
                    typeLabel = room.Role?.LabelCap ?? "",
                    lane = lane,
                    pinned = pinnedTo != null,
                    inHome = inHome,
                    filthCount = room.ContainedThings<Filth>().Count(),
                    anchor = room.Cells.First(),
                });
            }

            // Several rooms often share a label ("Kitchen", "Storeroom"); number them in a stable map order.
            foreach (var group in rows.GroupBy(r => r.name).Where(g => g.Count() > 1).ToList())
            {
                int i = 1;
                foreach (var row in group.OrderBy(r => r.anchor.z).ThenBy(r => r.anchor.x))
                    row.name = $"{row.name} {i++}";
            }
            rows.SortBy(r => r.name);
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

            Widgets.DrawMenuSection(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, LaneHeaderHeight);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = lane.color;
            Widgets.Label(headerRect, lane.LabelCap + " (" + laneRows.Count + ")");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            string workLabel = lane.WorkType != null ? lane.WorkType.labelShort.CapitalizeFirst() : "?";
            TooltipHandler.TipRegion(headerRect, lane.description + "\n\n" + "CC_Tab_WorkColumn".Translate(workLabel));
            Widgets.DrawLineHorizontal(rect.x + 4f, headerRect.yMax, rect.width - 8f);

            Rect listRect = new Rect(rect.x, headerRect.yMax + 2f, rect.width, rect.height - LaneHeaderHeight - 4f).ContractedBy(3f);
            float contentHeight = laneRows.Count * RowHeight;
            Rect viewRect = new Rect(0f, 0f, listRect.width - (contentHeight > listRect.height ? ScrollbarWidth : 0f), contentHeight);
            Widgets.BeginScrollView(listRect, ref scrollPositions[laneIndex], viewRect);
            float y = 0f;
            foreach (var row in laneRows)
            {
                DrawRow(new Rect(0f, y, viewRect.width, RowHeight), row, laneIndex, lanes);
                y += RowHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawRow(Rect rect, RoomRow row, int laneIndex, List<CleaningCategoryDef> lanes)
        {
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
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
            Widgets.Label(nameRect.TopHalf(), row.pinned ? "CC_Tab_Pinned".Translate(row.name).ToString() : row.name);
            Text.Font = GameFont.Tiny;
            GUI.color = Dim;
            string detail = "CC_Tab_FilthCount".Translate(row.filthCount);
            if (!row.inHome && !row.pinned)
                detail += "  " + "CC_Tab_OutsideHome".Translate();
            Widgets.Label(nameRect.BottomHalf(), detail);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            TooltipHandler.TipRegion(nameRect, () => "CC_Tab_RowTip".Translate(row.typeLabel, row.pinned ? "CC_Tab_PinnedYes".Translate() : "CC_Tab_PinnedNo".Translate(AutomaticLane(row.room)?.label ?? "-")).ToString(), row.room.ID ^ 0x5CC);

            if (Widgets.ButtonInvisible(nameRect))
            {
                CameraJumper.TryJump(new GlobalTargetInfo(row.anchor, row.room.Map));
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(nameRect))
            {
                Event.current.Use();
                Find.WindowStack.Add(new FloatMenu(BuildRowMenu(row, lanes)));
            }
        }

        private List<FloatMenuOption> BuildRowMenu(RoomRow row, List<CleaningCategoryDef> lanes)
        {
            var options = new List<FloatMenuOption>();
            var automatic = AutomaticLane(row.room);
            options.Add(new FloatMenuOption("CC_RoomOverride_Auto".Translate(automatic?.label ?? "-"), row.pinned ? (System.Action)(() => Move(row, null)) : null));
            foreach (var lane in lanes)
            {
                var local = lane;
                bool isCurrentPin = row.pinned && row.lane == local;
                options.Add(new FloatMenuOption("CC_RoomOverride_Set".Translate(local.label), isCurrentPin ? null : (System.Action)(() => Move(row, local))));
            }
            return options;
        }

        private void Move(RoomRow row, CleaningCategoryDef lane)
        {
            var map = row.room.Map;
            if (map == null || row.room.Dereferenced)
                return;
            map.GetComponent<FilthCache>().PaintRoom(row.room, lane);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            Refresh(force: true);
        }
    }
}
