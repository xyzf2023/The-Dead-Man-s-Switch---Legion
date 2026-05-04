using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 近战命中时按概率点燃目标的组件。逻辑在 Harmony Postfix 中实现。
    /// </summary>
    public class CompMeleeHitIgnite : ThingComp
    {
        public CompProperties_MeleeHitIgnite Props => (CompProperties_MeleeHitIgnite)props;
    }
}
