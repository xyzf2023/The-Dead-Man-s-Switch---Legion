using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 为带 CompMechCommanderMarker 的 Commander 提供额外 MechBandwidth / MechControlGroups 加成。
    /// 通过 XML Patch 挂载到对应 StatDef 的 parts 列表，仅在计算这两个 stat 时被调用。
    /// </summary>
    public class StatPart_MechCommanderBonus : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
                return;

            var marker = pawn.GetComp<CompMechCommanderMarker>();
            if (marker == null)
                return;

            if (parentStat == StatDefOf.MechBandwidth)
                val += marker.Props.extraMechBandwidth;
            else if (parentStat == StatDefOf.MechControlGroups)
                val += marker.Props.extraMechControlGroups;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
                return null!;

            var marker = pawn.GetComp<CompMechCommanderMarker>();
            if (marker == null)
                return null!;

            if (parentStat == StatDefOf.MechBandwidth && marker.Props.extraMechBandwidth != 0)
                return "DMSL_MechCommanderBandwidth".Translate(marker.Props.extraMechBandwidth);

            if (parentStat == StatDefOf.MechControlGroups && marker.Props.extraMechControlGroups != 0)
                return "DMSL_MechCommanderControlGroups".Translate(marker.Props.extraMechControlGroups);

            return null!;
        }
    }
}
