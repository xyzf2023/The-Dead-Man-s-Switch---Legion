using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 体型近战加伤逻辑组件的 Def 属性。挂在被攻击方（可被机械近战命中的种族）上。
    /// </summary>
    public class CompProperties_BodySizeMeleeBonus : CompProperties
    {
        public CompProperties_BodySizeMeleeBonus()
        {
            compClass = typeof(Comp_BodySizeMeleeBonus);
        }
    }
}
