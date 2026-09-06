using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Defaults only. (1) One column per lane that accepts room types; every room type sits in exactly one column.
    /// (2) Which filth kinds each work lane cleans. Per-room and per-zone choices live on the map, not here.
    /// </summary>
    public class CategorizedCleaning_Settings : ModSettings
    {
        public const float SCROLLBAR_WIDTH = 18f;
        public const float DEF_BUTTON_HEIGHT = 32f;
        public const float ARROW_WIDTH = 32f;
        private const float KindRowHeight = 28f;

        private const string LEGACY_STERILE_DEFNAME = "CategorizedCleaning_CleanRooms";
        private const string LEGACY_OUTDOOR_DEFNAME = "CategorizedCleaning_Exterior";

        private static Dictionary<CleaningCategoryDef, List<RoomRoleDef>> rolesByCategory;
        private static Dictionary<CleaningCategoryDef, List<CleaningFilthKindDef>> kindsByLane;
        private static Dictionary<RoomRoleDef, CleaningCategoryDef> categoryByRole = new Dictionary<RoomRoleDef, CleaningCategoryDef>();
        private static bool initialized;
        private static bool hasSavedLists;

        private Vector2[] scrollPositions = new Vector2[0];

        public static List<CleaningCategoryDef> RoomLanes => CleaningCategoryDef.All.Where(d => d.acceptsRoomTypes).ToList();
        public static List<CleaningCategoryDef> FilterLanes => CleaningCategoryDef.All.Where(d => d.hasFilthKindFilter).ToList();

        public static List<RoomRoleDef> RolesFor(CleaningCategoryDef def)
        {
            EnsureInitialized();
            return rolesByCategory != null && def != null && rolesByCategory.TryGetValue(def, out var list) ? list : new List<RoomRoleDef>();
        }

        public static CleaningCategoryDef CategoryForRole(RoomRoleDef role)
        {
            EnsureInitialized();
            return role != null && categoryByRole.TryGetValue(role, out var def) ? def : null;
        }

        public static void MoveRole(RoomRoleDef role, CleaningCategoryDef to)
        {
            EnsureInitialized();
            if (role == null || to == null || rolesByCategory == null || !rolesByCategory.ContainsKey(to))
                return;
            foreach (var list in rolesByCategory.Values)
                list.Remove(role);
            rolesByCategory[to].Add(role);
            RebuildRoleMap();
        }

        public static List<CleaningFilthKindDef> KindsFor(CleaningCategoryDef lane)
        {
            EnsureInitialized();
            return kindsByLane != null && lane != null && kindsByLane.TryGetValue(lane, out var list) ? list : new List<CleaningFilthKindDef>();
        }

        public static bool LaneAllows(CleaningCategoryDef lane, ThingDef filthDef)
        {
            if (lane == null)
                return false;
            if (!lane.hasFilthKindFilter)
                return true;
            EnsureInitialized();
            var kind = CleaningFilthKindDef.KindOf(filthDef);
            return kind != null && kindsByLane != null && kindsByLane.TryGetValue(lane, out var list) && list.Contains(kind);
        }

        public static void ToggleLaneKind(CleaningCategoryDef lane, CleaningFilthKindDef kind)
        {
            EnsureInitialized();
            if (kindsByLane == null || !kindsByLane.TryGetValue(lane, out var list))
                return;
            if (!list.Remove(kind))
                list.Add(kind);
        }

        private static void EnsureInitialized(List<RoomRoleDef> legacySterileRooms = null, List<RoomRoleDef> legacyOutdoorRooms = null)
        {
            if (initialized)
                return;
            var all = CleaningCategoryDef.All;
            if (all.Count == 0)
                return; // defs not loaded yet; try again on the next access

            // Room type columns.
            var roomLanes = RoomLanes;
            if (rolesByCategory == null)
                rolesByCategory = new Dictionary<CleaningCategoryDef, List<RoomRoleDef>>();
            foreach (var key in rolesByCategory.Keys.Where(k => k == null || !roomLanes.Contains(k)).ToList())
                rolesByCategory.Remove(key);
            foreach (var def in roomLanes)
            {
                if (!rolesByCategory.TryGetValue(def, out var list) || list == null)
                    rolesByCategory[def] = new List<RoomRoleDef>();
            }

            bool fresh = !hasSavedLists && legacySterileRooms == null && legacyOutdoorRooms == null;
            if (fresh)
            {
                foreach (var def in roomLanes)
                    rolesByCategory[def].AddRange(def.defaultRoomRoles);
            }
            else
            {
                // Settings written by the pre-column versions of the mod only knew sterile and outdoor rooms.
                if (legacySterileRooms != null && roomLanes.FirstOrDefault(d => d.defName == LEGACY_STERILE_DEFNAME) is CleaningCategoryDef sterile)
                    rolesByCategory[sterile].AddRange(legacySterileRooms);
                if (legacyOutdoorRooms != null && roomLanes.FirstOrDefault(d => d.defName == LEGACY_OUTDOOR_DEFNAME) is CleaningCategoryDef outdoor)
                    rolesByCategory[outdoor].AddRange(legacyOutdoorRooms);
            }

            var seen = new HashSet<RoomRoleDef>();
            foreach (var def in roomLanes)
                rolesByCategory[def].RemoveAll(r => r == null || r == RoomRoleDefOf.None || !seen.Add(r));

            var fallback = CleaningCategoryDef.IndoorDefault ?? roomLanes[0];
            if (!rolesByCategory.ContainsKey(fallback))
                fallback = roomLanes[0];
            foreach (var role in DefDatabase<RoomRoleDef>.AllDefsListForReading)
            {
                if (role != RoomRoleDefOf.None && !seen.Contains(role))
                    rolesByCategory[fallback].Add(role);
            }
            RebuildRoleMap();

            // Filth kind filters.
            var allKinds = CleaningFilthKindDef.All;
            if (kindsByLane == null)
                kindsByLane = new Dictionary<CleaningCategoryDef, List<CleaningFilthKindDef>>();
            foreach (var lane in FilterLanes)
            {
                if (!kindsByLane.TryGetValue(lane, out var list) || list == null)
                {
                    list = lane.defaultFilthKinds.Count > 0 ? new List<CleaningFilthKindDef>(lane.defaultFilthKinds) : new List<CleaningFilthKindDef>(allKinds);
                    kindsByLane[lane] = list;
                }
                list.RemoveAll(k => k == null);
            }

            initialized = true;
        }

        private static void RebuildRoleMap()
        {
            categoryByRole.Clear();
            if (rolesByCategory == null)
                return;
            foreach (var pair in rolesByCategory)
            {
                foreach (var role in pair.Value)
                    categoryByRole[role] = pair.Key;
            }
        }

        /// <summary>Called when the settings window closes: re-index the filth on every map with the new defaults.</summary>
        public static void ApplyToGame()
        {
            EnsureInitialized();
            RebuildRoleMap();
            FilthCache.RebuildAllMaps();
        }

        #region UI

        public void DoSettingsWindowContents(Rect inRect)
        {
            EnsureInitialized();
            var roomLanes = RoomLanes;
            var filterLanes = FilterLanes;
            var kinds = CleaningFilthKindDef.All;
            if (scrollPositions.Length != roomLanes.Count)
                scrollPositions = new Vector2[roomLanes.Count];

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect hintRect = inRect.TopPartPixels(48f);
            Widgets.Label(hintRect, "CC_Settings_Hint".Translate());

            float kindsSectionHeight = 30f + KindRowHeight * filterLanes.Count + 8f;
            float listsHeight = inRect.height - hintRect.height - kindsSectionHeight - 24f;
            Rect listsRect = new Rect(inRect.x, hintRect.yMax + 8f, inRect.width, listsHeight);
            float columnWidth = listsRect.width / roomLanes.Count;
            for (int i = 0; i < roomLanes.Count; i++)
            {
                var def = roomLanes[i];
                Rect column = new Rect(listsRect.x + columnWidth * i, listsRect.y, columnWidth, listsRect.height).ContractedBy(2f);
                Action<RoomRoleDef> moveLeft = i > 0 ? role => MoveRole(role, roomLanes[i - 1]) : (Action<RoomRoleDef>)null;
                Action<RoomRoleDef> moveRight = i < roomLanes.Count - 1 ? role => MoveRole(role, roomLanes[i + 1]) : (Action<RoomRoleDef>)null;
                DrawDefsList(def, column, ref scrollPositions[i], RolesFor(def).ToList(), moveLeft, moveRight);
            }

            Rect kindsRect = new Rect(inRect.x, listsRect.yMax + 12f, inRect.width, kindsSectionHeight);
            DrawKindFilters(kindsRect, filterLanes, kinds);
        }

        private void DrawKindFilters(Rect rect, List<CleaningCategoryDef> lanes, List<CleaningFilthKindDef> kinds)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 28f), "CC_Settings_KindsHeader".Translate());
            float y = rect.y + 30f;
            float laneLabelWidth = 150f;
            float kindWidth = Mathf.Min(220f, (rect.width - laneLabelWidth) / Mathf.Max(1, kinds.Count));
            foreach (var lane in lanes)
            {
                Rect row = new Rect(rect.x, y, rect.width, KindRowHeight);
                GUI.color = lane.color;
                Widgets.Label(new Rect(row.x, row.y, laneLabelWidth, row.height), lane.LabelCap);
                GUI.color = Color.white;
                var current = KindsFor(lane);
                for (int k = 0; k < kinds.Count; k++)
                {
                    var kind = kinds[k];
                    Rect cell = new Rect(row.x + laneLabelWidth + kindWidth * k, row.y, kindWidth - 4f, row.height);
                    bool on = current.Contains(kind);
                    bool before = on;
                    Widgets.CheckboxLabeled(cell, kind.LabelCap, ref on);
                    if (on != before)
                        ToggleLaneKind(lane, kind);
                    TooltipHandler.TipRegion(cell, kind.description);
                }
                y += KindRowHeight;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawDefsList(CleaningCategoryDef category, Rect rect, ref Vector2 scrollPos, List<RoomRoleDef> defs, Action<RoomRoleDef> moveLeft, Action<RoomRoleDef> moveRight)
        {
            Widgets.DrawBoxSolidWithOutline(rect, Color.black, Color.grey);

            var anchor = Text.Anchor;
            Text.Anchor = TextAnchor.LowerCenter;

            var labelRect = rect.TopPartPixels(24f);
            GUI.color = category.color;
            Widgets.LabelFit(labelRect, category.LabelCap);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(labelRect, category.description);
            Widgets.DrawLineHorizontal(labelRect.x, labelRect.y + labelRect.height, labelRect.width);

            rect.y += labelRect.height + 2;
            rect.height -= labelRect.height + 2 + 2;
            rect = rect.ContractedBy(1f);

            var viewRect = new Rect(0, 0, rect.width, (DEF_BUTTON_HEIGHT + 1) * defs.Count);

            bool hasScrollbar = rect.height < viewRect.height;
            if (hasScrollbar)
                viewRect.width -= SCROLLBAR_WIDTH; //clear space for scrollbar
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);

            var buttonRect = viewRect.ContractedBy(2f).TopPartPixels(DEF_BUTTON_HEIGHT);

            foreach (var def in defs.OrderBy(d => d.label))
            {
                DrawDefButton(buttonRect, def, moveLeft, moveRight);
                buttonRect.y += DEF_BUTTON_HEIGHT + 1;
            }

            Text.Anchor = anchor;

            Widgets.EndScrollView();
        }

        private void DrawDefButton(Rect buttonRect, RoomRoleDef def, Action<RoomRoleDef> moveLeft, Action<RoomRoleDef> moveRight)
        {
            Widgets.DrawHighlightIfMouseover(buttonRect);
            Widgets.LabelFit(buttonRect, def.LabelCap);
            if (moveLeft != null)
            {
                if (Widgets.ButtonImageWithBG(buttonRect.LeftPartPixels(ARROW_WIDTH), Textures_Custom.ArrowLeft))
                    moveLeft(def);
            }
            if (moveRight != null)
            {
                if (Widgets.ButtonImageWithBG(buttonRect.RightPartPixels(ARROW_WIDTH), Textures_Custom.ArrowRight))
                    moveRight(def);
            }
        }

        #endregion

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                EnsureInitialized();
                hasSavedLists = true;
            }

            Scribe_Values.Look(ref hasSavedLists, "hasSavedLists", false);

            var loadedRoles = new Dictionary<CleaningCategoryDef, List<RoomRoleDef>>();
            foreach (var def in RoomLanes)
            {
                List<RoomRoleDef> list = Scribe.mode == LoadSaveMode.Saving && rolesByCategory != null && rolesByCategory.TryGetValue(def, out var current) ? current : null;
                Scribe_Collections.Look(ref list, "rooms_" + def.defName, LookMode.Def);
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                    loadedRoles[def] = list ?? new List<RoomRoleDef>();
            }

            var loadedKinds = new Dictionary<CleaningCategoryDef, List<CleaningFilthKindDef>>();
            foreach (var lane in FilterLanes)
            {
                List<CleaningFilthKindDef> list = Scribe.mode == LoadSaveMode.Saving && kindsByLane != null && kindsByLane.TryGetValue(lane, out var current) ? current : null;
                Scribe_Collections.Look(ref list, "kinds_" + lane.defName, LookMode.Def);
                if (Scribe.mode == LoadSaveMode.LoadingVars && list != null)
                    loadedKinds[lane] = list;
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // Settings written by the pre-column versions of the mod.
                List<RoomRoleDef> legacySterileRooms = null;
                List<RoomRoleDef> legacyOutdoorRooms = null;
                Scribe_Collections.Look(ref legacySterileRooms, "sterileRooms", LookMode.Def);
                Scribe_Collections.Look(ref legacyOutdoorRooms, "outdoorRooms", LookMode.Def);

                rolesByCategory = loadedRoles;
                kindsByLane = loadedKinds;
                initialized = false;
                EnsureInitialized(legacySterileRooms, legacyOutdoorRooms);
            }
        }
    }
}
