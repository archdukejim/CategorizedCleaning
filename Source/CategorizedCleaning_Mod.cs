using HarmonyLib;
using UnityEngine;
using Verse;

namespace PeteTimesSix.CategorizedCleaning
{
    public class CategorizedCleaning_Mod : Mod
    {
        public static CategorizedCleaning_Mod ModSingleton { get; private set; }
        public static CategorizedCleaning_Settings Settings { get; internal set; }

        public static Harmony Harmony { get; internal set; }

        public CategorizedCleaning_Mod(ModContentPack content) : base(content)
        {
            ModSingleton = this;

            Harmony = new Harmony("PeteTimesSix.CategorizedCleaning");
            Harmony.PatchAll();
        }

        public override string SettingsCategory()
        {
            return "CategorizedCleaning_ModTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        /// <summary>Called when the settings window closes: push the edited categories into the live defs and maps.</summary>
        public override void WriteSettings()
        {
            base.WriteSettings();
            CategorizedCleaning_Settings.ApplyToGame();
        }
    }


    [StaticConstructorOnStartup]
    public static class CategorizedCleaning_PostInit
    {
        static CategorizedCleaning_PostInit()
        {
            CategorizedCleaning_Mod.Settings = CategorizedCleaning_Mod.ModSingleton.GetSettings<CategorizedCleaning_Settings>();
            CategorizedCleaning_Settings.ApplyToGame();
        }
    }

}
