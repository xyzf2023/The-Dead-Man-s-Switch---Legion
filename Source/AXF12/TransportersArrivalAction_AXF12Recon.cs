using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.AXF12
{
    public class TransportersArrivalAction_AXF12Recon : TransportersArrivalAction
    {
        private PlanetTile originTile;
        private PlanetTile targetTile;
        private IntVec3 originCell;
        private string? supportTypeDefName = "DMSL_AerialSupport_AXF12Recon";
        private string? transportShipDefName = "DMSL_AXF12_OffsetConfig";
        private string? worldObjectDefName = "DMSL_AXF12_OffsetConfig_Traveling";

        public override bool GeneratesMap => true;

        public TransportersArrivalAction_AXF12Recon()
        {
        }

        public TransportersArrivalAction_AXF12Recon(
            PlanetTile originTile,
            PlanetTile targetTile,
            IntVec3 originCell,
            string supportTypeDefName,
            string transportShipDefName,
            string worldObjectDefName)
        {
            this.originTile = originTile;
            this.targetTile = targetTile;
            this.originCell = originCell;
            this.supportTypeDefName = supportTypeDefName;
            this.transportShipDefName = transportShipDefName;
            this.worldObjectDefName = worldObjectDefName;
        }

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);
            if (mapParent == null)
            {
                Log.Error("[DMS_Legion][AXF12] 目标地点没有可生成地图的世界对象。");
                return;
            }

            AXF12ReconMissionManager.Instance?.SetObservedMap(mapParent);

            if (mapParent.HasMap)
            {
                ExecuteRecon(transporters, mapParent);
                return;
            }

            LongEventHandler.QueueLongEvent(
                () => ExecuteRecon(transporters, mapParent),
                "GeneratingMapForNewEncounter",
                false,
                GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        private void ExecuteRecon(List<ActiveTransporterInfo> transporters, MapParent mapParent)
        {
            bool hadMap = mapParent.HasMap;
            Map map = mapParent.HasMap
                ? mapParent.Map
                : GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, mapParent.def);
            if (map == null)
            {
                Log.Error("[DMS_Legion][AXF12] 生成目标地图失败。");
                return;
            }

            AXF12ReconMissionManager.Instance?.SetObservedMap(map.Parent);

            Current.Game.CurrentMap = map;
            if (!hadMap)
            {
                CameraJumper.TryJump(map.Center, map, CameraJumper.MovementMode.Pan);
            }

            if (mapParent.Faction != null
                && mapParent.Faction != Faction.OfPlayer
                && !mapParent.Faction.HostileTo(Faction.OfPlayer))
            {
                Faction faction = mapParent.Faction;
                if (faction.HasGoodwill && Faction.OfPlayer.HasGoodwill)
                {
                    HistoryEventDef? reason = DefDatabase<HistoryEventDef>.GetNamed("DMSL_ReconAirspaceViolation", false);
                    Faction.OfPlayer.TryAffectGoodwillWith(faction, Faction.OfPlayer.GoodwillToMakeHostile(faction), true, true, reason, null);
                }
                else
                {
                    faction.SetRelationDirect(Faction.OfPlayer, FactionRelationKind.Hostile, true, "领空冒犯", null);
                }
            }

            string supportDefName = supportTypeDefName ?? "DMSL_AerialSupport_AXF12Recon";
            var supportType = DefDatabase<AerialSupportTypeDef>.GetNamed(supportDefName, false);
            if (supportType == null)
            {
                Log.Error($"[DMS_Legion][AXF12] 未找到空中支援类型: {supportDefName}");
                return;
            }

            map.GetComponent<AXF12ReconSupportDelayComponent>()?
                .Schedule(map.Center, supportType.defName, clearFog: hadMap, delayTicks: 20);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref originTile, "originTile");
            Scribe_Values.Look(ref targetTile, "targetTile");
            Scribe_Values.Look(ref originCell, "originCell");
            Scribe_Values.Look(ref supportTypeDefName, "supportTypeDefName");
            Scribe_Values.Look(ref transportShipDefName, "transportShipDefName");
            Scribe_Values.Look(ref worldObjectDefName, "worldObjectDefName");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                supportTypeDefName ??= "DMSL_AerialSupport_AXF12Recon";
                transportShipDefName ??= "DMSL_AXF12_OffsetConfig";
                worldObjectDefName ??= "DMSL_AXF12_OffsetConfig_Traveling";
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
