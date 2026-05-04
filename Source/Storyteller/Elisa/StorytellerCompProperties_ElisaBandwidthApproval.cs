// ============================================================================
// 叙事者组件：艾丽萨的带宽认可
// 每 intervalDays 天若玩家与 DMS_Army 非敌对，按殖民地机械师使用的带宽增加好感并可选授予荣誉
// ============================================================================

using System;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 艾丽萨带宽认可叙事者组件的 XML 可配置属性。
    /// 预留接口：多少带宽增加 1 点好感 / 1 点荣誉点数。
    /// </summary>
    public class StorytellerCompProperties_ElisaBandwidthApproval : RimWorld.StorytellerCompProperties
    {
        /// <summary>扫描间隔（天）</summary>
        public float intervalDays = 15f;

        /// <summary>目标派系 defName（如 DMS_Army）</summary>
        public string factionDefName = "DMS_Army";

        /// <summary>每多少带宽增加 1 点好感度（默认 1 = 每 1 带宽 1 好感）</summary>
        public float goodwillPerBandwidth = 1f;

        /// <summary>每多少带宽增加 1 点荣誉点数（仅皇权，默认 20）</summary>
        public float honorPerBandwidth = 20f;

        /// <summary>好感变动理由的 HistoryEventDef.defName</summary>
        public string historyEventDefName = "DMSL_ElisaApproval";

        public StorytellerCompProperties_ElisaBandwidthApproval()
        {
            compClass = typeof(StorytellerComp_ElisaBandwidthApproval);
        }
    }
}
