using RimWorld;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>Architect > Zone > Cleaning zone. Same mechanics as growing/stockpile zones; one cell belongs to one zone.</summary>
    public class Designator_ZoneAdd_Cleaning : Designator_ZoneAdd
    {
        protected override string NewZoneLabel => "CC_CleaningZone".Translate();

        public Designator_ZoneAdd_Cleaning()
        {
            zoneTypeToPlace = typeof(Zone_Cleaning);
            defaultLabel = "CC_CleaningZone".Translate();
            defaultDesc = "CC_CleaningZoneDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile");
            tutorTag = "ZoneAdd_Cleaning";
            hotKey = null;
        }

        protected override Zone MakeNewZone()
        {
            return new Zone_Cleaning(Find.CurrentMap.zoneManager);
        }
    }
}
