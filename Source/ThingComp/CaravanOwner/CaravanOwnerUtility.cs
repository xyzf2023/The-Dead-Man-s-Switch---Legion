using RimWorld;
using Verse;

namespace DMSL
{
    /// <summary>
    /// 远行队编队 / 重整相关工具方法。
    /// </summary>
    public static class CaravanOwnerUtility
    {
        /// <summary>
        /// 在远行队编队或重整界面中，是否可作为“地图物资回收者”参与可达性判定。
        /// 用于修复：带 CompCaravanOwner 的机械体可当领队，但原版 CheckForErrors 只认殖民者能走到物资。
        /// </summary>
        public static bool CanActAsCaravanCollector(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                return false;
            }

            if (pawn.Dead || pawn.Downed)
            {
                return false;
            }

            return pawn.IsColonist || CompCaravanOwner.PawnCanBeCaravanOwner(pawn);
        }
    }
}
