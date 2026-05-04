using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 机体重构能力效果属性
    /// </summary>
    public class CompProperties_AbilityMechReconstruction : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityMechReconstruction()
        {
            this.compClass = typeof(CompAbilityEffect_MechReconstruction);
        }

        /// <summary>
        /// 验证失败时显示的错误消息（翻译键）
        /// </summary>
        [MustTranslate]
        public string failedMessage = "DMSL_MechReconstruction_FailedMessage";

        /// <summary>
        /// 复活读条时间（秒）
        /// 到达目标附近后，需要等待此时间才能完成复活
        /// 仅在C#中定义，不在XML中配置
        /// </summary>
        public float castTime = 15f;

        /// <summary>
        /// 复活时应用的效果器（视觉效果）
        /// 参考原版CompProperties_ResurrectMech的appliedEffecterDef
        /// 使用原版的MechResurrected效果器，显示红光特效
        /// </summary>
        public EffecterDef? appliedEffecterDef;
    }
}

