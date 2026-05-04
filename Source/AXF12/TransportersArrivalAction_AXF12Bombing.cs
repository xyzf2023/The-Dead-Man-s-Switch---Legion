using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// 轰炸任务抵达目标格后：仅支持已加载地图，预约 300 tick 后空中支援（不跳视角）。
    /// </summary>
    public class TransportersArrivalAction_AXF12Bombing : TransportersArrivalAction
    {
        public override bool GeneratesMap => false;

        private PlanetTile originTile;
        private PlanetTile targetTile;
        private IntVec3 originCell;
        private IntVec3 targetCell;
        private string? supportTypeDefName;
        private string? transportShipDefName;
        private string? worldObjectDefName;

        public TransportersArrivalAction_AXF12Bombing()
        {
        }

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

            string defName = supportTypeDefName ?? "DMSL_AerialSupport_AXF12Bombing_Once";
            map.GetComponent<AXF12ReconSupportDelayComponent>()?
                .Schedule(targetCell, defName, clearFog: false, delayTicks: 300);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref originTile, "originTile");
            Scribe_Values.Look(ref targetTile, "targetTile");
            Scribe_Values.Look(ref originCell, "originCell");
            Scribe_Values.Look(ref targetCell, "targetCell");
            Scribe_Values.Look(ref supportTypeDefName, "supportTypeDefName");
            Scribe_Values.Look(ref transportShipDefName, "transportShipDefName");
            Scribe_Values.Look(ref worldObjectDefName, "worldObjectDefName");
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
