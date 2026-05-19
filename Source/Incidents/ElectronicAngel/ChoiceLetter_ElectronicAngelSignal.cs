// ============================================================================
// 文件：ChoiceLetter_ElectronicAngelSignal.cs
// 说明：黑衣人事件触发时弹出的「讯息：电子天使」选择信。
//       选项：请求庇护（触发电子天使支援）、保持沉默（继续原版黑衣人事件）、推迟。
// ============================================================================

using System.Collections.Generic;
using DMS_Legion;
using RimWorld;
using Verse;

namespace DMS_Legion.Incidents.ElectronicAngel
{
    public class ChoiceLetter_ElectronicAngelSignal : ChoiceLetter
    {
        private const int TimeoutTicks = 2500; // 1h
        private const string FactionDefName = "DMSL_Faction_DigitalAngel";

        /// <summary>本次事件关联的地图；用于触发后续事件。</summary>
        public Map? triggerMap;

        public ChoiceLetter_ElectronicAngelSignal() { }

        public ChoiceLetter_ElectronicAngelSignal(Map map)
        {
            triggerMap = map;
            // 使用与原版正面事件相同的信封类型
            def = LetterDefOf.PositiveEvent;
            Label = "DMSL_ElectronicAngelSignal_LetterTitle".Translate();
            title = Label;
            Text = "DMSL_ElectronicAngelSignal_LetterText".Translate();
            lookTargets = map != null ? new LookTargets(map.Center, map) : LookTargets.Invalid;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (!ArchivedOnly)
                {
                    yield return Option_RequestShelter;
                    yield return Option_KeepSilent;
                    yield return base.Option_Postpone;
                }
                else
                {
                    yield return base.Option_Close;
                }
            }
        }

        private DiaOption Option_RequestShelter => new DiaOption("DMSL_ElectronicAngelSignal_OptionRequestShelter".Translate())
        {
            action = () =>
            {
                TryExecuteElectronicAngelSupport();
                Find.LetterStack.RemoveLetter(this);
            },
            resolveTree = true
        };

        private DiaOption Option_KeepSilent => new DiaOption("DMSL_ElectronicAngelSignal_OptionKeepSilent".Translate())
        {
            action = () =>
            {
                TryExecuteVanillaManInBlack();
                Find.LetterStack.RemoveLetter(this);
            },
            resolveTree = true
        };

        private void TryExecuteElectronicAngelSupport()
        {
            if (triggerMap == null || !Find.Maps.Contains(triggerMap))
                return;
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("DMSL_ElectronicAngelSupport");
            if (incidentDef?.Worker == null)
                return;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, triggerMap);
            parms.target = triggerMap;
            incidentDef.Worker.TryExecute(parms);
        }

        private void TryExecuteVanillaManInBlack()
        {
            if (triggerMap == null || !Find.Maps.Contains(triggerMap))
                return;
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("StrangerInBlackJoin");
            if (incidentDef?.Worker == null)
                return;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, triggerMap);
            parms.target = triggerMap;
            if (!incidentDef.Worker.CanFireNow(parms))
                return;
            incidentDef.Worker.TryExecute(parms);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref triggerMap, "triggerMap");
        }

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

