// ============================================================================
// 叙事者组件：艾丽萨每隔固定时间生成一支招募代理商队（或其它指定类型的商队）
// 使用原版 TraderCaravanArrival 事件，但强制派系与商人类型（如 DMS_Army + DMS_Caravan_TributeCollector）
// ============================================================================

using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 按间隔触发“贸易商队到达”，并固定为指定派系与商人类型（如失能机关的徵募代理）。
    /// </summary>
    public class StorytellerComp_ElisaRecruitmentAgencyCaravan : RimWorld.StorytellerComp
    {
        private StorytellerCompProperties_ElisaRecruitmentAgencyCaravan Props =>
            (StorytellerCompProperties_ElisaRecruitmentAgencyCaravan)props;

        private static DMSL_GameComponent_ElisaRecruitmentAgencyCaravan? GetComponent() =>
            Current.Game?.GetComponent<DMSL_GameComponent_ElisaRecruitmentAgencyCaravan>();

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (!ModsConfig.RoyaltyActive)
                yield break;

            Map? map = target as Map;
            if (map == null || !map.IsPlayerHome)
                yield break;

            if (GenDate.DaysPassedSinceSettleFloat < Props.minDaysPassed)
                yield break;

            var comp = GetComponent();
            if (comp == null || !comp.ShouldRunNow(Props.intervalDays))
                yield break;

            Faction? faction = DefDatabase<FactionDef>.GetNamedSilentFail(Props.factionDefName) is FactionDef fd
                ? Find.FactionManager.FirstFactionOfDef(fd)
                : null;
            if (faction == null || faction.HostileTo(Faction.OfPlayer))
                yield break;

            TraderKindDef? traderKind = DefDatabase<TraderKindDef>.GetNamedSilentFail(Props.traderKindDefName);
            if (traderKind == null)
                yield break;

            IncidentDef? incidentDef = IncidentDefOf.TraderCaravanArrival;
            if (incidentDef?.Worker == null)
                yield break;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, target);
            parms.target = target;
            parms.faction = faction;
            parms.traderKind = traderKind;

            if (!incidentDef.Worker.CanFireNow(parms))
                yield break;

            comp.MarkRun();
            yield return new FiringIncident(incidentDef, this, parms);
        }
    }
}
