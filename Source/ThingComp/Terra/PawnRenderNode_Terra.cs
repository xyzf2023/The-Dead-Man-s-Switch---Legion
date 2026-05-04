using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 尘世头部渲染节点：贴图路径来自 PawnRenderTreeDef 的 texPath 或 ThingDef.graphicData.texPath（Mechanoid/Terra/Head）。
    /// </summary>
    public class PawnRenderNode_Terra : PawnRenderNode
    {
        public PawnRenderNode_Terra(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (props.overrideMeshSize.HasValue)
                return MeshPool.GetMeshSetForSize(props.overrideMeshSize.Value.x, props.overrideMeshSize.Value.y);
            return HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn);
        }

        protected override string TexPathFor(Pawn pawn)
        {
            if (!string.IsNullOrEmpty(props.texPath))
                return props.texPath;
            return pawn?.def?.graphicData?.texPath ?? string.Empty;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            string texPath = TexPathFor(pawn);
            if (texPath.NullOrEmpty()) return null!;
            Shader? shader = ShaderFor(pawn);
            if (shader == null) return null!;
            string? maskPath = pawn?.def?.graphicData?.maskPath;
            if (!maskPath.NullOrEmpty())
                return GraphicDatabase.Get<Graphic_Multi>(texPath, shader, maskPath) ?? null!;
            return GraphicDatabase.Get<Graphic_Multi>(texPath, shader) ?? null!;
        }
    }
}
