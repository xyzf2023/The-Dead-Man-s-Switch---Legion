using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 抑止时间乘数的 StatPart：当带有 CompMoveSpeedMomentum 的 Pawn 面板移速大于 10 格/秒 时，
    /// 将抑止时间乘数置为 0，即不受抑止（硬直）影响。
    /// </summary>
    public class StatPart_StaggerDurationFactorMomentum : StatPart
    {
        /// <summary>面板移速超过此值（格/秒）时抑止时间×0。</summary>
        private const float StaggerImmunitySpeedThreshold = 10f;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
                return;

            if (pawn.GetComp<CompMoveSpeedMomentum>() == null)
                return;

            if (pawn.GetStatValue(StatDefOf.MoveSpeed) > StaggerImmunitySpeedThreshold)
                val *= 0f;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
                return null!;

            if (pawn.GetComp<CompMoveSpeedMomentum>() == null)
                return null!;

            float moveSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed);
            if (moveSpeed <= StaggerImmunitySpeedThreshold)
                return null!;

            return "DMSL_StaggerDurationMomentum".Translate();
        }
    }
}
