using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace DMS_Legion.RoyalPermits
{
    /// <summary>
    /// 皇权许可 Worker：召唤一架 AXF-12「偏构」归玩家所有
    /// 依赖 Odyssey 的 TransportShipDef DMSL_AXF12_OffsetConfig。
    /// </summary>
    public class RoyalTitlePermitWorker_RewardAXF12 : RoyalTitlePermitWorker_Targeted
    {
        private Faction? _calledFaction;

        private static TransportShipDef ShipDef =>
            DefDatabase<TransportShipDef>.GetNamedSilentFail("DMSL_AXF12_OffsetConfig");

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!CanHitTarget(target))
            {
                if (target.IsValid && showMessages)
                    Messages.Message(def.LabelCap + ": " + "AbilityCannotHitTarget".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            AcceptanceReport report = ShuttleCanLandHere(target, map);
            if (!report.Accepted)
            {
                if (showMessages)
                    Messages.Message(report.Reason, new LookTargets(target.Cell, map), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            return true;
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(caller.Position, RangeClamped, Color.white);
            if (ShipDef != null)
                DrawShuttleGhost(target, map, ShipDef.shipThing, ShipDef.shipThing.defaultPlacingRot);
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            CallShuttle(target.Cell);
        }

        public override void OnGUI(LocalTargetInfo target)
        {
            if (!target.IsValid || (ShipDef != null && !ShuttleCanLandHere(target, map).Accepted))
                GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
        }

        public override IEnumerable<FloatMenuOption> GetRoyalAidOptions(Map map, Pawn pawn, Faction faction)
        {
            if (!ModsConfig.RoyaltyActive)
                yield break;
            if (map.generatorDef?.isUnderground ?? false)
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + "CommandCallRoyalAidMapUnreachable".Translate(faction.Named("FACTION")), null);
                yield break;
            }

            if (faction.HostileTo(Faction.OfPlayer))
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + "CommandCallRoyalAidFactionHostile".Translate(faction.Named("FACTION")), null);
                yield break;
            }

            if (ShipDef == null)
            {
                yield return new FloatMenuOption(def.LabelCap + ": (AXF-12 def missing)", null);
                yield break;
            }

            string description = def.LabelCap + ": ";
            Action? action = null;
            if (FillAidOption(pawn, faction, ref description, out bool free))
            {
                action = () => BeginCallShuttle(pawn, pawn.MapHeld, faction, free);
            }

            yield return new FloatMenuOption(description, action, faction.def.FactionIcon, faction.Color);
        }

        private void BeginCallShuttle(Pawn callerPawn, Map targetMap, Faction faction, bool free)
        {
            targetingParameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetSelf = false,
                canTargetPawns = false,
                canTargetFires = false,
                canTargetBuildings = true,
                canTargetItems = true
            };
            caller = callerPawn;
            map = targetMap;
            _calledFaction = faction;
            this.free = free;
            float rangeActual = RangeClamped;
            targetingParameters.validator = (TargetInfo t) => rangeActual <= 0f || !(t.Cell.DistanceTo(callerPawn.Position) > rangeActual);
            Find.Targeter.BeginTargeting(this);
        }

        private void CallShuttle(IntVec3 landingCell)
        {
            if (caller == null || !caller.Spawned || ShipDef == null)
                return;

            Thing shipThing = ThingMaker.MakeThing(ShipDef.shipThing);
            shipThing.SetFactionDirect(Faction.OfPlayer);
            CompShuttle compShuttle = shipThing.TryGetComp<CompShuttle>();
            if (compShuttle != null)
                compShuttle.acceptChildren = true;

            TransportShip transportShip = TransportShipMaker.MakeTransportShip(ShipDef, null, shipThing);
            transportShip.ArriveAt(landingCell, map.Parent);

            caller.royalty.GetPermit(def, _calledFaction!).Notify_Used();
            if (!free && def.royalAid != null && _calledFaction != null)
                caller.royalty.TryRemoveFavor(_calledFaction, def.royalAid.favorCost);
        }

        public static void DrawShuttleGhost(LocalTargetInfo target, Map map, ThingDef shuttleDef, Rot4 rot)
        {
            Color ghostCol = ShuttleCanLandHere(target, map, shuttleDef, rot).Accepted
                ? Designator_Place.CanPlaceColor
                : Designator_Place.CannotPlaceColor;
            GhostDrawer.DrawGhostThing(target.Cell, rot, shuttleDef, shuttleDef.graphic, ghostCol, AltitudeLayer.Blueprint);
            Vector3 position = ThingUtility.InteractionCellWhenAt(shuttleDef, target.Cell, rot, map).ToVector3ShiftedWithAltitude(AltitudeLayer.Blueprint);
            Graphics.DrawMesh(MeshPool.plane10, position, Quaternion.identity, GenDraw.InteractionCellMaterial, 0);
        }

        public static AcceptanceReport ShuttleCanLandHere(LocalTargetInfo target, Map map, ThingDef? shuttleDef = null, Rot4? rot = null)
        {
            TaggedString prefix = "CannotCallShuttle".Translate() + ": ";
            if (!target.IsValid)
                return new AcceptanceReport(prefix + "MessageTransportPodsDestinationIsInvalid".Translate().CapitalizeFirst());

            shuttleDef ??= ShipDef?.shipThing ?? ThingDefOf.Shuttle;
            rot ??= shuttleDef.defaultPlacingRot;

            foreach (IntVec3 cell in GenAdj.OccupiedRect(target.Cell, rot.Value, shuttleDef.size))
            {
                string? reason = GetReportFromCell(cell, map, interactionSpot: false, shuttleDef);
                if (reason != null)
                    return new AcceptanceReport(prefix + reason);
            }

            IntVec3 interactionCell = ThingUtility.InteractionCellWhenAt(shuttleDef, target.Cell, rot.Value, map);
            string? interactionReason = GetReportFromCell(interactionCell, map, interactionSpot: true, shuttleDef);
            if (interactionReason != null)
                return new AcceptanceReport(prefix + interactionReason);

            return AcceptanceReport.WasAccepted;
        }

        private static string? GetReportFromCell(IntVec3 cell, Map map, bool interactionSpot, ThingDef shuttleDef)
        {
            if (!cell.InBounds(map))
                return "OutOfBounds".Translate().CapitalizeFirst();
            if (cell.Fogged(map))
                return "ShuttleCannotLand_Fogged".Translate().CapitalizeFirst();
            if (!cell.Walkable(map))
                return "ShuttleCannotLand_Unwalkable".Translate().CapitalizeFirst();
            if (shuttleDef.terrainAffordanceNeeded != null && !cell.GetAffordances(map).Contains(shuttleDef.terrainAffordanceNeeded))
                return "ShuttleCannotLand_Unaffordable".Translate(shuttleDef.label).CapitalizeFirst();

            RoofDef roof = cell.GetRoof(map);
            if (roof != null && (roof.isNatural || roof.isThickRoof))
                return "MessageTransportPodsDestinationIsInvalid".Translate().CapitalizeFirst();

            foreach (Thing thing in cell.GetThingList(map))
            {
                if (thing is IActiveTransporter || thing is Skyfaller ||
                    (thing.def.category == ThingCategory.Building && !thing.def.building.isPowerConduit))
                    return "BlockedBy".Translate(thing).CapitalizeFirst();
                if (!interactionSpot && thing.def.category == ThingCategory.Item)
                    return "BlockedBy".Translate(thing).CapitalizeFirst();
                if (thing.def.plant?.IsTree == true)
                    return "BlockedBy".Translate(thing).CapitalizeFirst();
            }

            return null;
        }
    }
}
