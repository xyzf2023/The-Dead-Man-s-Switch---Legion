using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 维修指定能力效果属性
    /// </summary>
    public class CompProperties_AbilityRepairDesignate : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityRepairDesignate()
        {
            this.compClass = typeof(CompAbilityEffect_RepairDesignate);
        }

        /// <summary>
        /// 验证失败时显示的错误消息（翻译键）
        /// </summary>
        [MustTranslate]
        public string failedMessage = "DMSL_RepairDesignateFailedMessage";
    }
}

