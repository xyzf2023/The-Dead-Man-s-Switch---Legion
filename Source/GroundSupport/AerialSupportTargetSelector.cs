using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport
{
    /// <summary>
    /// 通用空中支援选点管理器 - 一次性状态对象
    /// 严格按照MD文档定义实现，负责引导玩家连续选择指定数量的目标点
    /// </summary>
    public class AerialSupportTargetSelector
    {
        // 输入参数
        private Pawn? instigator;
        private Map? targetMap;
        private int selectionCount;
        private Action<List<IntVec3>>? onComplete;
        private Action? onCancelled;

        // 内部状态
        private List<IntVec3> selectedPoints;
        private TargetingParameters targetingParams;
        private bool isActive;

        /// <summary>
        /// 构造函数 - 创建一次性选点管理器实例
        /// </summary>
        public AerialSupportTargetSelector()
        {
            selectedPoints = new List<IntVec3>();
            targetingParams = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetSelf = false,
                canTargetPawns = false,
                canTargetBuildings = true,
                canTargetAnimals = false,
                canTargetHumans = false,
                canTargetMechs = false,
                canTargetItems = false,
                validator = (target) =>
                {
                    Map m = Find.CurrentMap;
                    return m != null && target.Cell.InBounds(m) && target.Cell.Walkable(m);
                }
            };
        }

        /// <summary>
        /// 开始选点流程（使用 Find.CurrentMap 校验，适用于能力等单地图场景）
        /// </summary>
        public void StartSelection(Pawn? instigator, int selectionCount, Action<List<IntVec3>> onComplete, Action onCancelled)
        {
            StartSelection(instigator, null, selectionCount, onComplete, onCancelled);
        }

        /// <summary>
        /// 开始选点流程
        /// </summary>
        /// <param name="instigator">发起选点的 pawn，可为 null</param>
        /// <param name="targetMap">目标地图；非 null 时校验与选点均限定在该地图，并会切换当前视图到该地图（多地图时不混淆）</param>
        /// <param name="selectionCount">需要选择的点数</param>
        /// <param name="onComplete">选点完成的回调</param>
        /// <param name="onCancelled">选点取消的回调</param>
        public void StartSelection(Pawn? instigator, Map? targetMap, int selectionCount, Action<List<IntVec3>> onComplete, Action onCancelled)
        {
            if (selectionCount <= 0)
            {
                Log.Error($"[DMS_Legion] selectionCount 必须大于0，当前值: {selectionCount}");
                return;
            }
            if (onComplete == null)
            {
                Log.Error("[DMS_Legion] onComplete 回调不能为null");
                return;
            }
            if (onCancelled == null)
            {
                Log.Error("[DMS_Legion] onCancelled 回调不能为null");
                return;
            }

            this.instigator = instigator;
            this.targetMap = targetMap;
            this.selectionCount = selectionCount;
            this.onComplete = onComplete;
            this.onCancelled = onCancelled;
            selectedPoints.Clear();
            isActive = true;

            if (targetMap != null)
            {
                targetingParams.validator = (target) =>
                    target.Cell.InBounds(targetMap) && target.Cell.Walkable(targetMap);
                Current.Game.CurrentMap = targetMap;
            }
            else
            {
                targetingParams.validator = (target) =>
                {
                    Map m = Find.CurrentMap;
                    return m != null && target.Cell.InBounds(m) && target.Cell.Walkable(m);
                };
            }

            StartSelectingNextPoint();
        }

        /// <summary>
        /// 开始选择下一个点
        /// </summary>
        private void StartSelectingNextPoint()
        {
            if (!isActive) return;

            // 检查是否已完成所有选点
            if (selectedPoints.Count >= selectionCount)
            {
                CompleteSelection();
                return;
            }

            // 使用Find.Targeter.BeginTargeting开始选点
            Find.Targeter.BeginTargeting(
                targetingParams,
                OnPointSelected,
                null,
                null,
                instigator,
                OnSelectionCancelled  // 任意取消都终止整个流程
            );
        }

        /// <summary>
        /// 点选择完成的回调
        /// </summary>
        private void OnPointSelected(LocalTargetInfo target)
        {
            if (!isActive) return;

            IntVec3 selectedCell = target.Cell;
            selectedPoints.Add(selectedCell);

            // 显示确认消息（统一格式）
            string progressMessage = $"位置已确认：{selectedCell}（{selectedPoints.Count}/{selectionCount}）";
            MessageTypeDef messageType = selectedPoints.Count >= selectionCount ?
                MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent;

            Messages.Message(progressMessage.Translate(), messageType);

            // 继续选择下一个点或完成
            if (selectedPoints.Count < selectionCount)
            {
                StartSelectingNextPoint();
            }
            else
            {
                CompleteSelection();
            }
        }

        /// <summary>
        /// 选点取消的回调 - 立即终止整个流程
        /// </summary>
        private void OnSelectionCancelled()
        {
            if (!isActive) return;

            isActive = false;
            Find.Targeter.StopTargeting();
            onCancelled?.Invoke();
        }

        /// <summary>
        /// 完成选点流程
        /// </summary>
        private void CompleteSelection()
        {
            if (!isActive) return;

            isActive = false;
            onComplete?.Invoke(selectedPoints);
        }
    }
}