using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 当面板移速大于 20 格/秒 时移动无视地形：用忽略地形/物体 pathCost 的移动成本替换原结果。
    /// 参考 [MAP]机械族指挥官 行为模式：机动作战 的实现。
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new[] { typeof(Pawn), typeof(IntVec3) })]
    public static class Patch_Pawn_PathFollower_CostToMoveIntoCell_Momentum
    {
        /// <summary>面板移速超过此值（格/秒）时移动无视地形。</summary>
        private const float TerrainIgnoreSpeedThreshold = 20f;

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, IntVec3 c, ref float __result)
        {
            if (pawn?.Map == null)
                return;

            if (pawn.GetComp<CompMoveSpeedMomentum>() == null)
                return;

            if (pawn.GetStatValue(StatDefOf.MoveSpeed) <= TerrainIgnoreSpeedThreshold)
                return;

            float? cost = ComputeCostIgnoringTerrain(pawn, c);
            if (cost.HasValue)
                __result = cost.Value;
        }

        private static float? ComputeCostIgnoringTerrain(Pawn pawn, IntVec3 c)
        {
            float num = (c.x == pawn.Position.x || c.z == pawn.Position.z)
                ? pawn.TicksPerMoveCardinal
                : pawn.TicksPerMoveDiagonal;

            int? baseCostOverride = Pawn_PathFollower.GetPawnCellBaseCostOverride(pawn, c);
            num += baseCostOverride ?? 0;

            Building edifice = c.GetEdifice(pawn.Map);
            if (edifice != null)
                num += edifice.PathWalkCostFor(pawn);

            if (num > 450f)
                num = 450f;

            if (pawn.CurJob != null)
            {
                Pawn locomotionUrgencySameAs = pawn.jobs.curDriver.locomotionUrgencySameAs;
                if (locomotionUrgencySameAs != null && locomotionUrgencySameAs != pawn && locomotionUrgencySameAs.Spawned)
                {
                    float num2 = InvokeCostToMoveIntoCell(locomotionUrgencySameAs, c);
                    if (num < num2)
                        num = num2;
                }
                else
                {
                    switch (pawn.jobs.curJob.locomotionUrgency)
                    {
                        case LocomotionUrgency.Amble:
                            num *= 3f;
                            if (num < 60f) num = 60f;
                            break;
                        case LocomotionUrgency.Walk:
                            num *= 2f;
                            if (num < 50f) num = 50f;
                            break;
                        case LocomotionUrgency.Jog:
                            break;
                        case LocomotionUrgency.Sprint:
                            num = Mathf.RoundToInt(num * 0.75f);
                            break;
                    }
                }
            }

            return Mathf.Max(num, 1f);
        }

        private static float InvokeCostToMoveIntoCell(Pawn pawn, IntVec3 c)
        {
            MethodInfo method = AccessTools.Method(typeof(Pawn_PathFollower), "CostToMoveIntoCell");
            return (float)method.Invoke(null, new object[] { pawn, c });
        }
    }
}
