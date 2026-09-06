using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse;
using static HarmonyLib.AccessTools;

namespace PeteTimesSix.CategorizedCleaning.HarmonyPatches
{
    [HarmonyPatch(typeof(Room), nameof(Room.Notify_RoomShapeChanged))]
    public static class Room_Notify_RoomShapeChanged_Patches
    {
        [HarmonyPrefix]
        public static void Room_Notify_RoomShapeChanged_Prefix(Room __instance)
        {
            __instance.Map?.GetComponent<FilthCache>()?.Notify_RoomChanged(__instance);
        }
    }

    /// <summary>
    /// Room roles drive categorization, so filth must be reclassified whenever a room's role actually changes.
    /// Injects a compare-and-notify around the single write to Room.role inside UpdateRoomStatsAndRole.
    /// </summary>
    [HarmonyPatch(typeof(Room), "UpdateRoomStatsAndRole")]
    public static class Room_UpdateRoomStatsAndRole_Patches
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Room_UpdateRoomStatsAndRole_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var toMatch = new CodeMatch(OpCodes.Stfld, Field(typeof(Room), "role"));
            var storageLocal = ilGenerator.DeclareLocal(typeof(RoomRoleDef));

            var prefix = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, Field(typeof(Room), "role")),
                new CodeInstruction(OpCodes.Stloc, storageLocal.LocalIndex),
            };
            //split so that by the time we call PostChangeCompare role is already set for calls from other functions
            var postfix = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, Field(typeof(Room), "role")),
                new CodeInstruction(OpCodes.Ldloc, storageLocal.LocalIndex),
                new CodeInstruction(OpCodes.Call, Method(typeof(Room_UpdateRoomStatsAndRole_Patches), nameof(PostChangeCompare))),
            };

            foreach (CodeInstruction instruction in instructions)
            {
                bool isWriteToRoleField = toMatch.opcode == instruction.opcode && toMatch.operand == instruction.operand;
                if (isWriteToRoleField)
                {
                    foreach (var prefixInstruction in prefix)
                        yield return prefixInstruction;
                }

                yield return instruction;

                if (isWriteToRoleField)
                {
                    foreach (var postfixInstruction in postfix)
                        yield return postfixInstruction;
                }
            }
        }

        public static void PostChangeCompare(Room room, RoomRoleDef first, RoomRoleDef second)
        {
            if (first != second)
                room.Map?.GetComponent<FilthCache>()?.Notify_RoomChanged(room);
        }
    }
}
