using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 近战命中时按概率点燃目标的组件属性。
    /// 用于纵火者等单位，每次近战命中可触发一次点燃判定。
    /// </summary>
    public class CompProperties_MeleeHitIgnite : CompProperties
    {
        /// <summary>近战命中时点燃目标的概率（0～1），默认 0.5。</summary>
        public float igniteChance = 0.5f;

        /// <summary>附着火焰的初始大小，传给 TryAttachFire，默认 0.25。</summary>
        public float fireSize = 0.25f;

        public CompProperties_MeleeHitIgnite()
        {
            compClass = typeof(CompMeleeHitIgnite);
        }
    }
}
