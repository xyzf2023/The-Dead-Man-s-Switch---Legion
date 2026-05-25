using RimWorld;
using Verse;

namespace DMS_Legion.IntegratedDetectionArray
{
    /// <summary>
    /// 综合探测阵列组件属性：可配置诱饵信号持续时间等（当前仅用默认 4 小时）。
    /// </summary>
    public class CompProperties_IntegratedDetectionArray : CompProperties
    {
        /// <summary>
        /// 生成诱饵信号的持续时间（Tick），与 AerialRaidBaitTargetComponent 默认一致：5 小时 = 12500
        /// </summary>
        public int decoyDurationTicks = 12500;

        /// <summary>建筑被禁止（CompForbiddable.Forbidden）时的实际耗电（W）。</summary>
        public float disabledPowerConsumption = 50f;

        /// <summary>本地模式且开启本地定向扫描时的实际耗电（W）。</summary>
        public float localTargetedScanPowerConsumption = 8000f;

        public CompProperties_IntegratedDetectionArray()
        {
            compClass = typeof(Comp_IntegratedDetectionArray);
        }
    }
}
