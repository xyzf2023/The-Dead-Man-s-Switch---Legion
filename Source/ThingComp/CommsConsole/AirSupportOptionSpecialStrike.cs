// ============================================================================
// 文件：AirSupportOptionSpecialStrike.cs
// 说明：空中支援子界面——「请求特殊打击」选项
// 功能：调用 CommsSupportSubNodeFactory，传入 category=SpecialStrike
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援子界面选项：请求特殊打击
    /// </summary>
    public static class AirSupportOptionSpecialStrike
    {
        private const string CategorySpecialStrike = "SpecialStrike";

        /// <summary>
        /// 将本选项添加到空中支援子节点
        /// </summary>
        public static void AddOptionTo(DiaNode subNode, Faction faction, Pawn negotiator)
        {
            DiaOption opt = new DiaOption("DMSL_Comms_SpecialStrikeOption".Translate());
            opt.link = CommsSupportSubNodeFactory.CreateOptionsNode(CategorySpecialStrike, faction, negotiator);
            subNode.options.Add(opt);
        }
    }
}
