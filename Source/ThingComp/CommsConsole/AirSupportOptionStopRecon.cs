// ============================================================================
// 文件：AirSupportOptionStopRecon.cs
// 说明：空中支援子界面——「停止侦察」选项，与请求侦察同级
// 功能：点击后执行与 AXF12 建筑停止观察按钮相同逻辑（无观测时发消息并安全清空）
// ============================================================================

using DMS_Legion.AXF12;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援子界面选项：停止侦察（停止所有侦察维持的地图观测）
    /// </summary>
    public static class AirSupportOptionStopRecon
    {
        /// <summary>
        /// 将本选项添加到空中支援子节点
        /// </summary>
        public static void AddOptionTo(DiaNode subNode, Faction faction, Pawn negotiator)
        {
            if (DMSL_ModSettings.settings?.enableExtraStopReconOption != true)
            {
                return;
            }
            DiaOption opt = new DiaOption("DMSL_Comms_StopReconOption".Translate());
            opt.action = () =>
            {
                CommsSupportSubNodeFactory.CloseCommsDialog();
                AXF12ReconMissionManager.StopObservingAllRecon();
            };
            subNode.options.Add(opt);
        }
    }
}
