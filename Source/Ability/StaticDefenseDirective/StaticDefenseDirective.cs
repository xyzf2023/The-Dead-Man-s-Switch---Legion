using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace DMS_Legion
{
    /// <summary>
    /// 自定义Command_Ability：隐藏范围环显示。
    /// </summary>
    public class Command_StaticDefenseDirective : Command_Ability
    {
        public Command_StaticDefenseDirective(Ability ability, Pawn pawn) : base(ability, pawn)
        {
        }

        public override void GizmoUpdateOnMouseover()
        {
            // 不绘制范围环，只调用基础更新
            this.ability.OnGizmoUpdate();
        }
    }

    /// <summary>
    /// 自定义verb：无施法动作，立即进入吟唱。
    /// </summary>
    public class Verb_StaticDefenseDirective : Verb_CastAbility
    {
        private AbilityExtension_ChannelingToggle Ext => AbilityExtension_ChannelingToggle.Get(ability?.def) ?? new AbilityExtension_ChannelingToggle();
        public AbilityDef? AbilityDef => ability?.def;

        protected override bool TryCastShot()
        {
            var pawn = CasterPawn;
            if (pawn == null)
            {
                return false;
            }

            // 将目标固定为自身，避免弹出选择
            currentTarget = new LocalTargetInfo(pawn);

            // 直接启动自定义吟唱Job；不走默认warmup姿态
            Job job = JobMaker.MakeJob(DMSL_JobDefOf.DMSL_StaticDefenseDirectiveChant, currentTarget);
            job.verbToUse = this;
            job.count = Ext.chantTicks; // 传递吟唱时长
            job.playerForced = true;
            job.SetTarget(TargetIndex.A, currentTarget);

            pawn.jobs?.StartJob(job, JobCondition.InterruptForced, null, cancelBusyStances: true);
            return true;
        }
    }

    /// <summary>
    /// 吟唱Job：等待指定tick，不移动不做其他动作。
    /// </summary>
    public class JobDriver_StaticDefenseDirectiveChant : JobDriver
    {
        private AbilityExtension_ChannelingToggle Ext
        {
            get
            {
                var verb = job.verbToUse as Verb_StaticDefenseDirective;
                return AbilityExtension_ChannelingToggle.Get(verb?.AbilityDef) ?? new AbilityExtension_ChannelingToggle();
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 不需要任何预留
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            int duration = job.count > 0 ? job.count : Ext?.chantTicks ?? 600;

            // 不可移动，停在原地
            pawn.pather.StopDead();

            var wait = Toils_General.Wait(duration, TargetIndex.A);
            wait.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            wait.FailOn(() => pawn.Downed);
            wait.handlingFacing = true;

            if (Ext != null && !Ext.canInterrupt)
            {
                // 禁用被攻击打断
                wait.FailOn(() => false);
            }

            wait.AddFinishAction(() =>
            {
                var hediffDef = Ext?.hediffOnComplete;
                var health = pawn.health;
                var hediffSet = health?.hediffSet;
                if (hediffDef == null || health == null || hediffSet == null) return;

                var existing = hediffSet.GetFirstHediffOfDef(hediffDef);
                if (existing != null)
                {
                    health.RemoveHediff(existing);
                }
                else
                {
                    health.AddHediff(hediffDef);
                }
            });

            wait.WithProgressBarToilDelay(TargetIndex.A);
            yield return wait;
        }
    }
}

