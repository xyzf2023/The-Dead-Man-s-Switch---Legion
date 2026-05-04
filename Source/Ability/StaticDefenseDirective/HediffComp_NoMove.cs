using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    /// <summary>
    /// Hediff组件：存在时强制原地不移动；每300tick清除一次移动指令（Goto）。
    /// </summary>
    public class HediffComp_NoMove : HediffComp
    {
        private int _ticksUntilClearMove;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            var pawn = Pawn;
            if (pawn?.pather == null) return;

            pawn.pather.StopDead();

            // 每300tick清除一次移动指令
            _ticksUntilClearMove--;
            if (_ticksUntilClearMove <= 0)
            {
                _ticksUntilClearMove = 300;
                if (pawn.CurJob?.def == JobDefOf.Goto)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
        }
    }

    public class HediffCompProperties_NoMove : HediffCompProperties
    {
        public HediffCompProperties_NoMove()
        {
            compClass = typeof(HediffComp_NoMove);
        }
    }
}

