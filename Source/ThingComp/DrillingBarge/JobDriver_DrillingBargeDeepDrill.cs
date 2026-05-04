using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    public class JobDriver_DrillingBargeDeepDrill : JobDriver
    {
        private const float WorkPerPortion = 8000f;

        private float _workDone;
        private float _portionYieldPct;

        private IntVec3 CenterCell => job.targetA.Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.CanReserve(CenterCell, 1, -1, null, false))
                return false;
            return pawn.Reserve(CenterCell, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil work = ToilMaker.MakeToil("DrillingBargeDeepDrill_Work");
            work.initAction = () =>
            {
                // 目标格已被采空（缓存未更新或他人开采）时直接结束本格，不进行开采动画
                if (!WorkGiver_DrillingBargeDeepDrill.CellHasValuableDeepResource(CenterCell, Map, pawn))
                    pawn.jobs.curDriver.ReadyForNextToil();
            };
            work.tickIntervalAction = delegate (int delta)
            {
                Pawn actor = work.actor;
                float speed = actor.GetStatValue(StatDefOf.DeepDrillingSpeed);
                _workDone += speed * delta;
                _portionYieldPct += speed * delta * actor.GetStatValue(StatDefOf.MiningYield) / WorkPerPortion;
                actor.skills?.Learn(SkillDefOf.Mining, 0.065f * delta);

                if (_workDone >= WorkPerPortion)
                {
                    TryProducePortion(actor, _portionYieldPct);
                    _workDone = 0f;
                    _portionYieldPct = 0f;
                }

                // 采完当前格（本格及 21 格无矿）时进入收尾，不再用 FailOn 同帧寻下一格，减轻卡顿
                if (!WorkGiver_DrillingBargeDeepDrill.CellHasValuableDeepResource(CenterCell, Map, actor))
                    actor.jobs.curDriver.ReadyForNextToil();
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.Drill, TargetIndex.A);
            work.WithProgressBar(TargetIndex.A, () => Mathf.Clamp01(_workDone / WorkPerPortion));
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.OnCell);
            work.activeSkill = () => SkillDefOf.Mining;
            yield return work;

            // 可选短时等待，把“找下一格”推到后续 tick，进一步分散负载
            yield return Toils_General.Wait(45, TargetIndex.None);

            Toil finish = ToilMaker.MakeToil("DrillingBargeDeepDrill_Finish");
            finish.initAction = () => { };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private void TryProducePortion(Pawn driller, float yieldPct)
        {
            Map map = driller.Map;
            bool nextResource = DeepDrillUtility.GetNextResource(CenterCell, map, out ThingDef resDef, out int countPresent, out IntVec3 cell);
            if (resDef == null)
                return;
            if (CompDrillingBargeDeepResourceOverlay.IsExcludedDeepResource(resDef))
                return;

            int num = Mathf.Min(countPresent, resDef.deepCountPerPortion);
            if (nextResource)
                map.deepResourceGrid.SetAt(cell, resDef, countPresent - num);

            int stackCount = Mathf.Max(1, GenMath.RoundRandom(num * yieldPct));
            Thing thing = ThingMaker.MakeThing(resDef);
            thing.stackCount = stackCount;
            GenPlace.TryPlaceThing(thing, CenterCell, map, ThingPlaceMode.Near, null, (IntVec3 p) => p != driller.Position);

            if (driller != null)
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.Mined, driller.Named(HistoryEventArgsNames.Doer)));
        }
    }
}
