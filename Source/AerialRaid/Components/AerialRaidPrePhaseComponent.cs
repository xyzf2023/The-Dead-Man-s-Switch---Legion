using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;
using DMS_Legion.AerialRaid;

namespace DMS_Legion.AerialRaid.AerialRaidComponents
{
    /// <summary>
    /// 空袭前置阶段状态
    /// </summary>
    public enum AerialRaidPrePhaseState
    {
        /// <summary>
        /// 倒计时进行中
        /// </summary>
        CountingDown,

        /// <summary>
        /// 即将结束（即将空袭）
        /// </summary>
        Approaching,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed
    }

    /// <summary>
    /// 空袭前置阶段组件 - 最小可运行骨架
    /// 负责倒计时并在倒计时结束时触发阶段结束逻辑
    /// </summary>
    public class AerialRaidPrePhaseComponent : MapComponent
    {
        /// <summary>
        /// 多目标准星的单个目标可视状态
        /// </summary>
        private struct CrosshairVisualState
        {
            /// <summary>当前显示位置（世界坐标，不含抖动）</summary>
            public Vector3 DisplayPos;

            /// <summary>平滑移动起点</summary>
            public Vector3 MoveFromPos;

            /// <summary>平滑移动终点</summary>
            public Vector3 MoveToPos;

            /// <summary>平滑移动开始的 Tick</summary>
            public int MoveStartTick;

            /// <summary>是否已初始化显示位置</summary>
            public bool HasDisplayPos;

            /// <summary>上一次用于可视化的目标格子</summary>
            public IntVec3 LastTargetCell;
        }

        /// <summary>
        /// 进入"即将结束"状态的阈值（单位：Tick）
        /// 当剩余时间小于等于此值时，状态切换为 Approaching
        /// </summary>
        private const int ApproachingThresholdTicks = 6000; // 100秒（游戏时间）


        /// <summary>
        /// 剩余时间（单位：Tick）
        /// </summary>
        private int remainingTicks;

        /// <summary>
        /// 当前阶段状态
        /// </summary>
        private AerialRaidPrePhaseState currentState = AerialRaidPrePhaseState.CountingDown;

        /// <summary>
        /// 当前预期空袭目标坐标
        /// 组件内部维护，表示"如果现在立刻空袭，将会打击的位置"
        /// </summary>
        private IntVec3 targetCell = IntVec3.Invalid;

        /// <summary>
        /// 执行次数
        /// 外部系统可以设置的空中支援执行次数，默认值为1
        /// </summary>
        private int executionCount = 1;

        /// <summary>
        /// 支援类型 defName（可选）
        /// 如果设置，将使用此类型而非默认类型
        /// </summary>
        private string? supportTypeDefName = null;

        /// <summary>
        /// 上一次执行目标决策的 Tick
        /// </summary>
        private int lastDecisionTick = -1;


        /// <summary>
        /// 初始化完成的 Tick（用于延迟播放音频）
        /// </summary>
        private int initializationTick = -1;

        /// <summary>
        /// 初始化完成后播放音频的延迟时间（Tick）
        /// </summary>
        private const int InitializationSoundDelayTicks = 10;

        /// <summary>
        /// 是否已播放初始化完成音频
        /// </summary>
        private bool initializationSoundPlayed = false;

        /// <summary>
        /// 暂停前的最后偏移位置（用于暂停时保持位置不变）
        /// </summary>
        private Vector3 lastJitterOffset = Vector3.zero;

        /// <summary>
        /// 时间基准（用于计算偏移的基准Tick）
        /// 使用游戏时间（TicksGame），自动与游戏倍速同步
        /// </summary>
        private int jitterTimeBaseTicks = -1;

        /// <summary>
        /// 平滑移动持续时间（Tick），固定 60 tick（约 1 秒）
        /// </summary>
        private int crosshairMoveDurationTicks = 60;

        /// <summary>
        /// 多目标准星的可视状态缓存（按序号存储，每个执行槽位一个平滑位移状态）
        /// </summary>
        private readonly Dictionary<int, CrosshairVisualState> crosshairVisualStates = new Dictionary<int, CrosshairVisualState>();
        private readonly List<IntVec3> cachedDrawTargetCells = new List<IntVec3>();

        /// <summary>
        /// 执行次数检查间隔（Tick）
        /// 每600 tick检查一次空袭次数
        /// </summary>
        private const int ExecutionCountCheckIntervalTicks = 600;

        /// <summary>
        /// 上一次检查执行次数的 Tick
        /// </summary>
        private int lastExecutionCountCheckTick = -1;

        /// <summary>
        /// 空袭前置阶段已完成事件
        /// 生命周期完成信号出口，用于通知外部系统阶段已完成
        /// </summary>
        public event Action<AerialRaidPrePhaseComponent>? OnPrePhaseCompletedEvent;

        /// <summary>
        /// 最近一次生成的空袭请求（中间层产出物，只读，不参与存档）
        /// 用于回答“本次前置阶段完成时要执行怎样的空袭”
        /// </summary>
        public AerialRaidRequest? LastGeneratedRequest => lastGeneratedRequest;

        /// <summary>
        /// 最近一次生成的空袭请求内部存储（不参与存档）
        /// </summary>
        private AerialRaidRequest? lastGeneratedRequest;

        /// <summary>
        /// 构造函数 - 用于 RimWorld 存档加载（必须只接受 Map 参数）
        /// </summary>
        /// <param name="map">目标地图</param>
        public AerialRaidPrePhaseComponent(Map map) : base(map)
        {
            // 存档加载时的初始化：不执行额外的初始化逻辑
            // 数据会通过 ExposeData 方法从存档中恢复
            // 注意：初始状态必须一致：如果 remainingTicks 为 0，状态必须是 Completed
            remainingTicks = 0;
            currentState = AerialRaidPrePhaseState.Completed; // 修正：初始状态应该与 remainingTicks=0 保持一致
        }

        /// <summary>
        /// 初始化组件（用于创建新组件时调用）
        /// </summary>
        /// <param name="initialTicks">初始倒计时时间（Tick），如果<=0则标记为Completed</param>
        private void Initialize(int initialTicks = 0)
        {
            // 如果initialTicks <= 0，直接标记为已完成，避免自动触发空袭
            if (initialTicks <= 0)
            {
                remainingTicks = 0;
                currentState = AerialRaidPrePhaseState.Completed;
                targetCell = IntVec3.Invalid;
                cachedDrawTargetCells.Clear();
                return;
            }
            
            remainingTicks = initialTicks;
            currentState = CalculateState(remainingTicks);
            // 记录初始化完成的 Tick
            initializationTick = Find.TickManager.TicksGame;
            int currentTick = Find.TickManager.TicksGame;
            lastDecisionTick = currentTick;
            var selector = GetOrCreateCandidateSelector();
            selector?.InitializeForPrePhase(executionCount);
            NotifyCommittedTargetUpdated();
        }

        /// <summary>
        /// 保存/加载数据
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref remainingTicks, "remainingTicks", 0);
            Scribe_Values.Look(ref currentState, "currentState", AerialRaidPrePhaseState.CountingDown);
            Scribe_Values.Look(ref targetCell, "targetCell", IntVec3.Invalid);
            Scribe_Values.Look(ref executionCount, "executionCount", 1);
            Scribe_Values.Look(ref supportTypeDefName, "supportTypeDefName", null);
            Scribe_Values.Look(ref lastDecisionTick, "lastDecisionTick", -1);
            Scribe_Values.Look(ref initializationTick, "initializationTick", -1);
            Scribe_Values.Look(ref initializationSoundPlayed, "initializationSoundPlayed", false);

            // 读档后重新计算状态，确保状态正确
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 从根本上修复状态不一致问题：确保 remainingTicks 和 currentState 始终保持一致
                // 如果剩余时间小于等于0，状态必须是 Completed
                if (remainingTicks <= 0)
                {
                    currentState = AerialRaidPrePhaseState.Completed;
                    targetCell = IntVec3.Invalid;
                    cachedDrawTargetCells.Clear();
                    // 重置其他相关状态，确保组件处于干净的已完成状态
                    lastDecisionTick = -1;
                    initializationTick = -1;
                    initializationSoundPlayed = false;
                    crosshairVisualStates.Clear();
                }
                else
                {
                    // 确保状态与 remainingTicks 匹配
                    currentState = CalculateState(remainingTicks);
                    var selector = GetOrCreateCandidateSelector();
                    selector?.SetDesiredTargetCount(executionCount);
                    selector?.ResumeForActivePrePhase();
                    NotifyCommittedTargetUpdated();
                }
            }
        }

        /// <summary>
        /// 每Tick更新 - Tick阶段
        /// </summary>
        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // 如果已完成，不再处理
            if (currentState == AerialRaidPrePhaseState.Completed)
            {
                return;
            }

            // 每600 tick检查一次空袭次数
            int currentTick = Find.TickManager.TicksGame;
            if (lastExecutionCountCheckTick < 0 || currentTick - lastExecutionCountCheckTick >= ExecutionCountCheckIntervalTicks)
            {
                lastExecutionCountCheckTick = currentTick;
                
                // 如果执行次数为0，终止倒计时流程
                if (executionCount <= 0)
                {
                    currentState = AerialRaidPrePhaseState.Completed;
                    remainingTicks = 0;
                    targetCell = IntVec3.Invalid;
                    cachedDrawTargetCells.Clear();
                    lastDecisionTick = -1;
                    crosshairVisualStates.Clear();
                    GetOrCreateCandidateSelector()?.StopScanning();
                    // 不触发 OnPrePhaseCompleted()，不调用空中支援框架
                    return;
                }
            }

            // 减少剩余时间
            remainingTicks--;

            // 检查是否归零（在更新状态之前，因为CalculateState会将状态设为Completed）
            if (remainingTicks <= 0)
            {
                // 触发阶段结束逻辑（必须在状态更新之前调用）
                OnPrePhaseCompleted();
                // OnPrePhaseCompleted() 内部已经将状态设置为 Completed，这里直接返回
                return;
            }

            // 更新状态（与倒计时同步）
            currentState = CalculateState(remainingTicks);

            // 检查是否需要在初始化完成后10tick播放音频
            CheckAndPlayInitializationSound(currentTick);
        }

        /// <summary>
        /// 绘制准星渲染（纯表现层）
        /// </summary>
        public override void MapComponentDraw()
        {
            base.MapComponentDraw();

            try
            {
                // 渲染触发条件检查
                if (currentState == AerialRaidPrePhaseState.Completed)
                {
                    return;
                }
                if (map == null || map != Find.CurrentMap)
                {
                    return;
                }
                if (executionCount <= 0)
                {
                    return;
                }

                if (cachedDrawTargetCells.Count == 0)
                {
                    return;
                }

                // 对所有目标绘制准星（包含平滑位移与手抖效果）
                for (int i = 0; i < cachedDrawTargetCells.Count; i++)
                {
                    DrawCrosshairAtIndex(i, cachedDrawTargetCells[i]);
                }

                // 诱饵目标标记由 AerialRaidBaitTargetComponent 通过 Mote 自动绘制，不需要额外绘制
            }
            catch (Exception)
            {
                // 渲染逻辑失败不应影响游戏运行
            }
        }

        /// <summary>
        /// 针对指定序号的目标更新准星显示位置（多目标准星用）
        /// </summary>
        /// <param name="index">目标槽位序号（0 开始）</param>
        /// <param name="cell">该槽位当前目标格子</param>
        /// <param name="displayPos">输出的世界坐标（不含抖动）</param>
        private void UpdateCrosshairDisplayPositionForIndex(int index, IntVec3 cell, out Vector3 displayPos)
        {
            displayPos = cell.ToVector3Shifted();

            if (map == null || !cell.IsValid)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;

            if (!crosshairVisualStates.TryGetValue(index, out var state))
            {
                // 首次初始化该槽位的可视状态
                state = new CrosshairVisualState
                {
                    DisplayPos = cell.ToVector3Shifted(),
                    MoveFromPos = cell.ToVector3Shifted(),
                    MoveToPos = cell.ToVector3Shifted(),
                    MoveStartTick = currentTick,
                    HasDisplayPos = true,
                    LastTargetCell = cell
                };
                crosshairVisualStates[index] = state;
                displayPos = state.DisplayPos;
                return;
            }

            // 该槽位的目标发生变化时，从旧位置平滑移动到新目标
            if (cell != state.LastTargetCell)
            {
                bool isMoving = state.MoveStartTick >= 0 &&
                                (currentTick - state.MoveStartTick) < crosshairMoveDurationTicks;

                if (!isMoving)
                {
                    state.MoveFromPos = state.DisplayPos;
                    state.MoveToPos = cell.ToVector3Shifted();
                    state.MoveStartTick = currentTick;
                    state.LastTargetCell = cell;
                }
            }

            if (state.MoveStartTick < 0)
            {
                state.MoveStartTick = currentTick;
                state.MoveFromPos = state.DisplayPos;
                state.MoveToPos = state.DisplayPos;
                state.LastTargetCell = cell;
            }

            int elapsedTicks = currentTick - state.MoveStartTick;
            float t = crosshairMoveDurationTicks > 0
                ? Mathf.Clamp01(elapsedTicks / (float)crosshairMoveDurationTicks)
                : 1f;
            float easedT = EaseInOut(t);

            state.DisplayPos = Vector3.Lerp(state.MoveFromPos, state.MoveToPos, easedT);
            displayPos = state.DisplayPos;
            crosshairVisualStates[index] = state;
        }

        /// <summary>
        /// 在指定目标格子绘制准星（多目标准星，用于所有即将被空袭的点）
        /// </summary>
        /// <param name="index">目标槽位序号（0 开始）</param>
        /// <param name="cell">该槽位当前目标格子</param>
        private void DrawCrosshairAtIndex(int index, IntVec3 cell)
        {
            if (!cell.IsValid || map == null)
            {
                return;
            }

            // 更新该槽位的平滑显示位置
            UpdateCrosshairDisplayPositionForIndex(index, cell, out Vector3 worldPosBase);

            // 计算带手抖偏移的最终位置（所有准星共用同一套抖动节奏）
            Vector3 jitterOffset = CalculateJitterOffset();
            Vector3 worldPos = worldPosBase + jitterOffset;

            float drawHeight = AltitudeLayer.MoteOverhead.AltitudeFor();
            Vector3 drawPos = new Vector3(worldPos.x, drawHeight, worldPos.z);

            Material crosshairMaterial = MaterialPool.MatFrom("Misc/Crosshair", ShaderDatabase.Transparent);
            if (crosshairMaterial == null || crosshairMaterial.mainTexture == null)
            {
                return;
            }

            float scale = 12f;
            Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(scale, 1f, scale));

            Graphics.DrawMesh(MeshPool.plane10, matrix, crosshairMaterial, 0);
        }

        /// <summary>
        /// S曲线插值函数（Ease-in-out）
        /// </summary>
        /// <param name="t">插值参数（0-1）</param>
        /// <returns>缓动后的插值参数</returns>
        private static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 计算偏移（私有方法）
        /// 使用游戏Tick计算偏移，自动适配游戏倍速和暂停
        /// 暂停时停止偏移，保持暂停前最后一刻的位置
        /// 游戏加速时频率也会相应加快
        /// </summary>
        /// <returns>偏移向量（X/Z方向）</returns>
        private Vector3 CalculateJitterOffset()
        {
            // 检查游戏是否暂停
            bool isPaused = Find.TickManager != null && Find.TickManager.Paused;
            if (isPaused)
            {
                // 暂停时返回最后保存的偏移位置
                return lastJitterOffset;
            }

            // 获取当前游戏Tick
            int currentTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;

            // 首次调用时初始化基准
            if (jitterTimeBaseTicks < 0)
            {
                jitterTimeBaseTicks = currentTick;
            }

            // 计算相对于基准的游戏时间（Tick）
            // 转换为秒：1 tick = 1/60 秒（60 FPS）
            int tickDelta = currentTick - jitterTimeBaseTicks;
            float timeInSeconds = tickDelta / 60f;

            // 使用不同频率的正弦函数分别计算 X 和 Z 方向的偏移
            // 偏移幅度为原来的2倍
            float amplitude = 0.30f;
            // 频率使用游戏时间，这样游戏加速时频率会自动加快
            float xOffset = Mathf.Sin(timeInSeconds * 2.3f) * amplitude;
            float zOffset = Mathf.Cos(timeInSeconds * 1.7f) * amplitude;

            Vector3 offset = new Vector3(xOffset, 0f, zOffset);
            
            // 保存当前偏移位置（用于暂停时保持）
            lastJitterOffset = offset;

            return offset;
        }


        /// <summary>
        /// 阶段结束逻辑 - 结束阶段
        /// 根据执行次数依次对目标地点执行空中支援
        /// </summary>
        private void OnPrePhaseCompleted()
        {
            // 确保阶段状态只会被设置为 Completed 一次
            if (currentState == AerialRaidPrePhaseState.Completed)
            {
                Log.Warning("[DMS_Legion]空袭前置阶段：组件已经处于已完成状态，跳过执行");
                return;
            }

            // 额外安全检查：如果remainingTicks > 0，说明不应该触发空袭（可能是在错误的时候被调用了）
            // 只有在倒计时正常归零时才应该执行空袭
            if (remainingTicks > 0)
            {
                Log.Warning($"[DMS_Legion]空袭前置阶段：OnPrePhaseCompleted被错误调用，remainingTicks={remainingTicks} > 0，不应该触发空袭。直接标记为Completed并返回。");
                currentState = AerialRaidPrePhaseState.Completed;
                targetCell = IntVec3.Invalid;
                cachedDrawTargetCells.Clear();
                GetOrCreateCandidateSelector()?.StopScanning();
                OnPrePhaseCompletedEvent?.Invoke(this);
                return;
            }

            // 验证地图是否有效
            if (map == null)
            {
                Log.Error("[DMS_Legion]空袭前置阶段：地图为null，无法执行空袭");
                currentState = AerialRaidPrePhaseState.Completed;
                GetOrCreateCandidateSelector()?.StopScanning();
                OnPrePhaseCompletedEvent?.Invoke(this);
                return;
            }

            // 标记为已完成
            currentState = AerialRaidPrePhaseState.Completed;

            crosshairVisualStates.Clear();
            cachedDrawTargetCells.Clear();

            var selector = GetOrCreateCandidateSelector();
            AerialRaidTargetSnapshot snapshot = selector != null
                ? selector.GetCommittedTargetSnapshot(executionCount)
                : default;
            List<IntVec3> targetCells = snapshot.TargetCells ?? new List<IntVec3>();
            selector?.StopScanning();

            if (targetCells.Count == 0)
            {
                Log.Warning("[DMS_Legion]空袭前置阶段：未找到有效的目标坐标，无法执行空袭");
                OnPrePhaseCompletedEvent?.Invoke(this);
                return;
            }

            // 依次对每个目标执行空中支援
            int createdTick = Find.TickManager.TicksGame;
            for (int i = 0; i < targetCells.Count; i++)
            {
                IntVec3 targetCell = targetCells[i];
                
                // 验证目标坐标是否有效（map已在循环前确认不为null）
                if (!targetCell.IsValid || !targetCell.InBounds(map))
                {
                    Log.Warning($"[DMS_Legion]空袭前置阶段：跳过无效的目标坐标：{targetCell}");
                    continue;
                }
                
                // 生成空袭请求（map已在循环前确认不为null）
                var request = new AerialRaidRequest(
                    targetMap: map,
                    targetCell: targetCell,
                    createdTick: createdTick + i, // 为每次执行使用不同的 tick，以便选择器可能选择不同的支援类型
                    sourceType: AerialRaidSourceType.PrePhaseComponent,
                    isIntercepted: false,
                    sourceComponent: this);

                // 执行空中支援
                AerialRaidSupportSelector.ExecuteAerialRaid(request);

                // 保存最后一次生成的请求（向后兼容）
                lastGeneratedRequest = request;
            }

            // 触发完成事件（使用空值检查）
            OnPrePhaseCompletedEvent?.Invoke(this);

            // 注意：不再需要通知延迟组件，因为地面部队的进攻时机已经在生成时直接设置
        }

        /// <summary>
        /// 根据剩余时间计算当前状态（内部方法）
        /// 判定逻辑只能存在于组件内部
        /// </summary>
        /// <param name="ticks">剩余时间（Tick）</param>
        /// <returns>阶段状态</returns>
        private AerialRaidPrePhaseState CalculateState(int ticks)
        {
            if (ticks <= 0)
            {
                return AerialRaidPrePhaseState.Completed;
            }
            if (ticks <= ApproachingThresholdTicks)
            {
                return AerialRaidPrePhaseState.Approaching;
            }
            return AerialRaidPrePhaseState.CountingDown;
        }

        /// <summary>
        /// 设置倒计时时间
        /// </summary>
        /// <param name="ticks">倒计时时间（Tick），必须大于0</param>
        public void SetRemainingTicks(int ticks)
        {
            // 验证参数有效性
            if (ticks <= 0)
            {
                Log.Warning($"[DMS_Legion]空袭前置阶段：尝试设置无效的倒计时时间（{ticks} tick），必须大于0。将状态设为Completed以避免自动触发空袭。");
                currentState = AerialRaidPrePhaseState.Completed;
                remainingTicks = 0;
                targetCell = IntVec3.Invalid;
                cachedDrawTargetCells.Clear();
                GetOrCreateCandidateSelector()?.StopScanning();
                return;
            }

            // 如果状态是Completed，也允许重新设置（允许重新启动）
            if (currentState == AerialRaidPrePhaseState.Completed)
            {
                // 重置组件状态以便重新使用
                currentState = AerialRaidPrePhaseState.CountingDown;
                targetCell = IntVec3.Invalid;
                cachedDrawTargetCells.Clear();
                lastDecisionTick = -1;
                supportTypeDefName = null; // 重置支援类型，使用默认类型
            }
            
            remainingTicks = ticks;
            // 更新状态以反映新的剩余时间
            currentState = CalculateState(remainingTicks);
            // 重置初始化标记，以便播放声音
            initializationTick = Find.TickManager.TicksGame;
            initializationSoundPlayed = false;
            
            // 初始化时执行一次目标决策
            int currentTick = Find.TickManager.TicksGame;
            lastDecisionTick = currentTick;
            var selector = GetOrCreateCandidateSelector();
            selector?.InitializeForPrePhase(executionCount);
            NotifyCommittedTargetUpdated();
            
            // 注意：空袭信封现在由 IncidentWorker_Army.TryExecuteWorker() 使用原版系统发送
            // 不再在这里发送信件
        }

        /// <summary>
        /// 获取当前阶段状态（只读查询）
        /// 外部系统只能查询状态，不能自行判断
        /// </summary>
        /// <returns>当前阶段状态</returns>
        public AerialRaidPrePhaseState GetCurrentState()
        {
            return currentState;
        }

        /// <summary>
        /// 获取当前预期空袭目标坐标（只读查询）
        /// 外部系统只能查询目标，不能直接修改
        /// </summary>
        /// <returns>当前预期的空袭目标坐标</returns>
        public IntVec3 GetTargetCell()
        {
            return targetCell;
        }

        /// <summary>
        /// 获取剩余倒计时时间（只读查询）
        /// 外部系统可以查询剩余时间，用于显示Alert等
        /// </summary>
        /// <returns>剩余时间（Tick），如果已完成则返回0</returns>
        public int GetRemainingTicks()
        {
            if (currentState == AerialRaidPrePhaseState.Completed)
            {
                return 0;
            }
            return remainingTicks;
        }


        /// <summary>
        /// 设置执行次数
        /// 外部系统可以通过此方法设置空中支援的执行次数
        /// </summary>
        /// <param name="count">执行次数（必须大于0，否则使用默认值1）</param>
        public void SetExecutionCount(int count)
        {
            // 允许 0：AXF12 拦截会调用 SetExecutionCount(0) 取消该场空袭
            executionCount = count >= 0 ? count : 1;
            if (executionCount <= 0 && currentState != AerialRaidPrePhaseState.Completed)
            {
                currentState = AerialRaidPrePhaseState.Completed;
                remainingTicks = 0;
                targetCell = IntVec3.Invalid;
                lastDecisionTick = -1;
                cachedDrawTargetCells.Clear();
                crosshairVisualStates.Clear();
                map?.GetComponent<AerialRaidTargetCandidateSelector>()?.StopScanning();
                return;
            }

            if (currentState != AerialRaidPrePhaseState.Completed)
            {
                var selector = GetOrCreateCandidateSelector();
                selector?.SetDesiredTargetCount(executionCount);
                selector?.InitializeForPrePhase(executionCount);
                NotifyCommittedTargetUpdated();
            }
        }

        /// <summary>
        /// 设置支援类型 defName
        /// 外部系统可以通过此方法指定要使用的支援类型
        /// </summary>
        /// <param name="defName">支援类型的 defName，如果为 null 或空字符串则使用默认类型</param>
        public void SetSupportTypeDefName(string? defName)
        {
            supportTypeDefName = string.IsNullOrEmpty(defName) ? null : defName;
        }

        /// <summary>
        /// 获取支援类型 defName（只读）
        /// </summary>
        /// <returns>支援类型 defName，如果未设置则返回 null</returns>
        public string? GetSupportTypeDefName()
        {
            return supportTypeDefName;
        }

        /// <summary>
        /// 获取或创建候选目标筛选组件（内部方法）
        /// 统一管理筛选组件的获取和创建
        /// </summary>
        /// <returns>候选目标筛选组件，如果无法创建则返回 null</returns>
        private AerialRaidTargetCandidateSelector? GetOrCreateCandidateSelector()
        {
            if (map == null)
            {
                return null;
            }
            return AerialRaidTargetCandidateSelector.GetOrCreate(map);
        }

        public void NotifyCommittedTargetUpdated()
        {
            cachedDrawTargetCells.Clear();
            var selector = GetOrCreateCandidateSelector();
            if (selector == null)
            {
                targetCell = IntVec3.Invalid;
                return;
            }

            var snapshot = selector.GetCommittedTargetSnapshot(executionCount);
            if (snapshot.TargetCells != null)
            {
                cachedDrawTargetCells.AddRange(snapshot.TargetCells);
            }

            targetCell = cachedDrawTargetCells.Count > 0 ? cachedDrawTargetCells[0] : IntVec3.Invalid;
        }

        /// <summary>
        /// 检查并在初始化完成后10tick播放音频（内部方法）
        /// </summary>
        /// <param name="currentTick">当前游戏 Tick</param>
        private void CheckAndPlayInitializationSound(int currentTick)
        {
            // 如果已经播放过音频，跳过
            if (initializationSoundPlayed)
            {
                return;
            }

            // 如果还没有记录初始化完成的tick，跳过
            if (initializationTick < 0)
            {
                return;
            }

            // 检查是否已经过了延迟时间（且设置允许播放防空警报）
            int elapsedTicks = currentTick - initializationTick;
            if (elapsedTicks >= InitializationSoundDelayTicks)
            {
                if (DMS_Legion.DMSL_ModSettings.settings?.playAirRaidSiren == true)
                    PlayInitializationSound();
                initializationSoundPlayed = true;
            }
            else if (elapsedTicks == 0)
            {
                // 初始化完成，将在指定tick后播放音效
            }
        }

        /// <summary>
        /// 播放初始化完成音频（内部方法）
        /// </summary>
        private void PlayInitializationSound()
        {
            // 获取音频定义
            var soundDef = DefDatabase<SoundDef>.GetNamed("DMSL_AerialRaid_AirRaidSiren", false);
            if (soundDef == null)
            {
                Log.Warning("[DMS_Legion]空袭前置阶段：未找到初始化音频定义：DMSL_AerialRaid_AirRaidSiren");
                return;
            }

            // 播放音频（在地图中心播放，确保玩家能听到）
            if (map != null)
            {
                soundDef.PlayOneShot(new TargetInfo(map.Center, map));
            }
            else
            {
                Log.Warning("[DMS_Legion]空袭前置阶段：无法播放音效：地图为null");
            }
        }

        /// <summary>
        /// 获取或创建组件（静态方法）
        /// 确保同一张地图上始终只有一个该组件实例
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <param name="initialTicks">初始倒计时时间（Tick），仅在创建新组件时使用</param>
        /// <returns>组件实例，如果map为null则返回null</returns>
        public static AerialRaidPrePhaseComponent? GetOrCreate(Map map, int initialTicks = 0)
        {
            if (map == null)
            {
                Log.Error("[DMS_Legion]空袭前置阶段：无法在地图为null时获取或创建组件");
                return null;
            }

            // 尝试获取已有组件
            var component = map.GetComponent<AerialRaidPrePhaseComponent>();
            if (component != null)
            {
                // 从根本上修复状态不一致：确保 remainingTicks 和 currentState 始终保持一致
                // 如果 remainingTicks <= 0，状态必须是 Completed
                if (component.remainingTicks <= 0)
                {
                    // 无论当前状态是什么，都强制设为 Completed，并清理相关状态
                    component.currentState = AerialRaidPrePhaseState.Completed;
                    component.targetCell = IntVec3.Invalid;
                    component.cachedDrawTargetCells.Clear();
                    component.lastDecisionTick = -1;
                    component.initializationTick = -1;
                    component.initializationSoundPlayed = false;
                    component.supportTypeDefName = null; // 重置支援类型
                    component.crosshairVisualStates.Clear();
                    
                    // 如果组件已完成，准备重置以便重新使用（但 remainingTicks 通过 SetRemainingTicks 设置）
                    // 注意：remainingTicks 保持为 0，直到 SetRemainingTicks 被调用
                }
                // 如果组件已存在且已完成（准备重新使用），重置它以便重新使用
                else if (component.currentState == AerialRaidPrePhaseState.Completed)
                {
                    // 重置组件状态，允许重新使用
                    // 注意：不要设置 remainingTicks = 0，因为这会导致立即触发空袭
                    // remainingTicks 应该通过 SetRemainingTicks() 方法设置
                    component.currentState = AerialRaidPrePhaseState.CountingDown;
                    component.targetCell = IntVec3.Invalid;
                    component.cachedDrawTargetCells.Clear();
                    component.lastDecisionTick = -1;
                    component.initializationTick = -1;
                    component.supportTypeDefName = null; // 重置支援类型
                    component.initializationSoundPlayed = false;
                    component.executionCount = 1; // 重置为默认值，后续会通过SetExecutionCount设置
                    component.crosshairVisualStates.Clear();
                }
                // 如果组件处于活动状态（remainingTicks > 0），验证状态一致性
                else if (component.remainingTicks > 0)
                {
                    // 验证状态是否与 remainingTicks 匹配
                    var expectedState = component.CalculateState(component.remainingTicks);
                    if (component.currentState != expectedState)
                    {
                        Log.Warning($"[DMS_Legion]空袭前置阶段：检测到状态不一致（currentState={component.currentState}，expectedState={expectedState}，remainingTicks={component.remainingTicks}），自动修复");
                        component.currentState = expectedState;
                    }
                }
                
                return component;
            }

            // 如果不存在，创建新组件并初始化
            component = new AerialRaidPrePhaseComponent(map);
            component.Initialize(initialTicks);
            map.components.Add(component);
            
            // 注意：空袭信封现在由 IncidentWorker_Army.TryExecuteWorker() 使用原版系统发送
            // 不再在这里发送信件
            
            return component;
        }

        // 注意：SendAirStrikeLetter() 方法已移除
        // 信件现在由 IncidentWorker_Army.TryExecuteWorker() 使用原版 SendStandardLetter() 方法发送
        // 信件内容从 IncidentDef 中读取（def.letterLabel, def.letterText, def.letterDef）
    }
}
