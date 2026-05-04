using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 移动动量组件：在移动时累计已移动格数，用于 StatPart 线性提升移动速度；
    /// 停止移动后经过 ticksToResetMomentum tick 再清零动量。
    /// </summary>
    public class CompMoveSpeedMomentum : ThingComp
    {
        private int cellsMoved;
        private IntVec3 lastRecordedPosition;
        private int ticksSinceStopped;

        public CompProperties_MoveSpeedMomentum Props => (CompProperties_MoveSpeedMomentum)props;

        /// <summary>当前连续移动的格数（用于 StatPart 计算倍率）。</summary>
        public int CellsMoved => cellsMoved;

        public override void CompTick()
        {
            base.CompTick();
            if (parent is not Pawn pawn || !pawn.Spawned)
                return;

            if (pawn.pather?.Moving ?? false)
            {
                ticksSinceStopped = 0;
                if (pawn.Position != lastRecordedPosition)
                {
                    cellsMoved++;
                    lastRecordedPosition = pawn.Position;
                }
            }
            else
            {
                lastRecordedPosition = pawn.Position;
                int threshold = Props.ticksToResetMomentum > 0 ? Props.ticksToResetMomentum : 120;
                ticksSinceStopped++;
                if (ticksSinceStopped >= threshold)
                {
                    cellsMoved = 0;
                    ticksSinceStopped = 0;
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cellsMoved, "cellsMoved", 0);
            Scribe_Values.Look(ref lastRecordedPosition, "lastRecordedPosition");
            Scribe_Values.Look(ref ticksSinceStopped, "ticksSinceStopped", 0);
        }
    }
}
