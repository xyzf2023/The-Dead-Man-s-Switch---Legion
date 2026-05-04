using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace DMS_Legion
{
    /// <summary>
    /// 维修指定能力效果：检查目标是否需要修理，如果需要则执行修理工作
    /// 参考WorkGiver_MechRepairMech.HasJobOnThing的判定逻辑
    /// </summary>
    public class CompAbilityEffect_RepairDesignate : CompAbilityEffect
    {
        /// <summary>
        /// 已发送消息的目标缓存（避免重复发送）
        /// 使用 Pawn 的 ID 作为键，每秒清空一次
        /// </summary>
        private static HashSet<int> notifiedTargets = new HashSet<int>();
        private static int lastTick = -1;
        /// <summary>
        /// 获取组件属性（类型安全的访问）
        /// </summary>
        public new CompProperties_AbilityRepairDesignate Props
        {
            get
            {
                return (CompProperties_AbilityRepairDesignate)this.props;
            }
        }

        /// <summary>
        /// 检查目标是否可以应用能力（控制高亮显示）
        /// 父类已经限制了只能对单位和机械体使用，这里检查是否为友方机械体且需要修理
        /// 如果目标不需要修理，会发送消息通知玩家
        /// </summary>
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 调用基类验证（检查距离、目标类型等，基于XML的targetParams）
            if (!base.CanApplyOn(target, dest))
            {
                return false;
            }

            // 父类已经限制了只能对单位和机械体使用，所以这里可以直接转换为Pawn
            if (!target.HasThing || !(target.Thing is Pawn targetPawn))
            {
                return false;
            }

            if (this.parent.pawn == null)
            {
                return false;
            }

            // 检查是否为友方机械体
            if (!IsFriendlyMechanoid(this.parent.pawn, targetPawn))
            {
                return false;
            }

            // 检查是否需要修理
            bool canRepair = MechRepairUtility.CanRepair(targetPawn);
            
            // 如果不需要修理，发送消息（避免重复发送）
            if (!canRepair)
            {
                // 每秒清空一次缓存（60 ticks = 1秒）
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastTick >= 60)
                {
                    notifiedTargets.Clear();
                    lastTick = currentTick;
                }

                // 如果还没有发送过消息，则发送
                int targetId = targetPawn.thingIDNumber;
                if (!notifiedTargets.Contains(targetId))
                {
                    notifiedTargets.Add(targetId);
                    Messages.Message(this.Props.failedMessage.Translate(), targetPawn, MessageTypeDefOf.RejectInput, false);
                }
            }

            // 只有需要修理时才返回true，允许使用能力
            return canRepair;
        }

        /// <summary>
        /// 应用能力效果：执行修理工作
        /// CanApplyOn已经检查了所有条件，这里只需要执行修理工作
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 父类已经限制了只能对单位和机械体使用，所以这里可以直接转换为Pawn
            if (!target.HasThing || !(target.Thing is Pawn targetPawn))
            {
                return;
            }

            Pawn caster = this.parent.pawn;
            if (caster == null)
            {
                return;
            }

            // CanApplyOn已经检查了所有条件，这里直接执行修理工作
            Job job = JobMaker.MakeJob(JobDefOf.RepairMech, targetPawn);
            caster.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
        }

        /// <summary>
        /// 检查目标是否可以修理（完整检查，用于CanApplyOn）
        /// 参考WorkGiver_MechRepairMech.HasJobOnThing的判定逻辑
        /// </summary>
        private bool CanRepairTarget(Pawn pawn, Pawn targetPawn)
        {
            // 空值检查
            if (pawn == null || targetPawn == null)
            {
                return false;
            }

            // 检查是否为友方机械体
            if (!IsFriendlyMechanoid(pawn, targetPawn))
            {
                return false;
            }

            // 检查是否可以修理（使用MechRepairUtility.CanRepair）
            if (!MechRepairUtility.CanRepair(targetPawn))
            {
                return false;
            }

            // 所有条件都满足，可以修理
            return true;
        }

        /// <summary>
        /// 检查目标是否为友方机械体（简化判断，父类已经限制了只能对单位和机械体使用）
        /// </summary>
        private bool IsFriendlyMechanoid(Pawn pawn, Pawn targetPawn)
        {
            // 空值检查
            if (pawn == null || targetPawn == null)
            {
                return false;
            }

            // 禁止自己修理自己（会导致逻辑冲突）
            if (pawn == targetPawn)
            {
                return false;
            }

            // 检查目标是否为机械体
            if (!targetPawn.RaceProps.IsMechanoid)
            {
                return false;
            }

            // 检查目标是否有CompMechRepairable组件
            if (targetPawn.TryGetComp<CompMechRepairable>() == null)
            {
                return false;
            }

            // 检查目标是否对执行者敌对（判断是否为友方）
            if (targetPawn.HostileTo(pawn))
            {
                return false;
            }

            // 检查是否可以保留目标（强制模式，忽略保留检查）
            if (!pawn.CanReserve(targetPawn, 1, -1, null, true))
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

            // 是友方机械体
            return true;
        }
    }
}

