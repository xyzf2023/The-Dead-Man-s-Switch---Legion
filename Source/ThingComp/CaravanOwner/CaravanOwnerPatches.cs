using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DMSL
{
    /// <summary>
    /// 判断地图上是否存在可担任远行队领队的 pawn（仅限带 CompCaravanOwner 的机械体）。
    /// </summary>
    public static class CaravanReformUtility
    {
        public static bool MapHasCaravanOwnerCapablePawn(Map map)
        {
            if (map == null)
            {
                return false;
            }

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead || p.Downed || p.Faction != Faction.OfPlayer)
                {
                    continue;
                }

                if (CompCaravanOwner.PawnCanBeCaravanOwner(p))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 放宽「可重组远行队」条件：无当前威胁且地图上有带 CompCaravanOwner 的机械体时也视为可重组。
    /// </summary>
    [HarmonyPatch(typeof(FormCaravanComp), "CanFormOrReformCaravanNow", MethodType.Getter)]
    public static class Patch_FormCaravanComp_CanFormOrReformCaravanNow
    {
        [HarmonyPostfix]
        public static void Postfix(FormCaravanComp __instance, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            if (__instance.parent is not MapParent mapParent || !mapParent.HasMap || !__instance.Reform)
            {
                return;
            }

            if (Traverse.Create(__instance).Property("AnyActiveThreatNow").GetValue<bool>())
            {
                return;
            }

            if (CaravanReformUtility.MapHasCaravanOwnerCapablePawn(mapParent.Map))
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// 放宽「现在可重组」判定，与 CanFormOrReformCaravanNow 一致。
    /// </summary>
    [HarmonyPatch(typeof(FormCaravanComp), "CanReformNow")]
    public static class Patch_FormCaravanComp_CanReformNow
    {
        [HarmonyPostfix]
        public static void Postfix(FormCaravanComp __instance, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            if (__instance.parent is not MapParent mapParent || !mapParent.HasMap || !__instance.Reform)
            {
                return;
            }

            if (!__instance.CanFormOrReformCaravanNow)
            {
                return;
            }

            if (CaravanReformUtility.MapHasCaravanOwnerCapablePawn(mapParent.Map))
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// 当原版不显示「重组远行队」按钮时，若地图上有可当领队的机械体，则补充显示该按钮。
    /// </summary>
    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(FormCaravanComp), "GetGizmos")]
    public static class Patch_FormCaravanComp_GetGizmos
    {
        private static readonly Texture2D FormCaravanCommand =
            ContentFinder<Texture2D>.Get("UI/Commands/FormCaravan", true);

        [HarmonyPostfix]
        public static void Postfix(FormCaravanComp __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.parent is not MapParent mapParent || !mapParent.HasMap || !__instance.Reform)
            {
                return;
            }

            if (!__instance.CanFormOrReformCaravanNow)
            {
                return;
            }

            List<Gizmo> list = __result?.ToList() ?? new List<Gizmo>();
            bool hasReformButton = list.Any(g =>
                g is Command_Action ca && ca.defaultLabel == "CommandReformCaravan".Translate());

            if (hasReformButton)
            {
                return;
            }

            if (!CaravanReformUtility.MapHasCaravanOwnerCapablePawn(mapParent.Map))
            {
                return;
            }

            Command_Action reformAction = new Command_Action
            {
                defaultLabel = "CommandReformCaravan".Translate(),
                defaultDesc = "CommandReformCaravanDesc".Translate(),
                icon = FormCaravanCommand,
                hotKey = KeyBindingDefOf.Misc2,
                tutorTag = "ReformCaravan",
                action = () =>
                {
                    if (ModsConfig.OdysseyActive &&
                        mapParent.Map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle).Any())
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "ConfirmLoseShuttle".Translate(),
                            () => Find.WindowStack.Add(new Dialog_FormCaravan(mapParent.Map, true)),
                            false, null, WindowLayer.Dialog));
                        return;
                    }

                    Find.WindowStack.Add(new Dialog_FormCaravan(mapParent.Map, true));
                }
            };

            if (GenHostility.AnyHostileActiveThreatToPlayer(mapParent.Map, true, false))
            {
                reformAction.Disable("CommandReformCaravanFailHostilePawns".Translate());
            }

            list.Add(reformAction);
            __result = list;
        }
    }

    /// <summary>
    /// 远行队领队资格：原版 IsOwner 为 false 时，若 pawn 带 CompCaravanOwner 且派系一致，则视为 Owner。
    /// </summary>
    [HarmonyPatch(typeof(CaravanUtility), "IsOwner")]
    public static class Patch_CaravanUtility_IsOwner
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Pawn pawn, Faction caravanFaction)
        {
            if (__result)
            {
                return;
            }

            if (pawn == null || caravanFaction == null)
            {
                return;
            }

            if (pawn.Faction != caravanFaction)
            {
                return;
            }

            if (CompCaravanOwner.PawnCanBeCaravanOwner(pawn))
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// 编队界面发送时，为带 CompCaravanOwner 的机械体临时注入 story/skills，以便原版逻辑能正确处理；发送后恢复。
    /// </summary>
    [HarmonyPatch(typeof(Dialog_FormCaravan), "TrySend")]
    public static class Patch_Dialog_FormCaravan_TrySend
    {
        public struct TempState
        {
            public List<Pawn> StoryAdded;
            public List<Pawn> SkillsAdded;
        }

        [HarmonyPrefix]
        public static void Prefix(Dialog_FormCaravan __instance, ref TempState __state)
        {
            __state.StoryAdded = new List<Pawn>();
            __state.SkillsAdded = new List<Pawn>();

            List<TransferableOneWay> transferables =
                Traverse.Create(__instance).Field("transferables").GetValue<List<TransferableOneWay>>();
            if (transferables == null)
            {
                return;
            }

            List<Pawn> pawns = TransferableUtility.GetPawnsFromTransferables(transferables);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Faction != Faction.OfPlayer)
                {
                    continue;
                }

                if (!CompCaravanOwner.PawnCanBeCaravanOwner(pawn))
                {
                    continue;
                }

                if (pawn.story == null)
                {
                    pawn.story = new Pawn_StoryTracker(pawn);
                    __state.StoryAdded.Add(pawn);
                }

                if (pawn.skills == null)
                {
                    pawn.skills = new Pawn_SkillTracker(pawn);
                    __state.SkillsAdded.Add(pawn);
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(ref TempState __state)
        {
            if (__state.SkillsAdded != null)
            {
                for (int i = 0; i < __state.SkillsAdded.Count; i++)
                {
                    Pawn pawn = __state.SkillsAdded[i];
                    if (pawn != null)
                    {
                        pawn.skills = null;
                    }
                }
            }

            if (__state.StoryAdded != null)
            {
                for (int i = 0; i < __state.StoryAdded.Count; i++)
                {
                    Pawn pawn = __state.StoryAdded[i];
                    if (pawn != null)
                    {
                        pawn.story = null;
                    }
                }
            }
        }
    }
}
