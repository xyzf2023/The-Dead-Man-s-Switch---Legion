// ============================================================================
// 文件：AirSupportOptionReconTarget.cs
// 说明：空中支援子界面——「请求侦察目标区域」选项
// 功能：子界面「提供坐标」「返回」；提供坐标后扣10好感、关窗口、唤起世界选点
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援子界面选项：请求侦察目标区域
    /// </summary>
    public static class AirSupportOptionReconTarget
    {
        private const int GoodwillCostBase = 10;

        /// <summary>
        /// 将本选项添加到空中支援子节点
        /// </summary>
        public static void AddOptionTo(DiaNode subNode, Faction faction, Pawn negotiator)
        {
            DiaOption opt = new DiaOption("DMSL_Comms_ReconTargetOption".Translate());
            opt.link = CreateReconPromptNode(faction, negotiator);
            subNode.options.Add(opt);
        }

        private static DiaNode CreateReconPromptNode(Faction faction, Pawn negotiator)
        {
            string leaderName = faction.leader?.Name?.ToStringFull ?? faction.Name;
            DiaNode promptNode = new DiaNode("DMSL_Comms_ReconPrompt".Translate(leaderName));

            DiaOption provideOpt = new DiaOption("DMSL_Comms_ProvideCoordinates".Translate());
            provideOpt.action = () =>
            {
                CommsSupportSubNodeFactory.CloseCommsDialog();
                Faction.OfPlayer.TryAffectGoodwillWith(faction, -GoodwillCostBase, false, true, null, null);
                CommsReconTargeting.BeginWorldTargeting(faction);
            };
            promptNode.options.Add(provideOpt);

            DiaOption returnOpt = new DiaOption("GoBack".Translate());
            returnOpt.linkLateBind = () => AirSupportSubNodeBuilder.CreateSubNode(faction, negotiator);
            promptNode.options.Add(returnOpt);

            return promptNode;
        }
    }
}
