// ============================================================================
// 文件：HediffComp_WanderingEngineer.cs
// 说明：游荡机兵 HediffComp，处理 60000-120000 tick 游荡后离图并销毁
// ============================================================================

using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.EngineerArrival
{
    /// <summary>
    /// 游荡机兵 HediffComp：设置 exitMapAfterTick，超时后赋予离图 Job，离图时销毁
    /// </summary>
    public class HediffComp_WanderingEngineer : HediffComp
    {
        private const int WanderDurationMin = 60000;
        private const int WanderDurationMax = 120000;

        private bool exitJobGiven;

        public override void CompPostMake()
        {
            base.CompPostMake();
            if (Pawn?.mindState != null)
            {
                Pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Rand.Range(WanderDurationMin, WanderDurationMax);
            }
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (Pawn?.mindState != null && Pawn.mindState.exitMapAfterTick < 0)
            {
                Pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Rand.Range(WanderDurationMin, WanderDurationMax);
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || !Pawn.Spawned || exitJobGiven)
                return;

            // 已被玩家控制时终止游荡/离图流程
            if (Pawn.IsColonyMechPlayerControlled)
            {
                TerminateWandering();
                return;
            }

            if (Pawn.mindState.exitMapAfterTick >= 0 && Find.TickManager.TicksGame > Pawn.mindState.exitMapAfterTick)
            {
                GiveExitJob();
            }
        }

        private void GiveExitJob()
        {
            if (exitJobGiven || Pawn?.Map == null)
                return;

            // 赋予离图 Job 前再次检查是否已被控制
            if (Pawn.IsColonyMechPlayerControlled)
            {
                TerminateWandering();
                return;
            }

            exitJobGiven = true;

            Lord lord = Pawn.GetLord();
            lord?.Notify_PawnLost(Pawn, PawnLostCondition.ForcedToJoinOtherLord);

            LordMaker.MakeNewLord(
                Pawn.Faction ?? Faction.OfPlayer,
                new LordJob_ExitMapBest(LocomotionUrgency.Walk, canDig: false, canDefendSelf: false),
                Pawn.Map,
                Gen.YieldSingle(Pawn)
            );

            Pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>
        /// 机械体从未受控转为受控后终止游荡/离图流程，移除 Hediff 并退出 Lord
        /// </summary>
        private void TerminateWandering()
        {
            if (Pawn == null)
                return;

            Lord lord = Pawn.GetLord();
            if (lord != null && (lord.LordJob is LordJob_WanderMapEdge || lord.LordJob is LordJob_ExitMapBest))
            {
                lord.Notify_PawnLost(Pawn, PawnLostCondition.ForcedToJoinOtherLord);
            }

            Pawn.health.RemoveHediff(parent);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            // 离图时 Pawn.DeSpawn 会移除 Hediff，此处不做销毁（由 Patch 处理）
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref exitJobGiven, "exitJobGiven", false);
        }
    }
}
