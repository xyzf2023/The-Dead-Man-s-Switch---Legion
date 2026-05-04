using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 高压电击能力效果：消耗能量并对目标添加电击昏迷效果
    /// </summary>
    public class CompAbilityEffect_HighVoltageShock : CompAbilityEffect
    {
        public new CompProperties_AbilityHighVoltageShock Props
        {
            get
            {
                return (CompProperties_AbilityHighVoltageShock)this.props;
            }
        }

        /// <summary>
        /// 检查目标是否有效（仅对血肉生物有效）
        /// </summary>
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!target.IsValid)
            {
                return false;
            }

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null)
            {
                if (throwMessages)
                {
                    Messages.Message("DMSL_HighVoltageShock_InvalidTarget".Translate(), this.parent.pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            // 仅对血肉生物有效（非机械体）
            if (targetPawn.RaceProps.IsMechanoid)
            {
                if (throwMessages)
                {
                    Messages.Message("DMSL_HighVoltageShock_NotFlesh".Translate(), this.parent.pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// 应用能力效果：消耗能量并添加Hediff
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = this.parent.pawn;
            Pawn targetPawn = target.Pawn;

            if (caster == null || targetPawn == null)
            {
                return;
            }

            // 消耗能量
            if (caster.needs != null && caster.needs.energy != null)
            {
                float energyToConsume = this.Props.energyConsumePercentage * caster.needs.energy.MaxLevel;
                float newLevel = Mathf.Max(0f, caster.needs.energy.CurLevel - energyToConsume);
                caster.needs.energy.CurLevel = newLevel;
            }

            // 对目标添加电击昏迷Hediff
            if (this.Props.hediffDef != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(this.Props.hediffDef, targetPawn, null);
                targetPawn.health.AddHediff(hediff, null, null, null);
            }
        }
    }
}

