// ============================================================================
// 存储艾丽萨事件循环组件上次触发时间及上次触发的事件，用于 5-10 天间隔及避免连续重复
// ============================================================================

using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 记录艾丽萨事件循环叙事者组件上次执行的 tick 及事件 defName。
    /// </summary>
    public class DMSL_GameComponent_ElisaIncidentCycle : GameComponent
    {
        private const string CompId = "DMSL_ElisaIncidentCycle";

        private const string ElisaStorytellerDefName = "DMSL_Storyteller_Elisa";

        private bool wasElisaActive;
        private bool storytellerStateInitialized;

        /// <summary>上次触发时的 TicksGame</summary>
        public int lastFireTick = -1;

        /// <summary>上次触发的事件 defName（用于避免连续重复）</summary>
        public string? lastFiredIncidentDefName;

        /// <summary>下次触发所需的间隔 tick 数（随机 5-10 天）</summary>
        public int nextIntervalTicks = -1;

        public DMSL_GameComponent_ElisaIncidentCycle(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastFireTick, "lastFireTick_" + CompId, -1);
            Scribe_Values.Look(ref lastFiredIncidentDefName, "lastFiredIncidentDefName_" + CompId);
            Scribe_Values.Look(ref nextIntervalTicks, "nextIntervalTicks_" + CompId, -1);
        }

        private static bool IsElisaStorytellerActive()
        {
            var storyteller = Find.Storyteller;
            return storyteller?.def?.defName == ElisaStorytellerDefName;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int ticksGame = Find.TickManager.TicksGame;
            bool isElisa = IsElisaStorytellerActive();

            // 当前为艾丽萨时，每 1800 tick 检查一次；否则每 30000 tick 检查一次
            int interval = isElisa ? 1800 : 30000;
            if (interval <= 0 || ticksGame % interval != 0)
                return;

            if (!storytellerStateInitialized)
            {
                wasElisaActive = isElisa;
                storytellerStateInitialized = true;
                return;
            }

            // 当叙事者从艾丽萨切换为其他叙事者时，停止计时并重置所有计时状态
            if (!isElisa && wasElisaActive)
            {
                lastFireTick = -1;
                nextIntervalTicks = -1;
                lastFiredIncidentDefName = null;
            }

            wasElisaActive = isElisa;
        }

        /// <summary>是否已过间隔可再次触发</summary>
        public bool ShouldFireNow(float minIntervalDays, float maxIntervalDays)
        {
            if (minIntervalDays <= 0f || maxIntervalDays < minIntervalDays)
                return false;

            // 仅在叙事者为艾丽萨时计时与判定
            if (!IsElisaStorytellerActive())
                return false;

            int now = Find.TickManager.TicksGame;

            // 首次或被重置后：先记录当前时间与随机间隔，不立刻触发
            if (lastFireTick < 0 || nextIntervalTicks < 0)
            {
                nextIntervalTicks = (int)(Rand.Range(minIntervalDays, maxIntervalDays) * 60000f);
                lastFireTick = now;
                return false;
            }

            return now - lastFireTick >= nextIntervalTicks;
        }

        /// <summary>标记本次已触发，并设置下次间隔</summary>
        public void MarkFired(string incidentDefName, float minIntervalDays, float maxIntervalDays)
        {
            lastFireTick = Find.TickManager.TicksGame;
            lastFiredIncidentDefName = incidentDefName;
            nextIntervalTicks = (int)(Rand.Range(minIntervalDays, maxIntervalDays) * 60000f);
        }
    }
}
