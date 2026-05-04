using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using DMS_Legion.GroundSupport.SupportEffects;

namespace DMS_Legion.GroundSupport
{
    /// <summary>
    /// 空中支援渲染器，专门负责飞机贴图的渲染、到达通知和组件协调
    /// </summary>
    public class AerialSupportRenderer : MapComponent
    {
        private List<AircraftFlight> activeFlights = new List<AircraftFlight>();
        private List<BombingSequence> activeBombingSequences = new List<BombingSequence>();
        private AerialSupportTypeDef? selectedSupportType = null;

        /// <summary>当前皇权支援调用上下文（不存档，选点完成并成功召唤后消耗并清空）</summary>
        private Pawn? royalPermitCaller;
        private RoyalTitlePermitDef? royalPermitDef;
        private Faction? royalPermitFaction;
        private bool royalPermitFree;

        // 静态缓存：所有实例共享，避免重复查找 MethodInfo
        // 使用 ConcurrentDictionary 确保线程安全（RimWorld 在某些场景下可能使用多线程）
        private static ConcurrentDictionary<Type, MethodInfo> cachedExecuteMethods = new ConcurrentDictionary<Type, MethodInfo>();

        public AerialSupportRenderer(Map map) : base(map)
        {
            // 效果执行现在由组件直接处理
        }

        /// <summary>
        /// 保存/加载数据，用于游戏存档
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // 保存活跃的飞行实例
            Scribe_Collections.Look(ref activeFlights, "activeFlights", LookMode.Deep, Array.Empty<object>());

            // 保存轰炸序列
            Scribe_Collections.Look(ref activeBombingSequences, "activeBombingSequences", LookMode.Deep, Array.Empty<object>());

            // 保存选中的支援类型
            Scribe_Defs.Look(ref selectedSupportType, "selectedSupportType");

            // 加载后进行基础清理（引用将在首次Tick时通过延迟初始化设置）
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 清理明显无效的对象（比如null引用或损坏的数据）
                activeFlights.RemoveAll(flight => flight == null || flight.supportType == null);
                activeBombingSequences.RemoveAll(seq => seq == null);
            }
        }

        /// <summary>
        /// 每tick更新所有飞行中的飞机
        /// </summary>
        public override void MapComponentTick()
        {
            // 更新所有飞行效果，移除已完成的飞行
            activeFlights.RemoveAll(flight => flight.Tick());

            // 更新所有轰炸序列
            activeBombingSequences.RemoveAll(seq => seq.Tick());
        }

        /// <summary>
        /// 渲染所有飞行中的飞机
        /// </summary>
        public override void MapComponentDraw()
        {
            foreach (var flight in activeFlights)
            {
                // 如果配置为不继续绘制且已到达目的地，则跳过绘制
                if (flight.supportType?.continueDrawingAfterArrival == false && flight.targetReached)
                {
                    continue;
                }
                flight.Draw();
            }
        }

        /// <summary>
        /// 设置当前选择的空中支援类型
        /// </summary>
        public void SetSelectedSupportType(AerialSupportTypeDef? supportType)
        {
            selectedSupportType = supportType;
        }

        /// <summary>
        /// 获取当前选择的空中支援类型
        /// </summary>
        public AerialSupportTypeDef? GetSelectedSupportType()
        {
            return selectedSupportType;
        }

        /// <summary>
        /// 设置当前皇权支援调用上下文（Worker 在点击时调用，选点成功并召唤后再消耗）
        /// </summary>
        public void SetRoyalPermitContext(Pawn caller, RoyalTitlePermitDef permitDef, Faction faction, bool free)
        {
            if (!ModsConfig.RoyaltyActive)
                return;
            royalPermitCaller = caller;
            royalPermitDef = permitDef;
            royalPermitFaction = faction;
            royalPermitFree = free;
        }

        /// <summary>
        /// 清除皇权支援上下文（取消选点或消耗后调用）
        /// </summary>
        public void ClearRoyalPermitContext()
        {
            royalPermitCaller = null;
            royalPermitDef = null;
            royalPermitFaction = null;
        }

        /// <summary>
        /// 若存在皇权支援上下文，则执行消耗（Notify_Used、TryRemoveFavor）并清空上下文。在「选点完成且空中支援成功召唤」后调用。
        /// </summary>
        public void ConsumeRoyalPermitIfSet()
        {
            if (!ModsConfig.RoyaltyActive)
            {
                ClearRoyalPermitContext();
                return;
            }
            if (royalPermitCaller == null || royalPermitDef == null || royalPermitFaction == null)
                return;
            try
            {
                royalPermitCaller.royalty?.GetPermit(royalPermitDef, royalPermitFaction)?.Notify_Used();
                if (!royalPermitFree && royalPermitDef.royalAid != null)
                    royalPermitCaller.royalty?.TryRemoveFavor(royalPermitFaction, royalPermitDef.royalAid.favorCost);
            }
            finally
            {
                ClearRoyalPermitContext();
            }
        }

        /// <summary>
        /// 添加飞行实例到活跃列表（供复合飞行类型使用）
        /// </summary>
        public void AddFlight(AircraftFlight flight)
        {
            if (flight != null)
            {
                activeFlights.Add(flight);
            }
        }

        /// <summary>
        /// 启动飞行（接收预创建的Flight实例）- 符合新架构
        /// </summary>
        /// <param name="flight">预创建的Flight实例</param>
        public void StartFlight(AircraftFlight flight)
        {
            if (flight == null)
            {
                Log.Error("[DMS_Legion] StartFlight 接收到null flight实例");
                return;
            }

            // 注册Flight实例
            activeFlights.Add(flight);

            // 触发飞机出现时的状态影响效果（Spawn Effects）
            TriggerSpawnEffects(flight);
        }

        /// <summary>
        /// 触发飞机出现时的状态影响效果（Spawn Effects）
        /// Renderer只负责触发，不解析具体逻辑
        /// </summary>
        private void TriggerSpawnEffects(AircraftFlight flight)
        {
            if (flight?.supportType?.spawnEffects == null || flight.supportType.spawnEffects.Count == 0)
            {
                // 未定义spawnEffects的支援类型不受影响
                return;
            }

            // 获取飞机出现位置（对于不同类型的flight可能不同）
            IntVec3 spawnPos = GetFlightSpawnPosition(flight);

            // 触发所有定义的状态影响效果
            foreach (var spawnEffectDef in flight.supportType.spawnEffects)
            {
                if (spawnEffectDef != null)
                {
                    try
                    {
                        var worker = spawnEffectDef.CreateWorker();
                        if (worker != null)
                        {
                            // 执行状态影响效果（Renderer不关心具体逻辑）
                            worker.ExecuteEffect(spawnPos, flight.supportType, map);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[DMS_Legion] 执行效果失败: {spawnEffectDef.defName} - {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 获取Flight的出现位置
        /// </summary>
        private IntVec3 GetFlightSpawnPosition(AircraftFlight flight)
        {
            if (flight == null) return IntVec3.Zero;

            // 根据不同Flight类型确定出现位置
            if (flight is MultiTargetFlight multiTargetFlight)
            {
                // 多目标支援：在第一个目标点出现
                var targetPoints = multiTargetFlight.GetTargetPoints();
                return targetPoints.Count > 0 ? targetPoints[0] : flight.startPos.ToIntVec3();
            }
            else
            {
                // 普通支援和自定义直线支援：在起始位置出现
                return flight.startPos.ToIntVec3();
            }
        }

        /// <summary>
        /// 使用当前选择的支援类型启动飞行（向后兼容）
        /// </summary>
        /// <param name="startPos">起点位置</param>
        /// <param name="targetPos">目标位置</param>
        public void StartFlightWithSelectedType(IntVec3 startPos, IntVec3 targetPos)
        {
            try
            {
                if (selectedSupportType != null)
                {
                    StartFlight(startPos, targetPos, selectedSupportType);
                }
                else
                {
                    Log.Warning("[DMS_Legion] 未选择支援类型，无法启动飞行");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DMS_Legion] StartFlightWithSelectedType 异常: {ex}");
            }
        }

        /// <summary>
        /// 启动自定义直线飞行
        /// </summary>
        /// <param name="startPoint">用户选择的直线起点</param>
        /// <param name="endPoint">用户选择的直线终点</param>
        /// <param name="supportType">支援类型</param>
        public void StartCustomLineFlight(IntVec3 startPoint, IntVec3 endPoint, AerialSupportTypeDef supportType)
        {
            try
            {
                // 计算用户选择线段的延长线与地图边界的交点
                var boundaryPoints = CalculateLineBoundaryIntersections(startPoint, endPoint);
                IntVec3 flightStart = boundaryPoints.start;
                IntVec3 flightEnd = boundaryPoints.end;

                // 在用户选择的线段上均匀分布执行点
                List<IntVec3> effectPoints = CalculateEffectPointsOnUserLine(startPoint, endPoint, supportType);
                // 启动飞行（飞机从边界起点飞到边界终点，在用户线段上执行效果）
                StartCustomLineFlightInternal(flightStart, flightEnd, effectPoints, supportType, startPoint, endPoint);
            }
            catch (Exception ex)
            {
                Log.Error($"[DMS_Legion] StartCustomLineFlight 异常: {ex}");
            }
        }

        /// <summary>
        /// 计算从地图边缘到目标点的飞行起点位置
        /// </summary>
        public IntVec3 CalculateFlightStartPosition(IntVec3 target, Map map, AerialSupportTypeDef? supportType)
        {
            if (map == null)
            {
                Log.Error("[DMS_Legion] 无法计算飞行起点：map为null");
                return target;
            }

            if (supportType == null)
            {
                Log.Error("[DMS_Legion] 无法计算飞行起点：supportType为null");
                return target;
            }

            IntVec3 startPos;

            // 根据配置的方向选择起点
            if (!string.IsNullOrEmpty(supportType.startDirection) &&
                supportType.startDirection != "Random")
            {
                startPos = GetEdgeCellByDirection(map, supportType.startDirection, target, supportType);
            }
            else
            {
                // 随机选择边缘点
                startPos = CellFinder.RandomEdgeCell(map);
            }

            // 确保起点和目标点不同
            if (startPos == target)
            {
                // 如果选到的点和目标点相同，稍微偏移一下
                startPos = new IntVec3(
                    Mathf.Clamp(startPos.x + 1, 0, map.Size.x - 1),
                    0,
                    startPos.z
                );
            }

            return startPos;
        }

        /// <summary>
        /// 根据指定方向获取地图边缘的单元格
        /// </summary>
        private IntVec3 GetEdgeCellByDirection(Map map, string? direction, IntVec3 target, AerialSupportTypeDef supportType)
        {
            if (string.IsNullOrEmpty(direction))
            {
                return CellFinder.RandomEdgeCell(map);
            }

            // 此时direction一定不为null，使用非空断言
            string directionLower = direction!.ToLower();
            IntVec3 mapSize = map.Size;
            int margin = 5; // 边缘距离，避免太靠近角落

            switch (directionLower)
            {
                case "north":
                    // 从北边（z最大）进入；preferNorthEntry 为 true 时起点 X 与选点相同，呈垂直路线
                    int northX = supportType.preferNorthEntry
                        ? Mathf.Clamp(target.x, margin, mapSize.x - margin)
                        : Rand.Range(margin, mapSize.x - margin);
                    return new IntVec3(northX, 0, mapSize.z - 1);

                case "south":
                    // 从南边（z最小）进入；preferNorthEntry 为 true 时起点 X 与选点相同，呈垂直路线
                    int southX = supportType.preferNorthEntry
                        ? Mathf.Clamp(target.x, margin, mapSize.x - margin)
                        : Rand.Range(margin, mapSize.x - margin);
                    return new IntVec3(southX, 0, 0);

                case "east":
                    // 从东边（x最大）进入
                    if (supportType.preferNorthEntry)
                    {
                        // 优先选择目标点北边的位置（z坐标 > target.z）
                        int minZ = Mathf.Max(margin, target.z + 1);
                        int maxZ = mapSize.z - margin;
                        if (minZ >= maxZ)
                        {
                            // 如果目标点太靠北，使用全范围
                            minZ = margin;
                        }
                        return new IntVec3(
                            mapSize.x - 1,
                            0,
                            Rand.Range(minZ, maxZ)
                        );
                    }
                    else
                    {
                        // 正常随机选择
                        return new IntVec3(
                            mapSize.x - 1,
                            0,
                            Rand.Range(margin, mapSize.z - margin)
                        );
                    }

                case "west":
                    // 从西边（x最小）进入
                    if (supportType.preferNorthEntry)
                    {
                        // 优先选择目标点北边的位置（z坐标 > target.z）
                        int minZ = Mathf.Max(margin, target.z + 1);
                        int maxZ = mapSize.z - margin;
                        if (minZ >= maxZ)
                        {
                            // 如果目标点太靠北，使用全范围
                            minZ = margin;
                        }
                        return new IntVec3(
                            0,
                            0,
                            Rand.Range(minZ, maxZ)
                        );
                    }
                    else
                    {
                        // 正常随机选择
                        return new IntVec3(
                            0,
                            0,
                            Rand.Range(margin, mapSize.z - margin)
                        );
                    }

                default:
                    Log.Warning($"[DMS_Legion] 未知的起始方向: {direction}，使用随机边缘");
                    return CellFinder.RandomEdgeCell(map);
            }
        }

        /// <summary>
        /// 计算线段延长线与地图边界的交点
        /// </summary>
        private (IntVec3 start, IntVec3 end) CalculateLineBoundaryIntersections(IntVec3 point1, IntVec3 point2)
        {
            Map map = this.map;
            IntVec3 mapSize = new IntVec3(map.Size.x, 0, map.Size.z);

            // 计算线段的方向向量
            Vector3 direction = (point2 - point1).ToVector3().normalized;

            // 地图边界：左下角(0,0)，右上角(mapSize.x, mapSize.z)
            float minX = 0, maxX = mapSize.x - 1;
            float minZ = 0, maxZ = mapSize.z - 1;

            Vector3 start = point1.ToVector3();
            Vector3 end = point2.ToVector3();

            List<Vector3> intersections = new List<Vector3>();

            // 计算与四条边的交点
            // 左边 (x = minX)
            if (Mathf.Abs(direction.x) > 0.001f)
            {
                float t = (minX - start.x) / direction.x;
                Vector3 intersection = start + direction * t;
                if (intersection.z >= minZ && intersection.z <= maxZ)
                    intersections.Add(intersection);
            }

            // 右边 (x = maxX)
            if (Mathf.Abs(direction.x) > 0.001f)
            {
                float t = (maxX - start.x) / direction.x;
                Vector3 intersection = start + direction * t;
                if (intersection.z >= minZ && intersection.z <= maxZ)
                    intersections.Add(intersection);
            }

            // 下边 (z = minZ)
            if (Mathf.Abs(direction.z) > 0.001f)
            {
                float t = (minZ - start.z) / direction.z;
                Vector3 intersection = start + direction * t;
                if (intersection.x >= minX && intersection.x <= maxX)
                    intersections.Add(intersection);
            }

            // 上边 (z = maxZ)
            if (Mathf.Abs(direction.z) > 0.001f)
            {
                float t = (maxZ - start.z) / direction.z;
                Vector3 intersection = start + direction * t;
                if (intersection.x >= minX && intersection.x <= maxX)
                    intersections.Add(intersection);
            }

            // 应该找到2个交点（线段延长线与地图边界）
            if (intersections.Count >= 2)
            {
                // 根据用户选择的线段方向确定飞机飞行方向
                // 如果用户选择从point1到point2，飞机应该沿着这个方向飞行

                // 计算用户线段的方向
                Vector3 userDirection = (point2 - point1).ToVector3().normalized;

                // 计算两个交点中哪个更接近point1（用户起点）
                Vector3 point1Vec = point1.ToVector3();
                intersections.Sort((a, b) => Vector3.Distance(a, point1Vec).CompareTo(Vector3.Distance(b, point1Vec)));

                Vector3 closerPoint = intersections[0];  // 更接近用户起点的交点
                Vector3 fartherPoint = intersections[1]; // 更远离用户起点的交点

                // 确保交点在合理范围内（边界上或边界内）
                IntVec3 closerCell = new IntVec3(
                    Mathf.Clamp(Mathf.RoundToInt(closerPoint.x), 0, map.Size.x - 1),
                    0,
                    Mathf.Clamp(Mathf.RoundToInt(closerPoint.z), 0, map.Size.z - 1)
                );
                IntVec3 fartherCell = new IntVec3(
                    Mathf.Clamp(Mathf.RoundToInt(fartherPoint.x), 0, map.Size.x - 1),
                    0,
                    Mathf.Clamp(Mathf.RoundToInt(fartherPoint.z), 0, map.Size.z - 1)
                );

                // 检查两个交点是否太接近（可能导致distance=0）
                float cellDistance = Vector3.Distance(closerCell.ToVector3Shifted(), fartherCell.ToVector3Shifted());
                if (cellDistance < 1.0f)
                {
                    // 如果两个交点太接近，使用用户选择的点并确保它们有足够距离
                    IntVec3 safePoint1 = new IntVec3(
                        Mathf.Clamp(point1.x, 0, map.Size.x - 1),
                        0,
                        Mathf.Clamp(point1.z, 0, map.Size.z - 1)
                    );
                    IntVec3 safePoint2 = new IntVec3(
                        Mathf.Clamp(point2.x, 0, map.Size.x - 1),
                        0,
                        Mathf.Clamp(point2.z, 0, map.Size.z - 1)
                    );
                    
                    // 如果两个点仍然太接近，扩展其中一个点
                    float userDistance = Vector3.Distance(safePoint1.ToVector3Shifted(), safePoint2.ToVector3Shifted());
                    if (userDistance < 1.0f)
                    {
                        // 沿着用户方向扩展safePoint2
                        Vector3 extendDirection = userDirection;
                        if (extendDirection == Vector3.zero || extendDirection.magnitude < 0.001f)
                        {
                            // 如果用户方向无效，使用默认方向（向右）
                            extendDirection = Vector3.right;
                        }
                        
                        safePoint2 = new IntVec3(
                            Mathf.Clamp(safePoint1.x + Mathf.RoundToInt(extendDirection.x * 10), 0, map.Size.x - 1),
                            0,
                            Mathf.Clamp(safePoint1.z + Mathf.RoundToInt(extendDirection.z * 10), 0, map.Size.z - 1)
                        );
                    }
                    
                    return (safePoint1, safePoint2);
                }

                // 计算从每个交点到用户终点的向量
                Vector3 toPoint2FromCloser = (point2 - closerCell).ToVector3();
                Vector3 toPoint2FromFarther = (point2 - fartherCell).ToVector3();

                // 根据用户方向，选择合适的起点和终点
                // 如果从fartherPoint到point2的方向更接近用户方向，那么从fartherPoint出发
                float dotCloser = Vector3.Dot(toPoint2FromCloser.normalized, userDirection);
                float dotFarther = Vector3.Dot(toPoint2FromFarther.normalized, userDirection);

                if (dotFarther > dotCloser)
                {
                    // 从远离的交点飞向接近的交点
                    return (fartherCell, closerCell);
                }
                else
                {
                    // 从接近的交点飞向远离的交点
                    return (closerCell, fartherCell);
                }
            }
            else if (intersections.Count == 1)
            {
                // 如果只找到1个交点（线段一端在地图内，一端在地图外）
                // 使用这个交点作为起点或终点，另一个点使用用户选择的点
                IntVec3 intersectionCell = new IntVec3(
                    Mathf.Clamp(Mathf.RoundToInt(intersections[0].x), 0, map.Size.x - 1),
                    0,
                    Mathf.Clamp(Mathf.RoundToInt(intersections[0].z), 0, map.Size.z - 1)
                );
                
                // 判断用户选择的点哪个在地图内
                bool point1InBounds = point1.x >= 0 && point1.x < map.Size.x && point1.z >= 0 && point1.z < map.Size.z;
                bool point2InBounds = point2.x >= 0 && point2.x < map.Size.x && point2.z >= 0 && point2.z < map.Size.z;
                
                IntVec3 resultStart, resultEnd;
                
                if (point1InBounds && !point2InBounds)
                {
                    // point1在地图内，point2在地图外，从point1飞向边界交点
                    resultStart = point1;
                    resultEnd = intersectionCell;
                }
                else if (!point1InBounds && point2InBounds)
                {
                    // point1在地图外，point2在地图内，从边界交点飞向point2
                    resultStart = intersectionCell;
                    resultEnd = point2;
                }
                else
                {
                    // 两个点都在地图外或都在地图内，使用边界交点和用户起点
                    resultStart = intersectionCell;
                    resultEnd = point1;
                }
                
                // 检查两个点是否太接近
                float resultDistance = Vector3.Distance(resultStart.ToVector3Shifted(), resultEnd.ToVector3Shifted());
                if (resultDistance < 1.0f)
                {
                    // 如果太接近，扩展resultEnd
                    Vector3 extendDir = (point2 - point1).ToVector3();
                    if (extendDir.magnitude < 0.001f)
                    {
                        extendDir = Vector3.right;
                    }
                    else
                    {
                        extendDir = extendDir.normalized;
                    }
                    
                    resultEnd = new IntVec3(
                        Mathf.Clamp(resultStart.x + Mathf.RoundToInt(extendDir.x * 10), 0, map.Size.x - 1),
                        0,
                        Mathf.Clamp(resultStart.z + Mathf.RoundToInt(extendDir.z * 10), 0, map.Size.z - 1)
                    );
                }
                
                return (resultStart, resultEnd);
            }
            else
            {
                // 如果找不到交点（线段完全在地图内或完全在地图外）
                // 使用用户选择的点，但确保它们在地图范围内
                IntVec3 safePoint1 = new IntVec3(
                    Mathf.Clamp(point1.x, 0, map.Size.x - 1),
                    0,
                    Mathf.Clamp(point1.z, 0, map.Size.z - 1)
                );
                IntVec3 safePoint2 = new IntVec3(
                    Mathf.Clamp(point2.x, 0, map.Size.x - 1),
                    0,
                    Mathf.Clamp(point2.z, 0, map.Size.z - 1)
                );
                
                // 如果两个点相同或太接近，扩展其中一个点
                float safeDistance = Vector3.Distance(safePoint1.ToVector3Shifted(), safePoint2.ToVector3Shifted());
                if (safeDistance < 1.0f)
                {
                    // 计算用户方向
                    Vector3 userDir = (point2 - point1).ToVector3();
                    if (userDir.magnitude < 0.001f)
                    {
                        // 如果用户方向无效，使用默认方向（向右）
                        userDir = Vector3.right;
                    }
                    else
                    {
                        userDir = userDir.normalized;
                    }
                    
                    // 沿着用户方向扩展safePoint2
                    safePoint2 = new IntVec3(
                        Mathf.Clamp(safePoint1.x + Mathf.RoundToInt(userDir.x * 10), 0, map.Size.x - 1),
                        0,
                        Mathf.Clamp(safePoint1.z + Mathf.RoundToInt(userDir.z * 10), 0, map.Size.z - 1)
                    );
                }
                
                return (safePoint1, safePoint2);
            }
        }

        /// <summary>
        /// 在用户选择的线段上计算效果点
        /// </summary>
        private List<IntVec3> CalculateEffectPointsOnUserLine(IntVec3 userStart, IntVec3 userEnd, AerialSupportTypeDef supportType)
        {
            List<IntVec3> effectPoints = new List<IntVec3>();

            // 根据组件配置决定效果点数量
            var bombingProps = CompAerialSupportEffect_CustomLineBombing.GetBombingProps(supportType);
            int numPoints = bombingProps?.explosionCount ?? 5;

            // 在用户线段上均匀分布效果点
            for (int i = 0; i < numPoints; i++)
            {
                float t = (float)i / (numPoints - 1); // 包括起点和终点
                IntVec3 effectPoint = userStart + ((userEnd - userStart).ToVector3() * t).ToIntVec3();

                // 确保在地图范围内
                if (effectPoint.InBounds(map))
                {
                    effectPoints.Add(effectPoint);
                }
            }

            return effectPoints;
        }

        /// <summary>
        /// 启动自定义直线飞行（内部实现）
        /// </summary>
        private void StartCustomLineFlightInternal(IntVec3 flightStart, IntVec3 flightEnd, List<IntVec3> effectPoints, AerialSupportTypeDef supportType, IntVec3 userStart, IntVec3 userEnd)
        {
            // 检查飞行起点和终点是否太接近（可能导致distance=0）
            float flightDistance = Vector3.Distance(flightStart.ToVector3Shifted(), flightEnd.ToVector3Shifted());
            if (flightDistance < 1.0f)
            {
                // 计算用户方向
                Vector3 userDirection = (userEnd - userStart).ToVector3();
                if (userDirection.magnitude < 0.001f)
                    userDirection = Vector3.right;
                else
                    userDirection = userDirection.normalized;
                
                // 沿着用户方向扩展flightEnd至少10格
                IntVec3 extendedEnd = new IntVec3(
                    Mathf.Clamp(flightStart.x + Mathf.RoundToInt(userDirection.x * 10), 0, map.Size.x - 1),
                    0,
                    Mathf.Clamp(flightStart.z + Mathf.RoundToInt(userDirection.z * 10), 0, map.Size.z - 1)
                );
                
                // 如果扩展后的终点仍然太接近，尝试垂直方向
                if (Vector3.Distance(flightStart.ToVector3Shifted(), extendedEnd.ToVector3Shifted()) < 1.0f)
                {
                    Vector3 perpendicularDir = new Vector3(-userDirection.z, 0, userDirection.x);
                    extendedEnd = new IntVec3(
                        Mathf.Clamp(flightStart.x + Mathf.RoundToInt(perpendicularDir.x * 10), 0, map.Size.x - 1),
                        0,
                        Mathf.Clamp(flightStart.z + Mathf.RoundToInt(perpendicularDir.z * 10), 0, map.Size.z - 1)
                    );
                }
                
                flightEnd = extendedEnd;
            }
            
            // 创建自定义直线飞行
            var flight = new CustomLineFlight(flightStart, flightEnd, effectPoints, supportType, this, userStart, userEnd);
            activeFlights.Add(flight);
        }

        /// <summary>
        /// 启动新的飞机飞行（仅渲染）
        /// </summary>
        public void StartFlight(IntVec3 startPos, IntVec3 targetPos, AerialSupportTypeDef supportType)
        {
            var flight = new AircraftFlight(startPos, targetPos, supportType, this);
            activeFlights.Add(flight);
            // 与 StartFlight(AircraftFlight) 一致：触发飞机出现时的 Spawn Effects（如静音等）
            TriggerSpawnEffects(flight);
        }

        /// <summary>
        /// 由AircraftFlight调用的内部方法，当飞机到达目标时执行效果
        /// 使用反射动态调用效果组件，支持通过XML配置接入新效果，无需修改框架代码
        /// </summary>
        internal void NotifyTargetReached(IntVec3 targetPos, AerialSupportTypeDef supportType)
        {
            // 执行所有配置的效果组件
            if (supportType?.effectComps == null)
            {
                return;
            }

            foreach (var compProps in supportType.effectComps)
            {
                try
                {
                    Type compType = compProps.compClass;
                    if (compType == null)
                    {
                        Log.Warning($"[DMS_Legion] 效果组件类型为空: {compProps.GetType().Name}");
                        continue;
                    }

                    // 核打击白屏+耳鸣：若设置关闭则跳过执行
                    if (compProps is CompProperties_AerialSupportEffect_WhiteScreenTinnitus &&
                        (DMS_Legion.DMSL_ModSettings.settings == null || !DMS_Legion.DMSL_ModSettings.settings.playNuclearStrikeAudioVisual))
                        continue;

                    // 性能优化：使用缓存避免重复查找 MethodInfo
                    if (!cachedExecuteMethods.TryGetValue(compType, out MethodInfo? executeMethod))
                    {
                        // 方法查找策略（按优先级）：
                        // 1. 优先查找静态方法 ExecuteEffect（标准接口）
                        // 2. 查找静态方法 ExecuteMessageEffect（消息效果专用，向后兼容）
                        // 3. 查找实例方法 ExecuteEffect（向后兼容）
                        
                        string[] methodNames = { "ExecuteEffect", "ExecuteMessageEffect" };
                        Type propsType = compProps.GetType();
                        
                        // 尝试查找静态方法
                        foreach (string methodName in methodNames)
                        {
                            executeMethod = compType.GetMethod(methodName,
                                BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod,
                                null,
                                new Type[] { typeof(IntVec3), typeof(AerialSupportTypeDef), typeof(Map), propsType },
                                null);
                            
                            if (executeMethod != null)
                            {
                                break;
                            }
                        }

                        // 如果找不到静态方法，尝试查找实例方法（向后兼容）
                        if (executeMethod == null)
                        {
                            executeMethod = compType.GetMethod("ExecuteEffect",
                                BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod,
                                null,
                                new Type[] { typeof(IntVec3), typeof(AerialSupportTypeDef), typeof(Map) },
                                null);
                        }

                        // 缓存 MethodInfo（使用线程安全的 TryAdd，避免重复添加）
                        if (executeMethod != null)
                        {
                            // 使用 TryAdd 确保线程安全，如果已存在则使用现有值
                            cachedExecuteMethods.TryAdd(compType, executeMethod);
                        }
                        else
                        {
                            // 未找到方法，记录警告并跳过
                            Log.Warning($"[DMS_Legion] 未找到效果组件的 ExecuteEffect 方法: {compType.Name}");
                            continue;
                        }
                    }

                    // 执行方法调用
                    if (executeMethod != null)
                    {
                        if (executeMethod.IsStatic)
                        {
                            // 静态方法调用：直接传入参数
                            executeMethod.Invoke(null, new object[] { targetPos, supportType, map, compProps });
                        }
                        else
                        {
                            // 实例方法调用（向后兼容）
                            var compInstance = Activator.CreateInstance(compType);
                            if (compInstance == null)
                            {
                                Log.Error($"[DMS_Legion] 无法创建效果组件实例: {compType.Name}");
                                continue;
                            }
                            
                            // 设置props字段（如果存在）
                            var propsField = compType.GetField("props", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            if (propsField != null)
                            {
                                propsField.SetValue(compInstance, compProps);
                            }
                            
                            executeMethod.Invoke(compInstance, new object[] { targetPos, supportType, map });
                        }
                    }
                }
                catch (TargetInvocationException ex)
                {
                    // 反射调用时，实际异常被包装在TargetInvocationException中
                    Log.Error($"[DMS_Legion] 执行效果组件失败: {compProps.GetType().Name} - {ex.InnerException?.Message ?? ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Log.Error($"[DMS_Legion] 内部异常堆栈: {ex.InnerException.StackTrace}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[DMS_Legion] 执行效果组件失败: {compProps.GetType().Name} - {ex.Message}");
                    Log.Error($"[DMS_Legion] 异常堆栈: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// 开始执行间隔轰炸序列
        /// </summary>
        public void StartBombingSequence(List<IntVec3> explosionPositions, CompProperties_AerialSupportEffect_Bombing props, AerialSupportTypeDef supportType, IntVec3 targetPos)
        {
            var sequence = new BombingSequence(explosionPositions, props, supportType, targetPos, this.map);
            activeBombingSequences.Add(sequence);
        }

    }

    /*
     * ===========================================
     *          空中支援Flight体系设计
     * ===========================================
     *
     * 设计原则：
     * 1. Renderer只调用flight.Tick()，不感知具体行为差异
     * 2. 所有节奏差异完全封装在Flight子类中
     * 3. 普通/自定义直线/多目标支援使用独立的Flight子类
     * 4. 不允许在Renderer/Coordinator中写"路径执行逻辑"
     *
     * Flight子类职责划分：
     * - AircraftFlight：普通支援，直线飞行+一次性效果
     * - CustomLineFlight：自定义直线支援，直线飞行+连续轰炸
     * - MultiTargetFlight：多目标支援，原地间隔执行
     */

    /// <summary>
    /// 普通支援飞行类 - 直线飞行到目标点并执行一次性效果
    /// 作为Flight体系的基类，提供通用飞行状态管理
    ///
    /// 行为特点：
    /// - 直线飞行路径
    /// - 到达目标点时执行一次性效果
    /// - 支持尾气等视觉效果
    /// </summary>
    public class AircraftFlight : IExposable
    {
        public Vector3 startPos;
        public Vector3 endPos;
        protected Vector3 direction;
        protected float distance;
        public float progress;
        public AerialSupportTypeDef? supportType;
        public AerialSupportRenderer? renderer;
        public bool targetReached;
        protected bool soundPlayed;
        protected int startDrawFrame; // 开始绘制时的游戏帧数，用于计算实际经过的游戏时间

        // 无参数构造函数，用于序列化系统
        public AircraftFlight()
        {
        }

        public AircraftFlight(IntVec3 start, IntVec3 target, AerialSupportTypeDef type, AerialSupportRenderer parentRenderer)
        {
            startPos = start.ToVector3Shifted();
            endPos = target.ToVector3Shifted();
            supportType = type;
            renderer = parentRenderer;
            targetReached = false;
            soundPlayed = false;
            startDrawFrame = -1; // -1表示还未开始绘制

            // 计算飞行方向和距离
            direction = (endPos - startPos).normalized;
            distance = Vector3.Distance(startPos, endPos);
            progress = 0f;
        }

        /// <summary>
        /// 多目标支援的构造函数（不预设固定目标）
        /// </summary>
        protected AircraftFlight(AerialSupportTypeDef type, AerialSupportRenderer parentRenderer)
        {
            supportType = type;
            renderer = parentRenderer;
            targetReached = false;
            soundPlayed = false;
            startDrawFrame = -1; // -1表示还未开始绘制

            // 多目标支援的startPos和endPos将在子类中动态设置
            startPos = Vector3.zero;
            endPos = Vector3.zero;
            direction = Vector3.forward;
            distance = 0f;
            progress = 0f;
        }

        /// <summary>
        /// 保存/加载飞行实例数据
        /// </summary>
        public virtual void ExposeData()
        {
            // 保存基本位置和状态信息
            Scribe_Values.Look(ref startPos, "startPos");
            Scribe_Values.Look(ref endPos, "endPos");
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref distance, "distance");
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref targetReached, "targetReached");
            Scribe_Values.Look(ref soundPlayed, "soundPlayed");
            Scribe_Values.Look(ref startDrawFrame, "startDrawFrame");

            // 保存支援类型定义
            Scribe_Defs.Look(ref supportType, "supportType");

            // renderer引用将在首次Tick/Draw时通过延迟初始化设置
        }

        /// <summary>
        /// 生成尾气效果（由子类实现）
        /// 使用"每tick数量"模型控制尾气密度，通过概率补偿实现非整数粒子数
        /// </summary>
        protected virtual void GenerateExhaust()
        {
            // 安全性检查：确保必要字段已初始化
            if (renderer == null || supportType == null)
            {
                return;
            }

            // 总开关检查：如果未启用尾气，直接返回，不进行任何计算
            if (!supportType.enableExhaust)
            {
                return;
            }

            // 生成率检查：本tick是否执行尾气逻辑
            if (Rand.Value >= supportType.exhaustSpawnRate)
            {
                return;
            }

            // 密度检查：如果粒子数<=0，不生成任何尾气
            if (supportType.exhaustParticlesPerTick <= 0f)
            {
                return;
            }

            // 到达检查：如果不继续绘制且已到达目标，不生成尾气
            if (!supportType.continueDrawingAfterArrival && targetReached)
            {
                return;
            }

            // 位置计算：计算飞机当前位置
            Vector3 currentPos = startPos + direction * distance * progress;
            Map map = renderer.map;

            // 计算地面投影位置：将飞机位置压到地面
            IntVec3 groundCell = currentPos.ToIntVec3();
            Vector3 groundPos = groundCell.ToVector3Shifted();

            // 使用概率补偿模型计算本tick应生成的粒子数
            // baseCount: 保证生成的粒子数
            // remainder: 额外生成1个的概率
            int baseCount = Mathf.FloorToInt(supportType.exhaustParticlesPerTick);
            float remainder = supportType.exhaustParticlesPerTick - baseCount;

            // 计算总粒子数：baseCount + 可能的额外1个
            int totalParticles = baseCount;
            if (Rand.Value < remainder)
            {
                totalParticles += 1;
            }

            // 生成指定数量的尾气粒子
            for (int i = 0; i < totalParticles; i++)
            {
                // 尾气基础位置 = 地面投影位置 - 飞行方向向量 × 固定后移距离
                Vector3 exhaustOffset = -direction * 1.2f; // 后移1.2个单位

                // 添加随机横向扰动（视觉表现参数化）
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
                float lateralOffset = Rand.Range(-0.2f, 0.2f);
                exhaustOffset += perpendicular * lateralOffset;

                // 为多个粒子添加微小随机位置偏移，避免完全重叠
                if (totalParticles > 1)
                {
                    Vector3 randomOffset = new Vector3(
                        Rand.Range(-0.1f, 0.1f),
                        0f,
                        Rand.Range(-0.1f, 0.1f)
                    );
                    exhaustOffset += randomOffset;
                }

                Vector3 exhaustPos = groundPos + exhaustOffset;

                // 可见性检查（参考原版ThrowSmoke）
                if (exhaustPos.ShouldSpawnMotesAt(map, true))
                {
                    // 使用FleckMaker.GetDataStatic初始化（参考原版ThrowSmoke）
                    FleckCreationData data = FleckMaker.GetDataStatic(exhaustPos, map, FleckDefOf.Smoke, supportType.exhaustBaseScale);

                    // 设置运动参数（完全参数化）
                    float baseAngle = direction.AngleFlat() + 180f; // 飞行方向的反向
                    data.velocityAngle = baseAngle + Rand.Range(-supportType.exhaustAngleVariance, supportType.exhaustAngleVariance);
                    data.velocitySpeed = Rand.Range(supportType.exhaustMinSpeed, supportType.exhaustMaxSpeed);
                    data.rotationRate = Rand.Range(-supportType.exhaustRotationRange, supportType.exhaustRotationRange);

                    map.flecks.CreateFleck(data);
                }
            }
        }

        /// <summary>
        /// 普通支援Tick逻辑 - 直线飞行到目标点
        /// </summary>
        public virtual bool Tick()
        {
            // 延迟初始化renderer引用
            if (renderer == null)
            {
                var currentMap = Find.CurrentMap;
                if (currentMap == null)
                {
                    Log.Error("[DMS_Legion] 延迟初始化失败：CurrentMap为null");
                    return true;
                }

                renderer = currentMap.GetComponent<AerialSupportRenderer>();
                if (renderer == null)
                {
                    Log.Error("[DMS_Legion] 延迟初始化失败：未找到AerialSupportRenderer");
                    return true;
                }
            }

            // 超时检查（1200游戏帧 ≈ 20秒）
            if (startDrawFrame >= 0 && Time.frameCount - startDrawFrame > 1200)
            {
                Log.Warning("[DMS_Legion] 绘制超时，强制结束");
                return true;
            }

            // 普通支援：直线飞行逻辑
            // 每次tick前进
            progress += supportType?.flightSpeed ?? 0f;

            // 检查是否到达目标点
            if (!targetReached)
            {
                Vector3 currentPos = startPos + direction * distance * progress;
                Vector3 targetPos2D = new Vector3(endPos.x, currentPos.y, endPos.z);

                // 使用距离检查而不是progress >= 1f，更准确
                if (supportType != null && Vector3.Distance(currentPos, targetPos2D) <= supportType.flightSpeed * distance)
                {
                    targetReached = true;
                    // 通知渲染器到达目标，触发效果
                    renderer?.NotifyTargetReached(endPos.ToIntVec3(), supportType);
                }
            }

            // 生成尾气效果
            GenerateExhaust();

            // 检查是否飞出地图边界
            Vector3 currentPosCheck = startPos + direction * distance * progress;
            if (renderer?.map is Map map &&
                (currentPosCheck.x < -0.5f || currentPosCheck.x > map.Size.x - 0.5f ||
                 currentPosCheck.z < -0.5f || currentPosCheck.z > map.Size.z - 0.5f))
            {
                return true; // 飞出地图，结束飞行
            }

            return false;
        }

        /// <summary>
        /// 渲染飞机
        /// </summary>
        public virtual void Draw()
        {
            // 安全性检查：确保renderer已初始化
            if (renderer == null)
            {
                return; // renderer未初始化，跳过绘制
            }

            // 记录开始绘制时的帧数（只在第一次绘制时设置）
            // 音效播放已移至协调器管理，飞行实例只负责绘制
            Vector3 currentPos = startPos + direction * distance * progress;
            currentPos.y = AltitudeLayer.MetaOverlays.AltitudeFor();

            // 计算飞行方向并确定贴图旋转
            Vector2 startPos2D = new Vector2(startPos.x, startPos.z);
            Vector2 endPos2D = new Vector2(endPos.x, endPos.z);
            Vector2 flightDirection = (endPos2D - startPos2D).normalized;

            // 计算飞行角度（从正东开始逆时针）
            float aimAngle = Mathf.Atan2(flightDirection.x, flightDirection.y) * Mathf.Rad2Deg;
            if (aimAngle < 0f)
            {
                aimAngle += 360f;
            }

            // 贴图旋转：假设贴图默认朝向是正东（0°），我们需要让它朝向飞行方向
            // Quaternion.AngleAxis使用的是从Y轴正方向开始的逆时针旋转
            float rotationAngle = aimAngle;
            rotationAngle %= 360f;

            // 绕Y轴旋转
            Quaternion rotation = Quaternion.AngleAxis(rotationAngle, Vector3.up);

            // 渲染飞机贴图
            if (supportType?.AircraftMat != null)
            {
                GenDraw.DrawQuad(supportType.AircraftMat, currentPos, rotation, supportType.drawSize, null);
            }
        }

        /// <summary>

        /// <summary>
        /// 获取当前飞行进度（0-1之间的值）
        /// </summary>
        public float Progress => Mathf.Clamp01(progress);

        /// <summary>
        /// 获取当前飞机位置
        /// </summary>
        public Vector3 CurrentPosition => startPos + direction * distance * progress;
    }

        /// <summary>
        /// 轰炸序列管理器，用于实现间隔爆炸
        /// </summary>
        public class BombingSequence : IExposable
        {
            private List<IntVec3>? explosionPositions;
            private CompProperties_AerialSupportEffect_Bombing? props;
            private AerialSupportTypeDef? supportType;
            private IntVec3 targetPos;
            private Map? map;
            private int currentExplosionIndex;

            // 无参数构造函数，用于序列化系统
            public BombingSequence()
            {
            }

            public BombingSequence(List<IntVec3> positions, CompProperties_AerialSupportEffect_Bombing bombingProps,
                                  AerialSupportTypeDef type, IntVec3 target, Map map)
            {
                explosionPositions = positions;
                props = bombingProps;
                supportType = type;
                targetPos = target;
                this.map = map;
                currentExplosionIndex = 0;
            }

            /// <summary>
            /// 检查轰炸序列是否对指定地图有效
            /// 注意：此方法在加载时可能返回false，因为map通过延迟初始化设置
            /// </summary>
            public bool IsValidForMap(Map targetMap)
            {
                // 如果map还未初始化，认为暂时有效（将在首次Tick时初始化）
                if (map == null)
                {
                    return true;
                }
                return map == targetMap;
            }

            /// <summary>
            /// 保存/加载轰炸序列数据
            /// </summary>
            public void ExposeData()
            {
                // 保存爆炸位置列表
                Scribe_Collections.Look(ref explosionPositions, "explosionPositions", LookMode.Value);

                // 保存属性和类型
                Scribe_Deep.Look(ref props, "props");
                Scribe_Defs.Look(ref supportType, "supportType");

                // 保存位置和状态
                Scribe_Values.Look(ref targetPos, "targetPos");
                Scribe_Values.Look(ref currentExplosionIndex, "currentExplosionIndex");

                // map引用将在首次Tick时通过延迟初始化设置
            }

        /// <summary>
        /// 更新轰炸序列，返回是否完成（参考CataFlameBomb实现）
        /// </summary>
        public bool Tick()
        {
            // 延迟初始化：确保map引用正确设置
            if (map == null)
            {
                var currentMap = Find.CurrentMap;
                if (currentMap == null)
                {
                    Log.Error("[DMS_Legion] BombingSequence延迟初始化失败：CurrentMap为null");
                    return true; // 初始化失败，结束轰炸序列
                }

                var aerialRenderer = currentMap.GetComponent<AerialSupportRenderer>();
                if (aerialRenderer != null)
                {
                    map = aerialRenderer.map;
                }
                else
                {
                    Log.Error("[DMS_Legion] BombingSequence延迟初始化失败：未找到AerialSupportRenderer组件");
                    return true; // 初始化失败，结束轰炸序列
                }
            }

            // 安全性检查：确保必要字段已初始化
            if (explosionPositions == null || props == null || map == null)
            {
                Log.Error("[DMS_Legion] BombingSequence字段未初始化");
                return true; // 结束序列
            }

            // 参考CataFlameBomb：每10 ticks检查一次是否产生爆炸
            if (Find.TickManager.TicksGame % 10 == 0 && currentExplosionIndex < explosionPositions.Count)
            {
                // 每次产生指定数量的爆炸（可通过XML配置）
                int explosionsThisTick = Mathf.Min(props.explosionsPerTick, explosionPositions.Count - currentExplosionIndex);

                for (int i = 0; i < explosionsThisTick; i++)
                {
                    IntVec3 explosionPos = explosionPositions[currentExplosionIndex];
                    CompAerialSupportEffect_Bombing.ExecuteSingleExplosion(explosionPos, map, props);
                    currentExplosionIndex++;
                }
            }


            // 我们设置为足够执行完所有爆炸的时间
            return currentExplosionIndex >= explosionPositions.Count;
        }
    }

    /// <summary>
    /// 自定义直线飞行类
    /// </summary>
    /// <summary>
    /// 自定义直线支援飞行类 - 沿玩家指定直线飞行并连续轰炸
    ///
    /// 行为特点：
    /// - 沿玩家选择的起点到终点的直线飞行
    /// - 在飞行路径上连续执行爆炸效果
    /// - 根据用户选择的线段计算爆炸位置
    /// - 飞行完成后所有爆炸必须执行完毕
    /// </summary>
    public class CustomLineFlight : AircraftFlight
    {
        private List<IntVec3>? effectPoints;
        private HashSet<int> executedPoints = new HashSet<int>();
        private int flightTickCounter = 0; // 飞行tick计数器
        private IntVec3 userStartPoint; // 用户选择的起点
        private IntVec3 userEndPoint;   // 用户选择的终点
        private float originalDistance; // 原始距离（用于保持飞行速度）

        public IntVec3 UserStartPoint => userStartPoint;
        public IntVec3 UserEndPoint => userEndPoint;
        private float userLineStartProgress; // 用户线段起点在飞行路径中的进度
        private float userLineEndProgress;   // 用户线段终点在飞行路径中的进度

        // 通用效果组件状态存储（键值对字典）
        // 键：状态字段名称（如 "cachedExplosionPositions", "lastFireProgress"）
        // 值：状态值（可以是任何类型）
        // 这样框架就不需要知道具体效果组件的实现细节，其他开发者可以自由添加新效果组件
        private Dictionary<string, object>? effectComponentStates;

        // 无参数构造函数，用于序列化系统
        public CustomLineFlight()
        {
        }

        public CustomLineFlight(IntVec3 flightStart, IntVec3 flightEnd, List<IntVec3> effectPoints, AerialSupportTypeDef supportType, AerialSupportRenderer renderer, IntVec3 userStart, IntVec3 userEnd)
            : base(flightStart, flightEnd, supportType, renderer)
        {
            this.effectPoints = effectPoints;
            this.userStartPoint = userStart;
            this.userEndPoint = userEnd;
            
            // 保存原始距离（基于用户选择的点），用于保持飞行速度一致性
            Vector3 userStartVec = userStart.ToVector3Shifted();
            Vector3 userEndVec = userEnd.ToVector3Shifted();
            this.originalDistance = Vector3.Distance(userStartVec, userEndVec);

            // 验证并修复初始状态
            if (renderer?.map != null)
            {
                Map map = renderer.map;
                
                // 检查起点和终点是否在地图范围内（允许边界上的点）
                if ((flightStart.x < -1 || flightStart.x > map.Size.x || flightStart.z < -1 || flightStart.z > map.Size.z) ||
                    (flightEnd.x < -1 || flightEnd.x > map.Size.x || flightEnd.z < -1 || flightEnd.z > map.Size.z))
                {
                    Log.Warning($"[DMS_Legion] 飞行起点或终点超出合理范围: start={flightStart}, end={flightEnd}，强制结束");
                    targetReached = true;
                    return;
                }
                
                // 检查并修复direction和distance
                if (direction == Vector3.zero || direction.magnitude < 0.001f || distance < 0.1f)
                {
                    Vector3 newDirection = (endPos - startPos);
                    float newDistance = newDirection.magnitude;
                    
                    if (newDistance < 0.1f)
                    {
                        // 使用原始距离保持飞行速度一致性
                        if (originalDistance < 0.1f)
                        {
                            // 原始距离也太短，使用用户方向扩展
                            Vector3 userDir = (userEndPoint - userStartPoint).ToVector3();
                            if (userDir.magnitude < 0.001f)
                                userDir = Vector3.right;
                            else
                                userDir = userDir.normalized;
                            
                            endPos = startPos + userDir * 10f;
                            newDirection = (endPos - startPos);
                            newDistance = newDirection.magnitude;
                        }
                        else
                        {
                            // 使用原始距离，保持方向
                            newDirection = (endPos - startPos);
                            if (newDirection.magnitude < 0.001f)
                                newDirection = (userEndVec - userStartVec).normalized;
                            else
                                newDirection = newDirection.normalized;
                            
                            endPos = startPos + newDirection * originalDistance;
                            newDistance = originalDistance;
                        }
                    }
                    
                    direction = newDirection.normalized;
                    distance = newDistance;
                }
                
                // 最终验证：确保direction有效
                if (direction == Vector3.zero || direction.magnitude < 0.001f || distance < 0.1f)
                {
                    direction = Vector3.forward;
                    distance = 10f;
                }
            }

            // 计算用户线段在整个飞行路径中的进度范围
            CalculateUserLineProgressRange();

            // 初始化通用状态存储
            effectComponentStates = new Dictionary<string, object>();

            // 调用效果组件的初始化方法
            InitializeEffectComponents();
        }

        /// <summary>
        /// 保存/加载CustomLineFlight特定数据
        /// </summary>
        public override void ExposeData()
        {
            // 先调用基类的序列化
            base.ExposeData();

            // 序列化CustomLineFlight特有的字段
            Scribe_Collections.Look(ref effectPoints, "effectPoints", LookMode.Value);
            Scribe_Values.Look(ref userStartPoint, "userStartPoint");
            Scribe_Values.Look(ref userEndPoint, "userEndPoint");
            Scribe_Values.Look(ref userLineStartProgress, "userLineStartProgress");
            Scribe_Values.Look(ref userLineEndProgress, "userLineEndProgress");
            Scribe_Values.Look(ref flightTickCounter, "flightTickCounter");
            Scribe_Values.Look(ref originalDistance, "originalDistance", 0f);

            // 序列化通用效果组件状态存储
            // 注意：Dictionary<string, object>的序列化比较复杂，这里使用简化方案：
            // 1. 只序列化支持的类型（List<IntVec3>, HashSet<int>, float, int）
            // 2. 其他类型在PostLoadInit时由效果组件重新初始化
            // 这样可以保持框架的通用性，同时支持基本的序列化需求
            
            // 序列化常见类型的状态字段（向后兼容）
            if (effectComponentStates != null)
            {
                // List<IntVec3> cachedExplosionPositions
                if (effectComponentStates.TryGetValue("cachedExplosionPositions", out var cachedPos) && cachedPos is List<IntVec3> cachedList)
                {
                    Scribe_Collections.Look(ref cachedList, "cachedExplosionPositions", LookMode.Value);
                    effectComponentStates["cachedExplosionPositions"] = cachedList ?? new List<IntVec3>();
                }
                else
                {
                    List<IntVec3>? tempList = null;
                    Scribe_Collections.Look(ref tempList, "cachedExplosionPositions", LookMode.Value);
                    if (tempList != null)
                    {
                        effectComponentStates["cachedExplosionPositions"] = tempList;
                    }
                }
                
                // HashSet<int> executedExplosionIndices
                if (effectComponentStates.TryGetValue("executedExplosionIndices", out var execIndices) && execIndices is HashSet<int> execSet)
                {
                    Scribe_Collections.Look(ref execSet, "executedExplosionIndices", LookMode.Value);
                    effectComponentStates["executedExplosionIndices"] = execSet ?? new HashSet<int>();
                }
                else
                {
                    HashSet<int>? tempSet = null;
                    Scribe_Collections.Look(ref tempSet, "executedExplosionIndices", LookMode.Value);
                    if (tempSet != null)
                    {
                        effectComponentStates["executedExplosionIndices"] = tempSet;
                    }
                }
                
                // float lastFireProgress
                if (effectComponentStates.TryGetValue("lastFireProgress", out var lastProgress) && lastProgress is float lastProg)
                {
                    Scribe_Values.Look(ref lastProg, "lastFireProgress", -1f);
                    effectComponentStates["lastFireProgress"] = lastProg;
                }
                else
                {
                    float tempFloat = -1f;
                    Scribe_Values.Look(ref tempFloat, "lastFireProgress", -1f);
                    effectComponentStates["lastFireProgress"] = tempFloat;
                }
                
                // int firedBulletCount
                if (effectComponentStates.TryGetValue("firedBulletCount", out var firedCount) && firedCount is int firedCnt)
                {
                    Scribe_Values.Look(ref firedCnt, "firedBulletCount", 0);
                    effectComponentStates["firedBulletCount"] = firedCnt;
                }
                else
                {
                    int tempInt = 0;
                    Scribe_Values.Look(ref tempInt, "firedBulletCount", 0);
                    effectComponentStates["firedBulletCount"] = tempInt;
                }
            }
            else
            {
                // 向后兼容：如果字典为null，尝试从旧格式加载
                List<IntVec3>? tempList = null;
                HashSet<int>? tempSet = null;
                float tempFloat = -1f;
                int tempInt = 0;
                
                Scribe_Collections.Look(ref tempList, "cachedExplosionPositions", LookMode.Value);
                Scribe_Collections.Look(ref tempSet, "executedExplosionIndices", LookMode.Value);
                Scribe_Values.Look(ref tempFloat, "lastFireProgress", -1f);
                Scribe_Values.Look(ref tempInt, "firedBulletCount", 0);
                
                if (tempList != null || tempSet != null || tempFloat != -1f || tempInt != 0)
                {
                    effectComponentStates = new Dictionary<string, object>();
                    if (tempList != null) effectComponentStates["cachedExplosionPositions"] = tempList;
                    if (tempSet != null) effectComponentStates["executedExplosionIndices"] = tempSet;
                    effectComponentStates["lastFireProgress"] = tempFloat;
                    effectComponentStates["firedBulletCount"] = tempInt;
                }
            }

            // 加载后重新计算派生数据
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 重新计算用户线段进度范围
                CalculateUserLineProgressRange();

                // 确保状态字典不为null
                if (effectComponentStates == null)
                {
                    effectComponentStates = new Dictionary<string, object>();
                }

                // 兜底恢复：重新初始化效果组件（效果组件会重新设置所需的状态）
                InitializeEffectComponents();
                
                // 如果originalDistance未加载，重新计算
                if (originalDistance < 0.1f && userStartPoint != IntVec3.Zero && userEndPoint != IntVec3.Zero)
                {
                    Vector3 userStartVec = userStartPoint.ToVector3Shifted();
                    Vector3 userEndVec = userEndPoint.ToVector3Shifted();
                    originalDistance = Vector3.Distance(userStartVec, userEndVec);
                }
            }
        }

        /// <summary>
        /// 计算用户线段在飞行路径中的进度范围
        /// </summary>
        private void CalculateUserLineProgressRange()
        {
            Vector3 flightStart = startPos;
            Vector3 flightEnd = endPos;
            float flightLength = Vector3.Distance(flightStart, flightEnd);

            if (flightLength < 0.1f)
            {
                // 如果飞行距离太短，设置默认值
                userLineStartProgress = 0.2f;
                userLineEndProgress = 0.8f;
                return;
            }

            // 使用已经计算好的direction（如果有效），否则重新计算
            Vector3 flightDirection = direction;
            if (flightDirection == Vector3.zero || flightDirection.magnitude < 0.001f)
            {
                flightDirection = (flightEnd - flightStart).normalized;
                if (flightDirection == Vector3.zero || flightDirection.magnitude < 0.001f)
                {
                    // 如果仍然无效，设置默认值
                    userLineStartProgress = 0.2f;
                    userLineEndProgress = 0.8f;
                    return;
                }
            }

            // 计算用户起点在飞行路径上的投影
            Vector3 toUserStart = userStartPoint.ToVector3() - flightStart;
            float projectionLength = Vector3.Dot(toUserStart, flightDirection);
            userLineStartProgress = Mathf.Clamp01(projectionLength / flightLength);

            // 计算用户终点在飞行路径上的投影
            Vector3 toUserEnd = userEndPoint.ToVector3() - flightStart;
            projectionLength = Vector3.Dot(toUserEnd, flightDirection);
            userLineEndProgress = Mathf.Clamp01(projectionLength / flightLength);

            // 确保起点进度小于终点进度
            if (userLineStartProgress > userLineEndProgress)
            {
                float temp = userLineStartProgress;
                userLineStartProgress = userLineEndProgress;
                userLineEndProgress = temp;
            }
            
            // 保险机制：如果进度范围无效（起点和终点进度相同或非常接近），设置默认值
            if (Mathf.Abs(userLineEndProgress - userLineStartProgress) < 0.01f)
            {
                userLineStartProgress = 0.2f;
                userLineEndProgress = 0.8f;
            }
        }

        /// <summary>
        /// 初始化效果组件（通过反射调用）
        /// </summary>
        private void InitializeEffectComponents()
        {
            if (supportType?.effectComps != null)
            {
                foreach (var compProps in supportType.effectComps)
                {
                    try
                    {
                        Type compType = compProps.compClass;
                        if (compType == null) continue;

                        // 查找初始化方法（可选，不是所有效果组件都需要）
                        MethodInfo initMethod = compType.GetMethod("InitializeExplosionPositions",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new Type[] { typeof(IntVec3), typeof(IntVec3), compProps.GetType(), typeof(List<IntVec3>).MakeByRefType() },
                            null);

                        if (initMethod != null)
                        {
                            // 调用初始化方法
                            List<IntVec3> explosionPositions = new List<IntVec3>();
                            object[] initArgs = new object[] { userStartPoint, userEndPoint, compProps, explosionPositions };
                            initMethod.Invoke(null, initArgs);

                            // 获取out参数的值，存储到通用状态字典
                            if (initArgs[3] is List<IntVec3> positions)
                            {
                                SetStateValue("cachedExplosionPositions", positions);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[DMS_Legion] 初始化效果组件失败: {compProps.GetType().Name} - {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 渲染飞机（CustomLineFlight版本）
        /// </summary>
        public new void Draw()
        {
            // 安全性检查：确保renderer已初始化
            if (renderer == null)
            {
                return; // renderer未初始化，跳过绘制
            }

            // 音效播放已移至协调器管理，飞行实例只负责绘制
            // 渲染飞机贴图
            if (supportType?.AircraftMat != null)
            {
                // 计算当前绘制位置
                Vector3 currentPos = startPos + direction * distance * progress;

                // 计算朝向（飞行方向）
                // 保险机制：如果direction为零向量，使用默认方向
                Vector3 safeDirection = direction;
                if (safeDirection == Vector3.zero || safeDirection.magnitude < 0.001f)
                {
                    safeDirection = Vector3.forward; // 使用默认方向
                }
                Quaternion rotation = Quaternion.LookRotation(safeDirection);

                // 计算绘制大小
                float drawSize = supportType.drawSize;

                // 渲染飞机贴图
                GenDraw.DrawQuad(supportType.AircraftMat, currentPos, rotation, drawSize, null);
            }

            // 注意：原版Projectile会自动绘制，不需要手动绘制
        }

        /// <summary>
        /// 自定义直线支援Tick逻辑 - 沿玩家指定直线飞行并连续轰炸
        /// </summary>
        public override bool Tick()
        {
            // 延迟初始化renderer引用
            if (renderer == null)
            {
                var currentMap = Find.CurrentMap;
                if (currentMap == null)
                {
                    Log.Error("[DMS_Legion] 延迟初始化失败：CurrentMap为null");
                    return true;
                }

                renderer = currentMap.GetComponent<AerialSupportRenderer>();
                if (renderer == null)
                {
                    Log.Error("[DMS_Legion] 延迟初始化失败：未找到AerialSupportRenderer");
                    return true;
                }
            }

            // 超时检查（1200游戏帧 ≈ 20秒）
            if (startDrawFrame >= 0 && Time.frameCount - startDrawFrame > 1200)
            {
                Log.Warning("[DMS_Legion] 绘制超时，强制结束");
                return true;
            }

            // 自定义直线支援：直线飞行 + 连续轰炸
            flightTickCounter++;
            progress += supportType?.flightSpeed ?? 0f;

            // 通知效果组件当前飞行状态（通过反射动态调用）
            // 效果组件会直接创建和发射Projectile，由RimWorld引擎自动管理
            NotifyEffectComponents(progress, userLineStartProgress, userLineEndProgress);

            // 检查是否到达目标点
            if (!targetReached)
            {
                Vector3 currentPos = startPos + direction * distance * progress;
                Vector3 targetPos2D = new Vector3(endPos.x, currentPos.y, endPos.z);

                if (supportType != null && Vector3.Distance(currentPos, targetPos2D) <= supportType.flightSpeed * distance)
                {
                    targetReached = true;
                }
            }

            // 生成尾气效果
            GenerateExhaust();

            // 检查是否飞出地图边界
            Vector3 currentPosCheck = startPos + direction * distance * progress;
            if (renderer?.map is Map map)
            {
                // 检查当前位置是否在地图范围外
                if (currentPosCheck.x < -0.5f || currentPosCheck.x > map.Size.x - 0.5f ||
                    currentPosCheck.z < -0.5f || currentPosCheck.z > map.Size.z - 0.5f)
                {
                    return true;
                }
                
                // 保险机制：如果direction为零向量或distance为0，尝试修复
                if (direction == Vector3.zero || direction.magnitude < 0.001f || distance < 0.1f)
                {
                    // 尝试重新计算direction和distance
                    Vector3 newDirection = (endPos - startPos);
                    float newDistance = newDirection.magnitude;
                    
                    if (newDistance < 0.1f)
                    {
                        // 如果距离确实太短，强制结束
                        Log.Warning($"[DMS_Legion] 飞行距离过短: distance={newDistance}，强制结束");
                        return true;
                    }
                    
                    // 修复direction和distance
                    direction = newDirection.normalized;
                    distance = newDistance;
                    
                    // 如果修复后仍然无效，使用默认值
                    if (direction == Vector3.zero || direction.magnitude < 0.001f)
                    {
                        Log.Warning("[DMS_Legion] 无法修复飞行方向，使用默认方向");
                        direction = Vector3.forward;
                        if (distance < 0.1f)
                        {
                            distance = 10f; // 设置默认距离
                        }
                    }
                }
            }

            return false;
        }

        // 静态缓存：所有 CustomLineFlight 实例共享，避免重复查找
        private static Dictionary<Type, MethodInfo> cachedUpdateMethods = new Dictionary<Type, MethodInfo>();

        /// <summary>
        /// 通知效果组件当前飞行状态（通过反射动态调用）
        /// </summary>
        private void NotifyEffectComponents(float progress, float startProgress, float endProgress)
        {
            if (supportType?.effectComps != null && renderer?.map != null)
            {
                foreach (var compProps in supportType.effectComps)
                {
                    try
                    {
                        // 通过反射动态查找并调用效果组件的 UpdateDuringFlight 静态方法
                        Type compType = compProps.compClass;
                        if (compType == null)
                        {
                            Log.Warning($"[DMS_Legion] 效果组件类型为空: {compProps.GetType().Name}");
                            continue;
                        }

                        // 性能优化：使用缓存避免重复查找 MethodInfo
                        // 首次调用：查找并缓存，后续调用：直接使用缓存
                        if (!cachedUpdateMethods.TryGetValue(compType, out MethodInfo updateMethod))
                        {
                            // 首次查找：查找 UpdateDuringFlight 静态方法
                            updateMethod = compType.GetMethod("UpdateDuringFlight",
                                BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);

                            if (updateMethod == null)
                            {
                                // 不是自定义直线效果组件，跳过
                                continue;
                            }

                            // 缓存 MethodInfo，后续调用直接使用
                            cachedUpdateMethods[compType] = updateMethod;
                        }

                        // 获取方法参数信息，动态构建参数数组
                        ParameterInfo[] parameters = updateMethod.GetParameters();
                        object[] methodArgs = BuildMethodArguments(parameters, compProps, progress,
                            startProgress, endProgress);

                        // 调用静态方法（使用缓存的 MethodInfo，性能接近直接调用）
                        updateMethod.Invoke(null, methodArgs);

                        // 处理ref参数：将修改后的值写回状态字段
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            var param = parameters[i];
                            if (param.ParameterType.IsByRef)
                            {
                                // 从参数数组中提取修改后的值
                                object modifiedValue = methodArgs[i];
                                
                                // 写回状态值到通用状态字典
                                Type actualType = param.ParameterType.GetElementType();
                                SetStateValue(param.Name, modifiedValue);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[DMS_Legion] 调用效果组件失败: {compProps.GetType().Name} - {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 根据方法参数信息动态构建参数数组
        /// </summary>
        private object[] BuildMethodArguments(ParameterInfo[] parameters, CompProperties compProps,
            float progress, float startProgress, float endProgress)
        {
            List<object> args = new List<object>();

            foreach (var param in parameters)
            {
                object? argValue = null;
                bool isRefParam = param.ParameterType.IsByRef;
                Type actualType = isRefParam ? param.ParameterType.GetElementType() : param.ParameterType;

                // 匹配标准参数
                if (actualType == typeof(CustomLineFlight))
                {
                    argValue = this;
                }
                else if (actualType == typeof(float) && param.Name == "progress")
                {
                    argValue = progress;
                }
                else if (actualType == typeof(float) && param.Name == "startProgress")
                {
                    argValue = startProgress;
                }
                else if (actualType == typeof(float) && param.Name == "endProgress")
                {
                    argValue = endProgress;
                }
                else if (actualType == typeof(AerialSupportTypeDef))
                {
                    argValue = supportType;
                }
                else if (actualType == typeof(Map))
                {
                    argValue = renderer?.map;
                }
                else if (actualType != null && actualType.IsAssignableFrom(compProps.GetType()))
                {
                    argValue = compProps;
                }
                // 匹配状态字段（ref参数）
                else if (isRefParam && actualType == typeof(List<IntVec3>))
                {
                    argValue = GetStateField<List<IntVec3>>(param.Name);
                    // 如果状态字段为null，创建默认值
                    if (argValue == null)
                    {
                        argValue = new List<IntVec3>();
                        SetStateField(param.Name, argValue);
                    }
                }
                else if (isRefParam && actualType == typeof(HashSet<int>))
                {
                    argValue = GetStateField<HashSet<int>>(param.Name);
                    // 如果状态字段为null，创建默认值
                    if (argValue == null)
                    {
                        argValue = new HashSet<int>();
                        SetStateField(param.Name, argValue);
                    }
                }
                // 匹配值类型状态字段（float, int等）
                else if (isRefParam && actualType != null && actualType.IsValueType)
                {
                    // 从通用状态字典中获取
                    argValue = GetStateValue(param.Name, actualType);
                    if (argValue == null)
                    {
                        // 如果不存在，创建默认值
                        try
                        {
                            argValue = Activator.CreateInstance(actualType);
                            if (argValue != null)
                            {
                                SetStateValue(param.Name, argValue);
                            }
                        }
                        catch
                        {
                            // 如果创建失败，使用类型的默认值
                            if (actualType.IsValueType)
                            {
                                try
                                {
                                    argValue = Activator.CreateInstance(actualType);
                                }
                                catch
                                {
                                    argValue = null;
                                }
                            }
                            else
                            {
                                argValue = null;
                            }
                        }
                    }
                }
                // 可以添加更多状态字段类型的匹配

                // 参数匹配失败处理
                if (argValue == null)
                {
                    Log.Warning($"[DMS_Legion] 无法匹配参数: {param.Name} ({param.ParameterType.Name})");
                    // 使用类型的默认值（可能不安全，但不会导致崩溃）
                    argValue = actualType != null && actualType.IsValueType
                        ? Activator.CreateInstance(actualType)
                        : null;
                }

                // 添加参数值（argValue可能为null，但这是反射调用所允许的）
                args.Add(argValue!); // 使用null-forgiving operator，因为我们已经在上面处理了null情况
            }

            return args.ToArray();
        }

        /// <summary>
        /// 根据字段名获取对应的状态字段（从通用状态字典）
        /// </summary>
        private T? GetStateField<T>(string fieldName) where T : class
        {
            if (effectComponentStates == null)
            {
                effectComponentStates = new Dictionary<string, object>();
            }
            
            if (effectComponentStates.TryGetValue(fieldName, out var value))
            {
                return value as T;
            }
            return null;
        }

        /// <summary>
        /// 设置状态字段的值（存储到通用状态字典）
        /// </summary>
        private void SetStateField(string fieldName, object? value)
        {
            if (value == null) return;
            
            if (effectComponentStates == null)
            {
                effectComponentStates = new Dictionary<string, object>();
            }
            
            effectComponentStates[fieldName] = value;
        }

        /// <summary>
        /// 从通用状态字典获取状态值（支持值类型）
        /// </summary>
        private object? GetStateValue(string fieldName, Type valueType)
        {
            if (effectComponentStates == null)
            {
                effectComponentStates = new Dictionary<string, object>();
            }
            
            if (effectComponentStates.TryGetValue(fieldName, out var value))
            {
                // 检查类型匹配
                if (value != null && (valueType.IsAssignableFrom(value.GetType()) || 
                    (valueType.IsValueType && value.GetType() == valueType)))
                {
                    return value;
                }
            }
            return null;
        }

        /// <summary>
        /// 设置状态值到通用状态字典（支持值类型）
        /// </summary>
        private void SetStateValue(string fieldName, object? value)
        {
            if (value == null) return;
            
            if (effectComponentStates == null)
            {
                effectComponentStates = new Dictionary<string, object>();
            }
            
            effectComponentStates[fieldName] = value;
        }
    }

    /// <summary>
    /// 多目标支援飞行类 - 按照选点顺序间隔执行打击
    ///
    /// 行为特点：
    /// - 不进行实际飞行运动
    /// - 在每个目标点原地执行效果
    /// - 使用TicksGame控制打击间隔节奏
    /// - 按玩家选择顺序依次打击所有目标
    /// - 支持暂停和时间倍率调节
    ///
    /// 时间语义：
    /// - selectionIntervalFrames：打击间隔（基于TicksGame）
    /// - 完全兼容游戏暂停和时间加速
    /// </summary>
    public class MultiTargetFlight : AircraftFlight
    {
        private List<IntVec3> targetPoints;        // 所有目标点（按选择顺序）
        private int currentTargetIndex;                     // 当前要启动的目标索引
        private int startTicksGame;                         // 开始执行时的TicksGame值
        private int nextTargetTicksGame;                   // 下次启动新飞行的TicksGame值

        // 无参数构造函数，用于序列化系统
        public MultiTargetFlight()
        {
            targetPoints = new List<IntVec3>();
        }

        public MultiTargetFlight(List<IntVec3> points, AerialSupportTypeDef supportType, AerialSupportRenderer renderer)
            : base(supportType, renderer)
        {
            targetPoints = new List<IntVec3>(points);
            currentTargetIndex = 0;
            startTicksGame = Find.TickManager.TicksGame;
            nextTargetTicksGame = startTicksGame + supportType.selectionIntervalFrames;

            if (targetPoints.Count == 0)
            {
                Log.Error("[DMS_Legion] 目标点列表为空");
                return;
            }

        }

        /// <summary>
        /// 多目标支援Tick逻辑 - 管理多个独立的飞行实例
        /// </summary>
        public override bool Tick()
        {
            // 延迟初始化renderer引用
            if (renderer == null)
            {
                var currentMap = Find.CurrentMap;
                if (currentMap == null)
                {
                    Log.Error("[DMS_Legion] 延迟初始化失败：CurrentMap为null");
                    return true;
                }

                renderer = currentMap.GetComponent<AerialSupportRenderer>();
                if (renderer == null)
                {
                    Log.Error("[DMS_Legion] 延迟初始化失败：未找到AerialSupportRenderer");
                    return true;
                }
            }

            // 超时检查（1200游戏帧 ≈ 20秒）
            if (startDrawFrame >= 0 && Time.frameCount - startDrawFrame > 1200)
            {
                Log.Warning("[DMS_Legion] 绘制超时，强制结束");
                return true;
            }

            int currentTicksGame = Find.TickManager.TicksGame;

            // 启动新的飞行实例（按时间间隔）
            if (currentTargetIndex < targetPoints.Count && currentTicksGame >= nextTargetTicksGame)
            {
                StartNextFlight();
            }

            // 检查是否所有目标都已启动
            return currentTargetIndex >= targetPoints.Count;
        }

        /// <summary>
        /// 启动下一个目标点的飞行实例
        /// </summary>
        private void StartNextFlight()
        {
            if (currentTargetIndex >= targetPoints.Count) return;

            IntVec3 currentTarget = targetPoints[currentTargetIndex];

            // 参数验证
            if (supportType == null || renderer == null)
            {
                Log.Error($"[DMS_Legion] 创建飞行实例失败：supportType或renderer为null");
                return;
            }

            // 创建独立的飞行实例（Normal类型，对单个目标点）
            // 需要计算从地图边缘到目标点的飞行路径
            IntVec3 flightStart = renderer.CalculateFlightStartPosition(currentTarget, renderer.map, supportType);

            AircraftFlight newFlight = new AircraftFlight(flightStart, currentTarget, supportType, renderer);

            // 将新飞行实例添加到渲染器的全局飞行列表中
            if (renderer != null)
            {
                renderer.AddFlight(newFlight);
            }

            currentTargetIndex++;

            // 设置下次启动时间
            if (currentTargetIndex < targetPoints.Count)
            {
                int currentTicksGame = Find.TickManager.TicksGame;

                // 使用配置的间隔时间
                int intervalTicks = supportType.selectionIntervalFrames;
                nextTargetTicksGame = currentTicksGame + intervalTicks;
            }
        }

        /// <summary>
        /// 多目标支援的绘制逻辑 - 绘制由子飞行实例负责，此处无需操作
        /// </summary>
        public override void Draw()
        {
            // 多目标支援的绘制由各个独立的子飞行实例负责
            // 此处无需额外操作
        }

        /// <summary>
        /// 获取目标点列表（供Renderer使用）
        /// </summary>
        public List<IntVec3> GetTargetPoints()
        {
            return targetPoints;
        }

        /// <summary>
        /// 保存/加载MultiTargetFlight特定数据
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref targetPoints, "targetPoints", LookMode.Value);
            Scribe_Values.Look(ref currentTargetIndex, "currentTargetIndex");
            Scribe_Values.Look(ref startTicksGame, "startTicksGame");
            Scribe_Values.Look(ref nextTargetTicksGame, "nextTargetTicksGame");
        }
    }
}
