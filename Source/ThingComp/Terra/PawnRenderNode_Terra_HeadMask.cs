using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 尘世头部忠诚度遮罩节点，仅己方控制时绘制。
    /// </summary>
    public class PawnRenderNode_Terra_HeadMask : PawnRenderNode
    {
        public PawnRenderNode_Terra_HeadMask(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (pawn.IsColonyMechPlayerControlled != true)
                return default!;
            GraphicMeshSet? set = HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn);
            return set ?? default!;
        }

        protected override string TexPathFor(Pawn pawn)
        {
            return pawn?.def?.graphicData?.maskPath ?? string.Empty;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            string maskPath = TexPathFor(pawn);
            if (maskPath.NullOrEmpty()) return null!;
            Shader? shader = ShaderFor(pawn);
            if (shader == null) return null!;
            return GraphicDatabase.Get<Graphic_Multi>(maskPath, shader) ?? null!;
        }
    }
}
