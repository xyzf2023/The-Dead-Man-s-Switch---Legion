using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DMS_Legion.AXF12
{
    [HarmonyPatch(typeof(CompLaunchable), "FuelNeededToLaunchAtDist")]
    [HarmonyPatch(new[] { typeof(float), typeof(PlanetLayer) })]
    public static class AXF12LaunchFuelCostPatch
    {
        public static void Postfix(CompLaunchable __instance, ref float __result)
        {
            if (AXF12LaunchContext.CustomFuelCostActive && IsReconTarget(__instance))
            {
                __result = Mathf.Max(0f, AXF12LaunchContext.CustomFuelCost);
                return;
            }

            if (!AXF12LaunchContext.ReconFuelMultiplierActive)
            {
                return;
            }

            if (!IsReconTarget(__instance))
            {
                return;
            }

            float multiplier = Mathf.Max(1f, AXF12LaunchContext.ReconFuelMultiplier);
            __result *= multiplier;
        }

        public static bool IsReconTarget(CompLaunchable launchable)
        {
            if (launchable?.parent?.def?.defName == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(AXF12LaunchContext.AllowedDefName) &&
                launchable.parent.def.defName != AXF12LaunchContext.AllowedDefName)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(CompLaunchable), "MaxLaunchDistanceAtFuelLevel")]
    [HarmonyPatch(new[] { typeof(float), typeof(PlanetLayer) })]
    public static class AXF12LaunchMaxDistancePatch
    {
        public static void Postfix(CompLaunchable __instance, ref int __result)
        {
            if (AXF12LaunchContext.CustomFuelCostActive)
            {
                return;
            }

            if (!AXF12LaunchContext.ReconFuelMultiplierActive)
            {
                return;
            }

            if (__result <= 0)
            {
                return;
            }

            if (!AXF12LaunchFuelCostPatch.IsReconTarget(__instance))
            {
                return;
            }

            float multiplier = Mathf.Max(1f, AXF12LaunchContext.ReconFuelMultiplier);
            __result = Mathf.Max(0, Mathf.FloorToInt(__result / multiplier));
        }
    }
 
    /// <summary>
    /// 侦察发射时完全拦截原版燃料消耗，仅按我们计算的 CustomFuelCost 自行扣油。
    /// 原版 ConsumeFuel 不执行；通过反射写 CompRefuelable.fuel 并视情况调用 Notify_RanOutOfFuel。
    /// 若同一发射流程中 ConsumeFuel 被调用多次，仅第一次扣油，后续调用仅跳过原版不再扣（CustomFuelCost 已置 0）。
    /// 参考：Rimworld-Source\1.6\Code\Assembly-CSharp\RimWorld\CompRefuelable.cs ConsumeFuel L294，fuel 字段，Notify_RanOutOfFuel L313。
    /// </summary>
    [HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.ConsumeFuel))]
    [HarmonyPatch(new[] { typeof(float) })]
    public static class AXF12ConsumeFuelPatch
    {
        private static readonly AccessTools.FieldRef<CompRefuelable, float> FuelFieldRef =
            AccessTools.FieldRefAccess<CompRefuelable, float>("fuel");
        private static readonly System.Reflection.MethodInfo NotifyRanOutOfFuelMethod =
            AccessTools.Method(typeof(CompRefuelable), "Notify_RanOutOfFuel");

        public static bool Prefix(CompRefuelable __instance, float amount)
        {
            if (!AXF12LaunchContext.CustomFuelCostActive)
            {
                return true;
            }
            if (__instance?.parent == null)
            {
                return true;
            }
            if (__instance.parent.GetComp<Comp_AXF12ReconLaunch>() == null)
            {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(AXF12LaunchContext.AllowedDefName) &&
                __instance.parent.def.defName != AXF12LaunchContext.AllowedDefName)
            {
                return true;
            }

            float toDeduct = Mathf.Min(__instance.Fuel, Mathf.Max(0f, AXF12LaunchContext.CustomFuelCost));
            float newFuel = Mathf.Max(0f, __instance.Fuel - toDeduct);

            if (FuelFieldRef != null)
            {
                FuelFieldRef(__instance) = newFuel;
            }
            else
            {
                Log.Warning("[DMS_Legion][AXF12] CompRefuelable.fuel 字段未找到，无法自行扣油。");
                AXF12LaunchContext.CustomFuelCostActive = false;
                AXF12LaunchContext.CustomFuelCost = 0f;
                return true;
            }

            if (newFuel <= 0f && NotifyRanOutOfFuelMethod != null)
            {
                try
                {
                    NotifyRanOutOfFuelMethod.Invoke(__instance, null);
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[DMS_Legion][AXF12] Notify_RanOutOfFuel 调用异常: {ex.Message}");
                }
            }

            AXF12LaunchContext.CustomFuelCost = 0f;
            return false;
        }
    }
}
