using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援能力组件属性
    /// </summary>
    public class CompProperties_AerialSupport : CompProperties_AbilityEffect
    {
        /// <summary>
        /// 此技能支持的空中支援类型DefName列表
        /// </summary>
        public List<string> supportedSupportTypes = new List<string>();

        public CompProperties_AerialSupport()
        {
            compClass = typeof(CompAbilityEffect_AerialSupport);
        }
    }
}
