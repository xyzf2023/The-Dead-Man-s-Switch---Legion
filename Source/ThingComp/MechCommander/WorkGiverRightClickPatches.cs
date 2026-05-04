using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 放行指挥官在 Smithing/Crafting 工作优先级为 0 时仍显示「在机械组装平台进行工作」等右键优先选项。
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuOptionProvider_WorkGivers), "GetWorkGiverOption")]
    public static class Patch_FloatMenuOptionProvider_WorkGivers_AllowCommanderZeroPriority
    {
        private static bool AllowZeroPriority(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || workType == null)
                return false;
            if (!CompMechCommanderMarker.PawnHasMarker(pawn))
                return false;
            return workType == WorkTypeDefOf.Crafting || workType == WorkTypeDefOf.Smithing;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            MethodInfo getPriority = AccessTools.Method(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.GetPriority));
            MethodInfo allowZeroPriority = AccessTools.Method(typeof(Patch_FloatMenuOptionProvider_WorkGivers_AllowCommanderZeroPriority), nameof(AllowZeroPriority));

            for (int i = 0; i < codes.Count - 2; i++)
            {
                CodeInstruction first = codes[i];
                if (first.opcode == OpCodes.Callvirt && (MethodInfo)first.operand == getPriority
                    && codes[i + 1].opcode == OpCodes.Ldc_I4_0
                    && codes[i + 2].opcode == OpCodes.Ceq)
                {
                    CodeInstruction loadWorkType = codes[i - 1];
                    if (!IsLoadLocal(loadWorkType.opcode))
                        break;

                    codes.InsertRange(i + 3, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(loadWorkType.opcode, loadWorkType.operand),
                        new CodeInstruction(OpCodes.Call, allowZeroPriority),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ceq),
                        new CodeInstruction(OpCodes.And)
                    });
                    break;
                }
            }

            return codes;
        }

        private static bool IsLoadLocal(OpCode opcode)
        {
            return opcode == OpCodes.Ldloc_0 || opcode == OpCodes.Ldloc_1
                || opcode == OpCodes.Ldloc_2 || opcode == OpCodes.Ldloc_3
                || opcode == OpCodes.Ldloc || opcode == OpCodes.Ldloc_S;
        }
    }
}
