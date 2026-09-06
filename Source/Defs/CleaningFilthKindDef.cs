using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// A coarse kind of filth the player can toggle per lane: body fluids, debris, natural dirt. A filth def belongs to
    /// the first kind (by order) that lists it, else to a kind matching terrain-sourced filth, else to the fallback kind,
    /// so filth from other mods always lands somewhere sensible.
    /// </summary>
    public class CleaningFilthKindDef : Def
    {
        public List<ThingDef> filthDefs = new List<ThingDef>();

        /// <summary>Matches filth whose placement mask includes Terrain (dirt, sand, mud tracked in from outside).</summary>
        public bool matchesTerrainSourced;

        /// <summary>Catches every filth def no other kind claims.</summary>
        public bool fallback;

        public int order;

        private static List<CleaningFilthKindDef> all;
        private static readonly Dictionary<ThingDef, CleaningFilthKindDef> cache = new Dictionary<ThingDef, CleaningFilthKindDef>();

        public static List<CleaningFilthKindDef> All
        {
            get
            {
                if (all == null || all.Count == 0)
                {
                    var defs = DefDatabase<CleaningFilthKindDef>.AllDefsListForReading;
                    if (defs.Count == 0)
                        return new List<CleaningFilthKindDef>();
                    all = defs.OrderBy(d => d.order).ToList();
                }
                return all;
            }
        }

        public static CleaningFilthKindDef KindOf(ThingDef filthDef)
        {
            if (filthDef == null)
                return null;
            if (cache.TryGetValue(filthDef, out var cached))
                return cached;

            var kinds = All;
            CleaningFilthKindDef result = kinds.FirstOrDefault(k => k.filthDefs.Contains(filthDef));
            if (result == null && filthDef.filth != null && filthDef.filth.TerrainSourced)
                result = kinds.FirstOrDefault(k => k.matchesTerrainSourced);
            if (result == null)
                result = kinds.FirstOrDefault(k => k.fallback) ?? kinds.FirstOrDefault();

            cache[filthDef] = result;
            return result;
        }
    }
}
