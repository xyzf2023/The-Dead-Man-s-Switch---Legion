using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// 挂在萨满等拥有万心失聪能力的机械体上，冷却完成时立即为 AI 下发自施法 Job。
    /// </summary>
    public class CompOmnimindSilenceAI : ThingComp
    {
        private static readonly AbilityDef OmnimindSilenceDef = DefDatabase<AbilityDef>.GetNamedSilentFail("DMSL_Ability_OmnimindSilence");

        private const int CheckIntervalTicks = 300;

        public override void CompTick()
        {
            base.CompTick();
            if (OmnimindSilenceDef == null || parent == null)
                return;
            if (parent is not Pawn pawn || pawn.Faction == Faction.OfPlayer || !pawn.Spawned)
                return;
            if (!pawn.IsHashIntervalTick(CheckIntervalTicks))
                return;
            if (pawn.abilities == null)
                return;

            Ability ability = pawn.abilities.GetAbility(OmnimindSilenceDef);
            if (ability == null || ability.OnCooldown || !ability.CanCast.Accepted)
                return;
            if (pawn.CurJob?.ability == ability)
                return;

            Job job = ability.GetJob(pawn, pawn);
            if (job != null)
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }

    public class CompProperties_OmnimindSilenceAI : CompProperties
    {
        public CompProperties_OmnimindSilenceAI()
        {
            compClass = typeof(CompOmnimindSilenceAI);
        }
    }
}
