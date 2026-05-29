// ============================================================================
// 文件：JobGiver_DigitalAngelFightManhunterAnimals.cs
// 说明：电子天使/未知机兵援军专用；在无普通活跃敌人时攻击非玩家猎杀人类动物。
//       远程单位使用 AttackStatic / Goto，避免 Wait_Combat 的原版自动选敌逻辑。
// ============================================================================

using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.DigitalAngelSupport
{
    public class JobGiver_DigitalAngelFightManhunterAnimals : JobGiver_AIFightEnemies
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
                return null!;

            Lord? lord = pawn.GetLord();
            if (lord?.LordJob is not LordJob_DigitalAngelAssistColony)
                return null!;

            Map map = pawn.Map;
            if (LordJob_DigitalAngelAssistColony.HasNormalActiveThreat(map))
                return null!;

            Thing? target = FindNearestManhunterAnimal(pawn, map);
            if (target == null)
                return null!;

            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman;
            Verb verb = pawn.TryGetAttackVerb(target, allowManualCastWeapons, allowTurrets);
            if (verb == null || verb.verbProps == null)
                return null!;

            Job? job = null;

            if (verb.verbProps.IsMeleeAttack)
            {
                job = MeleeAttackJob(pawn, target);
            }
            else if (verb.CanHitTarget(target))
            {
                job = MakeRangedAttackStaticJob(target);
            }
            else
            {
                bool foundDest;
                IntVec3 dest = IntVec3.Invalid;
                Thing? previousTarget = pawn.mindState.enemyTarget;

                pawn.mindState.enemyTarget = target;
                try
                {
                    foundDest = TryFindShootingPosition(pawn, out dest, verb);
                }
                finally
                {
                    pawn.mindState.enemyTarget = previousTarget;
                }

                if (!foundDest)
                    return null!;

                if (dest == pawn.Position)
                {
                    if (!verb.CanHitTarget(target))
                        return null!;

                    job = MakeRangedAttackStaticJob(target);
                }
                else
                {
                    job = JobMaker.MakeJob(JobDefOf.Goto, dest);
                    job.expiryInterval = ExpiryInterval_ShooterSucceeded.RandomInRange;
                    job.checkOverrideOnExpire = true;
                }
            }

            if (job == null)
                return null!;

            pawn.mindState.enemyTarget = target;
            pawn.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
            lord.Notify_PawnAcquiredTarget(pawn, target);

            return job;
        }

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

            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman;
            Verb verb = pawn.TryGetAttackVerb(candidate, allowManualCastWeapons, allowTurrets);
            if (verb == null || verb.verbProps == null)
                return false;

            if (verb.verbProps.IsMeleeAttack)
                return pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly);

            if (verb.CanHitTarget(candidate))
                return true;

            Thing? previousTarget = pawn.mindState.enemyTarget;
            pawn.mindState.enemyTarget = candidate;
            try
            {
                return TryFindShootingPosition(pawn, out _, verb);
            }
            finally
            {
                pawn.mindState.enemyTarget = previousTarget;
            }
        }

        private static Job MakeRangedAttackStaticJob(Thing target)
        {
            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
            job.expiryInterval = Rand.RangeInclusive(450, 550);
            job.checkOverrideOnExpire = true;
            job.endIfCantShootTargetFromCurPos = true;
            return job;
        }
    }
}
