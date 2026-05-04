using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DMS_Legion.AXF12
{
    [HarmonyPatch(typeof(MapParent), "CheckRemoveMapNow")]
    public static class AXF12ObservedMap_CheckRemoveMapNow_Patch
    {
        public static bool Prefix(MapParent __instance)
        {
            if (AXF12ReconMissionManager.Instance?.ObservedMapParent == __instance)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(MapParent), "GetGizmos")]
    public static class AXF12ObservedMap_GetGizmos_Patch
    {
        public static void Postfix(MapParent __instance, ref IEnumerable<Gizmo> __result)
        {
            if (AXF12ReconMissionManager.Instance?.ObservedMapParent != __instance)
            {
                return;
            }

            var gizmos = __result?.ToList() ?? new List<Gizmo>();
            gizmos.Add(new Command_Action
            {
                defaultLabel = "DMSL_AXF12_StopObserving_Label".Translate(),
                defaultDesc = "DMSL_AXF12_StopObserving_Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Unfocus", false),
                action = () => StopObserving(__instance)
            });

            __result = gizmos;
        }

        private static void StopObserving(MapParent mapParent)
        {
            AXF12ReconMissionManager.Instance?.SetObservedMap(null);
        }
    }

    [HarmonyPatch(typeof(Settlement), "ShouldRemoveMapNow")]
    public static class AXF12ObservedMap_SettlementShouldRemoveMapNow_Patch
    {
        public static void Postfix(ref bool __result, Settlement __instance)
        {
            if (!__result)
            {
                return;
            }

            if (AXF12ReconMissionManager.Instance?.ObservedMapParent == __instance)
            {
                __result = false;
            }
        }
    }
}
