using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Per-entry settings for something in the Custom lane (a cleaning zone or a pinned room): which filth kinds are
    /// cleaned, which Work-tab column does it, and the extra upkeep jobs the map component schedules for its cells.
    /// </summary>
    public class CustomCleaningConfig : IExposable
    {
        public List<CleaningFilthKindDef> kinds;
        public CustomCleaningPriority priority = CustomCleaningPriority.Normal;
        public bool cutPlants;
        public bool clearSnow;
        public bool haulDebris;

        public CustomCleaningConfig()
        {
            kinds = new List<CleaningFilthKindDef>(CleaningFilthKindDef.All);
        }

        public bool Allows(ThingDef filthDef)
        {
            var kind = CleaningFilthKindDef.KindOf(filthDef);
            return kind != null && kinds.Contains(kind);
        }

        /// <summary>The lane whose Work-tab column cleans this entry.</summary>
        public CleaningCategoryDef WorkLane => CleaningCategoryDef.ForPriority(priority);

        public bool NeedsUpkeep => cutPlants || clearSnow || haulDebris;

        public void ToggleKind(CleaningFilthKindDef kind)
        {
            if (!kinds.Remove(kind))
                kinds.Add(kind);
        }

        public CustomCleaningConfig Copy()
        {
            return new CustomCleaningConfig
            {
                kinds = new List<CleaningFilthKindDef>(kinds),
                priority = priority,
                cutPlants = cutPlants,
                clearSnow = clearSnow,
                haulDebris = haulDebris,
            };
        }

        public string Summary()
        {
            var sb = new StringBuilder();
            var workLane = WorkLane;
            sb.Append("CC_Custom_PrioritySummary".Translate(("CC_Priority_" + priority).Translate(), workLane?.WorkType?.labelShort ?? "-"));
            sb.Append("\n");
            var allKinds = CleaningFilthKindDef.All;
            if (kinds.Count == allKinds.Count)
                sb.Append("CC_Custom_KindsAll".Translate());
            else if (kinds.Count == 0)
                sb.Append("CC_Custom_KindsNone".Translate());
            else
                sb.Append("CC_Custom_KindsSome".Translate(kinds.Select(k => k.label).ToCommaList()));
            var tasks = new List<string>();
            if (cutPlants) tasks.Add("CC_Custom_CutPlants".Translate());
            if (clearSnow) tasks.Add("CC_Custom_ClearSnow".Translate());
            if (haulDebris) tasks.Add("CC_Custom_HaulDebris".Translate());
            if (tasks.Count > 0)
                sb.Append("\n").Append("CC_Custom_Tasks".Translate(tasks.ToCommaList()));
            return sb.ToString();
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref kinds, "kinds", LookMode.Def);
            Scribe_Values.Look(ref priority, "priority", CustomCleaningPriority.Normal);
            Scribe_Values.Look(ref cutPlants, "cutPlants", false);
            Scribe_Values.Look(ref clearSnow, "clearSnow", false);
            Scribe_Values.Look(ref haulDebris, "haulDebris", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (kinds == null)
                    kinds = new List<CleaningFilthKindDef>(CleaningFilthKindDef.All);
                else
                    kinds.RemoveAll(k => k == null);
            }
        }
    }
}
