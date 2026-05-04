using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 检测传呼器携带者是否死亡或Job失败
    /// 如果携带者死亡、消失或不在执行Job，触发失败信号
    /// </summary>
    public class Trigger_PagerCarrierFailed : Trigger
    {
        /// <summary>
        /// 检查间隔（Tick），每 30 tick 检查一次
        /// </summary>
        private const int CheckIntervalTicks = 30;

        /// <summary>
        /// 上一次检查的 Tick
        /// </summary>
        private int lastCheckTick = -1;

        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            // 只在 Tick 信号时检查
            if (signal.type != TriggerSignalType.Tick)
            {
                return false;
            }

            int currentTick = Find.TickManager.TicksGame;

            // 控制检查频率
            if (currentTick - lastCheckTick < CheckIntervalTicks)
            {
                return false;
            }

            lastCheckTick = currentTick;

            // 查找携带传呼器标记的 pawn
            Pawn? carrier = FindPagerCarrier(lord);
            
            if (carrier == null)
            {
                // 没有找到携带者，可能已经死亡或消失
                // 返回 true 触发转换（转换会发送 MemoCallFailed）
                return true;
            }

            // 检查携带者状态
            if (carrier.Dead || carrier.Downed || !carrier.Spawned)
            {
                // 携带者死亡或倒地，返回 true 触发转换
                return true;
            }

            // 检查携带者是否还在执行呼叫空袭Job
            Job? curJob = carrier.jobs?.curJob;
            bool isCallingJob = curJob != null && curJob.def != null && curJob.def.defName == "DMSL_RaidCallAirSupport";
            
            if (!isCallingJob)
            {
                // 携带者不在执行Job，可能Job已经失败或完成
                // 如果Job完成，应该已经发送了 MemoCallDone，所以这里应该是失败
                // 但为了避免重复触发，我们检查一下是否已经进入等待空袭阶段
                // 如果还在 waitToil 阶段，说明Job失败了
                if (lord.CurLordToil is LordToil_DefendPoint)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 在 Lord 中查找携带传呼器标记的 pawn
        /// </summary>
        private Pawn? FindPagerCarrier(Lord lord)
        {
            HediffDef? markerDef = DefDatabase<HediffDef>.GetNamed("DMSL_PagerCarrierMarker", false);
            if (markerDef == null)
            {
                return null;
            }

            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (pawn?.health?.hediffSet == null)
                {
                    continue;
                }

                Hediff? markerHediff = pawn.health.hediffSet.GetFirstHediffOfDef(markerDef);
                if (markerHediff != null)
                {
                    return pawn;
                }
            }

            return null;
        }
    }
}
