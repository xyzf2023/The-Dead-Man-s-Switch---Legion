using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion.AXF12
{
    public class TransportersArrivalAction_AXF12Return : TransportersArrivalAction
    {
        private const string FallbackArrivalMessageKey = "MessageShuttleArrived";

        private IntVec3 originCell;
        private string? transportShipDefName = "DMSL_AXF12_OffsetConfig";

        private static readonly List<Pawn> tmpPawns = new List<Pawn>();
        private static readonly List<Thing> tmpContainedThings = new List<Thing>();

        public override bool GeneratesMap => true;

        public TransportersArrivalAction_AXF12Return()
        {
        }

        public TransportersArrivalAction_AXF12Return(IntVec3 originCell, string transportShipDefName)
        {
            this.originCell = originCell;
            this.transportShipDefName = transportShipDefName;
        }

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, null);
            if (map == null)
            {
                Log.Warning("[DMS_Legion][AXF12] 返航地图生成失败，尝试在世界格组建远行队。");
                TryFormCaravanFallback(transporters, tile);
                return;
            }
            DoLandingAtMap(transporters, map, originCell, transportShipDefName ?? "DMSL_AXF12_OffsetConfig");
        }

        /// <summary>
        /// 在指定地图的指定格点执行着陆（供侦察返航与拦截返航复用）。
        /// </summary>
        /// <param name="applyInterceptDurabilityDamage">为 true 时（拦截成功返航），将船体耐久设为起飞时的 [1/2, 1] 倍随机值（向上取整）。</param>
        public static void DoLandingAtMap(
            List<ActiveTransporterInfo> transporters,
            Map map,
            IntVec3 originCell,
            string transportShipDefName,
            bool applyInterceptDurabilityDamage = false)
        {
            var shipDef = DefDatabase<TransportShipDef>.GetNamed(transportShipDefName, false);
            if (shipDef == null)
            {
                Log.Error($"[DMS_Legion][AXF12] 未找到运输船定义: {transportShipDefName}");
                return;
            }

            Thing? shuttle = ExtractShuttleFromTransporters(transporters, shipDef.shipThing);
            if (shuttle == null)
            {
                Log.Error("[DMS_Legion][AXF12] 运输体中未找到穿梭机，无法返航。");
                return;
            }

            if (applyInterceptDurabilityDamage && shuttle is ThingWithComps twc && twc.HitPoints > 0)
            {
                int takeoffHP = twc.HitPoints;
                int minHP = System.Math.Max(1, (int)System.Math.Ceiling(takeoffHP / 2.0));
                twc.HitPoints = Verse.Rand.Range(minHP, takeoffHP + 1);
            }

            shuttle.SetFactionDirect(Faction.OfPlayer);
            var compShuttle = shuttle.TryGetComp<CompShuttle>();
            if (compShuttle != null)
            {
                compShuttle.acceptChildren = true;
            }

            var shuttleTransporter = shuttle.TryGetComp<CompTransporter>();
            if (shuttleTransporter != null)
            {
                TransferCargoToShuttle(transporters, shuttleTransporter);
            }

            TransportShip transportShip = TransportShipMaker.MakeTransportShip(shipDef, null, shuttle);
            if (!AXF12LandingUtility.TryFindLandingCell(map, originCell, shuttle.def, out var landingCell))
            {
                landingCell = map.Center;
            }

            transportShip.ArriveAt(landingCell, map.Parent);

            if (shipDef.playerShuttle)
            {
                ShipJob_Unload unloadJob = (ShipJob_Unload)ShipJobMaker.MakeShipJob(ShipJobDefOf.Unload);
                unloadJob.dropMode = TransportShipDropMode.PawnsOnly;
                transportShip.AddJob(unloadJob);
            }
        }

        /// <summary>
        /// 从运输体中取出穿梭机（同一 Thing 实例），并从 innerContainer 中移除。
        /// </summary>
        private static Thing? ExtractShuttleFromTransporters(List<ActiveTransporterInfo> transporters, ThingDef shipThingDef)
        {
            if (transporters == null || shipThingDef == null) return null;
            foreach (var transporter in transporters)
            {
                if (transporter?.innerContainer == null) continue;
                Thing? shuttle = transporter.innerContainer.FirstOrDefault(t => t?.def == shipThingDef);
                if (shuttle != null)
                {
                    transporter.innerContainer.Remove(shuttle);
                    return shuttle;
                }
            }
            return null;
        }

        /// <summary>
        /// 将运输体中剩余的人与货物转入穿梭机的 CompTransporter（穿梭机已在上一步被取出，此处仅转人货）。
        /// </summary>
        private static void TransferCargoToShuttle(List<ActiveTransporterInfo> transporters, CompTransporter shuttleTransporter)
        {
            if (transporters == null || shuttleTransporter?.innerContainer == null) return;
            foreach (var transporter in transporters)
            {
                if (transporter?.innerContainer == null) continue;
                shuttleTransporter.innerContainer.TryAddRangeOrTransfer(transporter.innerContainer, true, true);
            }
        }

        /// <summary>
        /// 当无法在返航世界格生成 / 获取地图时，仿照原版 TransportersArrivalAction_FormCaravan 的逻辑，
        /// 在该世界格附近组建远行队，避免 AXF-12 及其乘员与货物凭空消失。
        /// </summary>
        private static void TryFormCaravanFallback(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            if (transporters == null || transporters.Count == 0)
                return;

            tmpPawns.Clear();
            for (int i = 0; i < transporters.Count; i++)
            {
                var inner = transporters[i].innerContainer;
                if (inner == null) continue;

                for (int j = inner.Count - 1; j >= 0; j--)
                {
                    if (inner[j] is Pawn pawn)
                    {
                        tmpPawns.Add(pawn);
                        inner.Remove(pawn);
                    }
                }
            }

            if (tmpPawns.Count == 0 && !transporters.IsShuttle())
            {
                // 没有可用的远行队所有者且也不是穿梭机，无法合理组建远行队，直接返回。
                tmpPawns.Clear();
                return;
            }

            if (!GenWorldClosest.TryFindClosestPassableTile(tile, out var foundTile))
            {
                foundTile = tile;
            }

            Caravan caravan = CaravanMaker.MakeCaravan(tmpPawns, Faction.OfPlayer, foundTile, addToWorldPawnsIfNotAlready: true);

            if (transporters.IsShuttle())
            {
                Thing shuttle = transporters[0].RemoveShuttle();
                if (shuttle != null)
                {
                    CaravanInventoryUtility.GiveThing(caravan, shuttle);
                }
            }

            for (int k = 0; k < transporters.Count; k++)
            {
                var inner = transporters[k].innerContainer;
                if (inner == null || inner.Count == 0) continue;

                tmpContainedThings.Clear();
                tmpContainedThings.AddRange(inner);

                for (int m = 0; m < tmpContainedThings.Count; m++)
                {
                    inner.Remove(tmpContainedThings[m]);
                    CaravanInventoryUtility.GiveThing(caravan, tmpContainedThings[m]);
                }
            }

            tmpPawns.Clear();
            tmpContainedThings.Clear();

            Messages.Message(FallbackArrivalMessageKey.Translate(), caravan, MessageTypeDefOf.TaskCompletion);
            Find.WorldObjects.WorldObjectAt<PeaceTalks>(tile)?.Notify_CaravanArrived(caravan);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref originCell, "originCell");
            Scribe_Values.Look(ref transportShipDefName, "transportShipDefName");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                transportShipDefName ??= "DMSL_AXF12_OffsetConfig";
            }
        }
    }
}
