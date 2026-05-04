// ============================================================================
// 选中钻井驳机时，在鼠标悬停格子上显示深矿名称与剩余数量（与选中深钻井/地质勘探仪一致）。
// 原版仅对 CompDeepScanner/CompDeepDrill 且 AnyActiveDeepScannersOnMap 时显示；
// 本补丁在选中物带 CompDrillingBargeDeepResourceOverlay 时也显示，且不要求地图上有地质勘探仪。
//
// 性能：DeepResourcesOnGUI 由 MapInterface.MapInterfaceOnGUI_AfterMainTabs 每帧调用。
// 本 Postfix 仅在“单选为钻井驳机”时调用 RenderMouseAttachments；否则仅做一次 Selector + TryGetComp
// 即 return，占用可忽略。MethodInfo 静态缓存，避免每帧反射查找。
// ============================================================================

using System.Reflection;
using HarmonyLib;
using Verse;

namespace DMS_Legion
{
    [HarmonyPatch(typeof(DeepResourceGrid), nameof(DeepResourceGrid.DeepResourcesOnGUI))]
    public static class DeepResourceGrid_DeepResourcesOnGUI_DrillingBarge_Patch
    {
        private static readonly MethodInfo? RenderMouseAttachmentsMethod =
            AccessTools.Method(typeof(DeepResourceGrid), "RenderMouseAttachments");

        [HarmonyPostfix]
        public static void Postfix(DeepResourceGrid __instance, Map ___map)
        {
            Thing singleSelectedThing = Find.Selector.SingleSelectedThing;
            if (singleSelectedThing == null)
                return;
            if (singleSelectedThing.TryGetComp<CompDrillingBargeDeepResourceOverlay>() == null)
                return;
            if (___map == null || !___map.Biome.hasBedrock)
                return;
            RenderMouseAttachmentsMethod?.Invoke(__instance, null);
        }
    }
}
