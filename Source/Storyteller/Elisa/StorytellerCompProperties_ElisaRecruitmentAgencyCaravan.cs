// ============================================================================
// 叙事者组件属性：艾丽萨每隔固定时间生成一支指定派系、指定商人类型的商队（如招募代理）
// 用于定期生成失能机关 DMS_Caravan_TributeCollector 商队，无需玩家消耗荣誉呼叫
// ============================================================================

using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 艾丽萨“招募代理商队”叙事者组件的 XML 可配置属性。
    /// </summary>
    public class StorytellerCompProperties_ElisaRecruitmentAgencyCaravan : RimWorld.StorytellerCompProperties
    {
        /// <summary>生成间隔（天）</summary>
        public float intervalDays = 20f;

        /// <summary>派系 defName（如 DMS_Army）</summary>
        public string factionDefName = "DMS_Army";

        /// <summary>商人类型 defName（如 DMS_Caravan_TributeCollector 招募代理）</summary>
        public string traderKindDefName = "DMS_Caravan_TributeCollector";

        /// <summary>开局至少经过多少天后才开始生成</summary>
        public new float minDaysPassed = 5f;

        public StorytellerCompProperties_ElisaRecruitmentAgencyCaravan()
        {
            compClass = typeof(StorytellerComp_ElisaRecruitmentAgencyCaravan);
        }
    }
}
