// ============================================================================
// 叙事者组件：DMS 派系偏向主事件
// 继承 StorytellerComp_RandomMain，在 FactionArrival 类别中按权重偏向指定派系
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// DMS 派系偏向主事件叙事者组件。
    /// 当事件类别为 FactionArrival（商队、访客、旅行者等）时，以概率偏向使用 preferredFactionDefNames 中的派系。
    /// </summary>
    public class StorytellerComp_DMSRandomMain : RimWorld.StorytellerComp_RandomMain
    {
        private StorytellerCompProperties_DMSRandomMain DMSProps =>
            (StorytellerCompProperties_DMSRandomMain)props;

        public override IncidentParms GenerateParms(IncidentCategoryDef incCat, IIncidentTarget target)
        {
            IncidentParms parms = base.GenerateParms(incCat, target);

            // 仅对派系到达类别应用派系偏向
            if (incCat != DefDatabase<IncidentCategoryDef>.GetNamed("FactionArrival"))
            {
                return parms;
            }

            var props = DMSProps;
            if (props.preferredFactionDefNames.NullOrEmpty() || props.preferredFactionWeightMultiplier <= 1f)
            {
                return parms;
            }

            // 概率 P = 1 - 1/multiplier，multiplier 越大越偏向优先派系
            float probability = 1f - 1f / props.preferredFactionWeightMultiplier;
            if (Rand.Value >= probability)
            {
                return parms;
            }

            // 从优先派系列表中筛选有效派系：非玩家、未 defeated、非隐藏、非临时、非敌对
            List<Faction> validPreferred = Find.FactionManager.AllFactions.Where(f =>
                props.preferredFactionDefNames.Contains(f.def.defName) &&
                !f.IsPlayer &&
                !f.defeated &&
                !f.Hidden &&
                !f.temporary &&
                !f.HostileTo(Faction.OfPlayer)
            ).ToList();

            if (validPreferred.Count > 0 && validPreferred.TryRandomElement(out Faction chosen))
            {
                parms.faction = chosen;
            }

            return parms;
        }
    }
}
