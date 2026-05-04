using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 指挥范围扩展组件的属性定义。
    /// 通过 XML 可配置半径，范围内友方机械体视为处于监管者控制范围内。
    /// </summary>
    public class CompProperties_CommandRangeExpansion : CompProperties
    {
        /// <summary>
        /// 扩展范围半径（格数）。此范围内的友方机械体均视为在监管者控制范围内。
        /// </summary>
        public float radius = 25f;

        public CompProperties_CommandRangeExpansion()
        {
            compClass = typeof(CompCommandRangeExpansion);
        }
    }
}
