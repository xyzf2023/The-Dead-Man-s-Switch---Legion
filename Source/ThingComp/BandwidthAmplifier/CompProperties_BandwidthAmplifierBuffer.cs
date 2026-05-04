// ============================================================================
// 频段增幅装置充能缓冲：仅作为 Comp 的 XML 挂载点，数值在 Comp 内按科技写死。
// ============================================================================

using Verse;

namespace DMS_Legion
{
    public class CompProperties_BandwidthAmplifierBuffer : CompProperties
    {
        public CompProperties_BandwidthAmplifierBuffer()
        {
            compClass = typeof(CompBandwidthAmplifierBuffer);
        }
    }
}
