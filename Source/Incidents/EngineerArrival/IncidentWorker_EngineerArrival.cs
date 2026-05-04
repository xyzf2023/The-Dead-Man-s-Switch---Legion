// ============================================================================
// 文件：IncidentWorker_EngineerArrival.cs
// 说明：游荡机兵到达事件的 Worker，在地图边缘生成未受控机械体
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.EngineerArrival
{
    /// <summary>
    /// 游荡机兵到达事件：在地图边缘生成属于玩家阵营但未受控的机械体，
    /// 机械体会游荡一段时间后尝试离开地图
    /// </summary>
    public class IncidentWorker_EngineerArrival : IncidentWorker
    {
        private const string LetterLabelKey = "DMSL_EngineerArrival_LetterLabel";
        private const string LetterTextKey = "DMSL_EngineerArrival_LetterText";

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
                return false;

            if (parms.target is not Map map || !map.IsPlayerHome)
                return false;

            PawnKindDef? mechKind = ResolveMechKind();
            if (mechKind == null)
                return false;

            return RCellFinder.TryFindRandomPawnEntryCell(out _, map, CellFinder.EdgeRoadChance_Neutral);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.target is not Map map || !map.IsPlayerHome)
                return false;

            PawnKindDef? mechKind = ResolveMechKind();
            if (mechKind == null)
                return false;

            if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entryCell, map, CellFinder.EdgeRoadChance_Neutral))
                return false;

            PawnGenerationRequest request = new PawnGenerationRequest(
                mechKind,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: false
            );

            Pawn mech = PawnGenerator.GeneratePawn(request);
            if (mech == null)
                return false;

            IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(entryCell, map, 10);
            GenSpawn.Spawn(mech, spawnCell, map);

            // 确保无 Overseer，处于未受控状态
            Pawn? overseer = mech.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Overseer);
            if (overseer != null && mech.relations != null)
                mech.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, overseer);

            // 添加游荡机兵 Hediff，用于计时、防野化、离图销毁
            mech.health.AddHediff(HediffDef.Named("DMSL_Hediff_WanderingEngineer"));

            // 加入 Lord，使机械体在地图边缘游荡
            LordMaker.MakeNewLord(
                Faction.OfPlayer,
                new LordJob_WanderMapEdge(),
                map,
                Gen.YieldSingle(mech)
            );

            SendLetter(parms, mech);

            return true;
        }

        private PawnKindDef? ResolveMechKind()
        {
            var props = def.GetModExtension<CompProperties_EngineerArrivalIncident>();
            if (props?.mechKindDefNames == null || props.mechKindDefNames.Count == 0)
                return DefDatabase<PawnKindDef>.GetNamedSilentFail("DMSL_Mech_Engineer");

            var validKinds = props.mechKindDefNames
                .Select(name => DefDatabase<PawnKindDef>.GetNamedSilentFail(name))
                .Where(k => k != null && k.RaceProps?.IsMechanoid == true)
                .ToList();

            return validKinds.Count > 0 ? validKinds.RandomElement() : null;
        }

        private void SendLetter(IncidentParms parms, Pawn mech)
        {
            TaggedString label = LetterLabelKey.Translate();
            TaggedString text = LetterTextKey.Translate();

            SendStandardLetter(
                label,
                text,
                LetterDefOf.PositiveEvent,
                parms,
                new LookTargets(mech)
            );
        }
    }
}
