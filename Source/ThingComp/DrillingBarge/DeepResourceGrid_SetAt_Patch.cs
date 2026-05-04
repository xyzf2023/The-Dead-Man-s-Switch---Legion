// ============================================================================
// 在 DeepResourceGrid.SetAt 时维护 DeepResourceGridTrackerComponent 的非零格计数，
// 使 WorkGiver ShouldSkip 可 O(1) 判断“是否有可钻深矿”，避免无矿时全图扫描。
// ============================================================================

using HarmonyLib;
using Verse;

namespace DMS_Legion
{
    [HarmonyPatch(typeof(DeepResourceGrid), nameof(DeepResourceGrid.SetAt))]
    [HarmonyPatch(new[] { typeof(IntVec3), typeof(ThingDef), typeof(int) })]
    public static class DeepResourceGrid_SetAt_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(DeepResourceGrid __instance, IntVec3 c, ref bool __state)
        {
            __state = __instance.CountAt(c) > 0;
        }

        [HarmonyPostfix]
        public static void Postfix(Map ___map, IntVec3 c, int count, bool __state)
        {
            if (___map == null)
                return;
            // 仅更新已存在的组件，避免与 GetOrCreate 时的 InitFromGrid 重复计数
            var comp = ___map.GetComponent<DeepResourceGridTrackerComponent>();
            if (comp == null)
                return;
            bool wasNonZero = __state;
            bool isNonZero = count > 0;
            if (!wasNonZero && isNonZero)
                comp.NonZeroCellCount++;
            else if (wasNonZero && !isNonZero)
                comp.NonZeroCellCount--;
        }
    }
}
