using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// 轰炸任务抵达目标格后：仅支持已加载地图，预约空中支援（不跳视角）。
    /// 集束模式：单点 + Once/Twice/Thrice/FourTimes/FiveTimes；多点模式：各点依次 Once，首点 300 tick，后续点间隔 120～240 tick（构造时确定以便存档稳定）。
    /// </summary>
    public class TransportersArrivalAction_AXF12Bombing : TransportersArrivalAction
    {
        public override bool GeneratesMap => false;

        private PlanetTile originTile;
        private PlanetTile targetTile;
        private IntVec3 originCell;
        private IntVec3 targetCell;
        private List<IntVec3>? targetCells;
        private bool multiPointBombing;
        private List<int>? supportDelayTicks;
        private int requiredLoiterTicks = 600;
        private string? supportTypeDefName;
        private string? transportShipDefName;
        private string? worldObjectDefName;

        public int RequiredLoiterTicks => Math.Max(600, requiredLoiterTicks);

        public TransportersArrivalAction_AXF12Bombing()
        {
        }

        /// <summary>集束轰炸：单目标点。</summary>
        public TransportersArrivalAction_AXF12Bombing(
            PlanetTile originTile,
            PlanetTile targetTile,
            IntVec3 originCell,
            IntVec3 targetCell,
            string supportTypeDefName,
            string transportShipDefName,
            string worldObjectDefName)
        {
            this.originTile = originTile;
            this.targetTile = targetTile;
            this.originCell = originCell;
            this.targetCell = targetCell;
            this.supportTypeDefName = supportTypeDefName;
            this.transportShipDefName = transportShipDefName;
            this.worldObjectDefName = worldObjectDefName;
            multiPointBombing = false;
            requiredLoiterTicks = 600;
        }

        /// <summary>多点轰炸：多个目标点，各点依次执行 Once。</summary>
        public TransportersArrivalAction_AXF12Bombing(
            PlanetTile originTile,
            PlanetTile targetTile,
            IntVec3 originCell,
            List<IntVec3> targetCells,
            string supportTypeDefName,
            string transportShipDefName,
            string worldObjectDefName,
            bool multiPointBombing)
        {
            this.originTile = originTile;
            this.targetTile = targetTile;
            this.originCell = originCell;
            this.targetCells = targetCells;
            this.supportTypeDefName = supportTypeDefName;
            this.transportShipDefName = transportShipDefName;
            this.worldObjectDefName = worldObjectDefName;
            this.multiPointBombing = multiPointBombing;

            supportDelayTicks = new List<int>();
            int delay = 300;
            for (int i = 0; i < targetCells.Count; i++)
            {
                supportDelayTicks.Add(delay);
                delay += Rand.RangeInclusive(120, 240);
            }

            requiredLoiterTicks = Math.Max(600, supportDelayTicks[supportDelayTicks.Count - 1] + 300);
        }

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);
            if (mapParent == null)
            {
                Log.Error("[DMS_Legion][AXF12] 轰炸目标地点没有世界对象。");
                return;
            }

            if (!mapParent.HasMap)
            {
                Log.Error("[DMS_Legion][AXF12] 轰炸仅支持已加载地图，当前格未加载。");
                return;
            }

            Map map = mapParent.Map;
            Current.Game.CurrentMap = map;

            var delayComp = map.GetComponent<AXF12ReconSupportDelayComponent>();

            if (!multiPointBombing)
            {
                string defName = supportTypeDefName ?? "DMSL_AerialSupport_AXF12Bombing_Once";
                delayComp?.Schedule(targetCell, defName, clearFog: false, delayTicks: 300);
                return;
            }

            if (targetCells == null || supportDelayTicks == null)
            {
                Log.Error("[DMS_Legion][AXF12] 多点轰炸数据不完整。");
                return;
            }

            int count = Math.Min(targetCells.Count, supportDelayTicks.Count);
            for (int i = 0; i < count; i++)
            {
                delayComp?.Schedule(
                    targetCells[i],
                    "DMSL_AerialSupport_AXF12Bombing_Once",
                    clearFog: false,
                    delayTicks: supportDelayTicks[i]);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref originTile, "originTile");
            Scribe_Values.Look(ref targetTile, "targetTile");
            Scribe_Values.Look(ref originCell, "originCell");
            Scribe_Values.Look(ref targetCell, "targetCell");
            Scribe_Collections.Look(ref targetCells, "targetCells", LookMode.Value);
            Scribe_Values.Look(ref multiPointBombing, "multiPointBombing", false);
            Scribe_Collections.Look(ref supportDelayTicks, "supportDelayTicks", LookMode.Value);
            Scribe_Values.Look(ref requiredLoiterTicks, "requiredLoiterTicks", 600);
            Scribe_Values.Look(ref supportTypeDefName, "supportTypeDefName");
            Scribe_Values.Look(ref transportShipDefName, "transportShipDefName");
            Scribe_Values.Look(ref worldObjectDefName, "worldObjectDefName");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                targetCells ??= new List<IntVec3>();
                if (targetCells.Count == 0 && targetCell.IsValid)
                {
                    targetCells.Add(targetCell);
                }

                supportDelayTicks ??= new List<int> { 300 };
                if (requiredLoiterTicks < 600)
                {
                    requiredLoiterTicks = 600;
                }
            }
        }

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
        {
            return destinationTile == targetTile;
        }

        public PlanetTile OriginTile => originTile;
        public IntVec3 OriginCell => originCell;
        public string TransportShipDefName => transportShipDefName ?? "DMSL_AXF12_OffsetConfig";
    }
}
