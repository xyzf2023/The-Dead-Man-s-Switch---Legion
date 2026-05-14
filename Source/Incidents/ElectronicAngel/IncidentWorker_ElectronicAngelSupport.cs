// ============================================================================
// 文件：IncidentWorker_ElectronicAngelSupport.cs
// 说明：“电子天使”支援事件 Worker，由黑衣人事件分支或其他组件调用。
//       整体逻辑参考 IncidentWorker_UnknownMechSupport，但编成更强，
//       并额外空投 1 个医护框架。
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using DMS_Legion;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.ElectronicAngel
{
    public class IncidentWorker_ElectronicAngelSupport : IncidentWorker
    {
        private const string FactionDefName = "DMSL_Faction_DigitalAngel";
        private const string AncientCorpsPackageId = "Aoba.DeadManSwitch.AncientCorps";

        private const string DigitalAngelHediffDefName = "DMSL_Hediff_DigitalAngel";
        private const string DoctorFramePawnKindDefName = "DMSL_Mech_DoctorFrame";

        private const int ExtraGuardians = 20;
        private const int ExtraProtectors = 5;
        private const int ExtraShamans = 3;
        private const int ExtraIronFaces = 2; // 仅在 AncientCorps 激活时追加

        private const int DropOffsetFromEnemies = 15;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            // 与未知机兵支援一致：仅由自定义逻辑调用，不参与叙事者轮询
            return false;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.target is not Map map || !map.IsPlayerHome)
                return false;

            // 确认电子天使派系存在
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            if (factionDef == null)
                return false;
            Faction faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null)
                return false;

            // 每次生成此事件时执行一次电子天使派系关系更新
            DigitalAngelRelationAligner.AlignRelations(faction, sendLetters: false);

            List<Pawn> combatPawns = GenerateCombatPawns(faction);
            if (combatPawns.NullOrEmpty())
                return false;

            // 生成医护框架（单独处理空投位置），并为其创建专用医疗 AI Lord
            Pawn? doctorFrame = GenerateDoctorFrame(faction);

            // 为所有机兵添加电子天使 Hediff
            ApplyDigitalAngelHediffToPawns(combatPawns);
            if (doctorFrame != null)
                ApplyDigitalAngelHediffToPawns(new List<Pawn> { doctorFrame });

            // 选择战斗部队空投位置：优先靠近敌人，否则回落到随机/贸易落点
            IntVec3 combatDropSpot = TryFindDropSpotNearEnemies(map);
            if (!combatDropSpot.IsValid)
                combatDropSpot = DropCellFinder.RandomDropSpot(map);
            if (!combatDropSpot.IsValid)
                combatDropSpot = DropCellFinder.TradeDropSpot(map);
            if (!combatDropSpot.IsValid)
                return false;

            // 战斗部队 Lord：沿用原版友军援军逻辑
            IntVec3 fallbackSpot = combatDropSpot;
            RCellFinder.TryFindRandomSpotJustOutsideColony(combatDropSpot, map, out fallbackSpot);
            LordMaker.MakeNewLord(faction, new LordJob_AssistColony(faction, fallbackSpot), map, combatPawns);

            DropPodUtility.DropThingsNear(
                combatDropSpot,
                map,
                combatPawns,
                openDelay: 110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: true,
                forbid: true,
                allowFogged: true,
                faction
            );

            // 医护框架空投位置：尽量靠近倒地殖民者，否则靠近基地或战斗落点
            if (doctorFrame != null)
            {
                // 为医护框架创建专用 Lord，驱动其执行医疗支援与离场逻辑
                LordMaker.MakeNewLord(faction, new LordJob_ElectronicAngelDoctor(), map, new List<Pawn> { doctorFrame });

                IntVec3 doctorDropSpot = TryFindDoctorDropSpot(map, combatDropSpot);
                if (!doctorDropSpot.IsValid)
                    doctorDropSpot = combatDropSpot;

                DropPodUtility.DropThingsNear(
                    doctorDropSpot,
                    map,
                    new List<Thing> { doctorFrame },
                    openDelay: 110,
                    canInstaDropDuringInit: false,
                    leaveSlag: false,
                    canRoofPunch: true,
                    forbid: true,
                    allowFogged: true,
                    faction
                );
            }

            SendStandardLetter(
                def.letterLabel,
                def.letterText,
                def.letterDef,
                parms,
                new TargetInfo(combatDropSpot, map)
            );

            return true;
        }

        private static List<Pawn> GenerateCombatPawns(Faction faction)
        {
            var list = new List<Pawn>();

            // 基于未知机兵支援的编成，并在其基础上追加数量：
            // 原：15 近卫、10 机动射手、1 萨满、5 塔盾手、（可选 1 铁面）
            // 电子天使：在原基础上额外 +20 近卫、+5 塔盾手、+3 萨满、+2 铁面。

            // 近卫
            var guardian = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMSL_Mech_Guardian");
            if (guardian != null)
            {
                int total = 15 + ExtraGuardians;
                for (int i = 0; i < total; i++)
                {
                    Pawn p = PawnGenerator.GeneratePawn(guardian, faction);
                    if (p != null)
                        list.Add(p);
                }
            }

            // 机动射手（数量与未知机兵支援一致）
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

            // 萨满：原 1 + 额外 3 = 4
            var shaman = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMSL_Mech_Shaman");
            if (shaman != null)
            {
                int total = 1 + ExtraShamans;
                for (int i = 0; i < total; i++)
                {
                    Pawn p = PawnGenerator.GeneratePawn(shaman, faction);
                    if (p != null)
                        list.Add(p);
                }
            }

            // 塔盾手：原 5 + 额外 5 = 10
            var protector = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMS_Mech_Protector");
            if (protector != null)
            {
                int total = 5 + ExtraProtectors;
                for (int i = 0; i < total; i++)
                {
                    Pawn p = PawnGenerator.GeneratePawn(protector, faction);
                    if (p != null)
                        list.Add(p);
                }
            }

            // 铁面：若加载 AncientCorps，则原 1 + 额外 2 = 3
            if (ModsConfig.IsActive(AncientCorpsPackageId))
            {
                var ironFace = DefDatabase<PawnKindDef>.GetNamedSilentFail("DMSL_Mech_IronFace");
                if (ironFace != null)
                {
                    int total = 1 + ExtraIronFaces;
                    for (int i = 0; i < total; i++)
                    {
                        Pawn p = PawnGenerator.GeneratePawn(ironFace, faction);
                        if (p != null)
                            list.Add(p);
                    }
                }
            }

            return list;
        }

        private static Pawn? GenerateDoctorFrame(Faction faction)
        {
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(DoctorFramePawnKindDefName);
            if (kindDef == null)
                return null;
            return PawnGenerator.GeneratePawn(kindDef, faction);
        }

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

        private static IntVec3 TryFindDoctorDropSpot(Map map, IntVec3 combatDropSpot)
        {
            // 优先靠近倒地的玩家殖民者
            var downedColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.Downed)
                .ToList();
            if (downedColonists.Count > 0)
            {
                Pawn center = downedColonists.RandomElement();
                if (DropCellFinder.TryFindDropSpotNear(center.Position, map, out IntVec3 result, allowFogged: true, canRoofPunch: true, 10))
                    return result;
            }

            // 无倒地殖民者时，退化为靠近基地中心或战斗落点
            IntVec3 centerPos = map.Center;
            if (!DropCellFinder.TryFindDropSpotNear(centerPos, map, out IntVec3 nearBase, allowFogged: true, canRoofPunch: true, 10))
                return combatDropSpot;
            return nearBase;
        }
    }
}

