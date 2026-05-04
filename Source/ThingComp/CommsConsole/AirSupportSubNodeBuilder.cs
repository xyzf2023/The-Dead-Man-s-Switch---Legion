// ============================================================================
// 文件：AirSupportSubNodeBuilder.cs
// 说明：组装「请求空中支援」子界面节点，汇总各独立选项并添加返回
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援子界面节点构建器：创建子节点并添加遣返轰炸机、火力打击、战场支援、特殊打击及返回
    /// </summary>
    public static class AirSupportSubNodeBuilder
    {
        /// <summary>
        /// 创建空中支援子界面节点（各选项由独立文件提供）
        /// </summary>
        public static DiaNode CreateSubNode(Faction faction, Pawn negotiator)
        {
            string leaderName = faction.leader?.Name?.ToStringFull ?? faction.Name;
            DiaNode subNode = new DiaNode("DMSL_Comms_AirSupportPrompt".Translate(leaderName));

            AirSupportOptionBomberRecall.AddOptionTo(subNode, faction, negotiator);
            AirSupportOptionFireStrike.AddOptionTo(subNode, faction, negotiator);
            AirSupportOptionBattlefieldSupport.AddOptionTo(subNode, faction, negotiator);
            AirSupportOptionSpecialStrike.AddOptionTo(subNode, faction, negotiator);
            AirSupportOptionReconTarget.AddOptionTo(subNode, faction, negotiator);
            AirSupportOptionStopRecon.AddOptionTo(subNode, faction, negotiator);

            DiaOption goBackOpt = new DiaOption("GoBack".Translate());
            goBackOpt.linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator);
            subNode.options.Add(goBackOpt);

            return subNode;
        }
    }
}
