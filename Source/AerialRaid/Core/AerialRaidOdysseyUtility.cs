using RimWorld.Planet;
using Verse;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 奥德赛 DLC 相关判定：仅在奥德赛启用时对“太空地图”做检测，避免在太空地图触发空袭等事件。
    /// </summary>
    public static class AerialRaidOdysseyUtility
    {
        /// <summary>
        /// 仅在奥德赛 DLC 启用时检测：若玩家地图位于太空中则返回 true，否则返回 false。
        /// 未启用奥德赛或 map 为 null 时返回 false。
        /// </summary>
        public static bool IsMapInSpace(Map? map)
        {
            if (map == null)
            {
                return false;
            }
            if (!ModsConfig.OdysseyActive)
            {
                return false;
            }
            return map.Tile.Valid && map.Tile.LayerDef.isSpace;
        }
    }
}
