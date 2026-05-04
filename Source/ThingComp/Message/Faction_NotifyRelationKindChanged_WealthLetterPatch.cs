using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 当与武装殖民舰队（DMS_Army）的关系发生变化时，立即做一次“是否发送结盟介绍信”的判断，避免只依赖定时检测导致信件迟迟不触发。
    /// </summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_RelationKindChanged))]
    public static class Faction_NotifyRelationKindChanged_WealthLetterPatch
    {
        private const string DmsArmyDefName = "DMS_Army";

        [HarmonyPostfix]
        public static void Postfix(Faction __instance, Faction other)
        {
            if (other != Faction.OfPlayer)
                return;
            if (__instance?.def == null || __instance.def.defName != DmsArmyDefName)
                return;
            if (__instance.PlayerRelationKind != FactionRelationKind.Ally)
                return;
            GameComponent_WealthTriggeredLetter? comp = Current.Game?.GetComponent<GameComponent_WealthTriggeredLetter>();
            comp?.TrySendArmyAllyLetterNow();
        }
    }
}
