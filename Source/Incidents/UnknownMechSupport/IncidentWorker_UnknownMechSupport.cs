// ============================================================================
// 文件：IncidentWorker_UnknownMechSupport.cs
// 说明：未知机兵支援事件 Worker，仅由特殊组件调用；空投电子天使派系机兵协助玩家。
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using DMS_Legion;
using DMS_Legion.Incidents.DigitalAngelSupport;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.UnknownMechSupport
{
    /// <summary>
    /// 未知机兵支援：不在叙事者流程中触发（CanFireNowSub 恒为 false），
    /// 空投 15 近卫、10 机动射手、1 萨满、5 塔盾手；若加载 AncientCorps 则额外 1 铁面。
    /// </summary>
    public class IncidentWorker_UnknownMechSupport : IncidentWorker
    {
        private const string FactionDefName = "DMSL_Faction_DigitalAngel";
        private const string AncientCorpsPackageId = "Aoba.DeadManSwitch.AncientCorps";

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return false; // 仅由组件调用，叙事者永不触发
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.target is not Map map || !map.IsPlayerHome)
                return false;

            // 派系 DMSL_Faction_DigitalAngel 不存在则不执行
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            if (factionDef == null)
                return false;
            Faction faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null)
                return false;

            // 每次生成此事件时执行一次电子天使派系关系更新
            DigitalAngelRelationAligner.AlignRelations(faction, sendLetters: false);

            List<Pawn> pawns = GenerateSupportPawns(faction);
            if (pawns == null || pawns.Count == 0)
                return false;

            // 为所有支援机兵添加永久「电子天使」健康状态（死亡时由原版 DisappearsOnDeath 自动移除）
            ApplyDigitalAngelHediffToPawns(pawns);

            // 优先落在有敌人的位置附近；无敌人或附近无合法落点时回退为随机/贸易落点
            IntVec3 dropSpot = TryFindDropSpotNearEnemies(map);
            if (!dropSpot.IsValid)
                dropSpot = DropCellFinder.RandomDropSpot(map);
            if (!dropSpot.IsValid)
                dropSpot = DropCellFinder.TradeDropSpot(map);
            if (!dropSpot.IsValid)
                return false;

            // 援军逻辑：与原版友军援军一致，自动搜寻并攻击地图上的敌人（LordJob_AssistColony + LordToil_HuntEnemies）
            IntVec3 fallbackSpot = dropSpot;
            RCellFinder.TryFindRandomSpotJustOutsideColony(dropSpot, map, out fallbackSpot);
            LordMaker.MakeNewLord(faction, new LordJob_DigitalAngelAssistColony(faction, fallbackSpot), map, pawns);

            DropPodUtility.DropThingsNear(
                dropSpot,
                map,
                pawns,
                openDelay: 110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: true,
                forbid: true,
                allowFogged: true,
                faction
            );

            SendStandardLetter(
                def.letterLabel,
                def.letterText,
                def.letterDef,
                parms,
                new TargetInfo(dropSpot, map)
            );

            return true;
        }

        private static List<Pawn> GenerateSupportPawns(Faction faction)
        {
            var list = new List<Pawn>();

            // 15 近卫
            var guardian = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMSL_Mech_Guardian");
            if (guardian != null)
            {
                for (int i = 0; i < 15; i++)
                {
                    Pawn p = PawnGenerator.GeneratePawn(guardian, faction);
                    if (p != null)
                        list.Add(p);
                }
            }

            // 10 机动射手
            var commandos = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMSL_Mech_Commandos");
            if (commandos != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    Pawn p = PawnGenerator.GeneratePawn(commandos, faction);
                    if (p != null)
                        list.Add(p);
                }
            }

            // 1 萨满
            var shaman = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMSL_Mech_Shaman");
            if (shaman != null)
            {
                Pawn p = PawnGenerator.GeneratePawn(shaman, faction);
                if (p != null)
                    list.Add(p);
            }

            // 5 塔盾手
            var protector = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMS_Mech_Protector");
            if (protector != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    Pawn p = PawnGenerator.GeneratePawn(protector, faction);
                    if (p != null)
                        list.Add(p);
                }
            }

            // 若加载 AncientCorps，额外 1 铁面
            if (ModsConfig.IsActive(AncientCorpsPackageId))
            {
                var ironFace = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMSL_Mech_IronFace");
                if (ironFace != null)
                {
                    Pawn p = PawnGenerator.GeneratePawn(ironFace, faction);
                    if (p != null)
                        list.Add(p);
                }
            }

            return list;
        }

        private const string DigitalAngelHediffDefName = "DMSL_Hediff_DigitalAngel";
        private const int DropOffsetFromEnemies = 15;

        /// <summary>
        /// 为当前事件生成的所有支援机兵添加永久「电子天使」健康状态；死亡时由原版 HediffComp_DisappearsOnDeath 自动移除。
        /// </summary>
        private static void ApplyDigitalAngelHediffToPawns(List<Pawn> pawns)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(DigitalAngelHediffDefName);
            if (hediffDef == null || pawns == null)
                return;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p?.health?.hediffSet == null)
                    continue;
                Hediff hediff = HediffMaker.MakeHediff(hediffDef, p);
                if (hediff != null)
                {
                    hediff.Part = null;
                    p.health.AddHediff(hediff, null);
                }
            }
        }

        /// <summary>
        /// 若有对殖民地活跃威胁，则在「敌群向殖民地方向偏移 15 格」附近找空投落点，避免直接落在敌群内；否则返回 Invalid。
        /// </summary>
        private static IntVec3 TryFindDropSpotNearEnemies(Map map)
        {
            if (map?.attackTargetsCache?.TargetsHostileToColony == null)
                return IntVec3.Invalid;

            var activeThreats = map.attackTargetsCache.TargetsHostileToColony
                .Where(t => t?.Thing != null && t.Thing.Spawned && GenHostility.IsActiveThreatToPlayer(t))
                .ToList();
            if (activeThreats.Count == 0)
                return IntVec3.Invalid;

            IntVec3 enemyPos = activeThreats.RandomElement().Thing.Position;
            IntVec3 colonyCenter = map.Center;
            Vector3 toColony = (colonyCenter - enemyPos).ToVector3();
            if (toColony.sqrMagnitude < 0.01f)
                toColony = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
            toColony.Normalize();
            IntVec3 offsetCenter = (enemyPos.ToVector3() + toColony * DropOffsetFromEnemies).ToIntVec3();
            if (!offsetCenter.InBounds(map))
                offsetCenter = offsetCenter.ClampInsideMap(map);

            if (!DropCellFinder.TryFindDropSpotNear(offsetCenter, map, out IntVec3 result, allowFogged: true, canRoofPunch: true, 10))
                return IntVec3.Invalid;
            return result;
        }
    }
}
