using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 高压电击自定义 Verb：不读条，改为走到目标旁执行一次“接触攻击”，在接触结束时应用效果。
    /// </summary>
    public class Verb_HighVoltageShockStrike : Verb_CastAbility
    {
        protected override bool TryCastShot()
        {
            Pawn caster = CasterPawn;
            if (caster == null || !currentTarget.IsValid)
            {
                return false;
            }

            Job job = JobMaker.MakeJob(DMSL_JobDefOf.DMSL_Job_HighVoltageShockStrike, currentTarget);
            job.verbToUse = this;
            job.playerForced = true;
            caster.jobs?.StartJob(job, JobCondition.InterruptForced, null, cancelBusyStances: true);
            return true;
        }
    }

    /// <summary>
    /// 高压电击“接触一击”Job：走到目标旁，短时 toil（模拟近战一击时长），结束时扣能量并添加电击昏迷。
    /// </summary>
    public class JobDriver_HighVoltageShockStrike : JobDriver
    {
        /// <summary>接触动作时长（ticks），约 0.75 秒，可后续在 Def 或扩展中配置。</summary>
        private const int StrikeDurationTicks = 45;

        private Pawn TargetPawn => job.GetTarget(TargetIndex.A).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetPawn, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 使用 FailOnDespawnedOrNull 避免 GetTarget(A).Thing 为 null 时在 end condition 中抛出 NullReferenceException（如能量恰为 50% 时的边界情况）
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOn(() => pawn.Downed);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil strikeToil = Toils_General.Wait(StrikeDurationTicks, TargetIndex.A);
            strikeToil.FailOnDespawnedOrNull(TargetIndex.A);
            strikeToil.FailOnDowned(TargetIndex.A);
            strikeToil.FailOn(() => pawn.Downed);
            strikeToil.WithProgressBarToilDelay(TargetIndex.A, StrikeDurationTicks);
            strikeToil.AddFinishAction(ApplyShockOnStrikeEnd);
            yield return strikeToil;
        }

        private void ApplyShockOnStrikeEnd()
        {
            // 防御：目标或 job 可能已被清除（如目标消失、能量边界导致 job 被替换等）
            if (pawn?.jobs?.curJob != job)
            {
                return;
            }

            LocalTargetInfo targetInfo = job.GetTarget(TargetIndex.A);
            if (!targetInfo.IsValid || targetInfo.Thing is not Pawn target)
            {
                return;
            }

            Verb_CastAbility? verb = job.verbToUse as Verb_CastAbility;
            Ability? ability = verb?.ability;
            if (ability?.comps == null)
            {
                return;
            }

            CompAbilityEffect_HighVoltageShock? effect = ability.comps.OfType<CompAbilityEffect_HighVoltageShock>().FirstOrDefault();
            if (effect != null)
            {
                effect.Apply(new LocalTargetInfo(target), LocalTargetInfo.Invalid);
            }

            ability.StartCooldown(ability.def.cooldownTicksRange.RandomInRange);
        }
    }
}
