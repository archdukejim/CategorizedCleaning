using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Per-map index of filth by cleaning category. Every spawned filth belongs to at most one category.
    /// Also owns the per-map painted areas: the Custom zone and the per-room overrides (both are <see cref="Area_Cleaning"/>
    /// instances living in the vanilla AreaManager, so they draw and save like any other area).
    /// </summary>
    public class FilthCache : MapComponent
    {
        private static readonly HashSet<Filth> Empty = new HashSet<Filth>();
        private static readonly Action<AreaManager> sortAreas =
            AccessTools.MethodDelegate<Action<AreaManager>>(AccessTools.Method(typeof(AreaManager), "SortAreas"));

        private readonly Dictionary<CleaningCategoryDef, HashSet<Filth>> filthByCategory = new Dictionary<CleaningCategoryDef, HashSet<Filth>>();
        private readonly Dictionary<Filth, CleaningCategoryDef> ownerByFilth = new Dictionary<Filth, CleaningCategoryDef>();
        private readonly Dictionary<CleaningCategoryDef, Area_Cleaning> areas = new Dictionary<CleaningCategoryDef, Area_Cleaning>();

        public FilthCache(Map map) : base(map) { }

        #region Queries

        public IEnumerable<Filth> FilthFor(CleaningCategoryDef def)
        {
            return def != null && filthByCategory.TryGetValue(def, out var set) ? set : Empty;
        }

        public int CountFor(CleaningCategoryDef def)
        {
            return def != null && filthByCategory.TryGetValue(def, out var set) ? set.Count : 0;
        }

        public CleaningCategoryDef OwnerOf(Filth filth)
        {
            return filth != null && ownerByFilth.TryGetValue(filth, out var owner) ? owner : null;
        }

        public CleaningCategoryDef Classify(Filth filth)
        {
            return Classify(filth.Position, filth.GetRoom());
        }

        /// <summary>
        /// Which category claims filth at <paramref name="cell"/> inside <paramref name="room"/>, or null if none.
        /// Painted areas win, then the room-type lists, then the indoor/outdoor defaults. Unpainted filth outside the
        /// home area is never claimed, matching vanilla.
        /// </summary>
        public CleaningCategoryDef Classify(IntVec3 cell, Room room)
        {
            var painted = PaintedCategoryAt(cell);
            if (painted != null)
                return painted;

            if (!(map.areaManager.Home?[cell] ?? false))
                return null;

            RoomRoleDef role = room?.Role;
            if (role != null && role != RoomRoleDefOf.None)
            {
                var byRole = CategorizedCleaning_Settings.CategoryForRole(role);
                if (byRole != null)
                    return byRole;
            }

            bool outdoors = room?.PsychologicallyOutdoors ?? true;
            return outdoors ? CleaningCategoryDef.OutdoorDefault : CleaningCategoryDef.IndoorDefault;
        }

        public CleaningCategoryDef PaintedCategoryAt(IntVec3 cell)
        {
            var all = CleaningCategoryDef.All;
            for (int i = 0; i < all.Count; i++)
            {
                var area = GetArea(all[i]);
                if (area != null && area[cell])
                    return all[i];
            }
            return null;
        }

        #endregion

        #region Index maintenance

        public void Notify_FilthSpawned(Filth filth) => Reclassify(filth);

        public void Notify_FilthDespawned(Filth filth) => Remove(filth);

        public void Reclassify(Filth filth)
        {
            if (filth == null)
                return;
            if (filth.Destroyed || !filth.Spawned || filth.Map != map)
            {
                Remove(filth);
                return;
            }

            var newOwner = Classify(filth);
            var oldOwner = OwnerOf(filth);
            if (newOwner == oldOwner)
                return;

            if (oldOwner != null)
                filthByCategory[oldOwner].Remove(filth);

            if (newOwner != null)
            {
                GetOrCreateSet(newOwner).Add(filth);
                ownerByFilth[filth] = newOwner;
            }
            else
            {
                ownerByFilth.Remove(filth);
            }
        }

        public void Remove(Filth filth)
        {
            if (filth == null)
                return;
            if (ownerByFilth.TryGetValue(filth, out var owner))
            {
                filthByCategory[owner].Remove(filth);
                ownerByFilth.Remove(filth);
            }
        }

        public void Notify_CellChanged(IntVec3 cell)
        {
            if (!cell.InBounds(map))
                return;
            var things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                if (things[i] is Filth filth)
                    Reclassify(filth);
            }
        }

        public void Notify_RoomChanged(Room room)
        {
            if (room == null)
                return;
            foreach (var filth in room.ContainedThings<Filth>().ToList())
                Reclassify(filth);
        }

        public void RebuildAll()
        {
            foreach (var set in filthByCategory.Values)
                set.Clear();
            ownerByFilth.Clear();

            var allFilth = map.listerThings.ThingsInGroup(ThingRequestGroup.Filth);
            for (int i = 0; i < allFilth.Count; i++)
            {
                if (allFilth[i] is Filth filth)
                    Reclassify(filth);
            }
        }

        public static void RebuildAllMaps()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.Maps == null)
                return;
            foreach (var map in Find.Maps)
                map.GetComponent<FilthCache>()?.RebuildAll();
        }

        private HashSet<Filth> GetOrCreateSet(CleaningCategoryDef def)
        {
            if (!filthByCategory.TryGetValue(def, out var set))
            {
                set = new HashSet<Filth>();
                filthByCategory[def] = set;
            }
            return set;
        }

        #endregion

        #region Painted areas

        /// <summary>The painted area of a category on this map, if anything has been painted into it yet.</summary>
        public Area_Cleaning GetArea(CleaningCategoryDef def)
        {
            if (def == null)
                return null;
            if (areas.TryGetValue(def, out var cached) && cached != null)
                return cached;

            var allAreas = map.areaManager.AllAreas;
            for (int i = 0; i < allAreas.Count; i++)
            {
                if (allAreas[i] is Area_Cleaning cleaningArea && cleaningArea.def == def)
                {
                    areas[def] = cleaningArea;
                    return cleaningArea;
                }
            }
            return null;
        }

        /// <summary>Gets the painted area of a category on this map, creating it on first use.</summary>
        public Area_Cleaning EnsureArea(CleaningCategoryDef def)
        {
            var area = GetArea(def);
            if (area != null)
                return area;

            area = new Area_Cleaning(map.areaManager, def);
            map.areaManager.AllAreas.Add(area);
            sortAreas(map.areaManager);
            areas[def] = area;
            return area;
        }

        /// <summary>
        /// Paints every cell of a room into <paramref name="def"/>'s area (and out of every other category's area).
        /// Pass null to clear the override so the room is routed automatically again.
        /// </summary>
        public void PaintRoom(Room room, CleaningCategoryDef def)
        {
            if (room == null)
                return;
            var cells = room.Cells.ToList();
            foreach (var category in CleaningCategoryDef.All)
            {
                bool paint = category == def;
                var area = paint ? EnsureArea(category) : GetArea(category);
                if (area == null)
                    continue;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (area[cells[i]] != paint)
                        area[cells[i]] = paint;
                }
            }
        }

        /// <summary>The category most of a room's cells are painted into, or null when the room is unpainted.</summary>
        public CleaningCategoryDef OverrideFor(Room room)
        {
            if (room == null)
                return null;
            CleaningCategoryDef best = null;
            int bestCount = 0;
            foreach (var category in CleaningCategoryDef.All)
            {
                var area = GetArea(category);
                if (area == null)
                    continue;
                int count = 0;
                foreach (var cell in room.Cells)
                {
                    if (area[cell])
                        count++;
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    best = category;
                }
            }
            return best;
        }

        #endregion
    }
}
