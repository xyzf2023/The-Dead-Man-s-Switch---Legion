// ============================================================================
// 文件：CommsAirSupportOptionDef.cs
// 说明：通讯台空中支援选项 Def（请求火力打击 / 战场支援 / 特殊打击 子界面用）
// 功能：定义 label、好感消耗、冷却、关联的空中支援 defName 及 category
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 通讯台空中支援选项定义
    /// defName 命名规则：将关联的 DMSL_AerialSupport_XXX 替换为 DMSL_Comms_XXX
    /// </summary>
    public class CommsAirSupportOptionDef : Def
    {
        /// <summary>
        /// 在子界面按钮上显示的文字（覆盖基类 Def.label，用于子界面按钮）
        /// </summary>
        public new string label = string.Empty;

        /// <summary>
        /// 消耗的基础好感度（整数）
        /// </summary>
        public int goodwillCostBase = 0;

        /// <summary>
        /// 使用后冷却时间（tick），此时间内该选项禁用
        /// </summary>
        public int cooldownTicks = 0;

        /// <summary>
        /// 关联的空中支援类型 defName（如 DMSL_AerialSupport_NuclearStrike）
        /// </summary>
        public string aerialSupportDefName = string.Empty;

        /// <summary>
        /// 分类：FireStrike / BattlefieldSupport / SpecialStrike，用于筛选子界面选项
        /// </summary>
        public string category = "FireStrike";
    }
}
