using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// Lord：等待 -> 接收信号后 assault
    /// </summary>
    public class LordJob_PagerRaid : LordJob
    {
        public const string MemoCallDone = "AirSupportDone";
        public const string MemoCallFailed = "AirSupportFailed";

        private IntVec3 rallyPoint;

        public LordJob_PagerRaid()
        {
        }

        public LordJob_PagerRaid(IntVec3 rallyPoint)
        {
            this.rallyPoint = rallyPoint;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();

            // Toil A：等待/防御（其他敌人在这里等待）
            LordToil_DefendPoint waitToil = new LordToil_DefendPoint(rallyPoint);
            graph.AddToil(waitToil);

            // Toil B：等待空袭完成（Job 完成后进入此状态）
            LordToil_DefendPoint waitForAirstrikeToil = new LordToil_DefendPoint(rallyPoint);
            graph.AddToil(waitForAirstrikeToil);

            // Toil C：冲锋
            LordToil_AssaultColony assaultToil = new LordToil_AssaultColony(false);
            graph.AddToil(assaultToil);

            graph.StartingToil = waitToil;

            // 转换1：Job 失败时立即进入 assault（包括携带者死亡的情况）
            Transition failToAssault = new Transition(waitToil, assaultToil);
            failToAssault.AddTrigger(new Trigger_Memo(MemoCallFailed)); // Job 失败信号（来自JobDriver）
            failToAssault.AddTrigger(new Trigger_PagerCarrierFailed()); // 检测携带者死亡或Job失败
            failToAssault.AddPreAction(new TransitionAction_Message("DMSL_PagerRaid_MessageCallFailed".Translate()));
            graph.AddTransition(failToAssault);

            // 转换2：Job 完成，进入等待空袭状态
            Transition jobDoneToWait = new Transition(waitToil, waitForAirstrikeToil);
            jobDoneToWait.AddTrigger(new Trigger_Memo(MemoCallDone)); // Job 完成信号
            graph.AddTransition(jobDoneToWait);

            // 转换3：空袭完成时进入 assault
            Transition completeToAssault = new Transition(waitForAirstrikeToil, assaultToil);
            completeToAssault.AddTrigger(new Trigger_AerialRaidCompleted()); // 空袭完成信号
            completeToAssault.AddPreAction(new TransitionAction_Message("DMSL_PagerRaid_MessageRaidComplete".Translate()));
            graph.AddTransition(completeToAssault);

            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref rallyPoint, "rallyPoint");
        }
    }
}
