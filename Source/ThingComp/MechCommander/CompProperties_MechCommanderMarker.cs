using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 机械师标记组件的 Def 属性。可配置额外带宽与额外控制组数量。
    /// </summary>
    public class CompProperties_MechCommanderMarker : CompProperties
    {
        public int extraMechBandwidth;
        public int extraMechControlGroups = 3;

        public CompProperties_MechCommanderMarker()
        {
            compClass = typeof(CompMechCommanderMarker);
        }
    }
}
