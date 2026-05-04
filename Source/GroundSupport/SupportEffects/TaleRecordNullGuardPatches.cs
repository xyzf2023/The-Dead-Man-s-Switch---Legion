using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 方案一：据点击败时若原版尝试记录 CaravanAssaultSuccessful 且传入的 Pawn 为 null（如核打击/空袭摧毁据点、无 caravan 攻击者），
    /// 则跳过此次轶事记录，避免 TaleData_Pawn.GenerateFrom(null) 的 NullReferenceException。
    /// </summary>
    [HarmonyPatch(typeof(TaleRecorder), nameof(TaleRecorder.RecordTale))]
    public static class TaleRecordNullGuardPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(TaleDef def, object[] args)
        {
            if (def == null) return true;
            if (def.defName != "CaravanAssaultSuccessful") return true;

            // CaravanAssaultSuccessful 需要单 Pawn 参数；若为 null 则跳过记录
            if (args == null || args.Length == 0 || args[0] == null)
                return false;

            return true;
        }
    }
}
