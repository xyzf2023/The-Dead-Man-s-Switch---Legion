using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 当 PawnCanUseWorkGiver 因机械体不可做深钻井而返回 false 时，对钻井驳机放行。
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    public static class JobGiver_Work_PawnCanUseWorkGiver_DrillingBargeDeepDrillPatch
    {
        private const string DrillingBargeRaceDefName = "DMSL_Mech_DrillingBarge";
        private const string DrillWorkGiverDefName = "Drill";

        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Pawn pawn, WorkGiver giver)
        {
            if (__result)
                return;
            if (pawn?.def?.defName != DrillingBargeRaceDefName || giver?.def?.defName != DrillWorkGiverDefName)
                return;
            __result = true;
        }
    }
}
