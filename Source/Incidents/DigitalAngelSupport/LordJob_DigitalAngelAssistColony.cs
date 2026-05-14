// ============================================================================
// 文件：LordJob_DigitalAngelAssistColony.cs
// 说明：继承原版 LordJob_AssistColony，在未交战时低频扫描并将
//       「对殖民地活跃威胁」与「非玩家猎杀人类动物」视为同级，
//       按距支援队伍中心的水平距离优先分配攻击任务（可达性由 CanReach 校验）。
// ============================================================================

using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.DigitalAngelSupport
{
    public class LordJob_DigitalAngelAssistColony : LordJob_AssistColony
    {
        private const int ThreatScanIntervalTicks = 300;
        private int lastThreatScanTick = -999999;

        public LordJob_DigitalAngelAssistColony()
        {
        }

        public LordJob_DigitalAngelAssistColony(Faction faction, IntVec3 exitSpot)
            : base(faction, exitSpot)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastThreatScanTick, "lastThreatScanTick", -999999);
        }

        public override void LordJobTick()
        {
            base.LordJobTick();

            if (lord == null || lord.Map == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now - lastThreatScanTick < ThreatScanIntervalTicks)
                return;

            lastThreatScanTick = now;

            if (SupportPawnsAreInCombat())
                return;

            TryAssignNearestThreatTarget();
        }

        private bool SupportPawnsAreInCombat()
        {
            if (lord == null || lord.ownedPawns == null)
                return false;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned)
                    continue;

                Job job = pawn.CurJob;
                if (job == null)
                    continue;

                if (job.def == JobDefOf.AttackStatic ||
                    job.def == JobDefOf.AttackMelee ||
                    job.ability != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryAssignNearestThreatTarget()
        {
            if (lord == null || lord.Map == null)
                return;

            Map map = lord.Map;

            if (!TryGetSupportPawnsCenter(out IntVec3 center))
                return;

            List<Thing> candidates = BuildThreatCandidates(map);
            if (candidates == null || candidates.Count == 0)
                return;

            Thing? target = TryFindNearestCandidate(candidates, center, map);
            if (target == null || !IsValidCandidateThing(target, map))
                return;

            TryAssignAttackJobToAvailablePawns(target);
        }

        private bool TryGetSupportPawnsCenter(out IntVec3 center)
        {
            center = IntVec3.Invalid;

            if (lord == null || lord.Map == null || lord.ownedPawns == null)
                return false;

            int sumX = 0;
            int sumZ = 0;
            int count = 0;
            Map map = lord.Map;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.Map != map)
                    continue;

                sumX += pawn.Position.x;
                sumZ += pawn.Position.z;
                count++;
            }

            if (count <= 0)
                return false;

            center = new IntVec3(sumX / count, 0, sumZ / count);
            return center.IsValid && center.InBounds(map);
        }

        private List<Thing> BuildThreatCandidates(Map map)
        {
            var candidates = new List<Thing>();
            var added = new HashSet<Thing>();

            HashSet<IAttackTarget>? hostileTargets = map.attackTargetsCache?.TargetsHostileToColony;
            if (hostileTargets != null)
            {
                foreach (IAttackTarget t in hostileTargets)
                {
                    if (t?.Thing == null)
                        continue;

                    Thing thing = t.Thing;
                    if (!thing.Spawned || thing.Destroyed)
                        continue;

                    if (!GenHostility.IsActiveThreatToPlayer(t))
                        continue;

                    if (thing is Pawn pawn)
                    {
                        if (pawn.Dead || pawn.Downed)
                            continue;
                    }

                    if (added.Add(thing))
                        candidates.Add(thing);
                }
            }

            IReadOnlyList<Pawn>? spawned = map.mapPawns?.AllPawnsSpawned;
            if (spawned != null)
            {
                for (int i = 0; i < spawned.Count; i++)
                {
                    Pawn pawn = spawned[i];
                    if (!IsValidManhunterAnimal(pawn))
                        continue;

                    if (added.Add(pawn))
                        candidates.Add(pawn);
                }
            }

            return candidates;
        }

        private static bool IsValidManhunterAnimal(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed)
                return false;

            if (pawn.RaceProps == null || !pawn.RaceProps.Animal)
                return false;

            if (!pawn.InMentalState || pawn.MentalStateDef == null)
                return false;

            if (pawn.Faction == Faction.OfPlayer)
                return false;

            string defName = pawn.MentalStateDef.defName;
            if (string.IsNullOrEmpty(defName))
                return false;

            return defName == "Manhunter" ||
                   defName == "ManhunterPermanent" ||
                   defName.Contains("Manhunter");
        }

        private static Thing? TryFindNearestCandidate(List<Thing> candidates, IntVec3 center, Map map)
        {
            Thing? best = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Thing thing = candidates[i];
                if (!IsValidCandidateThing(thing, map))
                    continue;

                float distSq = (thing.Position - center).LengthHorizontalSquared;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = thing;
                }
            }

            return best;
        }

        private static bool IsValidCandidateThing(Thing? thing, Map map)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != map)
                return false;

            if (thing is Pawn pawn)
            {
                if (pawn.Dead || pawn.Downed)
                    return false;
            }

            return thing.Position.IsValid && thing.Position.InBounds(map);
        }

        private void TryAssignAttackJobToAvailablePawns(Thing target)
        {
            if (lord == null || lord.Map == null || target == null || lord.ownedPawns == null)
                return;

            Map map = lord.Map;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (!IsAvailableForThreatJob(pawn))
                    continue;

                if (pawn.Map != map)
                    continue;

                if (!pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly))
                    continue;

                Job? job = MakeAttackJob(pawn, target);
                if (job == null)
                    continue;

                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }

        private static Job? MakeAttackJob(Pawn pawn, Thing target)
        {
            Verb verb = pawn.TryGetAttackVerb(target, !pawn.IsColonist);
            if (verb == null || verb.verbProps == null)
                return null;

            JobDef jobDef = verb.verbProps.IsMeleeAttack
                ? JobDefOf.AttackMelee
                : JobDefOf.AttackStatic;

            Job job = JobMaker.MakeJob(jobDef, target);
            job.expiryInterval = 600;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static bool IsAvailableForThreatJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.jobs == null)
                return false;

            Job curJob = pawn.CurJob;
            if (curJob == null)
                return true;

            if (curJob.def == JobDefOf.AttackStatic ||
                curJob.def == JobDefOf.AttackMelee ||
                curJob.ability != null ||
                curJob.def == JobDefOf.TendPatient ||
                curJob.def == JobDefOf.Rescue)
            {
                return false;
            }

            return curJob.def == JobDefOf.Wait ||
                   curJob.def == JobDefOf.Wait_Wander ||
                   curJob.def == JobDefOf.Wait_MaintainPosture ||
                   curJob.def == JobDefOf.Goto ||
                   curJob.def == JobDefOf.GotoWander;
        }
    }
}
