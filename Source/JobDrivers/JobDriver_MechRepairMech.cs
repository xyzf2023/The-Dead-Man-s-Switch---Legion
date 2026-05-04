using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 机械体修理机械体的工作驱动
    /// 参考原版JobDriver_RepairMech和[MAP]战术人形MOD的实现
    /// 允许机械体修理其他机械体（包括友方机械体）
    /// </summary>
    public class JobDriver_MechRepairMech : JobDriver_RepairMech
    {
        /// <summary>
        /// 获取每次治疗所需的tick数
        /// 机械体没有MechRepairSpeed属性时，使用统一基础修理速度 2.4
        /// 如果存在应急维修状态，修理速度会翻5倍
        /// 基础修理速度 = 2.4
        /// 应急维修模式：2.4 × 5.0 = 12.0
        /// TicksPerHeal = Mathf.RoundToInt(1f / 2.4 * 120f) = 50 ticks（基础）
        /// TicksPerHeal = Mathf.RoundToInt(1f / 12.0 * 120f) = 10 ticks（应急维修模式）
        /// </summary>
        protected new int TicksPerHeal
        {
            get
            {
                float baseSpeed;
                
                // 如果机械体有MechRepairSpeed属性，使用原版逻辑
                if (this.pawn.GetStatValue(StatDefOf.MechRepairSpeed, true, -1) > 0f)
                {
                    baseSpeed = this.pawn.GetStatValue(StatDefOf.MechRepairSpeed, true, -1);
                }
                else
                {
                    // 否则使用统一基础修理速度（原工程师加成后的速度）
                    const float defaultMechRepairSpeed = 2.4f;
                    baseSpeed = defaultMechRepairSpeed;
                }

                // 指挥官修理时速度为基础速度的 2.5 倍
                if (CompMechCommanderMarker.PawnHasMarker(this.pawn))
                {
                    baseSpeed *= 2.5f;
                }
                
                // 从缓存读取修理速度修正（应急维修模式等）
                // 使用推送模式，避免扫描 Hediff
                float boostMultiplier = RepairSpeedBoostCache.GetSpeedMultiplier(this.pawn);
                baseSpeed *= boostMultiplier;
                
                return Mathf.RoundToInt(1f / baseSpeed * 120f);
            }
        }

        /// <summary>
        /// 尝试进行预工作预留（确保目标可以被访问）
        /// 重写以允许机械体执行修理工作
        /// </summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 检查执行者是否为机械体
            if (this.pawn == null || !this.pawn.RaceProps.IsMechanoid)
            {
                // 不是机械体，使用原版逻辑
                return base.TryMakePreToilReservations(errorOnFailed);
            }

            // 对于机械体，尝试保留目标
            if (this.job != null && this.job.targetA.Thing is Pawn mech)
            {
                return this.pawn.Reserve(mech, this.job, 1, -1, null, errorOnFailed, false);
            }

            return false;
        }

        /// <summary>
        /// 工作流程：移动到目标附近，然后执行修理
        /// 重写以使用新的TicksPerHeal属性
        /// </summary>
        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            // 检查生物科技DLC
            if (!ModLister.CheckBiotech("Mech repair"))
            {
                yield break;
            }

            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            this.FailOn(() => this.Mech.IsAttacking());

            // 移动到目标附近
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false);

            // 创建修理Toil
            Toil repairToil = Toils_General.WaitWith(TargetIndex.A, int.MaxValue, false, true, true, TargetIndex.None, PathEndMode.Touch);
            
            // 添加修理特效
            repairToil.WithEffect(EffecterDefOf.MechRepairing, TargetIndex.A, null);
            
            // 播放修理音效
            repairToil.PlaySustainerOrSound(SoundDefOf.RepairMech_Touch, 1f);
            
            // 初始化修理计时器
            repairToil.AddPreInitAction(delegate
            {
                this.ticksToNextRepair = this.TicksPerHeal;
            });
            
            // 设置面向目标
            repairToil.handlingFacing = true;
            
            // 每tick执行修理逻辑
            repairToil.tickIntervalAction = delegate(int delta)
            {
                this.ticksToNextRepair -= delta;
                if (this.ticksToNextRepair <= 0)
                {
                    // 消耗机械体能量（每HP消耗的能量）
                    this.Mech.needs.energy.CurLevel -= this.Mech.GetStatValue(StatDefOf.MechEnergyLossPerHP, true, -1) * (float)delta;
                    
                    // 执行修理
                    MechRepairUtility.RepairTick(this.Mech, delta);
                    
                    // 重置计时器
                    this.ticksToNextRepair = this.TicksPerHeal;
                }
                
                // 面向目标
                this.pawn.rotationTracker.FaceTarget(this.Mech);
                
                // 学习Crafting技能（如果执行者有技能系统）
                if (this.pawn.skills != null)
                {
                    this.pawn.skills.Learn(SkillDefOf.Crafting, 0.05f * (float)delta, false, false);
                }
            };
            
            // 修理完成后的处理
            repairToil.AddFinishAction(delegate
            {
                // 如果被修理的机械体正在执行工作，强制结束
                Pawn_JobTracker jobs = this.Mech.jobs;
                if (((jobs != null) ? jobs.curJob : null) != null)
                {
                    this.Mech.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
                }
            });
            
            // 检查是否还需要修理
            repairToil.AddEndCondition(delegate
            {
                if (!MechRepairUtility.CanRepair(this.Mech))
                {
                    return JobCondition.Succeeded;
                }
                return JobCondition.Ongoing;
            });
            
            // 设置活跃技能（用于显示进度条）
            repairToil.activeSkill = () => SkillDefOf.Crafting;
            
            yield return repairToil;
        }
    }
}

