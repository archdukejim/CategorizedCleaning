using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Cleans only the filth that <see cref="FilthCache"/> has assigned to this work giver's category.
    /// Unlike vanilla, filth outside the Home area is fair game as long as its category's area contains it.
    /// </summary>
    public class WorkGiver_CleanFilth_Categorized : WorkGiver_CleanFilth
    {
        private const int MinTicksSinceThickened = 600;

        private CleaningCategoryDef category;
        private bool categoryResolved;

        public CleaningCategoryDef Category
        {
            get
            {
                if (!categoryResolved)
                {
                    category = CleaningCategoryDef.ForWorkGiver(def);
                    categoryResolved = true;
                    if (category == null)
                        Log.Warning($"CC: WorkGiverDef {def?.defName ?? "NULL"} is not referenced by any CleaningCategoryDef; it will never find work.");
                }
                return category;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var cat = Category;
            if (cat == null)
                return true;
            return pawn.Map.GetComponent<FilthCache>().CountFor(cat) == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var cat = Category;
            if (cat == null)
                return Enumerable.Empty<Thing>();
            return pawn.Map.GetComponent<FilthCache>().FilthFor(cat);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Filth filth) || filth.Map == null)
                return false;
            var cat = Category;
            if (cat == null || filth.Map.GetComponent<FilthCache>().OwnerOf(filth) != cat)
                return false;
            if (filth.Fogged() || !pawn.CanReserve(t, 1, -1, null, forced))
                return false;
            if (filth.TicksSinceThickened < MinTicksSinceThickened)
                return false;
            return true;
        }
    }
}
