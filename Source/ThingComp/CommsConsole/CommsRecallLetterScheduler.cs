// ============================================================================
// 文件：CommsRecallLetterScheduler.cs
// 说明：通讯台遣返轰炸机成功后，延迟 300 tick 发送「轰炸机编队已返航」信件
// ============================================================================

using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 通讯遣返成功后延迟发信：在成功将空袭次数改为 0 的 300 tick 后发送正面 letter。
    /// </summary>
    public class CommsRecallLetterScheduler : GameComponent
    {
        private static CommsRecallLetterScheduler? instance;
        public static CommsRecallLetterScheduler? Instance => instance;

        private List<int> pendingLetterEndTicks = new List<int>();

        private const int LetterDelayTicks = 300;

        public CommsRecallLetterScheduler(Game game)
        {
            instance = this;
        }

        /// <summary>
        /// 调度遣返成功信件：300 tick 后发送
        /// </summary>
        public void ScheduleRecallSuccessLetter()
        {
            int endTick = Find.TickManager.TicksGame + LetterDelayTicks;
            pendingLetterEndTicks.Add(endTick);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int now = Find.TickManager.TicksGame;
            for (int i = pendingLetterEndTicks.Count - 1; i >= 0; i--)
            {
                if (pendingLetterEndTicks[i] <= now)
                {
                    pendingLetterEndTicks.RemoveAt(i);
                    SendRecallSuccessLetter();
                }
            }
        }

        private static void SendRecallSuccessLetter()
        {
            Find.LetterStack.ReceiveLetter(
                "DMSL_Comms_RecallSuccessLetterTitle".Translate(),
                "DMSL_Comms_RecallSuccessLetterText".Translate(),
                LetterDefOf.PositiveEvent);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingLetterEndTicks, "commsRecallPendingLetterTicks", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingLetterEndTicks ??= new List<int>();
            }
        }
    }
}
