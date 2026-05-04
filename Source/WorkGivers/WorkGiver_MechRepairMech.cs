using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 机械体修理机械体的工作给予者
    /// 参考原版WorkGiver_RepairMech的实现
    /// 允许机械体自动修理开启自动修理选项的其他机械体
    /// </summary>
    public class WorkGiver_MechRepairMech : WorkGiver_Scanner
    {
        private const string DMSLRepairJobDefName = "DMSL_Job_MechRepairMech";

        /// <summary>
        /// 获取潜在工作目标请求（请求所有Pawn）
        /// </summary>
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForGroup(ThingRequestGroup.Pawn);
            }
        }

        /// <summary>
        /// 获取全局潜在工作目标（同一阵营的所有Pawn）
        /// </summary>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
        }

        /// <summary>
        /// 路径终点模式：需要接触目标
        /// </summary>
        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        /// <summary>
        /// 最大路径危险等级：允许致命危险
        /// </summary>
        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        /// <summary>
        /// 是否应该跳过此工作
        /// 工程师与指挥官均可执行修理机械体工作
        /// </summary>
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn == null || !pawn.RaceProps.IsMechanoid)
            {
                return true;
            }
            // 执行者正在被修理时，不允许分配修理工作，避免状态冲突。
            if (IsPawnBeingRepaired(pawn))
            {
                return true;
            }
            // 工程师或指挥官可执行
            if (pawn.def?.defName == "DMSL_Mech_Engineer")
            {
                return false;
            }
            if (CompMechCommanderMarker.PawnHasMarker(pawn))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检查是否有工作在此目标上
        /// 参考原版WorkGiver_RepairMech.HasJobOnThing的实现
        /// </summary>
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 执行者正在被修理时，禁止接取修理任务。
            if (IsPawnBeingRepaired(pawn))
            {
                return false;
            }

            // 检查生物科技DLC
            if (!ModLister.CheckBiotech("Repair mech"))
            {
                return false;
            }

            // 检查目标是否为Pawn
            if (!(t is Pawn targetPawn))
            {
                return false;
            }

            // 禁止自己修理自己（会导致逻辑冲突）
            if (pawn == targetPawn)
            {
                return false;
            }

            // 检查目标是否有CompMechRepairable组件
            CompMechRepairable compMechRepairable = t.TryGetComp<CompMechRepairable>();
            if (compMechRepairable == null)
            {
                return false;
            }

            // 检查目标是否为机械体
            if (!targetPawn.RaceProps.IsMechanoid)
            {
                return false;
            }

            // 检查目标是否处于敌对精神状态
            if (targetPawn.InAggroMentalState)
            {
                return false;
            }

            // 检查目标是否对执行者敌对
            if (targetPawn.HostileTo(pawn))
            {
                return false;
            }

            // 检查是否可以保留目标
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            // 检查目标是否正在燃烧
            if (targetPawn.IsBurning())
            {
                return false;
            }

            // 检查目标是否正在攻击
            if (targetPawn.IsAttacking())
            {
                return false;
            }

            // 检查目标是否有能量需求（机械体必须有能量系统）
            if (targetPawn.needs?.energy == null)
            {
                return false;
            }

            // 检查是否可以修理（使用MechRepairUtility.CanRepair）
            if (!MechRepairUtility.CanRepair(targetPawn))
            {
                return false;
            }

            // 如果是自动模式（非强制），需要检查autoRepair选项
            if (!forced && !compMechRepairable.autoRepair)
            {
                return false;
            }

            // 所有条件都满足
            return true;
        }

        /// <summary>
        /// 在此目标上创建工作
        /// 使用自定义的JobDef（DMSL_Job_MechRepairMech），使用JobDriver_MechRepairMech执行
        /// </summary>
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 使用自定义的JobDef，它使用JobDriver_MechRepairMech
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(DMSLRepairJobDefName);
            if (jobDef != null)
            {
                return JobMaker.MakeJob(jobDef, t);
            }
            
            // 如果找不到自定义JobDef，回退到原版（虽然不应该发生）
            return JobMaker.MakeJob(JobDefOf.RepairMech, t);
        }

        private static bool IsPawnBeingRepaired(Pawn pawn)
        {
            if (pawn == null || pawn.MapHeld == null)
            {
                return false;
            }

            ReservationManager reservationManager = pawn.MapHeld.reservationManager;
            if (reservationManager == null)
            {
                return false;
            }

            if (reservationManager.OnlyReservationsForJobDef(pawn, JobDefOf.RepairMech, requireAtLeastOne: true))
            {
                return true;
            }

            JobDef dmsRepairJobDef = DefDatabase<JobDef>.GetNamedSilentFail(DMSLRepairJobDefName);
            if (dmsRepairJobDef != null && reservationManager.OnlyReservationsForJobDef(pawn, dmsRepairJobDef, requireAtLeastOne: true))
            {
                return true;
            }

            return false;
        }
    }
}

