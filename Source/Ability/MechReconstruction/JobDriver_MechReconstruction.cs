using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 机体重构工作驱动：处理寻路到目标附近并执行复活
    /// 参考原版JobDriver_CastAbilityGoTo的实现方式
    /// </summary>
    public class JobDriver_MechReconstruction : JobDriver
    {
        private const float TargetDistance = 2.9f; // 目标距离：2.9格
        
        // 用于存储读条时的红光效果器（需要在类级别存储，以便在不同Toil之间共享）
        private Effecter? warmupEffecter = null;

        /// <summary>
        /// 获取工作报告文本
        /// 参考原版JobDriver_CastVerbOnce的实现方式
        /// </summary>
        public override string GetReport()
        {
            if (this.job.targetA.HasThing)
            {
                return "DMSL_MechReconstruction_ReportWithTarget".Translate(this.job.targetA.Thing.LabelCap);
            }
            return "DMSL_MechReconstruction_ReportDefault".Translate();
        }

        /// <summary>
        /// 尝试进行预工作预留（确保目标可以被访问）
        /// </summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.targetA, this.job, 1, -1, null, errorOnFailed);
        }

        /// <summary>
        /// 工作流程：移动到目标附近，然后执行复活
        /// </summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 参考原版JobDriver_CastAbility的实现：在Job完成时启动能力冷却
            // 只有在Job成功完成时才启动冷却，如果Job失败（如目标无效），不应该启动冷却
            this.AddFinishAction(delegate(JobCondition condition)
            {
                // 只有在Job成功完成时才启动冷却
                // JobCondition.Succeeded 表示Job成功完成
                if (condition == JobCondition.Succeeded && this.job.ability != null)
                {
                    this.job.ability.StartCooldown(this.job.ability.def.cooldownTicksRange.RandomInRange);
                }
            });
            
            // 1. 移动到目标附近（使用Touch模式，会自动停在可到达的最近位置）
            Toil gotoToil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedOrNull(TargetIndex.A)
                .FailOn(() => !this.IsValidTarget());
            yield return gotoToil;

            // 3. 读条阶段（可自定义时间）
            // 获取读条时间（从组件属性中读取，转换为ticks：1秒 = 60 ticks）
            int castDurationTicks = 900; // 默认15秒 = 900 ticks
            Ability? ability = this.job.ability;
            if (ability != null)
            {
                foreach (CompAbilityEffect comp in ability.comps)
                {
                    if (comp is CompAbilityEffect_MechReconstruction mechReconstruction)
                    {
                        // castTime是秒数，转换为ticks：1秒 = 60 ticks
                        castDurationTicks = (int)(mechReconstruction.Props.castTime * 60f);
                        break;
                    }
                }
            }

            // 使用Toils_General.Wait创建等待Toil（参数是ticks，不是秒）
            // 第二个参数TargetIndex.A表示在读条时面向目标A
            // 参考原版Toils_General.Wait的实现：如果face != TargetIndex.None，会在tickIntervalAction中调用rotationTracker.FaceTarget
            Toil castToil = Toils_General.Wait(castDurationTicks, TargetIndex.A);
            
            castToil.initAction = () =>
            {
                // 再次验证目标（防止状态变化）
                if (!this.IsValidTarget())
                {
                    Messages.Message("DMSL_MechReconstruction_FailedMessage".Translate(), this.pawn, MessageTypeDefOf.RejectInput, false);
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // 在读条开始时，给尸体创建并附加红光特效
                // 参考憎恶毒蜂的实现：使用Effecter在尸体上显示持续闪烁的红光
                if (this.job.targetA.HasThing && this.job.targetA.Thing is Corpse corpse && corpse.Spawned)
                {
                    // 使用自定义效果器定义
                    EffecterDef warmupEffecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail("DMSL_Effecter_MechReconstructionWarmupOnTarget");
                    if (warmupEffecterDef != null)
                    {
                        // 创建效果器并附加到尸体上
                        this.warmupEffecter = warmupEffecterDef.SpawnAttached(corpse, corpse.MapHeld, 1f);
                        // 立即触发一次，生成Mote（Mote_MechResurrectWarmupOnTarget会自动持续显示）
                        this.warmupEffecter.Trigger(corpse, corpse, -1);
                    }
                }
            };
            
            castToil.tickAction = () =>
            {
                // 在读条过程中持续验证目标
                if (!this.IsValidTarget())
                {
                    Messages.Message("DMSL_MechReconstruction_FailedMessage".Translate(), this.pawn, MessageTypeDefOf.RejectInput, false);
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // 在读条过程中持续更新效果器，使红光持续闪烁
                // 注意：Mote_MechResurrectWarmupOnTarget使用needsMaintenance=true，需要持续维护
                if (this.warmupEffecter != null && this.job.targetA.HasThing && this.job.targetA.Thing is Corpse corpse && corpse.Spawned)
                {
                    // 持续调用EffectTick来维护Mote的显示
                    this.warmupEffecter.EffectTick(corpse, corpse);
                }
            };
            
            castToil.FailOnDespawnedOrNull(TargetIndex.A);
            castToil.FailOn(() => !this.IsValidTarget());
            castToil.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            // 添加进度条显示
            castToil.WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return castToil;

            // 4. 执行复活（在JobDriver中直接执行，不调用comp.Apply）
            Toil applyToil = new Toil();
            applyToil.initAction = () =>
            {
                // 读条完成时清理红光特效
                if (this.warmupEffecter != null)
                {
                    this.warmupEffecter.Cleanup();
                    this.warmupEffecter = null;
                }
                
                // 最终验证目标
                if (!this.job.targetA.HasThing || !(this.job.targetA.Thing is Corpse corpse))
                {
                    Messages.Message("DMSL_MechReconstruction_FailedMessage".Translate(), this.pawn, MessageTypeDefOf.RejectInput, false);
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // 获取能力组件并验证
                CompAbilityEffect_MechReconstruction? mechReconstruction = null;
                Ability? ability = this.job.ability;
                if (ability != null)
                {
                    foreach (CompAbilityEffect comp in ability.comps)
                    {
                        if (comp is CompAbilityEffect_MechReconstruction mechRecon)
                        {
                            mechReconstruction = mechRecon;
                            break;
                        }
                    }
                }

                if (mechReconstruction == null)
                {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // 再次验证目标
                if (!mechReconstruction.CanReconstruct(corpse))
                {
                    Messages.Message(mechReconstruction.Props.failedMessage.Translate().Formatted(corpse.Label), 
                        this.pawn, MessageTypeDefOf.RejectInput, false);
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                Pawn innerPawn = corpse.InnerPawn;
                if (innerPawn == null)
                {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // 执行复活
                // 使用ResurrectionUtility.TryResurrect，参考原版机械体复活能力
                bool resurrected = ResurrectionUtility.TryResurrect(innerPawn, null);
                
                if (resurrected && innerPawn.Spawned)
                {
                    // 参考原版Bill_ResurrectMech.CreateProducts的实现
                    // 1. 重置能量水平为50%（参考原版第75行）
                    if (innerPawn.needs?.energy != null)
                    {
                        innerPawn.needs.energy.CurLevel = innerPawn.needs.energy.MaxLevel * 0.5f;
                    }
                    
                    // 2. 移除所有伤势（参考原版第76行）
                    // 这会让被复活的机械体像在机械培育器中复活那样重置伤势
                    innerPawn.health.RemoveAllHediffs();
                    
                    // 3. 恢复机械体武器（参考原版ResurrectionUtility.TryResurrect第60-63行）
                    // 虽然TryResurrect内部已经会生成武器，但为了确保万无一失，我们再次检查并生成
                    if (innerPawn.RaceProps.IsMechanoid && MechRepairUtility.IsMissingWeapon(innerPawn))
                    {
                        MechRepairUtility.GenerateWeapon(innerPawn);
                    }
                    
                    // 应用视觉效果（红光特效和闪光）
                    // 参考原版CompAbilityEffect_ResurrectMech的实现
                    // 注意：必须在复活后应用，因为innerPawn在复活前可能没有MapHeld
                    if (mechReconstruction.Props.appliedEffecterDef != null)
                    {
                        Effecter effecter = mechReconstruction.Props.appliedEffecterDef.SpawnAttached(
                            innerPawn, innerPawn.MapHeld, 1f);
                        effecter.Trigger(innerPawn, innerPawn, -1);
                        effecter.Cleanup();
                    }

                    // 添加眩晕效果（复活后短暂眩晕，参考原版实现）
                    innerPawn.stances.stagger.StaggerFor(60, 0.17f);
                    
                    // ===== 监管者分配逻辑 =====
                    // 1. 移除被复活机械体的旧监管者关系
                    Pawn oldOverseer = innerPawn.GetOverseer();
                    if (oldOverseer != null)
                    {
                        oldOverseer.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, innerPawn);
                    }
                    
                    // 2. 确认被复活机械体属于玩家阵营
                    if (innerPawn.Faction != Faction.OfPlayer)
                    {
                        innerPawn.SetFaction(Faction.OfPlayer, null);
                    }
                    
                    // 3. 获取释放机体重构技能的机械体的监管者（注意：不是被复活的机械体的监管者）
                    // 使用扩展方法 GetOverseer() 获取施法者的监管者
                    Pawn casterOverseer = this.pawn.GetOverseer();
                    
                    // 4. 如果有监管者，输出到下一逻辑并添加新的监管者关系
                    if (casterOverseer != null && MechanitorUtility.IsMechanitor(casterOverseer))
                    {
                        // 为被复活的机械体添加新的监管者关系
                        // 新的监管者为上一步中输出的角色（casterOverseer）
                        casterOverseer.relations.AddDirectRelation(PawnRelationDefOf.Overseer, innerPawn);
                    }
                    // ===== 结束：监管者分配逻辑 =====
                }
            };
            applyToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return applyToil;
        }

        /// <summary>
        /// 检查目标是否仍然有效
        /// </summary>
        private bool IsValidTarget()
        {
            if (!this.job.targetA.HasThing || !(this.job.targetA.Thing is Corpse corpse))
            {
                return false;
            }

            // 获取能力组件并验证
            Ability ability = this.job.ability;
            if (ability != null)
            {
                foreach (CompAbilityEffect comp in ability.comps)
                {
                    if (comp is CompAbilityEffect_MechReconstruction mechReconstruction)
                    {
                        return mechReconstruction.CanReconstruct(corpse);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 查找目标附近的可站立位置
        /// </summary>
        private IntVec3 FindStandableCellNearTarget()
        {
            Map map = this.pawn.Map;
            IntVec3 targetPos = this.job.targetA.Cell;
            
            // 在目标周围寻找可站立的位置
            for (int radius = 1; radius <= 3; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(targetPos, radius, true))
                {
                    if (cell.InBounds(map) && 
                        cell.Standable(map) && 
                        this.pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly) &&
                        cell.DistanceTo(targetPos) <= TargetDistance)
                    {
                        return cell;
                    }
                }
            }

            return IntVec3.Invalid;
        }
    }
}

