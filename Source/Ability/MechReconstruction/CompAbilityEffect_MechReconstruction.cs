using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 机体重构能力效果：添加友方机械体尸体过滤器，并实现复活流程
    /// 参考原版CompAbilityEffect_ResurrectMech的实现方式
    /// 注意：基础目标选择（距离、目标类型等）由XML的targetParams和基类处理
    /// 我们只需要添加业务逻辑过滤器（友方机械体尸体）和实现复活流程
    /// </summary>
    public class CompAbilityEffect_MechReconstruction : CompAbilityEffect
    {
        /// <summary>
        /// 获取组件属性（类型安全的访问）
        /// </summary>
        public new CompProperties_AbilityMechReconstruction Props
        {
            get
            {
                return (CompProperties_AbilityMechReconstruction)this.props;
            }
        }

        /// <summary>
        /// 检查目标是否可以应用能力（控制高亮显示）
        /// 参考原版CompAbilityEffect_ResurrectMech的实现
        /// 基类已经处理了距离、目标类型等基础检查，这里只需要添加业务逻辑过滤器
        /// </summary>
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 调用基类验证（检查距离、目标类型等，基于XML的targetParams）
            if (!base.CanApplyOn(target, dest))
            {
                return false;
            }

            // 添加业务逻辑过滤器：检查是否为友方机械体尸体
            if (target.HasThing && target.Thing is Corpse corpse)
            {
                return this.CanReconstruct(corpse);
            }

            return false;
        }

        /// <summary>
        /// 检查全局目标是否可以应用能力（用于世界地图等）
        /// </summary>
        public override bool CanApplyOn(GlobalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Corpse corpse)
            {
                return this.CanReconstruct(corpse);
            }

            return false;
        }

        /// <summary>
        /// 验证目标是否合法（点击时调用）
        /// 只有通过完整验证的目标才会创建工作
        /// </summary>
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            // 先调用基类验证（检查canTargetBaby、canTargetBosses等）
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            // 添加业务逻辑验证：检查是否为友方机械体尸体
            if (!target.HasThing || !(target.Thing is Corpse corpse))
            {
                if (throwMessages)
                {
                    Messages.Message(this.Props.failedMessage.Translate(), this.parent.pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (!this.CanReconstruct(corpse))
            {
                if (throwMessages)
                {
                    Messages.Message(this.Props.failedMessage.Translate().Formatted(target.Thing.Label), this.parent.pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// 重构验证（核心验证逻辑：是否为友方机械体尸体）
        /// 参考原版CompAbilityEffect_ResurrectMech的CanResurrect方法
        /// 注意：此方法需要是public，以便JobDriver可以访问
        /// </summary>
        public bool CanReconstruct(Corpse corpse)
        {
            // 1. 检查尸体内的Pawn是否存在
            Pawn innerPawn = corpse.InnerPawn;
            if (innerPawn == null)
            {
                return false;
            }

            // 2. 检查是否为机械体
            if (!innerPawn.RaceProps.IsMechanoid)
            {
                return false;
            }

            // 3. 获取施法者
            Pawn caster = this.parent.pawn;
            if (caster == null)
            {
                return false;
            }

            // 4. 检查是否为友方
            if (innerPawn.Faction == null || innerPawn.Faction != caster.Faction)
            {
                return false;
            }

            // 所有条件都满足
            return true;
        }

        /// <summary>
        /// 应用能力效果（执行重构/复活）
        /// 注意：复活流程已转移到JobDriver中执行，此方法保留为空实现
        /// 如果将来需要使用其他工作定义（如CastAbilityOnThing），可以在这里实现
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            // 复活流程在JobDriver_MechReconstruction中执行
        }
    }
}

