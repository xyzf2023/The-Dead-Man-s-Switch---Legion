using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 移动速度的 StatPart：对带有 CompMoveSpeedMomentum 的 Pawn，按已移动格数线性提升 MoveSpeed，最高为配置的倍率。
    /// </summary>
    public class StatPart_MoveSpeedMomentum : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
                return;

            var comp = pawn.GetComp<CompMoveSpeedMomentum>();
            if (comp == null)
                return;

            var props = comp.Props;
            int cells = comp.CellsMoved;
            if (cells <= 0 || props.cellsToMaxSpeed <= 0 || props.maxSpeedFactor <= 1f)
                return;

            float t = (float)cells / props.cellsToMaxSpeed;
            if (t > 1f)
                t = 1f;
            float factor = 1f + t * (props.maxSpeedFactor - 1f);
            val *= factor;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
                return null!;

            var comp = pawn.GetComp<CompMoveSpeedMomentum>();
            if (comp == null || comp.CellsMoved <= 0)
                return null!;

            var props = comp.Props;
            float t = (float)comp.CellsMoved / props.cellsToMaxSpeed;
            if (t > 1f)
                t = 1f;
            float factor = 1f + t * (props.maxSpeedFactor - 1f);
            return "DMSL_MomentumMoveSpeed".Translate(comp.CellsMoved, props.cellsToMaxSpeed, factor.ToStringPercent());
        }
    }
}
