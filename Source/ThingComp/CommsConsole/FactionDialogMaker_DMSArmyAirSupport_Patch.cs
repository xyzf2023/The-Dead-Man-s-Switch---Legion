// ============================================================================
// 文件：FactionDialogMaker_DMSArmyAirSupport_Patch.cs
// 说明：为 DMS_Army 派系的通讯台对话添加「请求空中支援」主选项，点击后进入子界面（子界面内容由 AirSupportSubNodeBuilder 及各 AirSupportOption* 文件提供）
// ============================================================================

using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// DMS_Army 派系通讯台对话补丁：添加「请求空中支援」入口选项
    /// </summary>
    [HarmonyPatch(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor))]
    public static class FactionDialogMaker_DMSArmyAirSupport_Patch
    {
        private const string DmsArmyFactionDefName = "DMS_Army";

        [HarmonyPostfix]
        public static void Postfix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            if (__result == null || faction?.def == null || faction.def.defName != DmsArmyFactionDefName)
                return;

            DiaOption requestAirSupportOpt;
            if (faction.PlayerRelationKind != FactionRelationKind.Ally)
            {
                requestAirSupportOpt = new DiaOption("DMSL_Comms_RequestAirSupport".Translate());
                requestAirSupportOpt.Disable("MustBeAlly".Translate());
            }
            else
            {
                requestAirSupportOpt = new DiaOption("DMSL_Comms_RequestAirSupport".Translate());
                requestAirSupportOpt.link = AirSupportSubNodeBuilder.CreateSubNode(faction, negotiator);
            }

            __result.options.Insert(__result.options.Count - 1, requestAirSupportOpt);
        }
    }
}
