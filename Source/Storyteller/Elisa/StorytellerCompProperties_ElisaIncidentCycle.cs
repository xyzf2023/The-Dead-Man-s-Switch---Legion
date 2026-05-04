// ============================================================================
// 叙事者组件属性：艾丽萨事件循环
// 每 minIntervalDays~maxIntervalDays 天遍历指定事件列表，尝试触发一个满足条件的事件
// 连续两次尽量不触发同一事件（除非仅该事件满足条件）
// ============================================================================

using System.Collections.Generic;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 艾丽萨事件循环叙事者组件的 XML 可配置属性。
    /// </summary>
    public class StorytellerCompProperties_ElisaIncidentCycle : RimWorld.StorytellerCompProperties
    {
        /// <summary>事件 defName 列表，按顺序遍历寻找可执行的事件</summary>
        public List<string> incidents = new List<string>();

        /// <summary>最小间隔天数（与 maxIntervalDays 之间随机）</summary>
        public float minIntervalDays = 5f;

        /// <summary>最大间隔天数</summary>
        public float maxIntervalDays = 10f;

        // minDaysPassed 继承自 StorytellerCompProperties，无需重复声明

        public StorytellerCompProperties_ElisaIncidentCycle()
        {
            compClass = typeof(StorytellerComp_ElisaIncidentCycle);
        }
    }
}
