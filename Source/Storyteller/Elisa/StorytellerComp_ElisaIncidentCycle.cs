// ============================================================================
// 叙事者组件：艾丽萨事件循环
// 每 minIntervalDays~maxIntervalDays 天遍历 incidents 列表，寻找满足条件的事件并触发
// 连续两次尽量不触发同一事件（除非仅该事件满足条件）
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 艾丽萨事件循环叙事者组件。
    /// </summary>
    public class StorytellerComp_ElisaIncidentCycle : RimWorld.StorytellerComp
    {
        private StorytellerCompProperties_ElisaIncidentCycle Props =>
            (StorytellerCompProperties_ElisaIncidentCycle)props;

        private static DMSL_GameComponent_ElisaIncidentCycle? GetComponent() =>
            Current.Game?.GetComponent<DMSL_GameComponent_ElisaIncidentCycle>();

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (Props.incidents.NullOrEmpty())
                yield break;

            if (GenDate.DaysPassedSinceSettleFloat < Props.minDaysPassed)
                yield break;

            if (!Props.allowedTargetTags.NullOrEmpty())
            {
                bool targetAllowed = false;
                foreach (IncidentTargetTagDef tag in target.IncidentTargetTags())
                {
                    if (Props.allowedTargetTags.Contains(tag))
                    {
                        targetAllowed = true;
                        break;
                    }
                }
                if (!targetAllowed)
                    yield break;
            }

            if (!Props.disallowedTargetTags.NullOrEmpty())
            {
                foreach (IncidentTargetTagDef tag in target.IncidentTargetTags())
                {
                    if (Props.disallowedTargetTags.Contains(tag))
                        yield break;
                }
            }

            var comp = GetComponent();
            if (comp == null || !comp.ShouldFireNow(Props.minIntervalDays, Props.maxIntervalDays))
                yield break;

            var fireable = new List<(IncidentDef def, IncidentParms parms)>();
            string? lastFired = comp.lastFiredIncidentDefName;

            foreach (string defName in Props.incidents)
            {
                IncidentDef? incDef = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
                if (incDef == null)
                    continue;

                IncidentParms parms = RimWorld.StorytellerUtility.DefaultParmsNow(incDef.category, target);
                parms.target = target;

                if (incDef.Worker.CanFireNow(parms))
                    fireable.Add((incDef, parms));
            }

            if (fireable.Count == 0)
                yield break;

            IncidentDef chosenDef;
            IncidentParms chosenParms;

            var preferred = fireable.Where(x => x.def.defName != lastFired).ToList();
            if (preferred.Count > 0)
            {
                var pick = preferred.RandomElement();
                chosenDef = pick.def;
                chosenParms = pick.parms;
            }
            else
            {
                var pick = fireable.RandomElement();
                chosenDef = pick.def;
                chosenParms = pick.parms;
            }

            comp.MarkFired(chosenDef.defName, Props.minIntervalDays, Props.maxIntervalDays);
            yield return new FiringIncident(chosenDef, this, chosenParms);
        }

        /// <summary>
        /// 立即尝试触发一次艾丽萨事件循环效果（供调试等调用）。
        /// 遍历 incidents 列表寻找可执行事件并触发，不检查间隔、不更新 MarkFired。
        /// </summary>
        /// <param name="target">事件目标（如主地图）</param>
        /// <param name="props">组件属性，为 null 时从艾丽萨叙事者 Def 中获取</param>
        /// <returns>是否成功触发</returns>
        public static bool TryRunOnce(IIncidentTarget target, StorytellerCompProperties_ElisaIncidentCycle? props = null)
        {
            props ??= DefDatabase<StorytellerDef>.GetNamedSilentFail("DMSL_Storyteller_Elisa")
                ?.comps?.OfType<StorytellerCompProperties_ElisaIncidentCycle>().FirstOrDefault();
            if (props == null || props.incidents.NullOrEmpty())
                return false;

            if (GenDate.DaysPassedSinceSettleFloat < props.minDaysPassed)
                return false;

            if (!props.allowedTargetTags.NullOrEmpty())
            {
                bool targetAllowed = false;
                foreach (IncidentTargetTagDef tag in target.IncidentTargetTags())
                {
                    if (props.allowedTargetTags.Contains(tag))
                    {
                        targetAllowed = true;
                        break;
                    }
                }
                if (!targetAllowed)
                    return false;
            }

            if (!props.disallowedTargetTags.NullOrEmpty())
            {
                foreach (IncidentTargetTagDef tag in target.IncidentTargetTags())
                {
                    if (props.disallowedTargetTags.Contains(tag))
                        return false;
                }
            }

            var fireable = new List<(IncidentDef def, IncidentParms parms)>();
            var comp = Current.Game?.GetComponent<DMSL_GameComponent_ElisaIncidentCycle>();
            string? lastFired = comp?.lastFiredIncidentDefName;

            foreach (string defName in props.incidents)
            {
                IncidentDef? incDef = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
                if (incDef == null)
                    continue;

                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incDef.category, target);
                parms.target = target;

                if (incDef.Worker.CanFireNow(parms))
                    fireable.Add((incDef, parms));
            }

            if (fireable.Count == 0)
                return false;

            var preferred = fireable.Where(x => x.def.defName != lastFired).ToList();
            var pick = preferred.Count > 0 ? preferred.RandomElement() : fireable.RandomElement();

            var fi = new FiringIncident(pick.def, Find.Storyteller.storytellerComps.OfType<StorytellerComp_ElisaIncidentCycle>().FirstOrDefault() ?? new StorytellerComp_ElisaIncidentCycle { props = props }, pick.parms);
            return Find.Storyteller.TryFire(fi);
        }
    }
}
