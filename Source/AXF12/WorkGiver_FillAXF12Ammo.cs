using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// 工作给予者：装填 AXF12 航弹储备。归属搬运（Haul），显式扫描带 CompAXF12AmmoReserve 且需装填的建筑。
    /// </summary>
    public class WorkGiver_FillAXF12Ammo : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        /// <summary>显式返回需装填的 AXF12 建筑，确保 WorkGiver 能分配到工作。</summary>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null)
                yield break;
            foreach (Thing t in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (t == null)
                    continue;
                var comp = t.TryGetComp<CompAXF12AmmoReserve>();
                if (comp != null && comp.ReserveThingDef != null && comp.AmountToAutofill > 0)
                    yield return t;
            }
        }

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn?.Map == null)
                return true;
            return false;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || !t.Spawned || t.Map != pawn.Map)
                return false;

            var comp = t.TryGetComp<CompAXF12AmmoReserve>();
            if (comp == null || comp.ReserveThingDef == null)
                return false;

            int amountToAutofill = comp.AmountToAutofill;
            if (amountToAutofill <= 0)
                return false;

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
                return false;

            return !HaulAIUtility.FindFixedIngredientCount(pawn, comp.ReserveThingDef, amountToAutofill).NullOrEmpty();
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var comp = t.TryGetComp<CompAXF12AmmoReserve>();
            if (comp == null || comp.ReserveThingDef == null)
                return null!;

            int amountToAutofill = comp.AmountToAutofill;
            if (amountToAutofill <= 0)
                return null!;

            List<Thing> list = HaulAIUtility.FindFixedIngredientCount(pawn, comp.ReserveThingDef, amountToAutofill);
            if (list.NullOrEmpty())
                return null!;

            JobDef fillDef = DefDatabase<JobDef>.GetNamedSilentFail("DMSL_Job_FillAXF12Ammo");
            if (fillDef == null)
                return null!;

            Job job = JobMaker.MakeJob(fillDef, t, list[0]);
            job.count = Mathf.Min(list[0].stackCount, amountToAutofill);
            job.targetQueueB = list.Skip(1).Select(i => new LocalTargetInfo(i)).ToList();
            return job;
        }
    }
}
