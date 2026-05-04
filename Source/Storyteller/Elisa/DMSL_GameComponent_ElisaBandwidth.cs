// ============================================================================
// 存储艾丽萨带宽认可组件上次运行时间，用于“每 15 天”间隔
// ============================================================================

using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 记录艾丽萨带宽认可叙事者组件上次执行的 tick，用于按间隔触发。
    /// </summary>
    public class DMSL_GameComponent_ElisaBandwidth : GameComponent
    {
        private const string CompId = "DMSL_ElisaBandwidthApproval";

        /// <summary>上次执行时的 TicksGame</summary>
        public int lastRunTick = -1;

        public DMSL_GameComponent_ElisaBandwidth(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastRunTick, "lastRunTick_" + CompId, -1);
        }

        /// <summary>是否已过 intervalDays 天可再次执行</summary>
        public bool ShouldRunNow(float intervalDays)
        {
            if (intervalDays <= 0f) return false;
            int ticksPerInterval = (int)(intervalDays * 60000f);
            int now = Find.TickManager.TicksGame;
            if (lastRunTick < 0) return now >= ticksPerInterval;
            return now - lastRunTick >= ticksPerInterval;
        }

        /// <summary>标记本次已执行</summary>
        public void MarkRun()
        {
            lastRunTick = Find.TickManager.TicksGame;
        }
    }
}
