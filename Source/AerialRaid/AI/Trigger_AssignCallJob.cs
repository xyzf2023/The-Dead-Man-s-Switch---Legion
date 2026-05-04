using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// Trigger：延迟后让携带者执行呼叫 Job
    /// 使用 Trigger 而不是 TransitionAction，因为 Trigger 可以直接获取 Lord
    /// </summary>
    public class Trigger_AssignCallJob : Trigger
    {
        /// <summary>
        /// 延迟时间（Tick），等待多久后开始执行 Job
        /// </summary>
        private const int DelayBeforeJobTicks = 300; // 5秒

        /// <summary>
        /// 记录每个 Lord 进入 Toil 的时间（Tick）
        /// </summary>
        private static System.Collections.Generic.Dictionary<Lord, int> lordStartTicks = new System.Collections.Generic.Dictionary<Lord, int>();

        /// <summary>
        /// 是否已经下达了 Job（防止重复执行）
        /// </summary>
        private static System.Collections.Generic.HashSet<Lord> assignedLords = new System.Collections.Generic.HashSet<Lord>();

        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            // 只在 Tick 信号时检查
            if (signal.type != TriggerSignalType.Tick)
            {
                return false;
            }

            // 检查是否已经下达过 Job（防止重复执行）
            if (assignedLords.Contains(lord))
            {
                return false;
            }

            // 记录 Lord 进入 Toil 的时间
            if (!lordStartTicks.ContainsKey(lord))
            {
                lordStartTicks[lord] = Find.TickManager.TicksGame;
                return false;
            }

            // 检查是否已经过了延迟时间
            int currentTick = Find.TickManager.TicksGame;
            int elapsedTicks = currentTick - lordStartTicks[lord];
            if (elapsedTicks < DelayBeforeJobTicks)
            {
                return false;
            }

            // 找到携带传呼器的 pawn
            Pawn? caller = lord.ownedPawns.FirstOrDefault(p => 
                !p.Dead && !p.Downed &&
                ((p.apparel?.WornApparel.Any(a => a.def.defName == "DMSL_AirSupportPager") == true) ||
                 (p.inventory?.innerContainer.Any(t => t.def.defName == "DMSL_AirSupportPager") == true)));

            if (caller != null)
            {
                // 检查是否已经在执行这个 Job（防止重复下达）
                if (caller.jobs?.curJob?.def == DMSL_JobDefOf.DMSL_RaidCallAirSupport)
                {
                    assignedLords.Add(lord);
                    return false;
                }

                // 下达呼叫 Job（设置 targetA 为 pawn 自身，用于进度条显示）
                Job callJob = JobMaker.MakeJob(DMSL_JobDefOf.DMSL_RaidCallAirSupport);
                callJob.targetA = caller;
                if (caller.jobs != null)
                {
                    caller.jobs.TryTakeOrderedJob(callJob, JobTag.Misc);
                    assignedLords.Add(lord);
                }
            }
            else
            {
                Log.Warning("[DMS_Legion]空袭支援袭击：延迟后未找到携带传呼器的 pawn");
            }

            // 这个 Trigger 不触发转换，只是执行动作
            return false;
        }
    }
}
