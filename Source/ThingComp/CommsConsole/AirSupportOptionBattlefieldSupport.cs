// ============================================================================
// 文件：AirSupportOptionBattlefieldSupport.cs
// 说明：空中支援子界面——「请求战场支援」选项
// 功能：调用 CommsSupportSubNodeFactory，传入 category=BattlefieldSupport
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援子界面选项：请求战场支援
    /// </summary>
    public static class AirSupportOptionBattlefieldSupport
    {
        private const string CategoryBattlefieldSupport = "BattlefieldSupport";

        /// <summary>
        /// 将本选项添加到空中支援子节点
        /// </summary>
        public static void AddOptionTo(DiaNode subNode, Faction faction, Pawn negotiator)
        {
            DiaOption opt = new DiaOption("DMSL_Comms_BattlefieldSupportOption".Translate());
            opt.link = CommsSupportSubNodeFactory.CreateOptionsNode(CategoryBattlefieldSupport, faction, negotiator);
            subNode.options.Add(opt);
        }
    }
}
