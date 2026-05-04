// ============================================================================
// 选中钻井驳机时显示当前地图深矿矿脉分布（与选中深钻井/地质勘探仪一致）。
// 通过 PostDrawExtraSelectionOverlays 触发 deepResourceGrid.MarkForDraw；
// 悬停格子的 tooltip 由 DeepResourceGrid_DeepResourcesOnGUI_DrillingBarge_Patch 补丁支持。
// ============================================================================

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 选中时显示深矿网格叠加层与鼠标悬停资源信息，行为与 CompDeepScanner/CompDeepDrill 一致。
    /// </summary>
    public class CompDrillingBargeDeepResourceOverlay : ThingComp
    {
        public CompProperties_DrillingBargeDeepResourceOverlay Props =>
            (CompProperties_DrillingBargeDeepResourceOverlay)props;

        public override void PostDrawExtraSelectionOverlays()
        {
            if (parent?.Map == null || !parent.Map.Biome.hasBedrock)
                return;
            parent.Map.deepResourceGrid.MarkForDraw();
        }

        /// <summary>
        /// 判断该矿物是否在驳机 XML 配置的排除列表中。
        /// </summary>
        public static bool IsExcludedDeepResource(ThingDef? resourceDef)
        {
            if (resourceDef == null)
                return false;
            ThingDef? drillingBargeDef = DefDatabase<ThingDef>.GetNamedSilentFail("DMSL_Mech_DrillingBarge");
            if (drillingBargeDef?.comps == null)
                return false;
            for (int i = 0; i < drillingBargeDef.comps.Count; i++)
            {
                if (drillingBargeDef.comps[i] is CompProperties_DrillingBargeDeepResourceOverlay overlayProps)
                    return overlayProps.excludedDeepResourceDefNames.Contains(resourceDef.defName);
            }
            return false;
        }
    }

    public class CompProperties_DrillingBargeDeepResourceOverlay : CompProperties
    {
        /// <summary>
        /// 需从深钻目标中排除的矿物 defName（在驳机种族 XML 的 comps 中配置）。
        /// </summary>
        public List<string> excludedDeepResourceDefNames = new List<string>();

        public CompProperties_DrillingBargeDeepResourceOverlay()
        {
            compClass = typeof(CompDrillingBargeDeepResourceOverlay);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            // 清理 XML 中可能出现的空白项，避免无效匹配。
            excludedDeepResourceDefNames = excludedDeepResourceDefNames
                .Where(defName => !string.IsNullOrWhiteSpace(defName))
                .Distinct()
                .ToList();
        }
    }
}
