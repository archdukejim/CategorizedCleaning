using RimWorld;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// Paints the Custom cleaning zone (the area of the category flagged <c>paintable</c>) from Architect > Zone,
    /// mirroring the vanilla snow-clear designators.
    /// </summary>
    public abstract class Designator_AreaCleaning : Designator_Cells
    {
        private readonly DesignateMode mode;

        public override bool DragDrawMeasurements => true;

        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

        protected Designator_AreaCleaning(DesignateMode mode)
        {
            this.mode = mode;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
        }

        protected static CleaningCategoryDef Category => CleaningCategoryDef.Paintable;

        public override bool Visible => Category != null;

        protected Area_Cleaning CurrentArea => Category == null ? null : Map.GetComponent<FilthCache>().GetArea(Category);

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || Category == null)
                return false;
            var area = CurrentArea;
            bool inArea = area != null && area[c];
            return mode == DesignateMode.Add ? !inArea : inArea;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (Category == null)
                return;
            var cache = Map.GetComponent<FilthCache>();
            if (mode == DesignateMode.Add)
            {
                // A cell belongs to one category at a time: painting the zone clears any room override there.
                foreach (var other in CleaningCategoryDef.All)
                {
                    if (other == Category)
                        continue;
                    var otherArea = cache.GetArea(other);
                    if (otherArea != null && otherArea[c])
                        otherArea[c] = false;
                }
                cache.EnsureArea(Category)[c] = true;
            }
            else
            {
                var area = CurrentArea;
                if (area != null)
                    area[c] = false;
            }
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            CurrentArea?.MarkForDraw();
        }
    }

    public class Designator_AreaCleaningExpand : Designator_AreaCleaning
    {
        public Designator_AreaCleaningExpand() : base(DesignateMode.Add)
        {
            defaultLabel = "CC_DesignatorCustomZoneExpand".Translate();
            defaultDesc = "CC_DesignatorCustomZoneExpandDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedExpand");
            soundDragSustain = SoundDefOf.Designate_DragAreaAdd;
            soundDragChanged = SoundDefOf.Designate_DragZone_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneAdd_AllowedArea;
        }
    }

    public class Designator_AreaCleaningClear : Designator_AreaCleaning
    {
        public Designator_AreaCleaningClear() : base(DesignateMode.Remove)
        {
            defaultLabel = "CC_DesignatorCustomZoneClear".Translate();
            defaultDesc = "CC_DesignatorCustomZoneClearDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedClear");
            soundDragSustain = SoundDefOf.Designate_DragAreaDelete;
            soundDragChanged = null;
            soundSucceeded = SoundDefOf.Designate_ZoneDelete;
        }
    }
}
