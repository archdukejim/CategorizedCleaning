using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// One cleaning column: a work type / work giver pair plus how filth is routed to it.
    /// Routing, in order: (1) cells painted into the category's own area (the Custom zone, or a room override made by
    /// right-clicking a room), (2) the room type lists from the mod settings, (3) the indoor/outdoor defaults.
    /// </summary>
    public class CleaningCategoryDef : Def
    {
        public WorkGiverDef workGiver;

        /// <summary>Room types routed here until the player moves them in the settings.</summary>
        public List<RoomRoleDef> defaultRoomRoles = new List<RoomRoleDef>();

        /// <summary>Receives filth in enclosed rooms whose type is not listed anywhere.</summary>
        public bool indoorDefault;

        /// <summary>Receives filth outdoors (and in rooms the game counts as outdoors) whose type is not listed anywhere.</summary>
        public bool outdoorDefault;

        /// <summary>Gets Expand/Clear designators under Architect > Zone so the player can paint its area directly.</summary>
        public bool paintable;

        /// <summary>Column order in the settings screen.</summary>
        public int order;

        /// <summary>Used for the area drawn on the map and the debug overlay.</summary>
        public Color color = Color.magenta;

        /// <summary>Label of this category's map area; falls back to the def label.</summary>
        public string areaLabel;

        private static List<CleaningCategoryDef> all;
        private static Dictionary<WorkGiverDef, CleaningCategoryDef> byWorkGiver;

        public WorkTypeDef WorkType => workGiver?.workType;

        public string AreaLabel => areaLabel.NullOrEmpty() ? LabelCap.ToString() : areaLabel;

        /// <summary>All categories in column order. Empty until defs are loaded.</summary>
        public static List<CleaningCategoryDef> All
        {
            get
            {
                if (all == null || all.Count == 0)
                {
                    var defs = DefDatabase<CleaningCategoryDef>.AllDefsListForReading;
                    if (defs.Count == 0)
                        return new List<CleaningCategoryDef>();
                    all = defs.OrderBy(d => d.order).ToList();
                }
                return all;
            }
        }

        public static CleaningCategoryDef IndoorDefault => All.FirstOrDefault(d => d.indoorDefault);
        public static CleaningCategoryDef OutdoorDefault => All.FirstOrDefault(d => d.outdoorDefault);
        public static CleaningCategoryDef Paintable => All.FirstOrDefault(d => d.paintable);

        public static CleaningCategoryDef ForWorkGiver(WorkGiverDef workGiverDef)
        {
            if (byWorkGiver == null)
            {
                byWorkGiver = new Dictionary<WorkGiverDef, CleaningCategoryDef>();
                foreach (var def in All)
                {
                    if (def.workGiver != null)
                        byWorkGiver[def.workGiver] = def;
                }
            }
            return workGiverDef != null && byWorkGiver.TryGetValue(workGiverDef, out var result) ? result : null;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors())
                yield return error;
            if (workGiver == null)
                yield return "CleaningCategoryDef " + defName + " has no workGiver";
        }
    }
}
