using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace DMSL
{
    /// <summary>
    /// 重整远行队时，让带 CompCaravanOwner 的机械体也能通过地图物资可达性检查。
    /// </summary>
    [HarmonyPatch(typeof(Dialog_FormCaravan), "CheckForErrors")]
    public static class Patch_Dialog_FormCaravan_CheckForErrors
    {
        [HarmonyPrefix]
        public static bool Prefix(Dialog_FormCaravan __instance, List<Pawn> pawns, ref bool __result)
        {
            if (__instance == null || pawns == null)
            {
                return true;
            }

            Traverse traverse = Traverse.Create(__instance);
            if (!traverse.Field("reform").GetValue<bool>())
            {
                return true;
            }

            if (!HasCaravanOwnerParticipant(pawns))
            {
                return true;
            }

            __result = CheckForErrorsCommanderReform(__instance, traverse, pawns);
            return false;
        }

        /// <summary>transferables 中是否包含可当远行队领队的机械体。</summary>
        private static bool HasCaravanOwnerParticipant(List<Pawn> pawns)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                if (CompCaravanOwner.PawnCanBeCaravanOwner(pawns[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 复刻原版 CheckForErrors；仅在物资可达性处将 IsColonist 换为 CanActAsCaravanCollector。
        /// </summary>
        private static bool CheckForErrorsCommanderReform(
            Dialog_FormCaravan dialog,
            Traverse traverse,
            List<Pawn> pawns)
        {
            if (traverse.Property("MustChooseRoute").GetValue<bool>()
                && !traverse.Field("destinationTile").GetValue<PlanetTile>().Valid)
            {
                Messages.Message(
                    "MessageMustChooseRouteFirst".Translate(),
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return false;
            }

            bool reform = traverse.Field("reform").GetValue<bool>();
            if (!reform && !traverse.Field("startingTile").GetValue<PlanetTile>().Valid)
            {
                Messages.Message(
                    "MessageNoValidExitTile".Translate(),
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return false;
            }

            if (!pawns.Any((Pawn x) => CaravanUtility.IsOwner(x, Faction.OfPlayer) && !x.Downed))
            {
                if (ModsConfig.IdeologyActive)
                {
                    Messages.Message(
                        "CaravanMustHaveAtLeastOneNonSlaveColonist".Translate(),
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                }
                else
                {
                    Messages.Message(
                        "CaravanMustHaveAtLeastOneColonist".Translate(),
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                }

                return false;
            }

            if (!reform && dialog.MassUsage > dialog.MassCapacity)
            {
                traverse.Method("FlashMass").GetValue();
                Messages.Message(
                    "TooBigCaravanMassUsage".Translate(),
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return false;
            }

            List<TransferableOneWay> transferables =
                traverse.Field("transferables").GetValue<List<TransferableOneWay>>();
            if (transferables == null)
            {
                return true;
            }

            for (int num = 0; num < transferables.Count; num++)
            {
                TransferableOneWay transferable = transferables[num];
                if (transferable == null || transferable.ThingDef.category != ThingCategory.Item)
                {
                    continue;
                }

                int countToTransfer = transferable.CountToTransfer;
                if (countToTransfer <= 0)
                {
                    continue;
                }

                int reachableStackCount = 0;
                for (int num3 = 0; num3 < transferable.things.Count; num3++)
                {
                    Thing t = transferable.things[num3];
                    if (t == null)
                    {
                        continue;
                    }

                    if (!t.Spawned
                        || pawns.Any((Pawn x) =>
                            CaravanOwnerUtility.CanActAsCaravanCollector(x)
                            && x.CanReach(t, PathEndMode.Touch, Danger.Deadly)))
                    {
                        reachableStackCount += t.stackCount;
                        if (reachableStackCount >= countToTransfer)
                        {
                            break;
                        }
                    }
                }

                if (reachableStackCount < countToTransfer)
                {
                    if (countToTransfer == 1)
                    {
                        Messages.Message(
                            "CaravanItemIsUnreachableSingle".Translate(transferable.ThingDef.label),
                            MessageTypeDefOf.RejectInput,
                            historical: false);
                    }
                    else
                    {
                        Messages.Message(
                            "CaravanItemIsUnreachableMulti".Translate(countToTransfer, transferable.ThingDef.label),
                            MessageTypeDefOf.RejectInput,
                            historical: false);
                    }

                    return false;
                }
            }

            return true;
        }
    }
}
