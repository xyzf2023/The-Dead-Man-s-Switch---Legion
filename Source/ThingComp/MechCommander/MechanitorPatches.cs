using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 作用：使带 CompMechCommanderMarker 的 Commander 满足「应是机械师」判定。
    /// 原版 ShouldBeMechanitor 仅对「玩家方 + 有机械链接植入」返回 true；本补丁在返回 false 时，
    /// 若 Pawn 为玩家方且带 Marker（Commander 机械体），则改为 true，以便后续 AddAndRemoveDynamicComponents 等为其挂上 mechanitor。
    /// 优化：仅对机械体做 Marker 检查（Commander 必为机械体），减少对大量人类殖民者的 GetComp 调用。
    /// </summary>
    [HarmonyPatch(typeof(MechanitorUtility), "ShouldBeMechanitor")]
    public static class Patch_ShouldBeMechanitor
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Pawn pawn)
        {
            if (__result || pawn == null)
            {
                return;
            }

            if (!ModsConfig.BiotechActive)
            {
                return;
            }

            if (pawn.Faction == null || !pawn.Faction.IsPlayerSafe())
            {
                return;
            }

            // Commander 均为机械体，人类殖民者无需查 Marker，避免热路径上的 GetComp
            if (!pawn.RaceProps.IsMechanoid)
            {
                return;
            }

            if (CompMechCommanderMarker.PawnHasMarker(pawn))
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// 作用：使 Commander 通过「是否为机械师」判定，并确保其拥有 Pawn_MechanitorTracker（若尚未有则创建并通知一次）。
    /// 原版 IsMechanitor 依赖 ShouldBeMechanitor + mechanitor!=null；本补丁在原版为 false 时，若为玩家方 Commander，则确保 tracker 存在并返回 true。
    /// 优化：仅对机械体查 Marker；EnsureMechanitorTracker 仅在新建 tracker 时调用 Notify_PawnSpawned，避免每次 IsMechanitor 都触发带宽/控制组重算。
    /// </summary>
    [HarmonyPatch(typeof(MechanitorUtility), "IsMechanitor")]
    public static class Patch_IsMechanitor
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result || pawn == null)
            {
                return;
            }

            // Commander 均为机械体，人类殖民者无需查 Marker
            if (!pawn.RaceProps.IsMechanoid)
            {
                return;
            }

            if (CompMechCommanderMarker.PawnHasMarker(pawn) && pawn.Faction == Faction.OfPlayer)
            {
                MechCommanderUtility.EnsureMechanitorTracker(pawn);
                __result = true;
            }
        }
    }

    /// <summary>
    /// 为 Commander 补齐 Pawn_MechanitorTracker 与 Pawn_RelationsTracker。
    /// </summary>
    [HarmonyPatch(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents")]
    public static class Patch_AddAndRemoveDynamicComponents
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn)
        {
            if (pawn == null || !ModsConfig.BiotechActive)
            {
                return;
            }

            if (!pawn.RaceProps.IsMechanoid)
            {
                return;
            }

            bool shouldBeMechanitor = MechanitorUtility.ShouldBeMechanitor(pawn);
            if (shouldBeMechanitor)
            {
                if (pawn.mechanitor == null)
                {
                    pawn.mechanitor = new Pawn_MechanitorTracker(pawn);
                }

                if (pawn.relations == null)
                {
                    pawn.relations = new Pawn_RelationsTracker(pawn);
                }
            }
            else if (pawn.mechanitor != null)
            {
                pawn.mechanitor = null;
            }
        }
    }

    /// <summary>
    /// 禁止指挥官被其他机械体/指挥官设为监管对象。
    /// </summary>
    [HarmonyPatch(typeof(MechanitorUtility), "CanControlMech")]
    public static class Patch_MechanitorUtility_CanControlMech
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, Pawn mech, ref AcceptanceReport __result)
        {
            if (!__result.Accepted || pawn == null || mech == null)
            {
                return;
            }

            // 被控目标是指挥官（带机控标记）且尝试监管的是机械体时，拒绝
            if (CompMechCommanderMarker.PawnHasMarker(mech) && pawn.RaceProps.IsMechanoid)
            {
                __result = new AcceptanceReport("DMSL_Commander_CannotBeControlledByMech".Translate());
            }
        }
    }

    /// <summary>
    /// 指挥范围判定：指挥官自身始终视为在范围内；由指挥官监管的机械体仅在与指挥官同一地图时视为在范围内。
    /// </summary>
    [HarmonyPatch(typeof(MechanitorUtility), "InMechanitorCommandRange")]
    public static class Patch_MechanitorUtility_InMechanitorCommandRange_Commander
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn mech, LocalTargetInfo target, ref bool __result)
        {
            if (mech == null)
            {
                return true;
            }

            // 指挥官自身：始终视为在范围内，可自由活动
            if (CompMechCommanderMarker.PawnHasMarker(mech))
            {
                __result = true;
                return false;
            }

            // 由指挥官监管的机械体：仅在与指挥官同一地图时视为在范围内
            Pawn overseer = mech.GetOverseer();
            if (overseer != null && CompMechCommanderMarker.PawnHasMarker(overseer))
            {
                __result = mech.Map != null && overseer.Map != null && mech.Map == overseer.Map;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 隐藏指挥官的控制范围绘制（选中指挥官时不显示范围圈）。
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MechanitorTracker), "DrawCommandRadius")]
    public static class Patch_Pawn_MechanitorTracker_DrawCommandRadius
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_MechanitorTracker __instance)
        {
            Pawn? pawn = __instance?.Pawn;
            if (pawn != null && CompMechCommanderMarker.PawnHasMarker(pawn))
                return false;
            return true;
        }
    }

    /// <summary>
    /// 放行指挥官对物体/工作的右键菜单：右键机械组装平台等时，Commander 可出现在「优先执行」的执行者列表中。
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuOptionProvider), "SelectedPawnValid")]
    public static class Patch_FloatMenuOptionProvider_SelectedPawnValid_WorkGivers
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, FloatMenuContext context, ref bool __result, FloatMenuOptionProvider __instance)
        {
            if (__result || pawn == null || !pawn.RaceProps.IsMechanoid)
            {
                return;
            }

            if (__instance is not FloatMenuOptionProvider_FromThing
                && __instance is not FloatMenuOptionProvider_WorkGivers
                && __instance is not FloatMenuOptionProvider_Trade)
            {
                return;
            }

            if (!CompMechCommanderMarker.PawnHasMarker(pawn))
            {
                return;
            }

            try
            {
                Traverse traverse = Traverse.Create(__instance);
                bool drafted = traverse.Property("Drafted").GetValue<bool>();
                bool undrafted = traverse.Property("Undrafted").GetValue<bool>();
                bool requiresManipulation = traverse.Property("RequiresManipulation").GetValue<bool>();

                bool draftedOk = drafted || !pawn.Drafted;
                bool undraftedOk = undrafted || pawn.Drafted;
                bool manipulationOk = !requiresManipulation || (pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false);

                if (draftedOk && undraftedOk && manipulationOk)
                {
                    __result = true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[DMS_Legion] 指挥官工作/物体右键菜单补丁失败: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// 放行指挥官的机控右键菜单：选中 Commander 时显示控制机械体的右键选项。
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuOptionProvider), "SelectedPawnValid")]
    public static class Patch_FloatMenuOptionProvider_SelectedPawnValid_Mechanitor
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, FloatMenuContext context, ref bool __result, FloatMenuOptionProvider __instance)
        {
            if (__result || pawn == null || !pawn.RaceProps.IsMechanoid)
            {
                return;
            }

            if (__instance is not FloatMenuOptionProvider_Mechanitor)
            {
                return;
            }

            if (!CompMechCommanderMarker.PawnHasMarker(pawn))
            {
                return;
            }

            Traverse traverse = Traverse.Create(__instance);
            try
            {
                bool drafted = traverse.Property("Drafted").GetValue<bool>();
                bool undrafted = traverse.Property("Undrafted").GetValue<bool>();
                bool requiresManipulation = traverse.Property("RequiresManipulation").GetValue<bool>();

                bool draftedOk = drafted || !pawn.Drafted;
                bool undraftedOk = undrafted || pawn.Drafted;
                bool manipulationOk = !requiresManipulation || (pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false);

                if (draftedOk && undraftedOk && manipulationOk)
                {
                    __result = true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[DMS_Legion] 指挥官机控右键菜单补丁失败: " + ex.Message);
            }
        }
    }
}
