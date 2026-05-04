using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion.AXF12
{
    public class WorldObjectCompProperties_AXF12ReconTravel : WorldObjectCompProperties
    {
        public int loiterTicks = 1200;

        public WorldObjectCompProperties_AXF12ReconTravel()
        {
            compClass = typeof(WorldObjectComp_AXF12ReconTravel);
        }
    }

    public class WorldObjectComp_AXF12ReconTravel : WorldObjectComp
    {
        private bool hidden;
        private int loiterTicksRemaining;
        private PlanetTile returnTile = PlanetTile.Invalid;
        private IntVec3 returnCell = IntVec3.Invalid;
        private string? returnShipDefName;
        private bool pendingReturn;

        private static readonly AccessTools.FieldRef<TravellingTransporters, bool> ArrivedField =
            AccessTools.FieldRefAccess<TravellingTransporters, bool>("arrived");
        private static readonly AccessTools.FieldRef<TravellingTransporters, float> TraveledPctField =
            AccessTools.FieldRefAccess<TravellingTransporters, float>("traveledPct");
        private static readonly AccessTools.FieldRef<TravellingTransporters, PlanetTile> InitialTileField =
            AccessTools.FieldRefAccess<TravellingTransporters, PlanetTile>("initialTile");

        public bool Hidden => hidden;
        public WorldObjectCompProperties_AXF12ReconTravel Props => (WorldObjectCompProperties_AXF12ReconTravel)props;

        public void BeginLoiter(PlanetTile originTile, IntVec3 originCell, string? shipDefName, int ticks)
        {
            if (originTile < 0 || parent is not TravellingTransporters)
            {
                return;
            }

            hidden = true;
            pendingReturn = true;
            returnTile = originTile;
            returnCell = originCell;
            returnShipDefName = shipDefName;
            loiterTicksRemaining = Math.Max(1, ticks);
        }

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);

            if (!pendingReturn)
            {
                return;
            }

            loiterTicksRemaining -= delta;
            if (loiterTicksRemaining > 0)
            {
                return;
            }

            StartReturnTravel();
        }

        private void StartReturnTravel()
        {
            if (parent is not TravellingTransporters transporters)
            {
                pendingReturn = false;
                return;
            }

            hidden = false;
            pendingReturn = false;

            PlanetTile loiterTile = transporters.destinationTile;
            transporters.Tile = loiterTile;
            transporters.destinationTile = returnTile;
            transporters.arrivalAction = new TransportersArrivalAction_AXF12Return(
                returnCell,
                returnShipDefName ?? "DMSL_AXF12_OffsetConfig");

            InitialTileField(transporters) = loiterTile;
            TraveledPctField(transporters) = 0f;
            ArrivedField(transporters) = false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hidden, "axf12Hidden", false);
            Scribe_Values.Look(ref loiterTicksRemaining, "axf12LoiterTicksRemaining", 0);
            Scribe_Values.Look(ref returnTile, "axf12ReturnTile");
            Scribe_Values.Look(ref returnCell, "axf12ReturnCell");
            Scribe_Values.Look(ref returnShipDefName, "axf12ReturnShipDefName");
            Scribe_Values.Look(ref pendingReturn, "axf12PendingReturn", false);
        }
    }
}
