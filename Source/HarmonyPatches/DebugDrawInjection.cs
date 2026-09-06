using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning.HarmonyPatches
{
    [HarmonyPatch(typeof(DoorsDebugDrawer), nameof(DoorsDebugDrawer.DrawDebug))]
    public static class DebugDrawInjection
    {
        [HarmonyPostfix]
        public static void AdditionalDebugDrawingInjection()
        {
            DrawOnMap();
        }

        public static void DrawOnMap()
        {
            if (!CategorizedCleaning_Debug.drawFilthCategoryOverlay)
                return;

            var map = Find.CurrentMap;
            if (map == null)
                return;

            var filthCache = map.GetComponent<FilthCache>();
            CellRect currentViewRect = Find.CameraDriver.CurrentViewRect;

            foreach (var def in CleaningCategoryDef.All)
            {
                var color = def.color;
                color.a = 0.25f;
                var material = SolidColorMaterials.SimpleSolidColorMaterial(color);
                foreach (var filth in filthCache.FilthFor(def))
                {
                    if (filth.Spawned && currentViewRect.Contains(filth.Position))
                        CellRenderer.RenderCell(filth.Position, material);
                }
            }
        }
    }
}
