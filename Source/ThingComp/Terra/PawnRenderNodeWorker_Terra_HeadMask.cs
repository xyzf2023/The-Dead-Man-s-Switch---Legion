using Verse;

namespace DMS_Legion
{
    public class PawnRenderNodeWorker_Terra_HeadMask : PawnRenderNodeWorker_FlipWhenCrawling
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            return parms.pawn.IsColonyMechPlayerControlled == true;
        }
    }
}
