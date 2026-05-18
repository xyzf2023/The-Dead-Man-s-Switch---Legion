using HarmonyLib;
using RimWorld;
using Verse;
using DMS_Legion.GroundSupport.SupportEffects;

namespace DMS_Legion
{
    /// <summary>
    /// 地图初始化时确保消防泡沫波纹控制器存在（供 FirefoamRipple 效果使用）。
    /// </summary>
    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    public static class Map_FinalizeInit_FirefoamRippleController_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Map __instance)
        {
            if (__instance == null)
                return;
            if (__instance.GetComponent<FirefoamRippleController>() != null)
                return;
            __instance.components.Add(new FirefoamRippleController(__instance));
        }
    }

    /// <summary>
    /// 拦截点选择：CompPowerTrader 的 StunnedByEMP 是私有 getter，控制 PowerOutput 等是否视为“被 EMP 停摆”。
    /// 仅在此 getter 上做 Postfix，让“EMP 波纹临时停摆表”中的建筑也表现为无电，不修改电网或其它逻辑，副作用范围最小。
    /// 优化：按 Map 缓存 GetComponent 结果，无停摆时快速返回，避免电器多时每 tick 大量重复查找。
    /// </summary>
    [HarmonyPatch(typeof(CompPowerTrader), "get_StunnedByEMP")]
    public static class EmpRipplePowerSuppressionPatch
    {
        [HarmonyPostfix]
        public static void Postfix(CompPowerTrader __instance, ref bool __result)
        {
            if (__result) return;
            if (!EmpRippleController.AnyPowerSuppressionActive) return;

            Thing? parent = __instance?.parent;
            if (parent == null || !parent.Spawned || parent.Map == null) return;

            EmpRippleController? controller = parent.Map.GetComponent<EmpRippleController>();
            if (controller == null) return;
            if (!controller.HasAnyPowerSuppression) return;

            if (controller.IsPowerSuppressed(parent))
                __result = true;
        }
    }
}
