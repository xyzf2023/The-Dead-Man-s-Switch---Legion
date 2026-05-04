using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    [HarmonyPatch(typeof(CompProjectileInterceptor), "Active", MethodType.Getter)]
    public static class CompProjectileInterceptor_Active_Patch
    {
        static void Postfix(CompProjectileInterceptor __instance, ref bool __result)
        {
            var controller = __instance?.parent?.TryGetComp<CompTimedShieldController>();
            if (controller != null)
                __result = controller.IsShieldActive && __result;
        }
    }
    
    [HarmonyPatch(typeof(CompProjectileInterceptor), "HitPointsMax", MethodType.Getter)]
    public static class CompProjectileInterceptor_HitPointsMax_Patch
    {
        static void Postfix(CompProjectileInterceptor __instance, ref int __result)
        {
            var controller = __instance?.parent?.TryGetComp<CompTimedShieldController>();
            if (controller?.Props != null)
                __result = controller.Props.hitPointsMapping;
        }
    }
    
    [HarmonyPatch(typeof(CompProjectileInterceptor), "CompTick")]
    public static class CompProjectileInterceptor_CompTick_Patch
    {
        static AccessTools.FieldRef<CompProjectileInterceptor, int> currentHitPointsField = 
            AccessTools.FieldRefAccess<CompProjectileInterceptor, int>("currentHitPoints");
        
        static void Postfix(CompProjectileInterceptor __instance)
        {
            if (__instance?.parent == null) return;
            var controller = __instance.parent.TryGetComp<CompTimedShieldController>();
            if (controller?.Props != null)
            {
                try
                {
                    currentHitPointsField(__instance) = controller.GetHitPointsFromTime();
                }
                catch
                {
                    // 如果字段访问失败，忽略错误
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(CompProjectileInterceptor), "CheckIntercept")]
    public static class CompProjectileInterceptor_CheckIntercept_Patch
    {
        static AccessTools.FieldRef<CompProjectileInterceptor, int> currentHitPointsField = 
            AccessTools.FieldRefAccess<CompProjectileInterceptor, int>("currentHitPoints");
        
        static void Postfix(CompProjectileInterceptor __instance, ref bool __result)
        {
            if (!__result || __instance?.parent == null) return;
            var controller = __instance.parent.TryGetComp<CompTimedShieldController>();
            if (controller?.Props != null && controller.IsShieldActive)
            {
                try
                {
                    currentHitPointsField(__instance) = controller.GetHitPointsFromTime();
                }
                catch
                {
                    // 如果字段访问失败，忽略错误
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(CompProjectileInterceptor), "CompGetGizmosExtra")]
    public static class CompProjectileInterceptor_CompGetGizmosExtra_Patch
    {
        /// <summary>有 TimedShieldController 时不执行原版逻辑，避免显示原版“护盾能量”gizmo。</summary>
        static bool Prefix(CompProjectileInterceptor __instance, ref bool __state)
        {
            if (__instance?.parent == null) { __state = false; return true; }
            var controller = __instance.parent.TryGetComp<CompTimedShieldController>();
            __state = controller?.Props != null;
            return !__state;
        }
        
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, CompProjectileInterceptor __instance, bool __state)
        {
            if (__state)
            {
                var controller = __instance.parent.TryGetComp<CompTimedShieldController>();
                if (controller?.Props != null)
                {
                    foreach (var gizmo in controller.GetGizmos())
                        yield return gizmo;
                }
                yield break;
            }
            if (__result != null)
            {
                foreach (var gizmo in __result)
                    yield return gizmo;
            }
        }
    }
    
    [HarmonyPatch(typeof(Verb), "CanHitTarget")]
    public static class Verb_CanHitTarget_Patch
    {
        static void Postfix(Verb __instance, ref bool __result)
        {
            if (!__result || !__instance.CasterIsPawn) return;
            var controller = __instance.CasterPawn?.TryGetComp<CompTimedShieldController>();
            if (controller?.Props != null && controller.IsShieldActive)
            {
                var interceptor = __instance.CasterPawn?.TryGetComp<CompProjectileInterceptor>();
                if (interceptor?.Props?.interceptOutgoingProjectiles == false)
                    return;
            }
        }
    }
}
