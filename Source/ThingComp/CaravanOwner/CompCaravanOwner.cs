using RimWorld;
using Verse;

namespace DMSL
{
    /// <summary>
    /// 远行队领队资格标识组件
    /// 简单的标记组件，拥有此组件的机械体可以成为远行队领队
    /// </summary>
    public class CompCaravanOwner : ThingComp
    {
        public CompProperties_CaravanOwner Props => (CompProperties_CaravanOwner)props;

        /// <summary>
        /// 判断此 Pawn 是否可担任远行队领队（仅检查是否带有 CompCaravanOwner）。
        /// </summary>
        public static bool PawnCanBeCaravanOwner(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            return pawn.GetComp<CompCaravanOwner>() != null;
        }
    }
}
