// ============================================================================
// 文件：CompProperties_AgriculturalFrameHay.cs
// 说明：农业框架完成收获/割除后额外掉落干草的组件属性
// ============================================================================

using System.Collections.Generic;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 挂载到农业框架种族 Def 的 comps 中，配置每次完成收获或割除后生成的额外干草数量范围。
    /// 可通过 onlyWhenCut / onlyWhenHarvest / allowedPlantDefs 细化为仅割除、仅收获或仅针对某些植物。
    /// </summary>
    public class CompProperties_AgriculturalFrameHay : CompProperties
    {
        /// <summary>额外产物数量下限（含）</summary>
        public int extraCountMin = 2;

        /// <summary>额外产物数量上限（含）</summary>
        public int extraCountMax = 3;

        /// <summary>额外产物 ThingDef，为空时使用原版干草</summary>
        public ThingDef? extraThingDef;

        /// <summary>为 true 时仅在「割除」(Cut) 时生成干草</summary>
        public bool onlyWhenCut;

        /// <summary>为 true 时仅在「收获」(Chop，如收作物、收木材) 时生成；与 onlyWhenCut 同时为 true 时等价于不按操作类型过滤</summary>
        public bool onlyWhenHarvest;

        /// <summary>非空时仅当被收集的植物 def 在此列表中才生成；为空或未配置则任意植物都生成</summary>
        public List<ThingDef>? allowedPlantDefs;

        public CompProperties_AgriculturalFrameHay()
        {
            compClass = typeof(CompAgriculturalFrameHay);
        }
    }
}
