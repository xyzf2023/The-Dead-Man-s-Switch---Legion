// ============================================================================
// 拉斐尔叙事者组件：独立计时，按原版袭击点数直接生成白名单任务（不走事件校验）
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 按固定 tick 间隔，用原版 StorytellerUtility.DefaultThreatPointsNow 计算点数，
    /// 直接调用 QuestUtility.GenerateQuestAndMakeAvailable 生成任务，不经过 IncidentWorker.CanFireNow。
    /// </summary>
    public class StorytellerComp_RaphaelExtraQuest : RimWorld.StorytellerComp
    {
        private StorytellerCompProperties_RaphaelExtraQuest Props =>
            (StorytellerCompProperties_RaphaelExtraQuest)props;

        public override IEnumerable<RimWorld.FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (DMSL_ModSettings.settings == null || !DMSL_ModSettings.settings.enableRaphaelExtraQuest)
            {
                yield break;
            }

            Map? map = target as Map;
            if (map == null || !map.IsPlayerHome)
            {
                yield break;
            }

            if (Props.intervalTicks <= 0)
            {
                yield break;
            }

            if (GenDate.DaysPassedSinceSettleFloat < Props.minDaysPassed)
            {
                yield break;
            }

            var comp = DMSL_GameComponent_RaphaelExtraQuest.GetOrCreate();
            if (comp == null)
            {
                yield break;
            }

            if (!comp.ShouldFireNow(Props.intervalTicks))
            {
                yield break;
            }

            // 原版逻辑：任务/袭击点数由 DefaultThreatPointsNow 计算（财富、殖民者等）
            float points = StorytellerUtility.DefaultThreatPointsNow(target);

            if (Props.questDefs.NullOrEmpty())
            {
                yield break;
            }

            var shuffled = Props.questDefs
                .Where(s => !string.IsNullOrEmpty(s))
                .OrderBy(_ => Rand.Value)
                .ToList();

            QuestScriptDef? questDef = null;

            foreach (string defName in shuffled)
            {
                QuestScriptDef? def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    continue;
                }

                if (def.rootIncreasesPopulation)
                    continue;

                if (!DMSL_GameComponent_RaphaelExtraQuest.SafeCanRun(def, points, target))
                    continue;

                questDef = def;
                break;
            }

            if (questDef == null)
            {
                yield break;
            }

            // 直接按控制台/原版 GiveQuest 方式：用点数生成任务并加入队列，不发事件
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, points);
            if (quest != null && !quest.hidden && questDef.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            comp.MarkFired();

            // 通知叙事状态：本次间隔已“触发”过一次给任务，便于原版冷却/统计一致
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.GiveQuest, target);
            parms.questScriptDef = questDef;
            var fi = new FiringIncident(IncidentDefOf.GiveQuest_Random, this, parms);
            target.StoryState.Notify_IncidentFired(fi);

            yield break;
        }
    }
}
