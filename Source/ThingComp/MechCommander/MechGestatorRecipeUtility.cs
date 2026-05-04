using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 培育相关判定：指挥官不能培育指挥官。
    /// 仅判断 DMSL Commander 与「培育 Commander」配方，不涉及 MAP 机械体。
    /// </summary>
    public static class MechGestatorRecipeUtility
    {
        private const string CommanderPawnDefName = "DMSL_Mech_Commander";
        private const string GestateCommanderRecipeDefName = "DMSL_Make_Commander";

        /// <summary>
        /// 该配方是否为「培育 Commander」。
        /// </summary>
        public static bool IsCommanderGestation(RecipeDef? recipe)
        {
            if (recipe == null)
                return false;
            return recipe.defName == GestateCommanderRecipeDefName;
        }

        /// <summary>
        /// 若该 Pawn 为 Commander 且配方为「培育 Commander」，则禁止执行（返回 true）。
        /// </summary>
        public static bool IsCommanderDisabledForGestation(RecipeDef? recipe, Pawn? pawn)
        {
            if (recipe == null || pawn == null)
                return false;
            if (!IsCommanderGestation(recipe))
                return false;
            return pawn.def?.defName == CommanderPawnDefName;
        }

        /// <summary>
        /// 账单配置中「指挥官不能培育指挥官」时显示的翻译 Key。
        /// </summary>
        public static string GetCommanderDisabledReasonKey(RecipeDef? recipe)
        {
            return "DMSL_Commander_CannotGestateCommander";
        }
    }
}
