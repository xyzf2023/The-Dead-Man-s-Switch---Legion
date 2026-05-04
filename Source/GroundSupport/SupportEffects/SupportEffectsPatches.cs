using HarmonyLib;
using RimWorld;
using Verse;
using DMS_Legion.GroundSupport.SupportEffects;

namespace DMS_Legion
{
    /// <summary>
    /// 拦截点选择：CompPowerTrader 的 StunnedByEMP 是私有 getter，控制 PowerOutput 等是否视为“被 EMP 停摆”。
    /// 仅在此 getter 上做 Postfix，让“EMP 波纹临时停摆表”中的建筑也表现为无电，不修改电网或其它逻辑，副作用范围最小。
    /// 优化：按 Map 缓存 GetComponent 结果，无停摆时快速返回，避免电器多时每 tick 大量重复查找。
    /// </summary>
    [HarmonyPatch(typeof(CompPowerTrader), "get_StunnedByEMP")]
    public static class EmpRipplePowerSuppressionPatch
    {
        private static Map? _cachedMap;
        private static EmpRippleController? _cachedController;

        [HarmonyPostfix]
        public static void Postfix(CompPowerTrader __instance, ref bool __result)
        {
            if (__result) return;
            Thing? parent = __instance?.parent;
            if (parent == null || !parent.Spawned || parent.Map == null) return;

            Map map = parent.Map;

            // 缓存失效：若缓存的 Map 已不在当前游戏地图列表中，清空缓存，避免持有已卸载地图引用
            if (_cachedMap != null && (Find.Maps == null || !Find.Maps.Contains(_cachedMap)))
            {
                _cachedMap = null;
                _cachedController = null;
            }

            // 按 Map 复用 Controller，同一 tick 内同地图只做一次 GetComponent
            EmpRippleController? controller;
            if (_cachedMap == map && _cachedController != null)
            {
                controller = _cachedController;
            }
            else
            {
                controller = map.GetComponent<EmpRippleController>();
                _cachedMap = map;
                _cachedController = controller;
            }

            if (controller == null) return;

            // 快速路径：当前没有任何用电停摆时直接返回，避免对每个电器做字典查询
            if (!controller.HasAnyPowerSuppression) return;

            if (controller.IsPowerSuppressed(parent))
                __result = true;
        }
    }
}
