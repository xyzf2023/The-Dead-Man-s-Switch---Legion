using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion.AXF12
{
    [HarmonyPatch(typeof(TravellingTransporters), "DoArrivalAction")]
    public static class AXF12TravellingTransporters_DoArrivalAction_Patch
    {
        private static readonly AccessTools.FieldRef<TravellingTransporters, List<ActiveTransporterInfo>> TransportersField =
            AccessTools.FieldRefAccess<TravellingTransporters, List<ActiveTransporterInfo>>("transporters");

        public static bool Prefix(TravellingTransporters __instance)
        {
            if (__instance.def?.defName != "DMSL_AXF12_OffsetConfig_Traveling")
            {
                return true;
            }

            var comp = __instance.GetComponent<WorldObjectComp_AXF12ReconTravel>();
            if (comp == null)
            {
                return true;
            }

            var transporters = TransportersField(__instance);
            if (transporters == null)
            {
                return true;
            }

            if (__instance.arrivalAction is TransportersArrivalAction_AXF12Bombing bombingAction)
            {
                bombingAction.Arrived(transporters, __instance.destinationTile);
                __instance.arrivalAction = null;
                comp.BeginLoiter(
                    bombingAction.OriginTile,
                    bombingAction.OriginCell,
                    bombingAction.TransportShipDefName,
                    bombingAction.RequiredLoiterTicks);
                return false;
            }

            if (__instance.arrivalAction is not TransportersArrivalAction_AXF12Recon reconAction)
            {
                return true;
            }

            reconAction.Arrived(transporters, __instance.destinationTile);
            __instance.arrivalAction = null;

            comp.BeginLoiter(
                reconAction.OriginTile,
                reconAction.OriginCell,
                reconAction.TransportShipDefName,
                comp.Props is WorldObjectCompProperties_AXF12ReconTravel props ? props.loiterTicks : 1200);

            return false;
        }
    }

    [HarmonyPatch(typeof(WorldObject), "Draw")]
    public static class AXF12WorldObject_Draw_Patch
    {
        public static bool Prefix(WorldObject __instance)
        {
            if (__instance is TravellingTransporters transporters &&
                transporters.def?.defName == "DMSL_AXF12_OffsetConfig_Traveling")
            {
                var comp = transporters.GetComponent<WorldObjectComp_AXF12ReconTravel>();
                if (comp != null && comp.Hidden)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
