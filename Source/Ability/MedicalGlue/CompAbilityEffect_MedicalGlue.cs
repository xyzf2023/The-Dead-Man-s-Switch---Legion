using RimWorld;
using Verse;
using System.Collections.Generic;
using UnityEngine;

namespace DMS_Legion
{
    /// <summary>
    /// 医用胶水能力效果：对目标身上所有流血且可包扎的伤口进行包扎。
    /// 逻辑参考原版 <see cref="CompAbilityEffect_Coagulate"/>，仅处理流血伤口，治疗品质由 Props.tendQualityRange 决定。
    /// </summary>
    public class CompAbilityEffect_MedicalGlue : CompAbilityEffect
    {
        public new CompProperties_AbilityMedicalGlue Props => (CompProperties_AbilityMedicalGlue)props;

        /// <summary>
        /// 判断目标是否至少有一个可包扎的流血伤口。
        /// </summary>
        private static bool HasTendableBleedingWound(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                return false;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if ((h is Hediff_Injury || h is Hediff_MissingPart) && h.Bleeding && h.TendableNow())
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 验证目标：必须至少有一个可包扎的流血伤口。
        /// </summary>
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null)
                return base.Valid(target, throwMessages);
            if (!HasTendableBleedingWound(pawn))
            {
                if (throwMessages && !Props.noBleedingWoundMessage.NullOrEmpty())
                    Messages.Message("CannotUseAbility".Translate(parent.def.label) + ": " + Props.noBleedingWoundMessage.Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        /// <summary>
        /// 对目标身上所有流血且可包扎的伤口进行包扎，品质在 tendQualityRange 内随机。
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = target.Pawn;
            if (pawn == null)
                return;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            int tendedCount = 0;
            float quality = Props.tendQualityRange.RandomInRange;
            float maxQuality = Props.tendQualityRange.TrueMax;

            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff h = hediffs[i];
                if ((h is Hediff_Injury || h is Hediff_MissingPart) && h.Bleeding && h.TendableNow())
                {
                    h.Tended(quality, maxQuality, 1);
                    tendedCount++;
                }
            }

            if (tendedCount > 0)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "NumWoundsTended".Translate(tendedCount), 3.65f);
            FleckMaker.AttachedOverlay(pawn, FleckDefOf.FlashHollow, Vector3.zero, 1.5f);
        }
    }
}
