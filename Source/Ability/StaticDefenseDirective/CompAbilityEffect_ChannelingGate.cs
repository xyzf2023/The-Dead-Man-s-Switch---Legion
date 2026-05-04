using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 吟唱期间禁用能力的通用门控Comp。
    /// </summary>
    public class CompAbilityEffect_ChannelingGate : CompAbilityEffect
    {
        public override bool GizmoDisabled(out string reason)
        {
            reason = string.Empty;
            var pawn = parent.pawn;
            if (pawn?.CurJob?.def == DMSL_JobDefOf.DMSL_StaticDefenseDirectiveChant)
            {
                reason = "DMSL_ChannelingGate_DisabledReason".Translate();
                return true;
            }
            return false;
        }
    }

    public class CompProperties_AbilityChannelingGate : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityChannelingGate()
        {
            compClass = typeof(CompAbilityEffect_ChannelingGate);
        }
    }
}

