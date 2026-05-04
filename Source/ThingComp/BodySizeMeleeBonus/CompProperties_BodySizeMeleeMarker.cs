using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 标记组件：挂载在 AncientCorps 机械体上，用于被攻击方逻辑组件判定「攻击者是否为带体型加伤的机械」。
    /// </summary>
    public class CompProperties_BodySizeMeleeMarker : CompProperties
    {
        public CompProperties_BodySizeMeleeMarker()
        {
            compClass = typeof(Comp_BodySizeMeleeMarker);
        }
    }
}
