using System.Collections.Generic;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 修理速度提升缓存管理器
    /// 避免在 JobDriver 中频繁扫描 Hediff
    /// </summary>
    public static class RepairSpeedBoostCache
    {
        private static Dictionary<Pawn, float> speedMultipliers = new Dictionary<Pawn, float>();

        /// <summary>
        /// 获取 Pawn 的修理速度修正值（默认1.0）
        /// </summary>
        public static float GetSpeedMultiplier(Pawn pawn)
        {
            if (pawn == null)
            {
                return 1f;
            }

            if (speedMultipliers.TryGetValue(pawn, out float multiplier))
            {
                return multiplier;
            }

            return 1f; // 默认无修正
        }

        /// <summary>
        /// 设置 Pawn 的修理速度修正值
        /// </summary>
        public static void SetSpeedMultiplier(Pawn pawn, float multiplier)
        {
            if (pawn == null)
            {
                return;
            }

            if (multiplier == 1f)
            {
                // 如果是默认值，从缓存中移除（节省内存）
                speedMultipliers.Remove(pawn);
            }
            else
            {
                speedMultipliers[pawn] = multiplier;
            }
        }

        /// <summary>
        /// 清除 Pawn 的缓存（当 Pawn 被销毁时调用）
        /// </summary>
        public static void ClearCache(Pawn pawn)
        {
            if (pawn != null)
            {
                speedMultipliers.Remove(pawn);
            }
        }

        /// <summary>
        /// 清除所有缓存（用于调试或重置）
        /// </summary>
        public static void ClearAllCache()
        {
            speedMultipliers.Clear();
        }
    }
}

