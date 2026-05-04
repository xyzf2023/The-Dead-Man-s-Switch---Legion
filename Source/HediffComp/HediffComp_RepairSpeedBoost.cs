using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 修理速度提升 Hediff 组件属性
    /// </summary>
    public class HediffCompProperties_RepairSpeedBoost : HediffCompProperties
    {
        /// <summary>
        /// 速度修正倍数（例如 5.0 表示5倍速度）
        /// </summary>
        public float speedMultiplier = 5f;

        public HediffCompProperties_RepairSpeedBoost()
        {
            compClass = typeof(HediffComp_RepairSpeedBoost);
        }
    }

    /// <summary>
    /// 修理速度提升 Hediff 组件
    /// 在添加/移除时主动更新缓存，避免 JobDriver 扫描
    /// </summary>
    public class HediffComp_RepairSpeedBoost : HediffComp
    {
        public HediffCompProperties_RepairSpeedBoost Props
        {
            get
            {
                return (HediffCompProperties_RepairSpeedBoost)this.props;
            }
        }

        /// <summary>
        /// Hediff 添加后，更新缓存
        /// </summary>
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (this.parent?.pawn != null)
            {
                RepairSpeedBoostCache.SetSpeedMultiplier(
                    this.parent.pawn, 
                    this.Props.speedMultiplier);
            }
        }

        /// <summary>
        /// Hediff 移除后，清除缓存
        /// </summary>
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (this.parent?.pawn != null)
            {
                // 检查是否还有其他修理速度提升 Hediff
                var otherBoost = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(
                    this.parent.def);
                if (otherBoost == null)
                {
                    // 没有其他提升，恢复默认值
                    RepairSpeedBoostCache.SetSpeedMultiplier(this.parent.pawn, 1f);
                }
            }
        }
    }
}

