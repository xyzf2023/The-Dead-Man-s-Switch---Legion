// ============================================================================
// 文件：HediffCompProperties_WanderingEngineer.cs
// 说明：游荡机兵 HediffComp 的属性定义
// ============================================================================

using Verse;

namespace DMS_Legion.Incidents.EngineerArrival
{
    public class HediffCompProperties_WanderingEngineer : HediffCompProperties
    {
        public HediffCompProperties_WanderingEngineer()
        {
            compClass = typeof(HediffComp_WanderingEngineer);
        }
    }
}
