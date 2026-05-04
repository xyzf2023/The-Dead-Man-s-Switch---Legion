// ============================================================================
// 文件：LordJob_ElectronicAngelDoctor.cs
// 说明：电子天使医护框架专用 LordJob：
//       - 优先对倒地殖民者施放医疗胶水
//       - 其次救援倒地殖民者到床上
//       - 再次为需要照料的殖民者执行 TendPatient
//       - 30000 tick 内无医疗相关工作后尝试离场
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.ElectronicAngel
{
    public class LordJob_ElectronicAngelDoctor : LordJob
    {
        private const string LogPrefix = "[DMSL_ElectronicAngelDoctor]";
        private const int NoWorkTimeoutTicks = 30000;

        private int lastMedicalWorkTick = -1;

        // 记录本次电子天使支援中，已用医疗胶水处理过流血的殖民者，避免对同一目标重复施放。
        private List<int> treatedByMedicalGlue = new List<int>();

        public LordJob_ElectronicAngelDoctor()
        {
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            var toil = new LordToil_ElectronicAngelDoctor();
            graph.AddToil(toil);
            graph.StartingToil = toil;
            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastMedicalWorkTick, "lastMedicalWorkTick", -1);
            Scribe_Collections.Look(ref treatedByMedicalGlue, "treatedByMedicalGlue", LookMode.Value);
            treatedByMedicalGlue ??= new List<int>();
        }

        public override void LordJobTick()
        {
            base.LordJobTick();

            if (lord == null || lord.ownedPawns.Count == 0)
                return;

            Pawn doctor = lord.ownedPawns[0];
            if (doctor == null || doctor.Dead || !doctor.Spawned)
                return;

            int now = Find.TickManager.TicksGame;

            // 记录正在执行的医疗相关工作
            if (IsMedicalJob(doctor.CurJob))
            {
                lastMedicalWorkTick = now;
                return;
            }

            // 长时间没有医疗工作则尝试离场
            if (lastMedicalWorkTick >= 0 && now - lastMedicalWorkTick >= NoWorkTimeoutTicks)
            {
                TryGiveExitJob(doctor);
                return;
            }

            Job? curJob = doctor.CurJob;

            // 空闲或仅在等待/闲逛/站立保持姿态/普通移动时尝试分配新医疗工作 / 闲逛
            if (curJob == null ||
                curJob.def == JobDefOf.Wait ||
                curJob.def == JobDefOf.Wait_Wander ||
                curJob.def == JobDefOf.Wait_MaintainPosture ||
                curJob.def == JobDefOf.Goto ||
                curJob.def == JobDefOf.GotoWander)
            {
                if (TryGiveMedicalGlueJob(doctor, now))
                    return;
                if (TryGiveTendJob(doctor, now))
                    return;
                TryGiveIdleWanderJob(doctor);
            }
        }

        private static bool IsMedicalJob(Job? job)
        {
            if (job == null)
                return false;
            if (job.ability != null)
                return true;
            return job.def == JobDefOf.Rescue || job.def == JobDefOf.TendPatient;
        }

        private static void TryGiveExitJob(Pawn doctor)
        {
            if (doctor.Dead || !doctor.Spawned)
                return;
            Map map = doctor.Map;
            if (map == null || !map.CanEverExit)
                return;

            // 参考原版 JobGiver_ExitMapBest：寻找最佳离开地点，然后下发带 exitMapOnArrival 的 Goto Job
            if (!RCellFinder.TryFindBestExitSpot(doctor, out IntVec3 dest, TraverseMode.ByPawn))
                return;

            Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
            job.exitMapOnArrival = true;
            job.locomotionUrgency = PawnUtility.ResolveLocomotion(doctor, LocomotionUrgency.Walk, LocomotionUrgency.Jog);
            doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private bool TryGiveMedicalGlueJob(Pawn doctor, int now)
        {
            if (doctor.abilities == null)
                return false;

            AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail("DMSL_Ability_MedicalGlue");
            if (abilityDef == null)
                return false;
            Ability ability = doctor.abilities.GetAbility(abilityDef);
            if (ability == null || !ability.CanQueueCast)
                return false;

            Map map = doctor.Map;
            if (map == null)
                return false;

            List<Pawn> candidates = map.mapPawns.FreeColonistsSpawned
                .Where(p =>
                    p != null &&
                    p.Downed &&
                    !p.Dead &&
                    p.Faction == Faction.OfPlayer &&
                    !treatedByMedicalGlue.Contains(p.thingIDNumber))
                .ToList();
            if (candidates.Count == 0)
                return false;

            Pawn? best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn p = candidates[i];
                if (!doctor.CanReach(p, PathEndMode.OnCell, Danger.Deadly))
                    continue;
                LocalTargetInfo target = p;
                if (!ability.CanApplyOn(target))
                    continue;
                float distSq = (p.Position - doctor.Position).LengthHorizontalSquared;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = p;
                }
            }

            if (best == null)
                return false;

            ability.QueueCastingJob(best, LocalTargetInfo.Invalid);
            // 标记该目标已使用过医疗胶水，本次支援不再对其重复施放
            treatedByMedicalGlue.Add(best.thingIDNumber);
            lastMedicalWorkTick = now;
            return true;
        }

        // 已移除将殖民者搬运至床上的逻辑，避免与床位/阵营等复杂条件产生额外冲突。

        private bool TryGiveTendJob(Pawn doctor, int now)
        {
            Map map = doctor.Map;
            if (map == null)
                return false;

            List<Pawn> candidates = map.mapPawns.FreeColonistsSpawned
                .Where(p => p != null && !p.Dead && p.Faction == Faction.OfPlayer)
                .ToList();
            if (candidates.Count == 0)
                return false;

            foreach (Pawn patient in candidates)
            {
                if (!HealthAIUtility.ShouldBeTendedNowByPlayer(patient))
                    continue;
                if (!doctor.CanReserve(patient, 1, -1, null, false))
                    continue;
                // 简化 GoodLayingStatusForTend：人形需在床上，非人形不站立
                if (patient.RaceProps.Humanlike && !patient.InBed())
                    continue;

                Thing medicine = HealthAIUtility.FindBestMedicine(doctor, patient);
                Job job;
                if (medicine != null && medicine.SpawnedParentOrMe != medicine)
                {
                    job = JobMaker.MakeJob(JobDefOf.TendPatient, patient, medicine, medicine.SpawnedParentOrMe);
                }
                else if (medicine != null)
                {
                    job = JobMaker.MakeJob(JobDefOf.TendPatient, patient, medicine);
                }
                else
                {
                    job = JobMaker.MakeJob(JobDefOf.TendPatient, patient);
                }

                if (!doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                    continue;

                lastMedicalWorkTick = now;
                return true;
            }

            return false;
        }

        private static void TryGiveIdleWanderJob(Pawn doctor)
        {
            if (doctor.Dead || !doctor.Spawned)
                return;
            Map map = doctor.Map;
            if (map == null)
                return;

            // 若已在闲逛类工作中则不重复下发
            Job? cur = doctor.CurJob;
            if (cur != null && (cur.def == JobDefOf.GotoWander || cur.def == JobDefOf.Wait_Wander))
                return;

            if (!RCellFinder.TryFindRandomCellNearWith(doctor.Position, (IntVec3 c) => c.Standable(map), map, out IntVec3 dest, 8))
                dest = doctor.Position;

            Job job = JobMaker.MakeJob(JobDefOf.GotoWander, dest);
            doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }

    /// <summary>
    /// 目前不使用额外数据，仅作为占位 Toil（所有逻辑在 LordJobTick 中驱动）。
    /// </summary>
    public class LordToil_ElectronicAngelDoctor : LordToil
    {
        public override void UpdateAllDuties()
        {
            // 为避免 ThinkNode_Duty 在没有 duty 时报错，为该 Lord 下的所有机体分配一个空闲 duty。
            if (lord == null || lord.ownedPawns == null)
                return;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn != null && pawn.mindState != null)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.Idle);
                }
            }
        }
    }
}

