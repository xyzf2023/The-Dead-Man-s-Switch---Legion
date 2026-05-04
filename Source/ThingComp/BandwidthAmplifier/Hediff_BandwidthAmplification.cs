// ============================================================================
// 频段增幅：独立于原版 BandNode 的带宽加成健康状态，仅统计 DMSL_Building_BandwidthAmplifier。
// 是否提供带宽由建筑的充能缓冲（CompBandwidthAmplifierBuffer）决定：充能 > 0 才计入。
// 研究「集群收发器」后每座 +3 带宽，否则 +2。提供的带宽达到 20 时增加控制组：+1，研究后额外 +2（共 +3）。
// 带宽降至 20 以下时由原版 Notify_ControlGroupAmountMayChanged 根据 stat 收缩控制组并 reassign 机械体。
// ============================================================================

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 由频段增幅装置提供的带宽加成 hediff，仅统计本 mod 的增幅装置建筑。
    /// </summary>
    public class Hediff_BandwidthAmplification : Hediff
    {
        private const int CheckInterval = 60;

        private static readonly MethodInfo NotifyControlGroupAmountMayChangedMethod =
            AccessTools.Method(typeof(Pawn_MechanitorTracker), "Notify_ControlGroupAmountMayChanged");

        private int _cachedBandwidth;
        private HediffStage? _curStage;

        public override bool ShouldRemove => _cachedBandwidth == 0;

        /// <summary>
        /// 在健康状态名称后显示提供的带宽数量，与原版频带节点在统计说明中的展示方式一致。
        /// </summary>
        public override string LabelBase =>
            _cachedBandwidth > 0
                ? def.label + " (+" + _cachedBandwidth + ")"
                : def.label;

        public override HediffStage CurStage
        {
            get
            {
                if (_curStage == null && _cachedBandwidth > 0)
                {
                    var list = new List<StatModifier>
                    {
                        new StatModifier { stat = StatDefOf.MechBandwidth, value = _cachedBandwidth }
                    };
                    if (_cachedBandwidth >= 20)
                    {
                        bool hasResearch = DMSL_GameComponent_ClusterTransceiverResearchCache.GetOrCreate()?.ClusterTransceiverCompleted == true;
                        list.Add(new StatModifier { stat = StatDefOf.MechControlGroups, value = hasResearch ? 3 : 1 });
                    }
                    _curStage = new HediffStage { statOffsets = list };
                }
                return _curStage!;
            }
        }

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (pawn.IsHashIntervalTick(CheckInterval, delta))
                RecacheBandwidth();
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            RecacheBandwidth();
        }

        private void RecacheBandwidth()
        {
            int prev = _cachedBandwidth;
            ThingDef? amplifierDef = DefDatabase<ThingDef>.GetNamed("DMSL_Building_BandwidthAmplifier", false);
            if (amplifierDef == null)
            {
                _cachedBandwidth = 0;
                _curStage = null;
                pawn.mechanitor?.Notify_BandwidthChanged();
                if (prev >= 20 && pawn.mechanitor != null)
                    NotifyControlGroupAmountMayChanged(pawn.mechanitor);
                return;
            }

            bool hasResearch = DMSL_GameComponent_ClusterTransceiverResearchCache.GetOrCreate()?.ClusterTransceiverCompleted == true;
            int bandwidthPerBuilding = hasResearch ? 3 : 2;

            _cachedBandwidth = 0;

            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                List<Building> buildings = map.listerBuildings.AllBuildingsColonistOfDef(amplifierDef);
                for (int j = 0; j < buildings.Count; j++)
                {
                    Building b = buildings[j];
                    CompBandNode? bandComp = b.TryGetComp<CompBandNode>();
                    if (bandComp == null || bandComp.tunedTo != pawn)
                        continue;
                    var buffer = b.TryGetComp<CompBandwidthAmplifierBuffer>();
                    if (buffer == null || !buffer.AllowBandwidth)
                        continue;
                    _cachedBandwidth += bandwidthPerBuilding;
                }
            }

            if (prev != _cachedBandwidth)
            {
                _curStage = null;
                pawn.mechanitor?.Notify_BandwidthChanged();
                if ((prev >= 20) != (_cachedBandwidth >= 20) && pawn.mechanitor != null)
                    NotifyControlGroupAmountMayChanged(pawn.mechanitor);
            }
        }

        private static void NotifyControlGroupAmountMayChanged(Pawn_MechanitorTracker mechanitor)
        {
            if (mechanitor == null) return;
            NotifyControlGroupAmountMayChangedMethod?.Invoke(mechanitor, null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _cachedBandwidth, "cachedBandwidth", 0);
        }
    }
}
