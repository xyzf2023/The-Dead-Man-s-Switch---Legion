using Verse;

namespace DMSL
{
    /// <summary>
    /// 远行队领队资格标识组件
    /// 简单的标记组件，用于标识机械体可以成为远行队领队
    /// </summary>
    public class CompProperties_CaravanOwner : CompProperties
    {
        public CompProperties_CaravanOwner()
        {
            compClass = typeof(CompCaravanOwner);
        }
    }
}
