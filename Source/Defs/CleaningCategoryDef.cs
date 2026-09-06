using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>Which Work-tab column does the cleaning for a Custom lane entry.</summary>
    public enum CustomCleaningPriority
    {
        High,
        Normal,
        Low
    }

    /// <summary>
    /// One swim lane of the Cleaning tab. Three lanes (clean rooms, interior, exterior) own a work giver and therefore a
    /// Work-tab column; the Ignored lane claims nothing; the Custom lane routes each entry to one of the three columns
    /// according to its own <see cref="CustomCleaningConfig"/>.
    /// </summary>
    public class CleaningCategoryDef : Def
    {
        /// <summary>Work giver whose Work-tab column cleans this lane. Null for the ignored and custom lanes.</summary>
        public WorkGiverDef workGiver;

        /// <summary>Room types can be assigned to this lane as a default in the mod settings.</summary>
        public bool acceptsRoomTypes = true;

        /// <summary>The mod settings let the player choose which filth kinds this lane cleans.</summary>
        public bool hasFilthKindFilter;

        public bool isCustom;
        public bool isIgnored;

        /// <summary>Custom entries with this priority are cleaned by this lane's Work-tab column.</summary>
        public bool servesCustomPriority;
        public CustomCleaningPriority customPriority = CustomCleaningPriority.Normal;

        /// <summary>Receives filth in enclosed rooms whose type is not assigned anywhere.</summary>
        public bool indoorDefault;

        /// <summary>Receives filth outdoors (and in rooms the game counts as outdoors) whose type is not assigned anywhere.</summary>
        public bool outdoorDefault;

        public List<RoomRoleDef> defaultRoomRoles = new List<RoomRoleDef>();
        public List<CleaningFilthKindDef> defaultFilthKinds = new List<CleaningFilthKindDef>();

        /// <summary>Lane order in the tab and the settings.</summary>
        public int order;

        public Color color = Color.magenta;

        private static List<CleaningCategoryDef> all;
        private static Dictionary<WorkGiverDef, CleaningCategoryDef> byWorkGiver;

        public WorkTypeDef WorkType => workGiver?.workType;

        /// <summary>All lanes in order. Empty until defs are loaded.</summary>
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
        public static CleaningCategoryDef Custom => All.FirstOrDefault(d => d.isCustom);
        public static CleaningCategoryDef Ignored => All.FirstOrDefault(d => d.isIgnored);

        /// <summary>The lane (and Work-tab column) that serves a custom priority.</summary>
        public static CleaningCategoryDef ForPriority(CustomCleaningPriority priority)
        {
            return All.FirstOrDefault(d => d.servesCustomPriority && d.customPriority == priority)
                ?? All.FirstOrDefault(d => d.workGiver != null);
        }

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
            if (workGiver == null && !isCustom && !isIgnored)
                yield return "CleaningCategoryDef " + defName + " has no workGiver but is neither custom nor ignored";
        }
    }
}
