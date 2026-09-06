using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// One column per cleaning category; every room type sits in exactly one column. Unlisted types (from newly added
    /// mods, for instance) default to the indoor category so the columns always show the full set.
    /// </summary>
    public class CategorizedCleaning_Settings : ModSettings
    {
        public const float LIST_DESIRED_HEIGHT = 440f;
        public const float SCROLLBAR_WIDTH = 18f;
        public const float DEF_BUTTON_HEIGHT = 32f;
        public const float ARROW_WIDTH = 32f;

        private const string LEGACY_STERILE_DEFNAME = "CategorizedCleaning_CleanRooms";
        private const string LEGACY_OUTDOOR_DEFNAME = "CategorizedCleaning_HomeArea";

        private static Dictionary<CleaningCategoryDef, List<RoomRoleDef>> rolesByCategory;
        private static Dictionary<RoomRoleDef, CleaningCategoryDef> categoryByRole = new Dictionary<RoomRoleDef, CleaningCategoryDef>();
        private static bool initialized;
        private static bool hasSavedLists;

        private Vector2[] scrollPositions = new Vector2[0];

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

        private static void EnsureInitialized(List<RoomRoleDef> legacySterileRooms = null, List<RoomRoleDef> legacyOutdoorRooms = null)
        {
            if (initialized)
                return;
            var all = CleaningCategoryDef.All;
            if (all.Count == 0)
                return; // defs not loaded yet; try again on the next access

            if (rolesByCategory == null)
                rolesByCategory = new Dictionary<CleaningCategoryDef, List<RoomRoleDef>>();
            foreach (var key in rolesByCategory.Keys.Where(k => k == null || !all.Contains(k)).ToList())
                rolesByCategory.Remove(key);
            foreach (var def in all)
            {
                if (!rolesByCategory.TryGetValue(def, out var list) || list == null)
                    rolesByCategory[def] = new List<RoomRoleDef>();
            }

            bool fresh = !hasSavedLists && legacySterileRooms == null && legacyOutdoorRooms == null;
            if (fresh)
            {
                foreach (var def in all)
                    rolesByCategory[def].AddRange(def.defaultRoomRoles);
            }
            else
            {
                // Settings written by the pre-column versions of the mod only knew sterile and outdoor rooms.
                if (legacySterileRooms != null && all.FirstOrDefault(d => d.defName == LEGACY_STERILE_DEFNAME) is CleaningCategoryDef sterile)
                    rolesByCategory[sterile].AddRange(legacySterileRooms);
                if (legacyOutdoorRooms != null && all.FirstOrDefault(d => d.defName == LEGACY_OUTDOOR_DEFNAME) is CleaningCategoryDef outdoor)
                    rolesByCategory[outdoor].AddRange(legacyOutdoorRooms);
            }

            // Every role in exactly one column; earlier columns win duplicates; unlisted roles go to the indoor default.
            var seen = new HashSet<RoomRoleDef>();
            foreach (var def in all)
                rolesByCategory[def].RemoveAll(r => r == null || r == RoomRoleDefOf.None || !seen.Add(r));

            var fallback = CleaningCategoryDef.IndoorDefault ?? all[0];
            foreach (var role in DefDatabase<RoomRoleDef>.AllDefsListForReading)
            {
                if (role != RoomRoleDefOf.None && !seen.Contains(role))
                    rolesByCategory[fallback].Add(role);
            }

            RebuildRoleMap();
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

        /// <summary>Called when the settings window closes: re-index the filth on every map with the new room lists.</summary>
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
            var all = CleaningCategoryDef.All;
            if (scrollPositions.Length != all.Count)
                scrollPositions = new Vector2[all.Count];

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect hintRect = inRect.TopPartPixels(48f);
            Widgets.Label(hintRect, "CC_Settings_Hint".Translate());

            float listsHeight = Mathf.Min(LIST_DESIRED_HEIGHT, inRect.height - hintRect.height - 8f);
            Rect listsRect = new Rect(inRect.x, hintRect.yMax + 8f, inRect.width, listsHeight);
            float columnWidth = listsRect.width / all.Count;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                Rect column = new Rect(listsRect.x + columnWidth * i, listsRect.y, columnWidth, listsRect.height).ContractedBy(2f);
                Action<RoomRoleDef> moveLeft = i > 0 ? role => MoveRole(role, all[i - 1]) : (Action<RoomRoleDef>)null;
                Action<RoomRoleDef> moveRight = i < all.Count - 1 ? role => MoveRole(role, all[i + 1]) : (Action<RoomRoleDef>)null;
                DrawDefsList(def, column, ref scrollPositions[i], RolesFor(def).ToList(), moveLeft, moveRight);
            }
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

            var all = CleaningCategoryDef.All;
            var loaded = new Dictionary<CleaningCategoryDef, List<RoomRoleDef>>();
            foreach (var def in all)
            {
                List<RoomRoleDef> list = Scribe.mode == LoadSaveMode.Saving && rolesByCategory != null && rolesByCategory.TryGetValue(def, out var current) ? current : null;
                Scribe_Collections.Look(ref list, "rooms_" + def.defName, LookMode.Def);
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                    loaded[def] = list ?? new List<RoomRoleDef>();
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // Settings written by the pre-column versions of the mod.
                List<RoomRoleDef> legacySterileRooms = null;
                List<RoomRoleDef> legacyOutdoorRooms = null;
                Scribe_Collections.Look(ref legacySterileRooms, "sterileRooms", LookMode.Def);
                Scribe_Collections.Look(ref legacyOutdoorRooms, "outdoorRooms", LookMode.Def);

                rolesByCategory = loaded;
                initialized = false;
                EnsureInitialized(legacySterileRooms, legacyOutdoorRooms);
            }
        }
    }
}
