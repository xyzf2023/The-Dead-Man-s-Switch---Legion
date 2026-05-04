using RimWorld;
using Verse;
using Verse.AI.Group;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 监听空袭前置阶段组件完成的 Trigger
    /// 当 AerialRaidPrePhaseComponent 状态变为 Completed 时触发转换
    /// </summary>
    public class Trigger_AerialRaidCompleted : Trigger
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

            // 获取地图组件
            Map? map = lord.Map;
            if (map == null)
            {
                return false;
            }

            var component = map.GetComponent<AerialRaidPrePhaseComponent>();
            if (component == null)
            {
                return false;
            }

            // 检查组件状态是否为 Completed
            // 同时确保组件确实处于活动状态（不是初始的 Completed 状态）
            // 如果 remainingTicks > 0，说明组件正在倒计时，不应该触发
            var state = component.GetCurrentState();
            if (state == AerialRaidPrePhaseState.Completed)
            {
                // 检查是否真的完成了（remainingTicks 应该为 0）
                // 如果 remainingTicks > 0 但状态是 Completed，说明是初始状态，不应该触发
                return component.GetRemainingTicks() <= 0;
            }

            return false;
        }
    }
}
