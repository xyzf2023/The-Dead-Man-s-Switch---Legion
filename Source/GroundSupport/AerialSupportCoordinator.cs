using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using DMS_Legion;
using DMS_Legion.GroundSupport.SupportEffects;

namespace DMS_Legion.GroundSupport
{
    /// <summary>
    /// 空中支援协调器 - 中央调度器
    /// 负责统一管理空中支援的请求调度、选点流程和Flight实例创建
    /// </summary>
    public class AerialSupportCoordinator : GameComponent
    {
        private static AerialSupportCoordinator? instance;

        /// <summary>
        /// 协调器单例实例
        /// </summary>
        public static AerialSupportCoordinator? Instance => instance;

        /// <summary>
        /// 配置数据：组件DefName -> 可用支援类型列表
        /// </summary>
        private Dictionary<string, List<AerialSupportTypeDef>> componentSupportTypes;

        /// <summary>
        /// 延迟飞行实例队列：管理延迟渲染和音效播放
        /// </summary>
        private Dictionary<AircraftFlight, DelayedFlightInfo> delayedFlights = new Dictionary<AircraftFlight, DelayedFlightInfo>();

        /// <summary>
        /// 延迟飞行信息
        /// </summary>
        private class DelayedFlightInfo
        {
            public int creationTick;              // 创建时间（基准点）
            public int renderDelayTicks;          // 绘制延迟
            public int soundDelayTicks;           // 声音延迟
            public SoundDef? soundDef;            // 要播放的声音
            public AerialSupportRenderer? renderer; // 渲染器引用
            public bool soundPlayed = false;      // 声音是否已播放
            public bool renderStarted = false;    // 绘制是否已开始
        }

        public AerialSupportCoordinator(Game game)
        {
            instance = this;
            componentSupportTypes = new Dictionary<string, List<AerialSupportTypeDef>>();
            delayedFlights = new Dictionary<AircraftFlight, DelayedFlightInfo>();
            LoadConfiguration();
        }

        /// <summary>
        /// 加载配置数据
        /// 从所有技能组件中收集支持的空中支援类型
        /// </summary>
        private void LoadConfiguration()
        {
            componentSupportTypes = new Dictionary<string, List<AerialSupportTypeDef>>();

            // 从所有技能定义中收集配置
            foreach (var abilityDef in DefDatabase<AbilityDef>.AllDefs)
            {
                var comp = abilityDef.comps?.OfType<CompProperties_AerialSupport>().FirstOrDefault();
                if (comp?.supportedSupportTypes != null && comp.supportedSupportTypes.Count > 0)
                {
                    componentSupportTypes[abilityDef.defName] = comp.supportedSupportTypes
                        .Select(name => DefDatabase<AerialSupportTypeDef>.GetNamed(name))
                        .Where(def => def != null)
                        .ToList();
                }
            }

            // 从JobDriver定义中收集配置（如果需要）
            // 这里可以扩展支持其他类型的调用源
        }

        /// <summary>
        /// 保存/加载数据
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // 协调器本身不需要持久化数据，因为配置数据从XML重新加载
        }

        /// <summary>
        /// 请求可用支援类型
        /// 根据调用源（如Ability）的DefName，返回该组件可调用的支援类型列表
        /// </summary>
        /// <param name="instigator">调用者pawn</param>
        /// <param name="sourceDefName">调用源的DefName（如技能DefName）</param>
        /// <param name="callback">返回可用支援类型列表的回调</param>
        public void RequestAerialSupportTypes(Pawn instigator, string sourceDefName, Action<List<AerialSupportTypeDef>> callback)
        {
            if (componentSupportTypes.TryGetValue(sourceDefName, out var types) && types.Count > 0)
            {
                callback(types);
            }
            else
            {
                Log.Warning($"[DMS_Legion] 未找到调用源 {sourceDefName} 的空中支援类型配置");
                callback(new List<AerialSupportTypeDef>());
            }
        }

        /// <summary>
        /// 检查pawn是否位于子地图（地下地图或口袋地图）
        /// 通过检查generatorDef.isUnderground标识来判断
        /// </summary>
        private bool IsInSubMap(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                return false;
            }

            Map map = pawn.Map;

            // 检查是否为地下地图/口袋地图（所有子地图都设置了isUnderground=true）
            // 包括：异象DLC的巨坑、奥德赛DLC的地下仓库等
            return map.generatorDef?.isUnderground == true;
        }

        /// <summary>
        /// 检查地图是否为子地图（地下/口袋地图），空中支援不可用
        /// </summary>
        private bool IsInSubMap(Map map)
        {
            return map?.generatorDef?.isUnderground == true;
        }

        /// <summary>
        /// 执行选定支援类型（核心方法）
        /// 根据flightPathType计算选点数量，创建TargetSelector，处理选点结果并创建Flight
        /// </summary>
        /// <param name="instigator">调用者pawn</param>
        /// <param name="supportType">选定的支援类型</param>
        /// <param name="onCompleted">选点完成的回调（可选，用于通知调用者）</param>
        /// <param name="onCancelled">选点取消的回调（可选，用于通知调用者）</param>
        public void ExecuteAerialSupport(Pawn instigator, AerialSupportTypeDef supportType, Action<List<IntVec3>>? onCompleted = null, Action? onCancelled = null)
        {
            if (instigator == null || supportType == null)
            {
                Log.Error("[协调器] ExecuteAerialSupport 参数无效");
                return;
            }

            // 检查是否位于子地图（地下地图或口袋地图）
            if (IsInSubMap(instigator))
            {
                Messages.Message("DMSL_AerialSupport_AreaUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                onCancelled?.Invoke();
                return;
            }

            // 根据flightPathType计算需要的选点数量
            int pointCount = GetPointCountForSupportType(supportType);

            // 创建一次性TargetSelector实例
            var selector = new AerialSupportTargetSelector();

            // TargetSelector仅接收选点次数、完成回调、取消回调
            // 与支援类型和flightPathType完全解耦
            selector.StartSelection(instigator, pointCount,
                points => OnSelectionComplete(points, supportType, onCompleted),
                () => OnSelectionCancelled(onCancelled));
        }

        /// <summary>
        /// 执行选定支援类型（无殖民者调用，如控制台指令）
        /// 直接根据当前地图唤起选点器，选点完成后创建并执行支援
        /// </summary>
        /// <param name="map">当前地图</param>
        /// <param name="supportType">选定的支援类型</param>
        /// <param name="onCompleted">选点完成的回调（可选）</param>
        /// <param name="onCancelled">选点取消的回调（可选）</param>
        public void ExecuteAerialSupport(Map map, AerialSupportTypeDef supportType, Action<List<IntVec3>>? onCompleted = null, Action? onCancelled = null)
        {
            if (map == null || supportType == null)
            {
                Log.Error("[协调器] ExecuteAerialSupport(map) 参数无效");
                return;
            }

            if (IsInSubMap(map))
            {
                Messages.Message("DMSL_AerialSupport_AreaUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                onCancelled?.Invoke();
                return;
            }

            int pointCount = GetPointCountForSupportType(supportType);
            var selector = new AerialSupportTargetSelector();
            selector.StartSelection(null, map, pointCount,
                points => OnSelectionComplete(points, supportType, map, onCompleted),
                () => OnSelectionCancelled(onCancelled));
        }

        /// <summary>
        /// 直接请求一次空中支援（跳过选点流程），遵循支援类型的延迟、起点方向等配置。
        /// 用于 Verb、事件等已有目标坐标的场景。
        /// </summary>
        public void RequestSupportAt(IntVec3 targetCell, Map map, AerialSupportTypeDef supportType)
        {
            RequestSupportAt(targetCell, map, supportType, null);
        }

        /// <summary>
        /// 直接请求一次空中支援（多点），跳过选点流程，遵循支援类型的延迟等配置。
        /// 用于通讯台等已有目标点列表的场景；单点可视为 points.Count == 1。
        /// </summary>
        public void RequestSupportAt(List<IntVec3> points, Map map, AerialSupportTypeDef supportType)
        {
            if (points == null || points.Count == 0 || map == null || supportType == null)
            {
                Log.Warning("[DMS_Legion] RequestSupportAt(List) 参数无效：points、map 或 supportType 为空");
                return;
            }

            if (IsInSubMap(map))
            {
                Log.Warning("[DMS_Legion] RequestSupportAt(List) 目标地图为子地图，空中支援不可用");
                return;
            }

            OnSelectionComplete(points, supportType, map, null);
        }

        /// <summary>
        /// 直接请求一次空中支援（跳过选点流程），可选传入 instigator 以执行冷却与消息回调。
        /// 用于能力单点选目标等场景：起点由协调器按 startDirection/preferNorthEntry 计算，延迟由 renderDelayTicks/soundDelayTicks 控制。
        /// </summary>
        public void RequestSupportAt(IntVec3 targetCell, Map map, AerialSupportTypeDef supportType, Pawn? instigator)
        {
            if (map == null || supportType == null)
            {
                Log.Warning("[DMS_Legion] RequestSupportAt 参数无效：map或supportType为空");
                return;
            }

            var points = new List<IntVec3> { targetCell };
            Action<List<IntVec3>>? onCompleted = instigator != null
                ? (pts => HandleAerialSupportExecuted(instigator, supportType, pts))
                : null;
            OnSelectionComplete(points, supportType, map, onCompleted);
        }

        /// <summary>
        /// 选点完成的处理方法
        /// 负责创建Flight实例并添加到延迟队列
        /// </summary>
        private void OnSelectionComplete(List<IntVec3> points, AerialSupportTypeDef supportType, Action<List<IntVec3>>? onCompleted)
        {
            OnSelectionComplete(points, supportType, Find.CurrentMap, onCompleted);
        }

        /// <summary>
        /// 选点完成的处理方法（重载，支持指定地图）
        /// 负责创建Flight实例并添加到延迟队列
        /// </summary>
        private void OnSelectionComplete(List<IntVec3> points, AerialSupportTypeDef supportType, Map map, Action<List<IntVec3>>? onCompleted)
        {
            try
            {
                // 获取renderer
                var renderer = map.GetComponent<AerialSupportRenderer>();
                if (renderer == null)
                {
                    Log.Error("[DMS_Legion] AerialSupportRenderer未找到");
                    onCompleted?.Invoke(points);
                    return;
                }

                // 创建对应的Flight实例（根据flightPathType）
                AircraftFlight flight = CreateFlightForSupportType(points, supportType, renderer);

                // 记录创建时间作为基准点
                int creationTick = Find.TickManager.TicksGame;

                // 创建延迟信息
                var delayInfo = new DelayedFlightInfo
                {
                    creationTick = creationTick,
                    renderDelayTicks = supportType.renderDelayTicks,
                    soundDelayTicks = supportType.soundDelayTicks,
                    soundDef = supportType.appearSoundDef,
                    renderer = renderer,
                    soundPlayed = false,
                    renderStarted = false
                };

                // 添加到延迟队列（不立即添加到渲染器）
                delayedFlights[flight] = delayInfo;

                // 通知调用者（如果提供了回调）
                onCompleted?.Invoke(points);

                Log.Message($"[DMS_Legion] 已创建支援类型 {supportType.defName}，目标点数：{points.Count}，绘制延迟：{supportType.renderDelayTicks} ticks，音效延迟：{supportType.soundDelayTicks} ticks");
            }
            catch (Exception ex)
            {
                Log.Error($"[DMS_Legion] 创建Flight失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 选点取消的处理方法
        /// </summary>
        private void OnSelectionCancelled(Action? onCancelled = null)
        {
            // 清理renderer中的selectedSupportType，确保下次点击时重新显示菜单
            var renderer = Find.CurrentMap?.GetComponent<AerialSupportRenderer>();
            renderer?.SetSelectedSupportType(null);

            // 调用外部取消回调（如果提供）
            onCancelled?.Invoke();

            // 显示取消消息（向后兼容）
            Messages.Message("空中支援已取消。".Translate(), MessageTypeDefOf.RejectInput);
        }

        /// <summary>
        /// 根据支援类型获取选点数量（供通讯台等外部调用）
        /// </summary>
        public static int GetPointCountForSupportType(AerialSupportTypeDef supportType)
        {
            if (supportType == null)
                return 1;
            switch (supportType.flightPathType)
            {
                case "Normal":
                    return 1;                    // 单点打击
                case "CustomLine":
                    return 2;                    // 两点直线
                case "MultiTarget":
                    return supportType.selectionPointCount; // N点多目标
                default:
                    Log.Warning($"[DMS_Legion] 未知的flightPathType: {supportType.flightPathType}，使用默认值1");
                    return 1;
            }
        }

        /// <summary>
        /// 为JobDriver启动选点流程（向后兼容接口）
        /// 从renderer获取当前选择的支援类型，然后启动相应流程
        /// </summary>
        public void StartTargetSelectionForJob(Pawn pawn, JobDriver_AerialSupport_SelectCustomLine jobDriver)
        {
            if (pawn == null || jobDriver == null)
            {
                Log.Error("[DMS_Legion] StartTargetSelectionForJob 参数无效");
                jobDriver?.OnTargetSelectionCancelled();
                return;
            }

            // 从renderer获取当前选择的支援类型（向后兼容）
            var renderer = Find.CurrentMap?.GetComponent<AerialSupportRenderer>();
            var selectedType = renderer?.GetSelectedSupportType();

            if (selectedType == null)
            {
                Log.Error("[DMS_Legion] 未找到选定的支援类型");
                jobDriver.OnTargetSelectionCancelled();
                return;
            }

            // 使用标准的ExecuteAerialSupport流程，但添加JobDriver回调
            ExecuteAerialSupport(pawn, selectedType, points =>
            {
                // 选点成功，执行业务逻辑（冷却、消息等）
                HandleAerialSupportExecuted(pawn, selectedType, points);
                // 通知JobDriver完成
                jobDriver.OnTargetSelectionCompleted();
            },
            () =>
            {
                // 选点取消，通知JobDriver取消
                jobDriver.OnTargetSelectionCancelled();
            });
        }

        /// <summary>
        /// 处理空中支援成功执行后的业务逻辑
        /// </summary>
        private void HandleAerialSupportExecuted(Pawn pawn, AerialSupportTypeDef supportType, List<IntVec3> points)
        {
            try
            {
                // 播放发动音效 - 使用DefDatabase获取SoundDef，确保类型安全
                var clickSound = DefDatabase<SoundDef>.GetNamed("Click", false);
                if (clickSound != null)
                {
                    clickSound.PlayOneShotOnCamera(null);
                }

                // 显示确认消息
                string message;
                if (points.Count == 1)
                {
                    message = "DMSL_AerialSupport_ConfirmedSingle".Translate(supportType.label, points[0]);
                }
                else if (points.Count == 2)
                {
                    message = "DMSL_AerialSupport_ConfirmedRange".Translate(supportType.label, points[0], points[1]);
                }
                else
                {
                    message = "DMSL_AerialSupport_ConfirmedMultiple".Translate(supportType.label, points.Count);
                }

                Messages.Message(message,
                    new TargetInfo(points[0], pawn.Map), MessageTypeDefOf.PositiveEvent);

                // 应用冷却时间
                if (supportType.cooldownTicks > 0)
                {
                    var ability = pawn.abilities?.abilities.Find(ab => ab.def.defName == "DMSL_Ability_AerialSupport");
                    if (ability != null)
                    {
                        ability.StartCooldown(supportType.cooldownTicks);
                    }
                }

                // 若本次来自皇权支援，成功召唤后消耗（进 CD、扣好感）
                var renderer = pawn.Map?.GetComponent<AerialSupportRenderer>();
                renderer?.ConsumeRoyalPermitIfSet();
                // 清除选择的支援类型，为下次使用做准备
                renderer?.SetSelectedSupportType(null);

                Log.Message($"[DMS_Legion] 空中支援 {supportType.defName} 执行完成，目标点数：{points.Count}");
            }
            catch (Exception ex)
            {
                Log.Error($"[DMS_Legion] 处理支援执行业务逻辑失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 计算从地图边缘到目标点的飞行起点位置
        /// </summary>
        private IntVec3 CalculateFlightStartPosition(IntVec3 target, Map map, AerialSupportTypeDef? supportType)
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
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("[DMS_Legion] 无法获取当前地图，无法计算边界交点");
                return (point1, point2);
            }

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

                // 计算从每个交点到用户终点的向量
                Vector3 toPoint2FromCloser = (point2 - closerPoint.ToIntVec3()).ToVector3();
                Vector3 toPoint2FromFarther = (point2 - fartherPoint.ToIntVec3()).ToVector3();

                // 根据用户方向，选择合适的起点和终点
                // 如果从fartherPoint到point2的方向更接近用户方向，那么从fartherPoint出发
                float dotCloser = Vector3.Dot(toPoint2FromCloser.normalized, userDirection);
                float dotFarther = Vector3.Dot(toPoint2FromFarther.normalized, userDirection);

                if (dotFarther > dotCloser)
                {
                    // 从远离的交点飞向接近的交点
                    return (fartherPoint.ToIntVec3(), closerPoint.ToIntVec3());
                }
                else
                {
                    // 从接近的交点飞向远离的交点
                    return (closerPoint.ToIntVec3(), fartherPoint.ToIntVec3());
                }
            }
            else
            {
                // 如果计算失败，使用随机边缘点
                Log.Warning("[DMS_Legion] 无法计算边界交点，使用随机边缘点");
                IntVec3 randomStart = CellFinder.RandomEdgeCell(map);
                IntVec3 randomEnd = CellFinder.RandomEdgeCell(map);
                return (randomStart, randomEnd);
            }
        }

        /// <summary>
        /// 计算CustomLine的用户线段上的效果点
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

                // 确保在地图范围内（使用CurrentMap作为近似）
                var map = Find.CurrentMap;
                if (map != null && effectPoint.InBounds(map))
                {
                    effectPoints.Add(effectPoint);
                }
            }

            return effectPoints;
        }

        /// <summary>
        /// 根据支援类型创建对应的Flight实例
        /// 这是唯一允许读取flightPathType并创建Flight实例的地方
        /// </summary>
        private AircraftFlight CreateFlightForSupportType(List<IntVec3> points, AerialSupportTypeDef supportType, AerialSupportRenderer renderer)
        {
            switch (supportType.flightPathType)
            {
                case "Normal":
                    // 计算从地图边缘到目标点的飞行路径
                    IntVec3 flightStart = CalculateFlightStartPosition(points[0], Find.CurrentMap, supportType);
                    return new AircraftFlight(flightStart, points[0], supportType, renderer);
                case "CustomLine":
                    // 计算用户选择线段的延长线与地图边界的交点
                    var boundaryPoints = CalculateLineBoundaryIntersections(points[0], points[1]);
                    IntVec3 customLineStart = boundaryPoints.start;
                    IntVec3 customLineEnd = boundaryPoints.end;

                    // 在用户选择的线段上均匀分布执行点
                    var effectPoints = CalculateEffectPointsOnUserLine(points[0], points[1], supportType);

                    return new CustomLineFlight(customLineStart, customLineEnd, effectPoints, supportType, renderer, points[0], points[1]);
                case "MultiTarget":
                    return new MultiTargetFlight(points, supportType, renderer);
                default:
                    Log.Error($"[DMS_Legion] 未知的flightPathType: {supportType.flightPathType}，使用默认Normal类型");
                    return new AircraftFlight(points[0], points[0], supportType, renderer);
            }
        }

        /// <summary>
        /// 游戏组件每帧更新
        /// 管理延迟队列，处理延迟渲染和音效播放
        /// </summary>
        public override void GameComponentTick()
        {
            if (delayedFlights.Count == 0)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;

            // 遍历延迟队列（使用ToList()避免修改集合时出错）
            foreach (var kvp in delayedFlights.ToList())
            {
                var flight = kvp.Key;
                var info = kvp.Value;
                int elapsedTicks = currentTick - info.creationTick;

                // 检查音效延迟
                if (!info.soundPlayed && elapsedTicks >= info.soundDelayTicks)
                {
                    if (info.soundDef != null)
                    {
                        try
                        {
                            info.soundDef.PlayOneShotOnCamera(null);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[DMS_Legion] 播放音效失败：{ex.Message}");
                        }
                    }
                    info.soundPlayed = true;
                }

                // 检查绘制延迟
                if (!info.renderStarted && elapsedTicks >= info.renderDelayTicks)
                {
                    try
                    {
                        if (info.renderer != null)
                        {
                            info.renderer.StartFlight(flight);
                        }
                        else
                        {
                            Log.Error("[DMS_Legion] 启动绘制失败：renderer为null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[DMS_Legion] 启动绘制失败：{ex.Message}");
                    }
                    info.renderStarted = true;
                }

                // 如果都完成了，从队列中移除
                if (info.renderStarted && (info.soundPlayed || info.soundDef == null))
                {
                    delayedFlights.Remove(flight);
                }
            }
        }
    }
}