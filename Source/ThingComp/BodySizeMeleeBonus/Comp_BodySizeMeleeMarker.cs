using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 体型近战加伤标记：仅作存在性检查，无逻辑。挂在 AncientCorps 机械体 Def 的 comps 中。
    /// </summary>
    public class Comp_BodySizeMeleeMarker : ThingComp
    {
        public CompProperties_BodySizeMeleeMarker Props => (CompProperties_BodySizeMeleeMarker)props;
    }
}
