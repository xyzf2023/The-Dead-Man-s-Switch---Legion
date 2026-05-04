using System.Collections.Generic;
using RimWorld;
using Verse;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.AXF12
{
    public class AXF12ReconSupportDelayComponent : MapComponent
    {
        private List<PendingSupport> pendingSupports = new List<PendingSupport>();

        public AXF12ReconSupportDelayComponent(Map map) : base(map)
        {
        }

        public void Schedule(IntVec3 targetCell, string supportTypeDefName, bool clearFog, int delayTicks)
        {
            pendingSupports.Add(new PendingSupport
            {
                targetCell = targetCell,
                supportTypeDefName = supportTypeDefName,
                clearFog = clearFog,
                remainingTicks = delayTicks
            });
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (pendingSupports.Count == 0)
            {
                return;
            }

            for (int i = pendingSupports.Count - 1; i >= 0; i--)
            {
                var entry = pendingSupports[i];
                entry.remainingTicks--;
                if (entry.remainingTicks > 0)
                {
                    pendingSupports[i] = entry;
                    continue;
                }

                pendingSupports.RemoveAt(i);
                ExecuteSupport(entry);
            }
        }

        private void ExecuteSupport(PendingSupport entry)
        {
            if (map == null)
            {
                return;
            }

            if (entry.clearFog)
            {
                map.fogGrid?.ClearAllFog();
            }

            if (string.IsNullOrWhiteSpace(entry.supportTypeDefName))
            {
                Log.Error("[DMS_Legion][AXF12] 空中支援类型为空，无法执行。");
                return;
            }

            var supportType = DefDatabase<AerialSupportTypeDef>.GetNamed(entry.supportTypeDefName, false);
            if (supportType == null)
            {
                Log.Error($"[DMS_Legion][AXF12] 未找到空中支援类型: {entry.supportTypeDefName}");
                return;
            }

            AerialSupportCoordinator.Instance?.RequestSupportAt(entry.targetCell, map, supportType);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingSupports, "axf12PendingSupports", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingSupports ??= new List<PendingSupport>();
            }
        }

        private struct PendingSupport : IExposable
        {
            public IntVec3 targetCell;
            public string? supportTypeDefName;
            public bool clearFog;
            public int remainingTicks;

            public void ExposeData()
            {
                Scribe_Values.Look(ref targetCell, "targetCell");
                Scribe_Values.Look(ref supportTypeDefName, "supportTypeDefName");
                Scribe_Values.Look(ref clearFog, "clearFog");
                Scribe_Values.Look(ref remainingTicks, "remainingTicks");
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    supportTypeDefName ??= string.Empty;
                }
            }
        }
    }
}
