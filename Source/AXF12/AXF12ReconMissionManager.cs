using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.AXF12
{
    public class AXF12ReconMissionManager : GameComponent
    {
        private static AXF12ReconMissionManager? instance;
        public static AXF12ReconMissionManager? Instance => instance;

        private List<AXF12ReconMission> missions = new List<AXF12ReconMission>();
        private MapParent? observedMapParent;

        public MapParent? ObservedMapParent => observedMapParent;

        private const int MinReturnDelayTicks = 120;

        private static readonly FieldInfo ActiveFlightsField =
            typeof(AerialSupportRenderer).GetField("activeFlights", BindingFlags.Instance | BindingFlags.NonPublic);

        public AXF12ReconMissionManager(Game game)
        {
            instance = this;
        }

        public void RegisterMission(AXF12ReconMission mission)
        {
            if (mission == null)
            {
                return;
            }
            mission.supportStartTick = Find.TickManager.TicksGame;
            missions.Add(mission);
        }

        public void SetObservedMap(MapParent? mapParent)
        {
            observedMapParent = mapParent;
        }

        /// <summary>
        /// 停止所有侦察维持的地图观测。若无正在观察的地图则发送提示消息，然后仍执行一次安全的清空。
        /// </summary>
        public static void StopObservingAllRecon()
        {
            if (Instance == null)
            {
                return;
            }
            if (Instance.ObservedMapParent == null)
            {
                Messages.Message("DMSL_AXF12_NoObservedMap".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            Instance.SetObservedMap(null);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (missions.Count == 0)
            {
                return;
            }

            for (int i = missions.Count - 1; i >= 0; i--)
            {
                var mission = missions[i];
                if (mission.returnLaunched)
                {
                    missions.RemoveAt(i);
                    continue;
                }

                Map? map = Find.Maps.FirstOrDefault(m => m.uniqueID == mission.targetMapId);
                if (map == null)
                {
                    continue;
                }

                if (Find.TickManager.TicksGame - mission.supportStartTick < MinReturnDelayTicks)
                {
                    continue;
                }

                string supportDefName = mission.supportTypeDefName ?? "DMSL_AerialSupport_AXF12Recon";
                if (IsSupportActive(map, supportDefName))
                {
                    continue;
                }

                LaunchReturn(mission);
                mission.returnLaunched = true;
            }
        }

        private static bool IsSupportActive(Map map, string supportDefName)
        {
            var renderer = map.GetComponent<AerialSupportRenderer>();
            if (renderer == null || ActiveFlightsField == null)
            {
                return false;
            }

            var flights = ActiveFlightsField.GetValue(renderer) as IEnumerable<AircraftFlight>;
            if (flights == null)
            {
                return false;
            }

            foreach (var flight in flights)
            {
                if (flight?.supportType?.defName == supportDefName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void LaunchReturn(AXF12ReconMission mission)
        {
            string worldObjectDefName = mission.worldObjectDefName ?? "DMSL_AXF12_OffsetConfig_Traveling";
            WorldObjectDef? worldObjectDef = DefDatabase<WorldObjectDef>.GetNamed(worldObjectDefName, false);
            if (worldObjectDef == null)
            {
                Log.Error($"[DMS_Legion][AXF12] 未找到世界对象定义: {worldObjectDefName}");
                return;
            }

            var worldObject = WorldObjectMaker.MakeWorldObject(worldObjectDef) as TravellingTransporters;
            if (worldObject == null)
            {
                Log.Error("[DMS_Legion][AXF12] 返航世界对象创建失败。");
                return;
            }

            worldObject.Tile = mission.targetTile;
            worldObject.SetFaction(Faction.OfPlayer);
            worldObject.destinationTile = mission.originTile;
            string transportShipDefName = mission.transportShipDefName ?? "DMSL_AXF12_OffsetConfig";
            worldObject.arrivalAction = new TransportersArrivalAction_AXF12Return(
                mission.originCell,
                transportShipDefName);

            foreach (var transporter in mission.transporters)
            {
                worldObject.AddTransporter(transporter, true);
            }

            Find.WorldObjects.Add(worldObject);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref missions, "axf12ReconMissions", LookMode.Deep);
            Scribe_References.Look(ref observedMapParent, "axf12ObservedMapParent", false);
        }
    }

    public class AXF12ReconMission : IExposable
    {
        public PlanetTile originTile;
        public PlanetTile targetTile;
        public IntVec3 originCell;
        public int targetMapId;
        public string? supportTypeDefName = "DMSL_AerialSupport_AXF12Recon";
        public string? transportShipDefName = "DMSL_AXF12_OffsetConfig";
        public string? worldObjectDefName = "DMSL_AXF12_OffsetConfig_Traveling";
        public List<ActiveTransporterInfo> transporters = new List<ActiveTransporterInfo>();
        public int supportStartTick;
        public bool returnLaunched;

        public void ExposeData()
        {
            Scribe_Values.Look(ref originTile, "originTile");
            Scribe_Values.Look(ref targetTile, "targetTile");
            Scribe_Values.Look(ref originCell, "originCell");
            Scribe_Values.Look(ref targetMapId, "targetMapId");
            Scribe_Values.Look(ref supportTypeDefName, "supportTypeDefName");
            Scribe_Values.Look(ref transportShipDefName, "transportShipDefName");
            Scribe_Values.Look(ref worldObjectDefName, "worldObjectDefName");
            Scribe_Collections.Look(ref transporters, "transporters", LookMode.Deep);
            Scribe_Values.Look(ref supportStartTick, "supportStartTick");
            Scribe_Values.Look(ref returnLaunched, "returnLaunched");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                supportTypeDefName ??= "DMSL_AerialSupport_AXF12Recon";
                transportShipDefName ??= "DMSL_AXF12_OffsetConfig";
                worldObjectDefName ??= "DMSL_AXF12_OffsetConfig_Traveling";
            }
        }
    }
}
