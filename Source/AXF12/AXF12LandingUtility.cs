using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion.AXF12
{
    public static class AXF12LandingUtility
    {
        public static bool TryFindLandingCell(Map map, IntVec3 desired, ThingDef shuttleDef, out IntVec3 result)
        {
            if (ShuttleCanLandHere(desired, map, shuttleDef, shuttleDef.defaultPlacingRot).Accepted)
            {
                result = desired;
                return true;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(desired, 20f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                if (ShuttleCanLandHere(cell, map, shuttleDef, shuttleDef.defaultPlacingRot).Accepted)
                {
                    result = cell;
                    return true;
                }
            }

            result = IntVec3.Invalid;
            return false;
        }

        public static AcceptanceReport ShuttleCanLandHere(LocalTargetInfo target, Map map, ThingDef? shuttleDef = null, Rot4? rot = null)
        {
            TaggedString taggedString = "CannotCallShuttle".Translate() + ": ";
            if (!target.IsValid)
            {
                return new AcceptanceReport(taggedString + "MessageTransportPodsDestinationIsInvalid".Translate().CapitalizeFirst());
            }

            shuttleDef ??= ThingDefOf.Shuttle;

            if (!rot.HasValue)
            {
                rot = shuttleDef.defaultPlacingRot;
            }

            foreach (IntVec3 item in GenAdj.OccupiedRect(target.Cell, rot.Value, shuttleDef.size))
            {
                string? reportFromCell = GetReportFromCell(item, map, interactionSpot: false, shuttleDef);
                if (reportFromCell != null)
                {
                    return new AcceptanceReport(taggedString + reportFromCell);
                }
            }

            string? reportFromCell2 = GetReportFromCell(ThingUtility.InteractionCellWhenAt(shuttleDef, target.Cell, rot.Value, map), map, interactionSpot: true, shuttleDef);
            if (reportFromCell2 != null)
            {
                return new AcceptanceReport(taggedString + reportFromCell2);
            }

            return AcceptanceReport.WasAccepted;
        }

        private static string? GetReportFromCell(IntVec3 cell, Map map, bool interactionSpot, ThingDef shuttleDef)
        {
            if (!cell.InBounds(map))
            {
                return "OutOfBounds".Translate().CapitalizeFirst();
            }

            if (cell.Fogged(map))
            {
                return "ShuttleCannotLand_Fogged".Translate().CapitalizeFirst();
            }

            if (!cell.Walkable(map))
            {
                return "ShuttleCannotLand_Unwalkable".Translate().CapitalizeFirst();
            }

            if (!cell.GetAffordances(map).Contains(shuttleDef.terrainAffordanceNeeded))
            {
                return "ShuttleCannotLand_Unaffordable".Translate(shuttleDef.label).CapitalizeFirst();
            }

            RoofDef roof = cell.GetRoof(map);
            if (roof != null && (roof.isNatural || roof.isThickRoof))
            {
                return "MessageTransportPodsDestinationIsInvalid".Translate().CapitalizeFirst();
            }

            List<Thing> thingList = cell.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing = thingList[i];
                if (thing is IActiveTransporter || thing is Skyfaller ||
                    (thing.def.category == ThingCategory.Building && !thing.def.building.isPowerConduit))
                {
                    return "BlockedBy".Translate(thing).CapitalizeFirst();
                }

                if (!interactionSpot && thing.def.category == ThingCategory.Item)
                {
                    return "BlockedBy".Translate(thing).CapitalizeFirst();
                }

                PlantProperties plant = thing.def.plant;
                if (plant != null && plant.IsTree)
                {
                    return "BlockedBy".Translate(thing).CapitalizeFirst();
                }
            }

            return null;
        }
    }
}
