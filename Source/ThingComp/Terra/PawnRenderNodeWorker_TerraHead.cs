using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    public class PawnRenderNodeWorker_TerraHead : PawnRenderNodeWorker_FlipWhenCrawling
    {
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 result = base.OffsetFor(node, parms, out pivot);
            if (parms.pawn?.Drawer?.renderer != null)
                result += parms.pawn.Drawer.renderer.BaseHeadOffsetAt(parms.facing);
            if (node.Props.narrowCrownHorizontalOffset != 0f && parms.facing.IsHorizontal)
            {
                if (parms.facing == Rot4.East)
                    result.x -= node.Props.narrowCrownHorizontalOffset;
                else if (parms.facing == Rot4.West)
                    result.x += node.Props.narrowCrownHorizontalOffset;
                result.z -= node.Props.narrowCrownHorizontalOffset;
            }
            return result;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion result = base.RotationFor(node, parms);
            if (!parms.Portrait && parms.pawn != null && parms.pawn.Crawling)
            {
                result *= PawnRenderUtility.CrawlingHeadAngle(parms.facing).ToQuat();
                if (parms.flipHead) result *= Quaternion.Euler(0f, 180f, 0f);
            }
            if (parms.pawn != null && parms.pawn.IsShambler && parms.pawn.mutant != null && parms.pawn.mutant.HasTurned && !parms.pawn.Dead)
            {
                var hediff = parms.pawn.mutant.Hediff as Hediff_Shambler;
                result *= Quaternion.Euler(Vector3.up * (hediff?.headRotation ?? 0f));
            }
            return result;
        }
    }
}
