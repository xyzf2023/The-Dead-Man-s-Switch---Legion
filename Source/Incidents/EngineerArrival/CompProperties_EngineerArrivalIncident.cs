// ============================================================================
// 文件：CompProperties_EngineerArrivalIncident.cs
// 说明：游荡机兵到达事件的 XML 可配置属性
// ============================================================================

using System.Collections.Generic;
using Verse;

namespace DMS_Legion.Incidents.EngineerArrival
{
    /// <summary>
    /// 游荡机兵到达事件的扩展属性，从 IncidentDef 的 modExtensions 读取
    /// </summary>
    public class CompProperties_EngineerArrivalIncident : DefModExtension
    {
        /// <summary>
        /// 可生成的机械体 PawnKindDef 的 defName 列表，多个时随机选一个
        /// </summary>
        public List<string> mechKindDefNames = new List<string> { "DMSL_Mech_Engineer" };
    }
}
