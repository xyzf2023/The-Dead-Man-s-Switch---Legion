using RimWorld;
using Verse;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 空袭请求来源类型（占位枚举）
    /// </summary>
    public enum AerialRaidSourceType
    {
        /// <summary>
        /// 未指定或未知来源
        /// </summary>
        Unknown,

        /// <summary>
        /// 来自前置阶段组件（AerialRaidPrePhaseComponent）
        /// </summary>
        PrePhaseComponent
    }

    /// <summary>
    /// 空袭请求数据对象（纯数据，不包含执行逻辑）
    /// 用于描述一次“预期空袭”，供后续系统消费
    /// </summary>
    public sealed class AerialRaidRequest
    {
        /// <summary>
        /// 目标地图
        /// </summary>
        public Map TargetMap { get; }

        /// <summary>
        /// 目标单元格
        /// </summary>
        public IntVec3 TargetCell { get; }

        /// <summary>
        /// 创建时刻（TicksGame）
        /// </summary>
        public int CreatedTick { get; }

        /// <summary>
        /// 请求来源类型
        /// </summary>
        public AerialRaidSourceType SourceType { get; }

        /// <summary>
        /// 是否已被拦截（未来反制接口，占位字段）
        /// </summary>
        public bool IsIntercepted { get; set; }

        /// <summary>
        /// 生成此请求的前置阶段组件（可选引用）
        /// </summary>
        public DMS_Legion.AerialRaid.AerialRaidComponents.AerialRaidPrePhaseComponent? SourceComponent { get; }

        public AerialRaidRequest(
            Map targetMap,
            IntVec3 targetCell,
            int createdTick,
            AerialRaidSourceType sourceType,
            bool isIntercepted,
            DMS_Legion.AerialRaid.AerialRaidComponents.AerialRaidPrePhaseComponent? sourceComponent)
        {
            TargetMap = targetMap;
            TargetCell = targetCell;
            CreatedTick = createdTick;
            SourceType = sourceType;
            IsIntercepted = isIntercepted;
            SourceComponent = sourceComponent;
        }
    }
}

