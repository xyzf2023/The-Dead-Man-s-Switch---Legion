using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 指挥范围扩展补丁：当机械体处于任意挂载了 CompCommandRangeExpansion 的友方机械体范围内时，
    /// 视为处于监管者控制范围内；同时，目标点若处于任意扩展范围内，也视为合法指挥目标
    /// （与原版 CanCommandTo(target) 的检查方式一致，使玩家可直接指挥机械体前往扩展范围）。
    /// </summary>
    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    public static class Patch_MechanitorUtility_InMechanitorCommandRange_CommandRangeExpansion
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn mech, LocalTargetInfo target, ref bool __result)
        {
            if (__result || mech == null || mech.Map == null)
                return;
            if (!mech.RaceProps.IsMechanoid || mech.Faction == null || !mech.Faction.IsPlayer)
                return;

            Map map = mech.Map;
            IntVec3 mechPos = mech.Position;

            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == mech || p.Faction != mech.Faction || !p.RaceProps.IsMechanoid)
                    continue;
                var comp = p.TryGetComp<CompCommandRangeExpansion>();
                if (comp == null || comp.Radius <= 0f)
                    continue;
                float r = comp.Radius;
                float rSq = r * r;

                // 机械体当前在扩展范围内 → 视为在指挥范围内
                if ((p.Position - mechPos).LengthHorizontalSquared <= rSq)
                {
                    __result = true;
                    return;
                }

                // 目标点在扩展范围内 → 视为合法指挥目标（与原版按 target 判断的方式一致）
                if (target.IsValid && target.Cell.InBounds(map) && (p.Position - target.Cell).LengthHorizontalSquared <= rSq)
                {
                    __result = true;
                    return;
                }
            }
        }
    }
}
