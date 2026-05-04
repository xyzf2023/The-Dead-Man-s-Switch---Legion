using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 培育器账单配置：将 Commander 加入执行者菜单；若配方为「培育 Commander」则显示禁用理由。
    /// </summary>
    [HarmonyPatch(typeof(Dialog_BillConfig), "GeneratePawnRestrictionOptions")]
    public static class Patch_Dialog_BillConfig_GeneratePawnRestrictionOptions
    {
        [HarmonyPostfix]
        public static void Postfix(Dialog_BillConfig __instance, ref IEnumerable<Widgets.DropdownMenuElement<Pawn>> __result)
        {
            if (!ModsConfig.BiotechActive)
                return;

            Bill_Production? bill = AccessTools.Field(typeof(Dialog_BillConfig), "bill")
                ?.GetValue(__instance) as Bill_Production;
            if (bill?.recipe == null || !bill.recipe.mechanitorOnlyRecipe)
                return;

            WorkGiverDef? workGiver = bill.billStack?.billGiver?.GetWorkgiver();
            if (workGiver == null)
                return;

            List<Widgets.DropdownMenuElement<Pawn>> list = __result.ToList();
            HashSet<Pawn> existing = new HashSet<Pawn>(list.Where(e => e.payload != null).Select(e => e.payload!));

            List<Pawn> commanders = Find.Maps
                .SelectMany(m => m.mapPawns.AllPawnsSpawned)
                .Where(p => p.Faction == Faction.OfPlayer && CompMechCommanderMarker.PawnHasMarker(p))
                .ToList();

            foreach (Pawn pawn in commanders)
            {
                if (existing.Contains(pawn))
                    continue;
                list.Add(BuildMenuElementForPawn(bill, pawn, workGiver));
                existing.Add(pawn);
            }

            __result = list;
        }

        private static Widgets.DropdownMenuElement<Pawn> BuildMenuElementForPawn(Bill_Production bill, Pawn pawn, WorkGiverDef workGiver)
        {
            if (MechGestatorRecipeUtility.IsCommanderDisabledForGestation(bill.recipe, pawn))
            {
                string reasonKey = MechGestatorRecipeUtility.GetCommanderDisabledReasonKey(bill.recipe);
                return new Widgets.DropdownMenuElement<Pawn>
                {
                    option = new FloatMenuOption(
                        string.Format("{0} ({1})", pawn.LabelShortCap, reasonKey.Translate()),
                        null),
                    payload = pawn
                };
            }

            if (pawn.WorkTypeIsDisabled(workGiver.workType))
            {
                return new Widgets.DropdownMenuElement<Pawn>
                {
                    option = new FloatMenuOption(
                        string.Format("{0} ({1})", pawn.LabelShortCap, "WillNever".Translate(workGiver.label)),
                        null),
                    payload = pawn
                };
            }

            if (bill.recipe.workSkill != null && !pawn.workSettings.WorkIsActive(workGiver.workType))
            {
                return new Widgets.DropdownMenuElement<Pawn>
                {
                    option = new FloatMenuOption(
                        string.Format("{0} ({1} {2}, {3})",
                            pawn.LabelShortCap,
                            pawn.skills.GetSkill(bill.recipe.workSkill).Level,
                            bill.recipe.workSkill.label,
                            "NotAssigned".Translate()),
                        () => bill.SetPawnRestriction(pawn)),
                    payload = pawn
                };
            }

            if (!pawn.workSettings.WorkIsActive(workGiver.workType))
            {
                return new Widgets.DropdownMenuElement<Pawn>
                {
                    option = new FloatMenuOption(
                        string.Format("{0} ({1})", pawn.LabelShortCap, "NotAssigned".Translate()),
                        () => bill.SetPawnRestriction(pawn)),
                    payload = pawn
                };
            }

            if (bill.recipe.workSkill != null)
            {
                return new Widgets.DropdownMenuElement<Pawn>
                {
                    option = new FloatMenuOption(
                        string.Format("{0} ({1} {2})",
                            pawn.LabelShortCap,
                            pawn.skills.GetSkill(bill.recipe.workSkill).Level,
                            bill.recipe.workSkill.label),
                        () => bill.SetPawnRestriction(pawn)),
                    payload = pawn
                };
            }

            return new Widgets.DropdownMenuElement<Pawn>
            {
                option = new FloatMenuOption(pawn.LabelShortCap, () => bill.SetPawnRestriction(pawn)),
                payload = pawn
            };
        }
    }
}
