// ============================================================================
// 文件：AirSupportOptionBomberRecall.cs
// 说明：空中支援子界面——「请求指示轰炸机编队遣返」选项及遣返成功确认节点
// 功能：需存在空袭倒计时时可用，消耗 15 好感度；将各地图空袭次数置 0 并调度遣返成功信件
// ============================================================================

using DMS_Legion.AerialRaid.AerialRaidComponents;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援子界面选项：遣返轰炸机
    /// </summary>
    public static class AirSupportOptionBomberRecall
    {
        private const int BomberRecallGoodwillCostBase = 15;

        /// <summary>
        /// 将本选项（及可能的后续节点）添加到空中支援子节点
        /// </summary>
        public static void AddOptionTo(DiaNode subNode, Faction faction, Pawn negotiator)
        {
            int goodwillCost = -Faction.OfPlayer.CalculateAdjustedGoodwillChange(faction, -BomberRecallGoodwillCostBase);
            DiaOption bomberOpt = new DiaOption("DMSL_Comms_BomberOption".Translate(goodwillCost));

            if (!HasAnyAerialRaidCountdown())
            {
                bomberOpt.Disable("DMSL_Comms_NoBomberToRecall".Translate());
            }
            else
            {
                bomberOpt.action = () =>
                {
                    Faction.OfPlayer.TryAffectGoodwillWith(faction, -BomberRecallGoodwillCostBase, false, true, null, null);
                    SetAllAerialRaidExecutionCountToZero();
                    CommsRecallLetterScheduler.Instance?.ScheduleRecallSuccessLetter();
                };
                bomberOpt.linkLateBind = () => CreateRecallSuccessNode(faction, negotiator);
            }

            subNode.options.Add(bomberOpt);
        }

        private static bool HasAnyAerialRaidCountdown()
        {
            foreach (var map in Find.Maps)
            {
                var comp = map?.GetComponent<AerialRaidPrePhaseComponent>();
                if (comp != null && comp.GetRemainingTicks() > 0)
                    return true;
            }
            return false;
        }

        private static void SetAllAerialRaidExecutionCountToZero()
        {
            foreach (var map in Find.Maps)
            {
                var comp = map?.GetComponent<AerialRaidPrePhaseComponent>();
                if (comp != null && comp.GetRemainingTicks() > 0)
                    comp.SetExecutionCount(0);
            }
        }

        private static DiaNode CreateRecallSuccessNode(Faction faction, Pawn negotiator)
        {
            string leaderName = faction.leader?.Name?.ToStringFull ?? faction.Name;
            DiaNode successNode = new DiaNode("DMSL_Comms_RecallSuccessDialog".Translate(leaderName));

            DiaOption goBackOpt = new DiaOption("GoBack".Translate());
            goBackOpt.linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator);
            successNode.options.Add(goBackOpt);

            return successNode;
        }
    }
}
