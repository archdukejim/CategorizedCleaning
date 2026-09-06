using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Per-map index of filth by Work-tab column, plus the per-map state behind the Cleaning tab: room pins (a room
    /// moved to a lane by hand, with its custom settings) and the upkeep jobs custom entries ask for.
    /// Routing for a filth: cleaning zone on its cell, else the pin of its room, else (inside the home area only) the
    /// lane of its room type from the settings, else the indoor/outdoor default lane. The lane then decides the column,
    /// after the lane's or entry's filth-kind filter.
    /// </summary>
    public class FilthCache : MapComponent
    {
        /// <summary>A room moved to a lane by hand. Anchored to one of its cells because rooms are not persistent objects.</summary>
        public class RoomPin : IExposable
        {
            public IntVec3 anchor;
            public CleaningCategoryDef lane;
            public CustomCleaningConfig custom = new CustomCleaningConfig();

            public Room Room(Map map) => anchor.IsValid && anchor.InBounds(map) ? anchor.GetRoom(map) : null;

            public void ExposeData()
            {
                Scribe_Values.Look(ref anchor, "anchor");
                Scribe_Defs.Look(ref lane, "lane");
                Scribe_Deep.Look(ref custom, "custom");
                if (Scribe.mode == LoadSaveMode.PostLoadInit && custom == null)
                    custom = new CustomCleaningConfig();
            }
        }

        private const int UpkeepIntervalTicks = 250;
        private static readonly HashSet<Filth> Empty = new HashSet<Filth>();

        private readonly Dictionary<CleaningCategoryDef, HashSet<Filth>> filthByColumn = new Dictionary<CleaningCategoryDef, HashSet<Filth>>();
        private readonly Dictionary<Filth, CleaningCategoryDef> ownerByFilth = new Dictionary<Filth, CleaningCategoryDef>();
        private readonly Dictionary<Room, RoomPin> pinByRoom = new Dictionary<Room, RoomPin>();

        private List<RoomPin> pins = new List<RoomPin>();
        private List<IntVec3> managedSnowCells = new List<IntVec3>();

        public FilthCache(Map map) : base(map) { }

        public IReadOnlyList<RoomPin> Pins => pins;

        #region Queries

        public IEnumerable<Filth> FilthFor(CleaningCategoryDef column)
        {
            return column != null && filthByColumn.TryGetValue(column, out var set) ? set : Empty;
        }

        public int CountFor(CleaningCategoryDef column)
        {
            return column != null && filthByColumn.TryGetValue(column, out var set) ? set.Count : 0;
        }

        public CleaningCategoryDef OwnerOf(Filth filth)
        {
            return filth != null && ownerByFilth.TryGetValue(filth, out var owner) ? owner : null;
        }

        public CleaningCategoryDef Classify(Filth filth)
        {
            return Classify(filth.def, filth.Position, filth.GetRoom());
        }

        /// <summary>The Work-tab column that claims filth of <paramref name="filthDef"/> at <paramref name="cell"/>, or null.</summary>
        public CleaningCategoryDef Classify(ThingDef filthDef, IntVec3 cell, Room room)
        {
            if (map.zoneManager.ZoneAt(cell) is Zone_Cleaning zone)
                return ResolveColumn(zone.lane, zone.custom, filthDef);

            var pin = PinFor(room);
            if (pin != null)
                return ResolveColumn(pin.lane, pin.custom, filthDef);

            if (!(map.areaManager.Home?[cell] ?? false))
                return null;

            return ResolveColumn(AutomaticLane(room), null, filthDef);
        }

        /// <summary>Lane a room falls into without a pin: its room type's lane, else the indoor/outdoor default.</summary>
        public static CleaningCategoryDef AutomaticLane(Room room)
        {
            var role = room?.Role;
            if (role != null && role != RoomRoleDefOf.None)
            {
                var byRole = CategorizedCleaning_Settings.CategoryForRole(role);
                if (byRole != null)
                    return byRole;
            }
            bool outdoors = room?.PsychologicallyOutdoors ?? true;
            return outdoors ? CleaningCategoryDef.OutdoorDefault : CleaningCategoryDef.IndoorDefault;
        }

        /// <summary>Lane shown for a room in the tab: its pin, else the automatic lane.</summary>
        public CleaningCategoryDef LaneFor(Room room)
        {
            return PinFor(room)?.lane ?? AutomaticLane(room);
        }

        /// <summary>Work-tab column a room's filth would go to, ignoring filth-kind filters (used by CommonSense compat).</summary>
        public CleaningCategoryDef WorkColumnForRoom(Room room)
        {
            var pin = PinFor(room);
            var lane = pin?.lane ?? AutomaticLane(room);
            if (lane == null || lane.isIgnored)
                return null;
            if (lane.isCustom)
                return (pin?.custom ?? new CustomCleaningConfig()).WorkLane;
            return lane;
        }

        private static CleaningCategoryDef ResolveColumn(CleaningCategoryDef lane, CustomCleaningConfig custom, ThingDef filthDef)
        {
            if (lane == null || lane.isIgnored)
                return null;
            if (lane.isCustom)
            {
                if (custom == null || !custom.Allows(filthDef))
                    return null;
                return custom.WorkLane;
            }
            if (lane.workGiver == null)
                return null;
            return CategorizedCleaning_Settings.LaneAllows(lane, filthDef) ? lane : null;
        }

        #endregion

        #region Room pins

        public RoomPin PinFor(Room room)
        {
            if (room == null || pins.Count == 0)
                return null;
            if (pinByRoom.TryGetValue(room, out var cached))
                return cached;

            RoomPin found = null;
            for (int i = 0; i < pins.Count; i++)
            {
                if (pins[i].Room(map) == room)
                {
                    found = pins[i];
                    break;
                }
            }
            pinByRoom[room] = found;
            return found;
        }

        /// <summary>Pins a room to a lane (null lane = back to automatic). Keeps the room's custom settings across moves.</summary>
        public void PinRoom(Room room, CleaningCategoryDef lane)
        {
            if (room == null || room.CellCount == 0)
                return;
            var existing = PinFor(room);
            var custom = existing?.custom ?? new CustomCleaningConfig();
            pins.RemoveAll(p => p == existing || p.Room(map) == room);
            if (lane != null)
                pins.Add(new RoomPin { anchor = room.Cells.First(), lane = lane, custom = custom });
            pinByRoom.Clear();
            Notify_RoomChanged(room);
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
                filthByColumn[oldOwner].Remove(filth);

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
                filthByColumn[owner].Remove(filth);
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
            pinByRoom.Clear();
            foreach (var filth in room.ContainedThings<Filth>().ToList())
                Reclassify(filth);
        }

        public void Notify_ZoneChanged(Zone_Cleaning zone)
        {
            if (zone == null)
                return;
            for (int i = zone.cells.Count - 1; i >= 0; i--)
                Notify_CellChanged(zone.cells[i]);
        }

        public void RebuildAll()
        {
            foreach (var set in filthByColumn.Values)
                set.Clear();
            ownerByFilth.Clear();
            pinByRoom.Clear();

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

        private HashSet<Filth> GetOrCreateSet(CleaningCategoryDef column)
        {
            if (!filthByColumn.TryGetValue(column, out var set))
            {
                set = new HashSet<Filth>();
                filthByColumn[column] = set;
            }
            return set;
        }

        #endregion

        #region Upkeep jobs for custom entries (cut plants, clear snow, haul debris)

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % UpkeepIntervalTicks != 7)
                return;
            RunUpkeep();
        }

        /// <summary>Cells of every custom entry that wants at least one upkeep job, with the config that asked for it.</summary>
        private IEnumerable<(CustomCleaningConfig config, IEnumerable<IntVec3> cells)> CustomEntries()
        {
            foreach (var zone in map.zoneManager.AllZones)
            {
                if (zone is Zone_Cleaning cleaningZone && cleaningZone.IsCustom)
                    yield return (cleaningZone.custom, cleaningZone.cells);
            }
            for (int i = pins.Count - 1; i >= 0; i--)
            {
                var pin = pins[i];
                var room = pin.Room(map);
                if (room == null || room.PsychologicallyOutdoors || room.TouchesMapEdge)
                {
                    // Anchor no longer sits in an enclosed room (wall built over it, room opened up): the pin is dead.
                    pins.RemoveAt(i);
                    pinByRoom.Clear();
                    continue;
                }
                if (pin.lane != null && pin.lane.isCustom)
                    yield return (pin.custom, room.Cells);
            }
        }

        private void RunUpkeep()
        {
            var snowWanted = new HashSet<IntVec3>();
            foreach (var (config, cells) in CustomEntries().ToList())
            {
                if (!config.NeedsUpkeep)
                    continue;
                foreach (var cell in cells)
                {
                    if (config.clearSnow)
                        snowWanted.Add(cell);
                    if (config.cutPlants || config.haulDebris)
                        DesignateUpkeepAt(cell, config);
                }
            }
            SyncSnowArea(snowWanted);
        }

        private void DesignateUpkeepAt(IntVec3 cell, CustomCleaningConfig config)
        {
            var designations = map.designationManager;
            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (config.cutPlants && thing is Plant plant && !plant.sown && !plant.def.plant.IsTree
                    && plant.def.plant.harvestedThingDef == null && designations.DesignationOn(plant) == null)
                {
                    designations.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
                }
                else if (config.haulDebris && thing.def.designateHaulable && !thing.IsForbidden(Faction.OfPlayer)
                    && !thing.IsInValidStorage() && designations.DesignationOn(thing, DesignationDefOf.Haul) == null)
                {
                    designations.AddDesignation(new Designation(thing, DesignationDefOf.Haul));
                }
            }
        }

        /// <summary>Keeps the vanilla snow-clear area in step with the custom entries that asked for snow clearing, touching only cells we added.</summary>
        private void SyncSnowArea(HashSet<IntVec3> wanted)
        {
            var snowArea = map.areaManager.SnowOrSandClear;
            if (snowArea == null)
                return;
            for (int i = managedSnowCells.Count - 1; i >= 0; i--)
            {
                var cell = managedSnowCells[i];
                if (!wanted.Contains(cell))
                {
                    if (cell.InBounds(map) && snowArea[cell])
                        snowArea[cell] = false;
                    managedSnowCells.RemoveAt(i);
                }
            }
            foreach (var cell in wanted)
            {
                if (!snowArea[cell])
                {
                    snowArea[cell] = true;
                    managedSnowCells.Add(cell);
                }
                else if (!managedSnowCells.Contains(cell))
                {
                    // Already painted by the player: leave ownership with them.
                }
            }
        }

        #endregion

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pins, "pins", LookMode.Deep);
            Scribe_Collections.Look(ref managedSnowCells, "managedSnowCells", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pins == null)
                    pins = new List<RoomPin>();
                pins.RemoveAll(p => p == null || p.lane == null);
                if (managedSnowCells == null)
                    managedSnowCells = new List<IntVec3>();
                pinByRoom.Clear();
            }
        }
    }
}
