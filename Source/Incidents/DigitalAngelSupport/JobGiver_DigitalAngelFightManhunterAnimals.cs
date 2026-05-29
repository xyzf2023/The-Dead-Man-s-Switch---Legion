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

        private Thing? FindNearestManhunterAnimal(Pawn pawn, Map map)
        {
            Thing? best = null;
            float bestDistSq = float.MaxValue;
            IntVec3 pos = pawn.Position;

            LordJob_DigitalAngelAssistColony.ForEachValidManhunterAnimal(map, candidate =>
            {
                if (!IsUsableManhunterTargetFor(pawn, candidate, map))
                    return;

                float distSq = (candidate.Position - pos).LengthHorizontalSquared;
                if (distSq >= bestDistSq)
                    return;

                bestDistSq = distSq;
                best = candidate;
            });

            return best;
        }

        private bool IsUsableManhunterTargetFor(Pawn pawn, Pawn candidate, Map map)
        {
            if (pawn == null || candidate == null || map == null)
                return false;

            if (!LordJob_DigitalAngelAssistColony.IsValidManhunterAnimal(candidate))
                return false;

            if (!candidate.Position.IsValid || !candidate.Position.InBounds(map))
                return false;

            Verb verb = pawn.TryGetAttackVerb(candidate, !pawn.IsColonist, allowTurrets);
            if (verb == null || verb.verbProps == null)
                return false;

            if (verb.verbProps.IsMeleeAttack)
                return pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly);

            if (verb.CanHitTarget(candidate))
                return true;

            return pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly);
        }
    }
}
