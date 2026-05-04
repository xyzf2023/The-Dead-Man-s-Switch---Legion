using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// IED 发射器组件属性：指定遥控引爆用的抛射体 Def，用于 Gizmo 查找与计数。
    /// </summary>
    /// <summary>
    /// IED 发射器组件属性：指定遥控引爆用的抛射体 Def；compClass 为 CompIEDLauncher（继承 CompEquippable），武器上仅此一个 equippable 即可。
    /// </summary>
    public class CompProperties_IEDLauncher : CompProperties
    {
        public ThingDef? projectileDef;

        public CompProperties_IEDLauncher()
        {
            compClass = typeof(CompIEDLauncher);
        }
    }
}
