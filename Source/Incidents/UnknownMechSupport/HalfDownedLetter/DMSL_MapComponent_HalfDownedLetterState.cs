// ============================================================================
// 半数倒地支援请求：每地图状态，随地图移除自动清理
// ============================================================================

using Verse;

namespace DMS_Legion.Incidents.UnknownMechSupport
{
    /// <summary>
    /// 存储该地图「上次发信 tick」，用于 30000 tick 冷却；地图移除时随 Map 一起销毁。
    /// </summary>
    public class DMSL_MapComponent_HalfDownedLetterState : MapComponent
    {
        private const int NeverSent = -1;

        /// <summary>上次对该地图发送半数倒地信的 TicksGame；-1 表示从未发送。</summary>
        public int lastLetterTick = NeverSent;

        public DMSL_MapComponent_HalfDownedLetterState(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastLetterTick, "lastLetterTick", NeverSent);
        }

        public bool CanSendNow(int cooldownTicks)
        {
            int now = Find.TickManager.TicksGame;
            if (lastLetterTick == NeverSent)
                return true;
            return now - lastLetterTick >= cooldownTicks;
        }

        public void MarkSent()
        {
            lastLetterTick = Find.TickManager.TicksGame;
        }
    }
}
