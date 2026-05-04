using RimWorld;
using Verse;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 空袭支援类型选择器
    /// 将空袭请求转化为对应的支援类型 defName，并向空中支援框架发送请求
    /// </summary>
    public static class AerialRaidSupportSelector
    {
        /// <summary>
        /// 根据空袭请求判断应该使用的支援类型 defName
        /// </summary>
        /// <param name="request">空袭请求</param>
        /// <returns>支援类型的 defName（不会返回 null）</returns>
        public static string GetSupportTypeDefName(AerialRaidRequest request)
        {
            // 如果请求来自 PrePhaseComponent，检查组件是否指定了支援类型
            if (request.SourceComponent != null)
            {
                string? customType = request.SourceComponent.GetSupportTypeDefName();
                if (!string.IsNullOrEmpty(customType))
                {
                    return customType!; // 已检查非空，使用 ! 消除警告
                }
            }

            // 默认返回武装殖民舰队空袭支援类型
            return "DMSL_AerialSupport_ArmyEnemyRaid";
        }

        /// <summary>
        /// 向空中支援框架发送空袭请求并执行支援
        /// </summary>
        /// <param name="request">空袭请求</param>
        public static void ExecuteAerialRaid(AerialRaidRequest request)
        {
            if (request == null || request.TargetMap == null)
            {
                Log.Error("[DMS_Legion]空袭支援选择器：空袭请求无效");
                return;
            }

            // 获取支援类型 defName
            string supportTypeDefName = GetSupportTypeDefName(request);
            
            // 获取支援类型定义
            var supportType = DefDatabase<AerialSupportTypeDef>.GetNamed(supportTypeDefName, false);
            if (supportType == null)
            {
                Log.Error($"[DMS_Legion]空袭支援选择器：未找到支援类型定义：{supportTypeDefName}");
                return;
            }

            // 通过协调器发起支援：起点按 startDirection/preferNorthEntry 计算，延迟按 renderDelayTicks/soundDelayTicks 生效（无 instigator，不执行冷却/消息回调）
            var coordinator = AerialSupportCoordinator.Instance;
            if (coordinator != null)
            {
                coordinator.RequestSupportAt(request.TargetCell, request.TargetMap, supportType);
                return;
            }

            // 降级：无协调器时按原逻辑随机起点、立即开始
            var renderer = request.TargetMap.GetComponent<AerialSupportRenderer>();
            if (renderer == null)
            {
                Log.Error("[DMS_Legion]空袭支援选择器：未找到AerialSupportRenderer组件");
                return;
            }
            IntVec3 flightStart = CellFinder.RandomEdgeCell(request.TargetMap);
            if (flightStart == request.TargetCell)
            {
                flightStart = new IntVec3(
                    Verse.Rand.Range(0, request.TargetMap.Size.x - 1),
                    0,
                    Verse.Rand.Range(0, request.TargetMap.Size.z - 1)
                );
            }
            renderer.StartFlight(flightStart, request.TargetCell, supportType);
        }
    }
}
