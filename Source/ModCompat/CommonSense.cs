using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace PeteTimesSix.CategorizedCleaning.ModCompat
{
    [StaticConstructorOnStartup]
    public static class CommonSense
    {
        public static bool active = false;

        public static Func<WorkGiverDef> getter_CleanFilth;

        static CommonSense()
        {
            if (ModLister.GetActiveModWithIdentifier("avilmask.CommonSense", true) != null)
            {
                active = true;

                try
                {
                    var getter = AccessTools.TypeByName("CommonSense.Utility").PropertyGetter("CleanFilth");
                    getter_CleanFilth = AccessTools.MethodDelegate<Func<WorkGiverDef>>(getter);
                }
                catch (Exception ex)
                {
                    Log.Warning("CC: Failed to initialize compat. with CommonSense. Exception: " + ex.ToString());

                    active = false;
                    getter_CleanFilth = null;
                }

                if (active)
                {
                    CategorizedCleaning_Mod.Harmony.Patch(AccessTools.TypeByName("CommonSense.Utility").Method("SelectAllFilth"), transpiler: new HarmonyMethod(typeof(CommonSense), nameof(CommonSense.SelectAllFilthTranspiler)));
                }
            }
        }

        public static IEnumerable<CodeInstruction> SelectAllFilthTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
            CodeMatch[] toMatch = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Call,AccessTools.TypeByName("CommonSense.Utility").PropertyGetter("CleanFilth"))
            };
            //thanks to display class shenanigans, the reference to Room needs to be pilfered from an instruction elsewhere instead of using Ldloc_2
            CodeMatch[] roomFieldPilfer = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Ldloc_0),
                new CodeMatch(OpCodes.Ldfld),
                new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Room), nameof(Room.IsHuge)))
            };

            CodeInstruction[] toInsert = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CommonSense), nameof(GetWorkGiver)))
            };

            codeMatcher.MatchStartForward(roomFieldPilfer);
            if (codeMatcher.IsInvalid)
            {
                Log.Warning("CC: Failed to apply CommonSense SelectAllFilth transpiler (in step 1). May result in issues with pre-job room clearing.");
                return instructions;
            }
            else
            {
                toInsert[1].operand = codeMatcher.InstructionAt(1).operand;
                codeMatcher.Start();
            }

            codeMatcher.MatchStartForward(toMatch);

            if (codeMatcher.IsInvalid)
            {
                Log.Warning("CC: Failed to apply CommonSense SelectAllFilth transpiler (in step 2). May result in issues with pre-job room clearing.");
                return instructions;
            }
            else
            {
                codeMatcher.InstructionAt(0).MoveLabelsTo(toInsert[0]);

                codeMatcher.RemoveInstructions(toMatch.Length);
                codeMatcher.Insert(toInsert);
            }

            return codeMatcher.InstructionEnumeration();
        }

        /// <summary>
        /// The work giver whose category would claim filth in this room. Falls back to CommonSense's own getter
        /// (which other mods may have patched) when the room resolves to the vanilla CleanFilth giver or to nothing.
        /// </summary>
        public static WorkGiverDef GetWorkGiver(Room room)
        {
            var cache = room?.Map?.GetComponent<FilthCache>();
            if (cache != null && room.CellCount > 0)
            {
                var category = cache.Classify(room.Cells.First(), room);
                if (category?.workGiver != null && category.workGiver.defName != "CleanFilth")
                    return category.workGiver;
            }

            //call the original getter in case someone else patched it
            return getter_CleanFilth();
        }
    }
}
