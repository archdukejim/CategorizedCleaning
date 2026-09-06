using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Painted cells that route filth straight to one cleaning category: the Custom zone, or rooms the player overrode
    /// by right-clicking them. Created on demand per map by <see cref="FilthCache.EnsureArea"/>; never deletable through the UI.
    /// </summary>
    public class Area_Cleaning : Area
    {
        public CleaningCategoryDef def;

        public Area_Cleaning() { }

        public Area_Cleaning(AreaManager areaManager, CleaningCategoryDef def) : base(areaManager)
        {
            this.def = def;
        }

        public override string Label => def?.AreaLabel ?? "CC_CleaningArea".Translate();

        public override Color Color => def?.color ?? Color.magenta;

        public override int ListPriority => 4000;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref def, "def");
        }

        public override string GetUniqueLoadID()
        {
            return "Area_" + ID + "_Cleaning_" + (def?.defName ?? "Unknown");
        }
    }
}
