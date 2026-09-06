using RimWorld;
using System.Collections.Generic;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Right-click inside an enclosed room (with a colonist selected) to pin that room to a cleaning lane, or to
    /// return it to automatic routing. One entry that opens a submenu, so the vanilla order menu stays uncluttered.
    /// The Cleaning tab offers the same choices with more context.
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
            var cache = context.map.GetComponent<FilthCache>();
            var pin = cache.PinFor(room);
            var automatic = FilthCache.AutomaticLane(room);
            string currentLabel = pin != null ? pin.lane.LabelCap.ToString() : "CC_RoomOverride_AutoShort".Translate(automatic?.label ?? "-").ToString();

            return new FloatMenuOption("CC_RoomOverride_Menu".Translate(currentLabel), delegate
            {
                Find.WindowStack.Add(new FloatMenu(BuildSubmenu(room, cache, pin?.lane, automatic)));
            }, MenuOptionPriority.Low, delegate
            {
                room.DrawFieldEdges();
            });
        }

        private static List<FloatMenuOption> BuildSubmenu(Room room, FilthCache cache, CleaningCategoryDef current, CleaningCategoryDef automatic)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CC_RoomOverride_Auto".Translate(automatic?.label ?? "-"), current == null ? null : (System.Action)delegate
                {
                    cache.PinRoom(room, null);
                })
            };

            foreach (var def in CleaningCategoryDef.All)
            {
                var local = def;
                options.Add(new FloatMenuOption("CC_RoomOverride_Set".Translate(local.label), local == current ? null : (System.Action)delegate
                {
                    cache.PinRoom(room, local);
                }));
            }
            return options;
        }
    }
}
