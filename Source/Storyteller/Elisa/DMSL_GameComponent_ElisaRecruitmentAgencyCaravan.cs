// ============================================================================
// 存储艾丽萨“招募代理商队”组件上次运行时间，用于每隔固定天数生成一支招募代理商队
// ============================================================================

using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 记录招募代理商队叙事者组件上次执行的 tick，用于按间隔触发。
    /// </summary>
    public class DMSL_GameComponent_ElisaRecruitmentAgencyCaravan : GameComponent
    {
        private const string CompId = "DMSL_ElisaRecruitmentAgencyCaravan";

        private const string ElisaStorytellerDefName = "DMSL_Storyteller_Elisa";

        private bool wasElisaActive;
        private bool storytellerStateInitialized;

        /// <summary>上次执行时的 TicksGame</summary>
        public int lastRunTick = -1;

        public DMSL_GameComponent_ElisaRecruitmentAgencyCaravan(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastRunTick, "lastRunTick_" + CompId, -1);
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

            // 当叙事者从艾丽萨切换为其他叙事者时，停止计时并重置计时状态
            if (!isElisa && wasElisaActive)
            {
                lastRunTick = -1;
            }

            wasElisaActive = isElisa;
        }

        /// <summary>是否已过 intervalDays 天可再次执行</summary>
        public bool ShouldRunNow(float intervalDays)
        {
            if (intervalDays <= 0f) return false;
            int ticksPerInterval = (int)(intervalDays * 60000f);
            int now = Find.TickManager.TicksGame;

            // 仅在叙事者为艾丽萨时计时与判定
            if (!IsElisaStorytellerActive())
                return false;

            // 首次或被重置后：先记录当前时间，不立刻触发
            if (lastRunTick < 0)
            {
                lastRunTick = now;
                return false;
            }

            return now - lastRunTick >= ticksPerInterval;
        }

        /// <summary>标记本次已执行</summary>
        public void MarkRun()
        {
            lastRunTick = Find.TickManager.TicksGame;
        }
    }
}
