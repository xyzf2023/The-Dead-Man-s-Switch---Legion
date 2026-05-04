using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 医用胶水能力效果属性。
    /// 仅对目标身上所有流血且可包扎的伤口进行包扎，治疗品质由 tendQualityRange 决定。
    /// </summary>
    public class CompProperties_AbilityMedicalGlue : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityMedicalGlue()
        {
            compClass = typeof(CompAbilityEffect_MedicalGlue);
        }

        /// <summary>
        /// 包扎品质随机范围（例如 0.1~0.2 表示低品质快速止血）。
        /// </summary>
        public FloatRange tendQualityRange = new FloatRange(0.1f, 0.2f);

        /// <summary>
        /// 当目标没有可包扎的流血伤口时显示的消息（翻译键）。
        /// </summary>
        [MustTranslate]
        public string noBleedingWoundMessage = "DMSL_MedicalGlueNoBleedingWound";
    }
}
