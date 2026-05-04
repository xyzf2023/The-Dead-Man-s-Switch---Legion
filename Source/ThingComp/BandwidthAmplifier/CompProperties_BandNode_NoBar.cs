// ============================================================================
// 对应 CompBandNode_NoBar 的 CompProperties，仅将 compClass 设为不画状态条的实现。
// 在 ThingDef 中使用本类即可实现与 DMSL_BandNode_NoEffect 类似的效果：无需补丁即可不显示状态条。
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 继承原版 CompProperties_BandNode，指定 compClass 为 CompBandNode_NoBar。
    /// </summary>
    public class CompProperties_BandNode_NoBar : CompProperties_BandNode
    {
        public CompProperties_BandNode_NoBar()
        {
            compClass = typeof(CompBandNode_NoBar);
        }
    }
}
