using RimWorld;
using Verse;
using UnityEngine;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 空袭袭击工作器 - 最小测试实现
    /// 仅负责创建并初始化空袭前置阶段组件
    /// </summary>
    public class IncidentWorker_AerialRaid : IncidentWorker
    {
        /// <summary>
        /// 倒计时时间范围（Tick）
        /// 1小时 = 2500 tick
        /// 3小时 = 7500 tick
        /// 5小时 = 12500 tick
        /// </summary>
        private const int MinCountdownTicks = 7500;  // 3小时
        private const int MaxCountdownTicks = 12500; // 5小时

        /// <summary>
        /// 检查事件是否可以在当前条件下触发。
        /// 奥德赛 DLC 启用时，若玩家地图在太空中则不允许触发空袭。
        /// </summary>
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }
            Map? map = parms.target as Map;
            if (AerialRaidOdysseyUtility.IsMapInSpace(map))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 执行袭击逻辑
        /// </summary>
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map == null)
            {
                Log.Error("[DMS_Legion] IncidentWorker_AerialRaid: Target map is null");
                return false;
            }

            // 获取或创建组件
            var component = AerialRaidPrePhaseComponent.GetOrCreate(map);
            if (component == null)
            {
                Log.Error("[DMS_Legion] IncidentWorker_AerialRaid: Failed to get or create component");
                return false;
            }

            // 生成3-5小时之间的随机倒计时时间
            int countdownTicks = Rand.RangeInclusive(MinCountdownTicks, MaxCountdownTicks);
            
            // 设置倒计时时间
            component.SetRemainingTicks(countdownTicks);

            // 使用“关键消息”形式的 Alert 通知玩家空袭即将到达

            return true;
        }
    }
}
