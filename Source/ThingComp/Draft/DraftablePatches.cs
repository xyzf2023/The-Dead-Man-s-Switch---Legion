// [此组件暂时未被使用] 

/*
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 假控制组，供带 CompDraftable 的机械体使用，避免原版 GetMechControlGroup 返回 null 导致空引用。
    /// </summary>
    public class FakeMechControlGroup : MechanitorControlGroup
    {
        private static FakeMechControlGroup? _instance;

        public static FakeMechControlGroup Instance =>
            _instance ??= new FakeMechControlGroup();

        private FakeMechControlGroup() : base(null) { }

        public new void Assign(Pawn pawn) { }

        public new bool TryUnassign(Pawn pawn) => false;

        public new void SetWorkMode(MechWorkModeDef workMode) { }
    }

    /// <summary>
    /// 为带 CompDraftable 的机械体创建 DraftController，使其显示征召按钮。
    /// </summary>
    [HarmonyPatch(typeof(PawnComponentsUtility), "CreateInitialComponents")]
    public static class Patch_PawnComponentsUtility_CreateInitialComponents
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn)
        {
            if (pawn.drafter == null &&
                pawn.RaceProps.IsMechanoid &&
                CompDraftable.PawnIsDraftable(pawn))
            {
                pawn.drafter = new Pawn_DraftController(pawn);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_IsColonyMech")]
    public static class Patch_Pawn_IsColonyMech
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (!__result && __instance.RaceProps.IsMechanoid && CompDraftable.PawnIsDraftable(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_IsColonyMechPlayerControlled")]
    public static class Patch_Pawn_IsColonyMechPlayerControlled
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (!__result && CompDraftable.PawnIsDraftable(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), "GetMechControlGroup", typeof(Pawn))]
    public static class Patch_MechanitorUtility_GetMechControlGroup
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, ref MechanitorControlGroup __result)
        {
            if (CompDraftable.PawnIsDraftable(pawn))
            {
                __result = FakeMechControlGroup.Instance;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), "InMechanitorCommandRange")]
    public static class Patch_MechanitorUtility_InMechanitorCommandRange
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn mech, LocalTargetInfo target, ref bool __result)
        {
            if (CompDraftable.PawnIsDraftable(mech))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), "CanDraftMech")]
    public static class Patch_MechanitorUtility_CanDraftMech
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn mech, ref AcceptanceReport __result)
        {
            if (!CompDraftable.PawnIsDraftable(mech))
            {
                return true;
            }

            if (mech.needs?.energy != null && mech.needs.energy.IsLowEnergySelfShutdown)
            {
                __result = "IsLowEnergySelfShutdown".Translate(mech.Named("PAWN"));
                return false;
            }

            __result = true;
            return false;
        }
    }

    /// <summary>
    /// 在每次加载存档或新建游戏时清空 CompDraftable 的 ID 缓存，
    /// 防止跨存档 thingIDNumber 碰撞导致误判。
    /// 各 CompDraftable 实例会在自身生命周期钩子中重新注册。
    /// </summary>
    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    public static class Patch_Game_FinalizeInit_ClearDraftableCache
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            CompDraftable.ClearCache();
        }
    }
}
*/
