using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 检查机械体能量并禁用能力的能力组件
    /// </summary>
    public class CompAbilityEffect_CheckEnergy : AbilityComp
    {
        public CompProperties_AbilityCheckEnergy Props
        {
            get
            {
                return (CompProperties_AbilityCheckEnergy)this.props;
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn pawn = this.parent.pawn;
            if (pawn != null && pawn.needs != null && pawn.needs.energy != null)
            {
                float energyPercentage = pawn.needs.energy.CurLevelPercentage;
                if (energyPercentage < this.Props.minEnergyPercentage)
                {
                    reason = this.Props.disabledReason;
                    return true;
                }
            }
#pragma warning disable CS8625
            reason = null;
#pragma warning restore CS8625
            return false;
        }
    }
}

