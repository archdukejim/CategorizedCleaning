using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    /// <summary>
    /// A cleaning zone drawn with the Zone tool. Filth on its cells is routed by its lane regardless of the home area
    /// or the room it lies in. New zones start in the Custom lane with everything ticked and normal priority.
    /// </summary>
    public class Zone_Cleaning : Zone
    {
        private static readonly AccessTools.FieldRef<Zone, Material> materialRef = AccessTools.FieldRefAccess<Zone, Material>("materialInt");

        public CleaningCategoryDef lane;
        public CustomCleaningConfig custom = new CustomCleaningConfig();

        public Zone_Cleaning() { }

        public Zone_Cleaning(ZoneManager zoneManager) : base("CC_CleaningZone".Translate(), zoneManager)
        {
            lane = CleaningCategoryDef.Custom ?? CleaningCategoryDef.All.FirstOrDefault();
            ApplyLaneColor();
        }

        public override bool IsMultiselectable => true;

        protected override Color NextZoneColor => CleaningCategoryDef.Custom?.color ?? Color.cyan;

        public bool IsCustom => lane != null && lane.isCustom;

        public void SetLane(CleaningCategoryDef newLane)
        {
            if (newLane == null || newLane == lane)
                return;
            lane = newLane;
            ApplyLaneColor();
            Map?.GetComponent<FilthCache>()?.Notify_ZoneChanged(this);
        }

        public void Notify_ConfigChanged()
        {
            Map?.GetComponent<FilthCache>()?.Notify_ZoneChanged(this);
        }

        private void ApplyLaneColor()
        {
            color = lane?.color ?? Color.cyan;
            materialRef(this) = null;
            if (Map != null)
            {
                foreach (var cell in cells)
                    Map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Zone);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref lane, "lane");
            Scribe_Deep.Look(ref custom, "custom");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lane == null)
                    lane = CleaningCategoryDef.Custom ?? CleaningCategoryDef.All.FirstOrDefault();
                if (custom == null)
                    custom = new CustomCleaningConfig();
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            sb.AppendLine();
            sb.Append("CC_Zone_Lane".Translate(lane?.LabelCap ?? "-"));
            if (IsCustom)
            {
                sb.AppendLine();
                sb.Append(custom.Summary());
            }
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetZoneAddGizmos()
        {
            var designator = DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_Cleaning>();
            if (designator != null)
                yield return designator;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            yield return new Command_Action
            {
                defaultLabel = "CC_Zone_LaneCommand".Translate(lane?.LabelCap ?? "-"),
                defaultDesc = "CC_Zone_LaneCommandDesc".Translate(),
                icon = TexCommand.ForbidOff,
                action = delegate
                {
                    var options = new List<FloatMenuOption>();
                    foreach (var def in CleaningCategoryDef.All)
                    {
                        var local = def;
                        options.Add(new FloatMenuOption(local.LabelCap, local == lane ? null : (System.Action)(() => SetLane(local))));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };

            if (!IsCustom)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = "CC_Custom_PriorityCommand".Translate(("CC_Priority_" + custom.priority).Translate()),
                defaultDesc = "CC_Custom_PriorityCommandDesc".Translate(),
                icon = TexCommand.ForbidOff,
                action = delegate
                {
                    var options = new List<FloatMenuOption>();
                    foreach (CustomCleaningPriority p in System.Enum.GetValues(typeof(CustomCleaningPriority)))
                    {
                        var local = p;
                        var column = CleaningCategoryDef.ForPriority(local);
                        string label = "CC_Custom_PriorityOption".Translate(("CC_Priority_" + local).Translate(), column?.WorkType?.labelShort ?? "-");
                        options.Add(new FloatMenuOption(label, local == custom.priority ? null : (System.Action)delegate
                        {
                            custom.priority = local;
                            Notify_ConfigChanged();
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };

            foreach (var kind in CleaningFilthKindDef.All)
            {
                var local = kind;
                yield return new Command_Toggle
                {
                    defaultLabel = "CC_Custom_KindToggle".Translate(local.LabelCap),
                    defaultDesc = local.description,
                    icon = TexCommand.ForbidOff,
                    isActive = () => custom.kinds.Contains(local),
                    toggleAction = delegate
                    {
                        custom.ToggleKind(local);
                        Notify_ConfigChanged();
                    }
                };
            }

            yield return new Command_Toggle
            {
                defaultLabel = "CC_Custom_CutPlants".Translate(),
                defaultDesc = "CC_Custom_CutPlantsDesc".Translate(),
                icon = TexCommand.ForbidOff,
                isActive = () => custom.cutPlants,
                toggleAction = delegate { custom.cutPlants = !custom.cutPlants; }
            };
            yield return new Command_Toggle
            {
                defaultLabel = "CC_Custom_ClearSnow".Translate(),
                defaultDesc = "CC_Custom_ClearSnowDesc".Translate(),
                icon = TexCommand.ForbidOff,
                isActive = () => custom.clearSnow,
                toggleAction = delegate { custom.clearSnow = !custom.clearSnow; }
            };
            yield return new Command_Toggle
            {
                defaultLabel = "CC_Custom_HaulDebris".Translate(),
                defaultDesc = "CC_Custom_HaulDebrisDesc".Translate(),
                icon = TexCommand.ForbidOff,
                isActive = () => custom.haulDebris,
                toggleAction = delegate { custom.haulDebris = !custom.haulDebris; }
            };
        }
    }
}
