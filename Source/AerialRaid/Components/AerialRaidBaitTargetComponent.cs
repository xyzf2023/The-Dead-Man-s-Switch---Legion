using RimWorld;
using Verse;
using UnityEngine;
using System;

namespace DMS_Legion.AerialRaid.AerialRaidComponents
{
    /// <summary>
    /// 空袭诱饵目标坐标组件
    /// 独立管理诱饵目标坐标，生命周期独立于前置阶段组件
    /// 可以在任何时候设置和访问诱饵坐标
    /// </summary>
    public class AerialRaidBaitTargetComponent : MapComponent
    {
        /// <summary>
        /// 诱饵坐标的默认持续时间（Tick）
        /// 12500 tick = 5 小时（1小时 = 2500 tick）
        /// </summary>
        private const int DefaultBaitTargetLifetimeTicks = 12500;

        /// <summary>
        /// 当前诱饵坐标的持续时间（Tick），由最近一次 SetBaitTarget 调用指定
        /// </summary>
        private int baitTargetLifetimeTicks = DefaultBaitTargetLifetimeTicks;

        /// <summary>
        /// 诱饵目标坐标
        /// 可通过外部系统（如迫击炮炮弹落点、技能瞄准点等）设置的诱饵目标位置
        /// </summary>
        private IntVec3 baitTargetCell = IntVec3.Invalid;

        /// <summary>
        /// 诱饵坐标设置的时刻（TicksGame）
        /// 用于计算诱饵坐标的生命周期
        /// </summary>
        private int baitTargetSetTick = -1;

        /// <summary>
        /// 持续标记效果的 Mote
        /// 用于显示诱饵坐标位置的持续灰白色特效
        /// </summary>
        private Mote? markerMote = null;

        public AerialRaidBaitTargetComponent(Map map) : base(map)
        {
        }

        /// <summary>
        /// 获取或创建诱饵目标组件（静态方法）
        /// 如果组件不存在，会自动创建并添加到地图
        /// </summary>
        /// <param name="map">地图</param>
        /// <returns>诱饵目标组件，如果地图为null则返回null</returns>
        public static AerialRaidBaitTargetComponent? GetOrCreate(Map map)
        {
            if (map == null)
            {
                return null;
            }

            var component = map.GetComponent<AerialRaidBaitTargetComponent>();
            if (component == null)
            {
                component = new AerialRaidBaitTargetComponent(map);
                map.components.Add(component);
            }

            return component;
        }

        /// <summary>
        /// 设置诱饵目标坐标（使用默认持续时间，约 5 小时）
        /// </summary>
        /// <param name="cell">诱饵目标坐标</param>
        public void SetBaitTarget(IntVec3 cell)
        {
            SetBaitTarget(cell, DefaultBaitTargetLifetimeTicks);
        }

        /// <summary>
        /// 设置诱饵目标坐标及持续时间
        /// 外部系统可以通过此方法设置诱饵目标（例如综合探测阵列 Gizmo 指定 4 小时）
        /// 如果当前已有诱饵坐标，立即打断当前生命周期，创建新的生命周期
        /// </summary>
        /// <param name="cell">诱饵目标坐标</param>
        /// <param name="durationTicks">持续时间（Tick），例如 4 小时 = 10000</param>
        public void SetBaitTarget(IntVec3 cell, int durationTicks)
        {
            if (map != null && cell.IsValid && cell.InBounds(map))
            {
                // 清除旧的持续标记 Mote
                ClearMarkerMote();
                
                baitTargetLifetimeTicks = durationTicks <= 0 ? DefaultBaitTargetLifetimeTicks : durationTicks;
                baitTargetCell = cell;
                // 记录设置时刻，打断当前生命周期，创建新的生命周期
                baitTargetSetTick = Find.TickManager.TicksGame;
                
                // 触发闪光效果（一次性）
                EffecterDef flashEffecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail("DMSL_Effecter_BaitTargetFlash");
                if (flashEffecterDef != null)
                {
                    Effecter flashEffecter = flashEffecterDef.Spawn(cell, map, 1f);
                    flashEffecter.Trigger(new TargetInfo(cell, map), new TargetInfo(cell, map));
                    flashEffecter.Cleanup();
                }
                
                // 创建持续标记 Mote
                CreateMarkerMote(cell);

                var selector = AerialRaidTargetCandidateSelector.GetOrCreate(map);
                selector?.NotifyBaitTargetChanged(cell);
            }
            else
            {
                ClearBaitTarget();
            }
        }

        /// <summary>
        /// 清除诱饵目标坐标
        /// </summary>
        public void ClearBaitTarget()
        {
            ClearMarkerMote();
            baitTargetCell = IntVec3.Invalid;
            baitTargetSetTick = -1;
            if (map != null)
            {
                var selector = map.GetComponent<AerialRaidTargetCandidateSelector>();
                selector?.NotifyBaitTargetCleared();
            }
        }

        /// <summary>
        /// 获取诱饵目标坐标（只读查询）
        /// 仅在生命周期内返回有效坐标，超出生命周期返回 IntVec3.Invalid
        /// </summary>
        /// <returns>诱饵目标坐标，如果不存在或已过期则返回 IntVec3.Invalid</returns>
        public IntVec3 GetBaitTarget()
        {
            if (!IsBaitTargetWithinLifetime())
            {
                return IntVec3.Invalid;
            }

            if (baitTargetCell.IsValid && IsTargetStillValid(baitTargetCell))
            {
                return baitTargetCell;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// 检查是否存在有效的诱饵目标（在生命周期内且坐标有效）
        /// </summary>
        /// <returns>是否存在有效的诱饵目标</returns>
        public bool HasValidBaitTarget()
        {
            if (!IsBaitTargetWithinLifetime())
            {
                return false;
            }
            return baitTargetCell.IsValid && IsTargetStillValid(baitTargetCell);
        }

        /// <summary>
        /// 检查诱饵目标是否在生命周期内（内部方法）
        /// </summary>
        /// <returns>是否在生命周期内</returns>
        private bool IsBaitTargetWithinLifetime()
        {
            // 如果没有设置时间，说明没有有效的诱饵坐标
            if (baitTargetSetTick < 0)
            {
                return false;
            }

            // 计算当前时刻与设置时刻的差值
            int currentTick = Find.TickManager.TicksGame;
            int elapsedTicks = currentTick - baitTargetSetTick;

            // 检查是否在生命周期内
            return elapsedTicks >= 0 && elapsedTicks < baitTargetLifetimeTicks;
        }

        /// <summary>
        /// 检查目标是否仍然有效（内部方法）
        /// </summary>
        /// <param name="cell">要检查的目标坐标</param>
        /// <returns>目标是否有效</returns>
        private bool IsTargetStillValid(IntVec3 cell)
        {
            if (!cell.IsValid)
            {
                return false;
            }
            if (map == null)
            {
                return false;
            }
            return cell.InBounds(map);
        }

        /// <summary>
        /// 创建持续标记 Mote
        /// </summary>
        /// <param name="cell">目标坐标</param>
        private void CreateMarkerMote(IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            ThingDef markerMoteDef = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_DMSL_BaitTargetMarker");
            if (markerMoteDef == null)
            {
                Log.Warning($"[DMS_Legion]诱饵组件：未找到Mote定义：Mote_DMSL_BaitTargetMarker");
                return;
            }
            
            try
            {
                MoteThrown mote = (MoteThrown)ThingMaker.MakeThing(markerMoteDef);
                if (mote == null)
                {
                    Log.Error($"[DMS_Legion]诱饵组件：ThingMaker.MakeThing返回null");
                    return;
                }
                
                mote.exactPosition = cell.ToVector3Shifted();
                mote.airTimeLeft = 999999f; // 设置很长的持续时间
                // velocity 默认为 Vector3.zero，会保持静止
                
                GenSpawn.Spawn(mote, cell, map);
                markerMote = mote;
                
                // 立即调用一次 Maintain()，确保 Mote 立即显示（特别是组件刚创建时）
                // 因为 needsMaintenance=true 的 Mote 需要调用 Maintain() 才会显示
                mote.Maintain();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DMS_Legion]诱饵组件：创建Mote时发生异常：{ex.Message}，堆栈：{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 清除持续标记 Mote
        /// </summary>
        private void ClearMarkerMote()
        {
            if (markerMote != null)
            {
                try
                {
                    if (!markerMote.Destroyed && markerMote.Map != null)
                    {
                        markerMote.Destroy();
                    }
                }
                catch (System.Exception ex)
                {
                    // 捕获异常，防止销毁 Mote 时出现空引用
                    Log.Warning($"[DMS_Legion]诱饵组件：清除 Mote 时发生异常：{ex.Message}");
                }
                finally
                {
                    markerMote = null;
                }
            }
        }

        /// <summary>
        /// 每Tick更新
        /// </summary>
        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // 检查诱饵坐标是否已过期，如果过期则清除
            if (baitTargetCell.IsValid && !IsBaitTargetWithinLifetime())
            {
                ClearBaitTarget();
            }
            
            // 检查持续标记 Mote 是否需要清除（如果存在）
            if (markerMote != null)
            {
                try
                {
                    // 检查 Mote 是否已被销毁或无效
                    if (markerMote.Destroyed || markerMote.Map == null || markerMote.Map != map)
                    {
                        markerMote = null;
                        return;
                    }

                    // 检查坐标是否仍然有效
                    if (!baitTargetCell.IsValid || !IsTargetStillValid(baitTargetCell) || !IsBaitTargetWithinLifetime())
                    {
                        // 如果坐标无效或已过期，清除 Mote
                        ClearMarkerMote();
                    }
                    else
                    {
                        // 参考原版实现：对于 needsMaintenance=true 的 Mote，需要每 tick 调用 Maintain() 来保持活跃
                        // 如果不调用，Mote 会自动淡出并消失（因为 fadeOutUnmaintained=true）
                        markerMote.Maintain();
                    }
                }
                catch (System.Exception ex)
                {
                    // 捕获异常，防止 Mote 更新时出现空引用导致循环报错
                    Log.Warning($"[DMS_Legion]诱饵组件：检查 Mote 时发生异常：{ex.Message}");
                    markerMote = null;
                }
            }
        }

        /// <summary>
        /// 保存/加载数据
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref baitTargetLifetimeTicks, "baitTargetLifetimeTicks", DefaultBaitTargetLifetimeTicks);
            Scribe_Values.Look(ref baitTargetCell, "baitTargetCell", IntVec3.Invalid);
            Scribe_Values.Look(ref baitTargetSetTick, "baitTargetSetTick", -1);
            
            // 读档后重新创建持续标记 Mote（Mote 不需要存档）
            if (Scribe.mode == LoadSaveMode.PostLoadInit && baitTargetCell.IsValid && IsBaitTargetWithinLifetime())
            {
                CreateMarkerMote(baitTargetCell);
            }
        }
    }
}
