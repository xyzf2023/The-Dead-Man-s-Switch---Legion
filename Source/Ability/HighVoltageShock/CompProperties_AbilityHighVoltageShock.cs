using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 检查能量组件属性（用于禁用能力）
    /// </summary>
    public class CompProperties_AbilityCheckEnergy : AbilityCompProperties
    {
        public CompProperties_AbilityCheckEnergy()
        {
            this.compClass = typeof(CompAbilityEffect_CheckEnergy);
        }

        /// <summary>
        /// 最低能量百分比（0-1），低于此值能力将被禁用
        /// </summary>
        public float minEnergyPercentage = 0.5f;

        /// <summary>
        /// 禁用原因文本
        /// </summary>
        public string disabledReason = "释放高压电击需要至少50%最大能量";
    }

    /// <summary>
    /// 高压电击能力效果属性
    /// </summary>
    public class CompProperties_AbilityHighVoltageShock : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityHighVoltageShock()
        {
            this.compClass = typeof(CompAbilityEffect_HighVoltageShock);
        }

        /// <summary>
        /// 消耗的能量百分比（0-1），基于最大能量
        /// </summary>
        public float energyConsumePercentage = 0.5f;

        /// <summary>
        /// 要添加的Hediff定义
        /// </summary>
        public HediffDef? hediffDef;
    }
}

