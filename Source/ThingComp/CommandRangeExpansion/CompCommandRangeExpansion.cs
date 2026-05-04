using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 指挥范围扩展组件：挂载此组件的机械体拥有一个可配置半径，
    /// 范围内的友方机械体均视为位于监管者控制范围内。范围可绘制显示。
    /// </summary>
    public class CompCommandRangeExpansion : ThingComp
    {
        public CompProperties_CommandRangeExpansion? Props => props as CompProperties_CommandRangeExpansion;

        public float Radius => Props?.radius ?? 0f;

        /// <summary>
        /// 征召时绘制范围圈，便于玩家查看控制范围。
        /// </summary>
        public override void PostDraw()
        {
            base.PostDraw();
            if (parent is not Pawn pawn || Props == null || Radius <= 0f)
                return;
            if (pawn.Drafted)
                GenDraw.DrawRadiusRing(parent.Position, Radius, Color.white);
        }
    }
}
