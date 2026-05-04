using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 战争践踏能力组件属性：落地时在范围内造成钝器伤害。
    /// </summary>
    public class CompProperties_AbilityWarStomp : RimWorld.CompProperties_AbilityEffect
    {
        /// <summary>伤害半径（格）</summary>
        public float radius = 5f;

        /// <summary>伤害数值</summary>
        public int damageAmount = 50;

        /// <summary>伤害类型（钝器碾压），未配置时使用 Blunt</summary>
        public DamageDef? damageDef;

        /// <summary>护甲穿透系数（默认 0.02 * 伤害值）</summary>
        public float armorPenFactor = 0.02f;

        /// <summary>护甲穿透覆盖值（>=0 时直接使用该值；<0 表示使用 armorPenFactor 计算）</summary>
        public float armorPenOverride = -1f;

        public CompProperties_AbilityWarStomp()
        {
            compClass = typeof(CompAbilityEffect_WarStomp);
        }
    }
}
