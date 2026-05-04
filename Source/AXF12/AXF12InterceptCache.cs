using System.Collections.Generic;
using System.Linq;
using DMS_Legion.AerialRaid.AerialRaidComponents;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// 拦截缓存项：持有运输体列表、起降信息、到点 tick 与目标地图，到点后取消空袭并执行返航着陆。
    /// 支持存档：通过 IExposable 序列化字段。
    /// </summary>
    public class AXF12InterceptCacheEntry : IExposable
    {
        public List<ActiveTransporterInfo> Transporters = new List<ActiveTransporterInfo>();
        public Map? OriginMap;
        public IntVec3 OriginCell;
        public int EndTick;
        public Map? TargetMap;
        public string TransportShipDefName = "DMSL_AXF12_OffsetConfig";

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Transporters, "transporters", LookMode.Deep);
            Scribe_References.Look(ref OriginMap, "originMap");
            Scribe_Values.Look(ref OriginCell, "originCell");
            Scribe_Values.Look(ref EndTick, "endTick");
            Scribe_References.Look(ref TargetMap, "targetMap");
            Scribe_Values.Look(ref TransportShipDefName, "transportShipDefName", "DMSL_AXF12_OffsetConfig");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Transporters ??= new List<ActiveTransporterInfo>();
                TransportShipDefName ??= "DMSL_AXF12_OffsetConfig";
            }
        }
    }

    /// <summary>
    /// 待降落项：成功将空袭次数改为 0 后延迟 600 tick 再执行降落。
    /// 支持存档：通过 IExposable 序列化字段。
    /// </summary>
    public class AXF12PendingLandingEntry : IExposable
    {
        public List<ActiveTransporterInfo> Transporters = new List<ActiveTransporterInfo>();
        public Map? OriginMap;
        public IntVec3 OriginCell;
        public int EndTick;
        public string TransportShipDefName = "DMSL_AXF12_OffsetConfig";

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Transporters, "transporters", LookMode.Deep);
            Scribe_References.Look(ref OriginMap, "originMap");
            Scribe_Values.Look(ref OriginCell, "originCell");
            Scribe_Values.Look(ref EndTick, "endTick");
            Scribe_Values.Look(ref TransportShipDefName, "transportShipDefName", "DMSL_AXF12_OffsetConfig");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Transporters ??= new List<ActiveTransporterInfo>();
                TransportShipDefName ??= "DMSL_AXF12_OffsetConfig";
            }
        }
    }

    /// <summary>
    /// 管理 AXF12 拦截缓存与计时：到点后对 PrePhase 调用 SetExecutionCount(0)、发信，600 tick 后再降落；无倒计时时到点直接降落。
    /// </summary>
    public class AXF12InterceptCache : GameComponent
    {
        private static AXF12InterceptCache? instance;
        public static AXF12InterceptCache? Instance => instance;

        private List<AXF12InterceptCacheEntry> entries = new List<AXF12InterceptCacheEntry>();
        private List<AXF12PendingLandingEntry> pendingLandings = new List<AXF12PendingLandingEntry>();

        public AXF12InterceptCache(Game game)
        {
            instance = this;
        }

        public void AddEntry(AXF12InterceptCacheEntry entry)
        {
            if (entry?.Transporters == null || entry.Transporters.Count == 0)
                return;
            entries.Add(entry);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            Scribe_Collections.Look(ref pendingLandings, "pendingLandings", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                entries ??= new List<AXF12InterceptCacheEntry>();
                pendingLandings ??= new List<AXF12PendingLandingEntry>();
                instance = this;
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int now = Find.TickManager.TicksGame;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.EndTick > now)
                    continue;

                entries.RemoveAt(i);
                ExecuteInterceptArrival(e);
            }

            for (int i = pendingLandings.Count - 1; i >= 0; i--)
            {
                var p = pendingLandings[i];
                if (p.EndTick > now)
                    continue;

                pendingLandings.RemoveAt(i);
                if (p.OriginMap != null && p.Transporters != null && p.Transporters.Count > 0)
                {
                    TransportersArrivalAction_AXF12Return.DoLandingAtMap(
                        p.Transporters,
                        p.OriginMap,
                        p.OriginCell,
                        p.TransportShipDefName ?? "DMSL_AXF12_OffsetConfig",
                        applyInterceptDurabilityDamage: true);
                }
            }
        }

        private static void ExecuteInterceptArrival(AXF12InterceptCacheEntry e)
        {
            Map? originMap = e.OriginMap;
            if (originMap == null || !originMap.IsPlayerHome)
            {
                // 优先寻找任意玩家家园地图，避免 originMap 丢失时无法返航
                originMap = Find.Maps.FirstOrDefault(m => m != null && m.IsPlayerHome);
                if (originMap == null)
                {
                    originMap = Find.CurrentMap;
                }
                if (originMap == null)
                {
                    Log.Warning("[DMS_Legion][AXF12] 拦截返航时无法解析 origin 地图，无法确定返航地点。");
                }
            }

            Map? targetMap = e.TargetMap;
            var prePhase = targetMap?.GetComponent<AerialRaidPrePhaseComponent>();
            if (prePhase != null)
            {
                prePhase.SetExecutionCount(0);
                Find.LetterStack.ReceiveLetter(
                    "DMSL_AXF12_InterceptSuccessLetterTitle".Translate(),
                    "DMSL_AXF12_InterceptSuccessLetterText".Translate(),
                    LetterDefOf.PositiveEvent);

                const int LandingDelayTicks = 600;
                int now = Find.TickManager.TicksGame;
                var pending = new AXF12PendingLandingEntry
                {
                    Transporters = e.Transporters,
                    OriginMap = originMap,
                    OriginCell = e.OriginCell,
                    EndTick = now + LandingDelayTicks,
                    TransportShipDefName = e.TransportShipDefName ?? "DMSL_AXF12_OffsetConfig"
                };
                instance?.pendingLandings.Add(pending);
            }
            else
            {
                if (targetMap != null)
                    Log.Warning("[DMS_Legion][AXF12] 拦截到点时 targetMap 上未找到 AerialRaidPrePhaseComponent，空袭可能未被取消。");
                else
                    Log.Warning("[DMS_Legion][AXF12] 拦截到点时 targetMap 为空，无法取消空袭。");

                // 即便无法取消空袭，也尽量保证 AXF12 有返航着陆：originMap 解析失败时再尝试一次兜底地图解析
                if (originMap == null)
                {
                    originMap = Find.Maps.FirstOrDefault(m => m != null && m.IsPlayerHome) ?? Find.CurrentMap;
                }

                if (originMap != null && e.Transporters != null && e.Transporters.Count > 0)
                {
                    TransportersArrivalAction_AXF12Return.DoLandingAtMap(
                        e.Transporters,
                        originMap,
                        e.OriginCell,
                        e.TransportShipDefName ?? "DMSL_AXF12_OffsetConfig");
                }
            }
        }
    }
}
