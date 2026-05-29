// ============================================================================
// 文件：JobGiver_DigitalAngelFightManhunterAnimals.cs
// 说明：电子天使/未知机兵援军专用；在无普通活跃敌人时，将非玩家猎杀人类动物
//       作为攻击目标，并复用 JobGiver_AIFightEnemies 的原版战斗 Job 流程。
// ============================================================================

using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.DigitalAngelSupport
{
    public class JobGiver_DigitalAngelFightManhunterAnimals : JobGiver_AIFightEnemies
    {
        protected override Thing FindAttackTarget(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
                return null!;

            Lord? lord = pawn.GetLord();
            if (lord?.LordJob is not LordJob_DigitalAngelAssistColony)
                return null!;

            Map map = pawn.Map;
            if (LordJob_DigitalAngelAssistColony.HasNormalActiveThreat(map))
                return null!;

            return FindNearestManhunterAnimal(pawn, map)!;
        }

        private static Thing? FindNearestManhunterAnimal(Pawn pawn, Map map)
        {
            Thing? best = null;
            float bestDistSq = float.MaxValue;
            IntVec3 pos = pawn.Position;

            LordJob_DigitalAngelAssistColony.ForEachValidManhunterAnimal(map, candidate =>
            {
                if (!candidate.Position.IsValid || !candidate.Position.InBounds(map))
                    return;

                float distSq = (candidate.Position - pos).LengthHorizontalSquared;
                if (distSq >= bestDistSq)
                    return;

                bestDistSq = distSq;
                best = candidate;
            });

            return best;
        }
    }
}
