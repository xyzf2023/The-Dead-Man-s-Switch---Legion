using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using DMS_Legion.GroundSupport;

namespace DMS_Legion
{

    /// <summary>
    /// 精简后的空中支援目标选择JobDriver
    /// 仅作为pawn行为容器，实际逻辑由Coordinator处理
    /// </summary>
    public class JobDriver_AerialSupport_SelectCustomLine : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 预留pawn，防止其他行为中断
            return pawn.Reserve(pawn, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 单个Toil处理整个流程
            var aerialSupportToil = new Toil()
            {
                initAction = () =>
                {
                    // 获取Coordinator并启动选点流程
                    var coordinator = Current.Game.GetComponent<AerialSupportCoordinator>();

                    if (coordinator == null)
                    {
                        Log.Error("[DMS_Legion] JobDriver_AerialSupport_SelectCustomLine: AerialSupportCoordinator未找到");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    // Coordinator负责整个支援流程：选点、Flight创建、Renderer调用
                    // JobDriver仅作为pawn行为容器，提供回调入口
                    coordinator.StartTargetSelectionForJob(pawn, this);
                }
            };

            yield return aerialSupportToil;
        }

        /// <summary>
        /// 选点完成回调 - Coordinator调用此方法结束Job
        /// </summary>
        public void OnTargetSelectionCompleted()
        {
            // 选点成功完成，结束Job
            EndJobWith(JobCondition.Succeeded);
        }

        /// <summary>
        /// 选点取消回调 - Coordinator调用此方法结束Job
        /// </summary>
        public void OnTargetSelectionCancelled()
        {
            // 取消不消耗，清除皇权上下文与选中类型
            var renderer = pawn?.Map?.GetComponent<AerialSupportRenderer>();
            renderer?.ClearRoyalPermitContext();
            renderer?.SetSelectedSupportType(null);
            EndJobWith(JobCondition.Incompletable);
        }
    }
}