// ============================================================================
// 文件：CompProperties_GiftFromArmyIncident.cs
// 说明：来自武装殖民舰队馈赠事件的 XML 可配置属性
// ============================================================================

using System.Collections.Generic;
using Verse;

namespace DMS_Legion.Incidents.GiftFromArmy
{
    /// <summary>
    /// 来自武装殖民舰队馈赠事件的扩展属性，从 IncidentDef 的 modExtensions 读取。
    /// 根据与 Army 的好感计算赠送总价值，在允许的物品 def 中随机分配生成。
    /// </summary>
    public class CompProperties_GiftFromArmyIncident : DefModExtension
    {
        /// <summary>
        /// 可生成物品的 ThingDef defName 列表
        /// </summary>
        public List<string> thingDefNames = new List<string>();

        /// <summary>
        /// 每点好感增加的价值
        /// </summary>
        public int valuePerGoodwill = 5;

        /// <summary>
        /// 赠送物品总价值上限
        /// </summary>
        public int maxTotalValue = 500;
    }
}
