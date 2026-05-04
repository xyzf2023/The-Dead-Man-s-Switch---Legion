// ============================================================================
// 文件：CommsSupportPendingComponent.cs
// 说明：通讯台空中支援待执行 MapComponent，存储待执行的支援（supportTypeDefName、points、remainingTicks）
// 功能：每 tick 减一，到期调协调器并发 Message
// ============================================================================

using System.Collections.Generic;
using DMS_Legion.GroundSupport;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 通讯台空中支援待执行组件
    /// </summary>
    public class CommsSupportPendingComponent : MapComponent
    {
        /// <summary>核打击支援类型 defName，与 CommsNukeStrikeTargeting 一致，用于 Alert 筛选</summary>
        public const string NuclearStrikeDefName = "DMSL_AerialSupport_NuclearStrike";

        private List<PendingSupport> pendingSupports = new List<PendingSupport>();

        public CommsSupportPendingComponent(Map map) : base(map)
        {
        }

        /// <summary>
        /// 获取或创建该 Map 上的待执行组件（若不存在则添加）
        /// </summary>
        public static CommsSupportPendingComponent? GetOrCreate(Map map)
        {
            if (map == null)
                return null;
            var comp = map.GetComponent<CommsSupportPendingComponent>();
            if (comp == null)
            {
                comp = new CommsSupportPendingComponent(map);
                map.components.Add(comp);
            }
            return comp;
        }

        /// <summary>
        /// 调度待执行支援：remainingTicks 随机 300～900，立即发 Message
        /// </summary>
        public void Schedule(string supportTypeDefName, List<IntVec3> points)
        {
            if (string.IsNullOrWhiteSpace(supportTypeDefName) || points == null || points.Count == 0)
            {
                Log.Warning("[DMS_Legion] CommsSupportPendingComponent.Schedule 参数无效");
                return;
            }

            int remainingTicks = Rand.Range(120, 481);///支援时间区间
            pendingSupports.Add(new PendingSupport
            {
                supportTypeDefName = supportTypeDefName,
                points = new List<IntVec3>(points),
                remainingTicks = remainingTicks
            });

            int seconds = Mathf.CeilToInt(remainingTicks / 60f);
            Messages.Message("DMSL_Comms_SupportEtaMessage".Translate(seconds), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// 调度核打击：remainingTicks 随机 2500～7500，立即发 Message（小时数 = 倒计时/2500，保留一位小数向下取整），到期后执行一次核打击
        /// </summary>
        public void ScheduleNukeStrike(string supportTypeDefName, List<IntVec3> points)
        {
            if (string.IsNullOrWhiteSpace(supportTypeDefName) || points == null || points.Count == 0)
            {
                Log.Warning("[DMS_Legion] CommsSupportPendingComponent.ScheduleNukeStrike 参数无效");
                return;
            }

            int remainingTicks = Rand.Range(2500, 7501);
            pendingSupports.Add(new PendingSupport
            {
                supportTypeDefName = supportTypeDefName,
                points = new List<IntVec3>(points),
                remainingTicks = remainingTicks
            });

            float hours = Mathf.Floor(remainingTicks / 2500f * 10f) / 10f;
            Messages.Message("DMSL_NukeStrike_EtaMessage".Translate(hours), MessageTypeDefOf.NeutralEvent);
            NukeStrikeCooldownComponent.GetOrCreate()?.StartCooldown();
        }

        /// <summary>
        /// 获取本地图上所有待执行核打击的剩余 tick 列表（用于严重警告 Alert 显示），仅返回 &gt; 0 的项。
        /// </summary>
        public List<int> GetPendingNukeStrikeRemainingTicks()
        {
            var list = new List<int>();
            foreach (var p in pendingSupports)
            {
                if (p.supportTypeDefName == NuclearStrikeDefName && p.remainingTicks > 0)
                    list.Add(p.remainingTicks);
            }
            return list;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (pendingSupports.Count == 0)
            {
                return;
            }

            for (int i = pendingSupports.Count - 1; i >= 0; i--)
            {
                var entry = pendingSupports[i];
                entry.remainingTicks--;
                if (entry.remainingTicks > 0)
                {
                    pendingSupports[i] = entry;
                    continue;
                }

                pendingSupports.RemoveAt(i);
                ExecuteSupport(entry);
            }
        }

        private void ExecuteSupport(PendingSupport entry)
        {
            if (map == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.supportTypeDefName))
            {
                Log.Error("[DMS_Legion] CommsSupportPendingComponent 空中支援类型为空，无法执行。");
                return;
            }

            if (entry.points == null || entry.points.Count == 0)
            {
                Log.Error("[DMS_Legion] CommsSupportPendingComponent 目标点列表为空，无法执行。");
                return;
            }

            var supportType = DefDatabase<AerialSupportTypeDef>.GetNamed(entry.supportTypeDefName, false);
            if (supportType == null)
            {
                Log.Error($"[DMS_Legion] CommsSupportPendingComponent 未找到空中支援类型: {entry.supportTypeDefName}");
                return;
            }

            AerialSupportCoordinator.Instance?.RequestSupportAt(entry.points, map, supportType);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingSupports, "commsSupportPendingSupports", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingSupports ??= new List<PendingSupport>();
            }
        }

        private struct PendingSupport : IExposable
        {
            public string? supportTypeDefName;
            public List<IntVec3>? points;
            public int remainingTicks;

            public void ExposeData()
            {
                Scribe_Values.Look(ref supportTypeDefName, "supportTypeDefName");
                Scribe_Collections.Look(ref points, "points", LookMode.Value);
                Scribe_Values.Look(ref remainingTicks, "remainingTicks");
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    supportTypeDefName ??= string.Empty;
                    points ??= new List<IntVec3>();
                }
            }
        }
    }
}
