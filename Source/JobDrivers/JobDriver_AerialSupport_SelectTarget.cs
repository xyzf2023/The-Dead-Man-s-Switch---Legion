using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using DMS_Legion.GroundSupport;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援目标选择JobDriver
    /// </summary>
    public class JobDriver_AerialSupport_SelectTarget : JobDriver
    {
        private AerialSupportRenderer? AerialSupportRenderer => pawn?.Map?.GetComponent<AerialSupportRenderer>();
        private bool targetSelected = false; // 标记是否已成功选择目标

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 预留pawn，防止其他行为中断
            return pawn.Reserve(pawn, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Toil 1: 验证支援类型已选择
            yield return new Toil()
            {
                initAction = () =>
                {
                    var renderer = AerialSupportRenderer;
                    var selectedType = renderer?.GetSelectedSupportType();

                    if (selectedType == null)
                    {
                        // 没有选择支援类型，结束Job
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                }
            };

            // Toil 2: 开始目标选择
            yield return new Toil()
            {
                initAction = () =>
                {
                    // 使用Find.Targeter开始目标选择
                    var targetingParams = new TargetingParameters()
                    {
                        canTargetLocations = true,
                        canTargetSelf = false,
                        canTargetPawns = false,
                        canTargetBuildings = true,
                        canTargetAnimals = false,
                        canTargetHumans = false,
                        canTargetMechs = false,
                        canTargetItems = false,
                        validator = (target) => target.Cell.InBounds(pawn.Map) && target.Cell.Walkable(pawn.Map)
                    };

                    Find.Targeter.BeginTargeting(targetingParams,
                        (LocalTargetInfo target) => OnTargetSelected(target), // 成功选择
                        null, // highlightAction
                        (LocalTargetInfo target) => target.Cell.InBounds(pawn.Map) && target.Cell.Walkable(pawn.Map), // targetValidator
                        pawn, // caster
                        () => OnTargetCancelled(), // actionWhenFinished (cancel callback)
                        null, // mouseAttachment
                        true, // playSoundOnAction
                        null, // onGuiAction
                        null); // onUpdateAction
                },
                tickAction = () =>
                {
                    // 等待目标选择完成（由Find.Targeter回调处理）
                }
            };
        }

        private void OnTargetSelected(LocalTargetInfo target)
        {
            targetSelected = true; // 标记已成功选择目标

            // 执行支援
            ExecuteAerialSupport(target.Cell);

            // 结束Job
            EndJobWith(JobCondition.Succeeded);
        }

        private void OnTargetCancelled()
        {
            // 如果已经成功选择了目标，则不显示取消消息
            if (targetSelected)
            {
                return;
            }

            Log.Warning("[空中支援] 目标选择已取消 - 取消整个技能");

            // 清除选择的支援类型与皇权上下文（取消不消耗）
            var renderer = AerialSupportRenderer;
            renderer?.ClearRoyalPermitContext();
            renderer?.SetSelectedSupportType(null);

            // 显示取消消息
            Messages.Message("DMSL_AerialSupport_Cancelled".Translate(), MessageTypeDefOf.RejectInput);

            // 结束Job
            EndJobWith(JobCondition.Succeeded);
        }

        private void ExecuteAerialSupport(IntVec3 targetPos)
        {
            var renderer = AerialSupportRenderer;
            if (renderer == null) return;

            var selectedType = renderer.GetSelectedSupportType();
            if (selectedType == null) return;

            if (selectedType.flightPathType == "CustomLine")
            {
                // 对于CustomLine类型，需要玩家选择两个点，由专门 JobDriver 处理
                return;
            }

            // 通过协调器发起支援：起点按 startDirection/preferNorthEntry 计算，延迟按 renderDelayTicks/soundDelayTicks 生效
            var coordinator = AerialSupportCoordinator.Instance;
            if (coordinator != null)
            {
                coordinator.RequestSupportAt(targetPos, pawn.Map, selectedType, pawn);
            }
            else
            {
                // 降级：无协调器时按原逻辑随机起点、立即开始
                IntVec3 startPos = CellFinder.RandomEdgeCell(pawn.Map);
                renderer.StartFlightWithSelectedType(startPos, targetPos);
                if (selectedType.cooldownTicks > 0)
                {
                    var ability = pawn.abilities?.abilities.Find(ab => ab.def.defName == "DMSL_Ability_AerialSupport");
                    ability?.StartCooldown(selectedType.cooldownTicks);
                }
                Messages.Message("DMSL_AerialSupport_SupportCalled".Translate(selectedType.label, targetPos),
                    new TargetInfo(targetPos, pawn.Map), MessageTypeDefOf.PositiveEvent);
            }

            // 若本次来自皇权支援，成功召唤后消耗（进 CD、扣好感）
            renderer.ConsumeRoyalPermitIfSet();
            // 清除选择的支援类型
            renderer.SetSelectedSupportType(null);
        }
    }
}
