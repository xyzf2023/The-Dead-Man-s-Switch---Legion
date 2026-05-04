using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 万心失聪能力效果属性
    /// </summary>
    public class CompProperties_AbilityOmnimindSilence : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityOmnimindSilence()
        {
            this.compClass = typeof(CompAbilityEffect_OmnimindSilence);
        }

        /// <summary>
        /// 效果范围（格数）
        /// </summary>
        public float radius = 3f;

        /// <summary>
        /// 要添加的Hediff定义
        /// </summary>
        public HediffDef? hediffDef;
    }
}
