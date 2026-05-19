// ============================================================================
// 半数倒地支援请求：未知讯号选择信，倒计时 1h，选项为尝试回应 / 保持静默 / 推迟
// ============================================================================

using System.Collections.Generic;
using DMS_Legion;
using RimWorld;
using Verse;

namespace DMS_Legion.Incidents.UnknownMechSupport
{
    /// <summary>
    /// “未知讯号”信：位于某地图的殖民者收到来源不明的军事援助询问；可选尝试回应（触发未知机兵支援）、保持静默或推迟。
    /// </summary>
    public class ChoiceLetter_UnknownSignal : ChoiceLetter
    {
        private const int TimeoutTicks = 2500; // 1h
        private const string FactionDefName = "DMSL_Faction_DigitalAngel";

        /// <summary>触发该信的地图；选「尝试回应」时对该地图执行 DMSL_UnknownMechSupport。</summary>
        public Map? triggerMap;

        public ChoiceLetter_UnknownSignal() { }

        public ChoiceLetter_UnknownSignal(Map map)
        {
            triggerMap = map;
            def = LetterDefOf.PositiveEvent;
            Label = "DMSL_UnknownSignal_LetterTitle".Translate();
            title = Label;
            Text = "DMSL_UnknownSignal_LetterText".Translate();
            lookTargets = map != null ? new LookTargets(map.Center, map) : LookTargets.Invalid;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (!ArchivedOnly)
                {
                    yield return Option_Respond;
                    yield return Option_Silent;
                    yield return Option_Postpone;
                }
                else
                {
                    yield return Option_Close;
                }
            }
        }

        private DiaOption Option_Respond => new DiaOption("DMSL_UnknownSignal_OptionRespond".Translate())
        {
            action = () =>
            {
                TryExecuteUnknownMechSupport();
                Find.LetterStack.RemoveLetter(this);
            },
            resolveTree = true
        };

        private DiaOption Option_Silent => new DiaOption("DMSL_UnknownSignal_OptionSilent".Translate())
        {
            action = () => Find.LetterStack.RemoveLetter(this),
            resolveTree = true
        };

        private void TryExecuteUnknownMechSupport()
        {
            // 若未开启未知机兵支援，则直接不响应
            var settings = DMS_Legion.DMSL_ModSettings.settings;
            if (settings == null || !settings.enableUnknownMechSupport)
                return;

            if (triggerMap == null || !Find.Maps.Contains(triggerMap))
                return;
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail("DMSL_UnknownMechSupport");
            if (def?.Worker == null)
                return;
            var parms = new IncidentParms { target = triggerMap };
            def.Worker.TryExecute(parms);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref triggerMap, "triggerMap");
        }

        /// <summary>发信时调用：设置倒计时并投递。</summary>
        public void Send()
        {
            TryAlignDigitalAngelRelations();
            StartTimeout(TimeoutTicks);
            Find.LetterStack.ReceiveLetter(this);
        }

        private static void TryAlignDigitalAngelRelations()
        {
            FactionDef? factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            if (factionDef == null)
                return;

            Faction? faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null)
                return;

            DigitalAngelRelationAligner.AlignRelations(faction, sendLetters: false);
        }
    }
}
