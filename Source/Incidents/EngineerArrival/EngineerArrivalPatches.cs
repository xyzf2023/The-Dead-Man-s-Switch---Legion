// ============================================================================
// 文件：EngineerArrivalPatches.cs
// 说明：游荡机兵到达事件相关的 Harmony 补丁
// ============================================================================

using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion.Incidents.EngineerArrival
{
    /// <summary>
    /// 机控中枢植入后触发游荡机兵事件的补丁
    /// </summary>
    [HarmonyPatch(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.DoEffect))]
    public static class Patch_CompUseEffect_InstallImplant_DoEffect
    {
        [HarmonyPostfix]
        public static void Postfix(CompUseEffect_InstallImplant __instance, Pawn user)
        {
            if (user == null || !user.Spawned || user.Map == null)
                return;

            if (__instance.Props.hediffDef != HediffDefOf.MechlinkImplant)
                return;

            if (!ModsConfig.BiotechActive)
                return;

            Thing parent = __instance.parent;
            if (parent?.def == null)
                return;

            float chance;
            if (parent.def.defName == "DMS_MechLink" || parent.def.defName == "DMS_MechLink_Nerfed")
            {
                chance = 1f;
            }
            else if (parent.def.defName == "Mechlink")
            {
                chance = 0.5f;
            }
            else
            {
                return;
            }

            if (!Rand.Chance(chance))
                return;

            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("DMSL_EngineerArrival");
            if (incidentDef?.Worker == null)
                return;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, user.Map);
            parms.target = user.Map;

            // 延迟 300 tick 后触发，再生成机兵并发送信件
            Find.Storyteller.incidentQueue.Add(incidentDef, Find.TickManager.TicksGame + 300, parms);
        }
    }

    /// <summary>
    /// 防止带游荡机兵 Hediff 的机械体被 CompOverseerSubject 判定为可野化
    /// </summary>
    [HarmonyPatch(typeof(CompOverseerSubject), "CanGoFeral")]
    public static class Patch_CompOverseerSubject_CanGoFeral
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && pawn?.health?.hediffSet != null)
            {
                if (pawn.health.hediffSet.HasHediff(HediffDef.Named("DMSL_Hediff_WanderingEngineer")))
                {
                    __result = false;
                }
            }
        }
    }

    /// <summary>
    /// 游荡机兵离图时销毁，而非进入世界
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
    public static class Patch_Pawn_ExitMap
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn __instance, bool allowedToJoinOrCreateCaravan, Rot4 exitDir)
        {
            if (__instance?.health?.hediffSet == null)
                return true;

            if (!__instance.health.hediffSet.HasHediff(HediffDef.Named("DMSL_Hediff_WanderingEngineer")))
                return true;

            __instance.Destroy();
            return false;
        }
    }
}
