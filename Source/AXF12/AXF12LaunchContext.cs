using System.Collections.Generic;
using Verse;

namespace DMS_Legion.AXF12
{
    public static class AXF12LaunchContext
    {
        public static string? AllowedDefName;
        public static HashSet<IThingHolder>? AllowedHolders;
        public static bool ReconFuelMultiplierActive;
        public static float ReconFuelMultiplier = 1f;
        public static bool CustomFuelCostActive;
        public static float CustomFuelCost = 0f;

        /// <summary> 是否为拦截发射；为 true 时 LeaveMap 不创建 WorldObject，改为写入拦截缓存。 </summary>
        public static bool IsInterceptLaunch;
        /// <summary> 拦截起飞时的地图（用于返航降落）。 </summary>
        public static Map? OriginMap;
        /// <summary> 拦截起飞时的格点（用于返航落点）。 </summary>
        public static IntVec3 OriginCell;

        public static void Reset()
        {
            AllowedDefName = null;
            AllowedHolders = null;
            ReconFuelMultiplierActive = false;
            ReconFuelMultiplier = 1f;
            CustomFuelCostActive = false;
            CustomFuelCost = 0f;
            IsInterceptLaunch = false;
            OriginMap = null;
            OriginCell = IntVec3.Invalid;
        }
    }
}
