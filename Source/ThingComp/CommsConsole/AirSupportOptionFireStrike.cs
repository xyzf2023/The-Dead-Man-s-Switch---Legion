// ============================================================================
// 文件：AirSupportOptionFireStrike.cs
// 说明：空中支援子界面——「请求火力打击」选项
// 功能：调用 CommsSupportSubNodeFactory，传入 category=FireStrike
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援子界面选项：请求火力打击
    /// </summary>
    public static class AirSupportOptionFireStrike
    {
        private const string CategoryFireStrike = "FireStrike";

        /// <summary>
        /// 将本选项添加到空中支援子节点（链接到 Def 驱动的火力打击选项列表）
        /// </summary>
        public static void AddOptionTo(DiaNode subNode, Faction faction, Pawn negotiator)
        {
            DiaOption opt = new DiaOption("DMSL_Comms_FireStrikeOption".Translate());
            opt.link = CommsSupportSubNodeFactory.CreateOptionsNode(CategoryFireStrike, faction, negotiator);
            subNode.options.Add(opt);
        }
    }
}
