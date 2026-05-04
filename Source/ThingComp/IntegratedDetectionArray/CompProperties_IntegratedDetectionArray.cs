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

        public CompProperties_IntegratedDetectionArray()
        {
            compClass = typeof(Comp_IntegratedDetectionArray);
        }
    }
}
