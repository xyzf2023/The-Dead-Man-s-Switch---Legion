// ============================================================================
// 文件：FactionDialogMaker_DMSArmyNukeStrike_Patch.cs
// 说明：仅在启用皇权 DLC 时生效。在与武装殖民舰队（DMS_Army）的通讯对话中增加「连接核打击系统」选项；
//       仅在与该派系处于盟友状态且头衔不低于准将（DMS_Brigadier）时可选；禁用时统一显示「权限不足」，满足条件时显示「连接核打击系统」。
// ============================================================================

using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// DMS_Army 派系通讯台对话补丁（皇权）：添加「连接核打击系统」选项，按头衔启用/禁用。
    /// </summary>
    [HarmonyPatch(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor))]
    public static class FactionDialogMaker_DMSArmyNukeStrike_Patch
    {
        private const string DmsArmyFactionDefName = "DMS_Army";
        private const string DmsBrigadierTitleDefName = "DMS_Brigadier";

        [HarmonyPostfix]
        public static void Postfix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            if (!ModsConfig.RoyaltyActive)
                return;
            if (__result == null || faction?.def == null || faction.def.defName != DmsArmyFactionDefName)
                return;

            DiaOption nukeStrikeOpt = BuildNukeStrikeOption(negotiator, faction);
            __result.options.Insert(__result.options.Count - 1, nukeStrikeOpt);
        }

        /// <summary>
        /// 头衔不低于准将（含）且为 DMS_Army 头衔、且有皇权组件时返回 true。
        /// </summary>
        private static bool NegotiatorHasBrigadierOrAbove(Pawn negotiator, Faction faction)
        {
            if (negotiator?.royalty == null)
                return false;

            RoyalTitleDef brigadierDef = DefDatabase<RoyalTitleDef>.GetNamed(DmsBrigadierTitleDefName, false);
            if (brigadierDef == null)
                return false;

            RoyalTitleDef currentTitle = negotiator.royalty.GetCurrentTitle(faction);
            if (currentTitle == null)
                return false;

            return currentTitle.seniority >= brigadierDef.seniority;
        }

        private static DiaOption BuildNukeStrikeOption(Pawn negotiator, Faction faction)
        {
            bool isAlly = faction?.PlayerRelationKind == FactionRelationKind.Ally;
            bool hasTitle = faction != null && NegotiatorHasBrigadierOrAbove(negotiator, faction);
            bool allowed = isAlly && hasTitle;

            if (allowed && faction != null)
            {
                Faction factionCapture = faction;
                var opt = new DiaOption("DMSL_Comms_NukeStrike_Connect".Translate());
                opt.action = () =>
                {
                    CommsSupportSubNodeFactory.CloseCommsDialog();
                    Find.WindowStack.Add(new NukeStrikeConnectWindow(negotiator, factionCapture));
                };
                return opt;
            }

            var disabledOpt = new DiaOption("DMSL_Comms_NukeStrike_InsufficientPermission".Translate());
            disabledOpt.disabled = true;
            disabledOpt.disabledReason = null;
            return disabledOpt;
        }
    }
}
