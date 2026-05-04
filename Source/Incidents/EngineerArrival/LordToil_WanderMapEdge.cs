// ============================================================================
// 文件：LordToil_WanderMapEdge.cs
// 说明：赋予 DMSL_WanderMapEdge Duty，使机械体在地图边缘游荡
// ============================================================================

using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.EngineerArrival
{
    public class LordToil_WanderMapEdge : LordToil
    {
        public override void UpdateAllDuties()
        {
            DutyDef dutyDef = DefDatabase<DutyDef>.GetNamedSilentFail("DMSL_WanderMapEdge");
            if (dutyDef == null)
                return;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(dutyDef);
            }
        }
    }
}
