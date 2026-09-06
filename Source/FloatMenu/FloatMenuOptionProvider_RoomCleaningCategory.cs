using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Right-click inside an enclosed room (with a colonist selected) to pin that room to a cleaning column, or to
    /// return it to automatic routing. A single entry that opens a submenu, so the vanilla order menu stays uncluttered.
    /// Discovered automatically by FloatMenuMakerMap via reflection.
    /// </summary>
    public class FloatMenuOptionProvider_RoomCleaningCategory : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => true;
        protected override bool MechanoidCanDo => true;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            var room = context.ClickedRoom;
            return room != null && !room.PsychologicallyOutdoors && !room.TouchesMapEdge && room.CellCount > 0
                && CleaningCategoryDef.All.Count > 0;
        }

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            var room = context.ClickedRoom;
            var map = context.map;
            var cache = map.GetComponent<FilthCache>();
            var current = cache.OverrideFor(room);
            var automatic = AutomaticCategoryFor(room);
            string currentLabel = current != null ? current.LabelCap.ToString() : "CC_RoomOverride_AutoShort".Translate(automatic?.label ?? "-").ToString();

            return new FloatMenuOption("CC_RoomOverride_Menu".Translate(currentLabel), delegate
            {
                Find.WindowStack.Add(new FloatMenu(BuildSubmenu(room, cache, current, automatic)));
            }, MenuOptionPriority.Low, delegate
            {
                room.DrawFieldEdges();
            });
        }

        private static List<FloatMenuOption> BuildSubmenu(Room room, FilthCache cache, CleaningCategoryDef current, CleaningCategoryDef automatic)
        {
            var options = new List<FloatMenuOption>();

            string autoLabel = "CC_RoomOverride_Auto".Translate(automatic?.label ?? "-");
            options.Add(new FloatMenuOption(autoLabel, current == null ? null : (System.Action)delegate
            {
                cache.PaintRoom(room, null);
            }));

            foreach (var def in CleaningCategoryDef.All)
            {
                var local = def;
                string label = "CC_RoomOverride_Set".Translate(local.label);
                options.Add(new FloatMenuOption(label, local == current ? null : (System.Action)delegate
                {
                    cache.PaintRoom(room, local);
                }, MenuOptionPriority.Default, delegate
                {
                    cache.GetArea(local)?.MarkForDraw();
                }));
            }
            return options;
        }

        /// <summary>Where the room's filth goes without an override: its room type's column, else the indoor default.</summary>
        private static CleaningCategoryDef AutomaticCategoryFor(Room room)
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
    }
}
