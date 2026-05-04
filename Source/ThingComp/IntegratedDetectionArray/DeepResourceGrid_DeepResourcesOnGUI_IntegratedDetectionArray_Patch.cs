// ============================================================================
// 选中综合探测阵列时，在鼠标悬停格子上显示深矿名称与剩余数量（与选中深钻井/地质勘探仪一致）。
// ============================================================================

using System.Reflection;
using HarmonyLib;
using Verse;
using DMS_Legion.IntegratedDetectionArray;

namespace DMS_Legion
{
    [HarmonyPatch(typeof(DeepResourceGrid), nameof(DeepResourceGrid.DeepResourcesOnGUI))]
    public static class DeepResourceGrid_DeepResourcesOnGUI_IntegratedDetectionArray_Patch
    {
        private static readonly MethodInfo? RenderMouseAttachmentsMethod =
            AccessTools.Method(typeof(DeepResourceGrid), "RenderMouseAttachments");

        [HarmonyPostfix]
        public static void Postfix(DeepResourceGrid __instance, Map ___map)
        {
            Thing? singleSelectedThing = Find.Selector.SingleSelectedThing;
            if (singleSelectedThing == null)
                return;
            var comp = singleSelectedThing.TryGetComp<Comp_IntegratedDetectionArray>();
            if (comp == null || !comp.ShouldShowDeepResourceOverlay())
                return;
            if (___map == null || !___map.Biome.hasBedrock)
                return;
            RenderMouseAttachmentsMethod?.Invoke(__instance, null);
        }
    }
}
