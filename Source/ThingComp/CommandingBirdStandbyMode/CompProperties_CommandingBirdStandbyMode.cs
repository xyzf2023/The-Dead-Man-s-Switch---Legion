using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 指挥鸟待命模式开关组件的属性定义。
    /// </summary>
    public class CompProperties_CommandingBirdStandbyMode : CompProperties
    {
        public CompProperties_CommandingBirdStandbyMode()
        {
            compClass = typeof(CompCommandingBirdStandbyMode);
        }
    }
}
