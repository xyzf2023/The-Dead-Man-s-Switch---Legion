using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 当分配到的账单为「培育 Commander」时，禁止 Commander 接单；
    /// 培育器上其他账单（如培育其他机械体）仍可由 Commander 执行。
    /// </summary>
    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    public static class Patch_WorkGiver_DoBill_JobOnThing_Gestation
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Job? __result)
        {
            if (__result == null || pawn == null)
                return;

            if (__result.bill is not Bill_Production bill || bill.recipe == null)
                return;

            if (!MechGestatorRecipeUtility.IsCommanderDisabledForGestation(bill.recipe, pawn))
                return;

            __result = null;
            if (bill.PawnRestriction == pawn)
                bill.SetAnyPawnRestriction();
        }
    }
}
