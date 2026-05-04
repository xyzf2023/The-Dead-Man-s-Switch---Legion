using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 为带机械师标记的 Commander 确保存在 Pawn_MechanitorTracker 与 Pawn_RelationsTracker。
    /// </summary>
    public static class MechCommanderUtility
    {
        /// <summary>确保 Commander 拥有 mechanitor 与 relations；仅在本次调用中新建了 tracker 时才调用 Notify_PawnSpawned，避免热路径上重复触发带宽/控制组重算。</summary>
        public static void EnsureMechanitorTracker(Pawn pawn)
        {
            if (pawn == null || !ModsConfig.BiotechActive)
            {
                return;
            }

            bool createdTracker = false;
            if (pawn.mechanitor == null)
            {
                pawn.mechanitor = new Pawn_MechanitorTracker(pawn);
                createdTracker = true;
            }

            if (pawn.relations == null)
            {
                pawn.relations = new Pawn_RelationsTracker(pawn);
            }

            if (createdTracker)
            {
                pawn.mechanitor.Notify_PawnSpawned(true);
            }
        }
    }
}
