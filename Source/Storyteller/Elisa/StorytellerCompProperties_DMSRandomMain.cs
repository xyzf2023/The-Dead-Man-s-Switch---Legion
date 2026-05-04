// ============================================================================
// 叙事者组件属性：DMS 派系偏向主事件
// 继承 StorytellerCompProperties_RandomMain，在 FactionArrival 类别中偏向指定派系
// ============================================================================

using System.Collections.Generic;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// DMS 派系偏向主事件叙事者组件的 XML 可配置属性。
    /// 保留 RandomMain 全部字段，额外支持：
    /// - preferredFactionDefNames：优先派系 defName 列表（多派系时权重均分）
    /// - preferredFactionWeightMultiplier：额外权重倍率（&gt;1 时生效）
    /// </summary>
    public class StorytellerCompProperties_DMSRandomMain : RimWorld.StorytellerCompProperties_RandomMain
    {
        /// <summary>
        /// 需要增加互动的派系 defName 列表。可填多个，多派系时额外权重在其中均分。
        /// 默认 DMS_Army。
        /// </summary>
        public List<string> preferredFactionDefNames = new List<string> { "DMS_Army" };

        /// <summary>
        /// 额外权重倍率。&gt;1 时，FactionArrival 事件（商队、访客等）有更高概率使用 preferredFactionDefNames 中的派系。
        /// 数值越大，偏向越明显。例如 4 表示约 75% 概率使用优先派系。
        /// </summary>
        public float preferredFactionWeightMultiplier = 2f;

        public StorytellerCompProperties_DMSRandomMain()
        {
            compClass = typeof(StorytellerComp_DMSRandomMain);
        }
    }
}
