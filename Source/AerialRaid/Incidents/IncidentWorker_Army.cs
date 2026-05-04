using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;
using UnityEngine;
using DMS_Legion.AerialRaid.AerialRaidComponents;
using DMS_Legion;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 空袭事件工作器（仅空袭，不生成地面敌人）
    /// 继承自 IncidentWorker_Raid 以保持兼容性，但仅使用空袭前置阶段组件
    /// </summary>
    public class IncidentWorker_Army : IncidentWorker_Raid
    {
        static IncidentWorker_Army()
        {
            // 验证袭击定义是否正确加载（只在失败时记录）
            IncidentDef? def = DefDatabase<IncidentDef>.GetNamed("DMSL_AerialRaid_Army", false);
            if (def == null)
            {
                Log.Error("[DMS_Legion]空袭袭击工作器：无法加载IncidentDef：DMSL_AerialRaid_Army");
            }
        }

        /// <summary>
        /// 倒计时时间范围（Tick）
        /// 1小时 = 2500 tick
        /// 3小时 = 7500 tick
        /// 5小时 = 12500 tick
        /// </summary>
        private const int MinCountdownTicks = 7500;  // 3小时
        private const int MaxCountdownTicks = 12500; // 5小时

        /// <summary>
        /// 每多少点数触发1次空中支援
        /// </summary>
        private const float PointsPerAirStrike = 5000f;

        /// <summary>
        /// 最小空中支援次数
        /// </summary>
        private const int MinAirStrikeCount = 1;

        /// <summary>
        /// DMS_Army 派系的 defName
        /// </summary>
        private const string RequiredFactionDefName = "DMS_Army";

        /// <summary>
        /// 检查事件是否可以在当前条件下触发
        /// </summary>
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            // 先调用基类检查（检查地图是否有效等基本条件）
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            Map? map = parms.target as Map;
            if (map == null)
            {
                return false;
            }

            // 检查MOD设置：是否启用空袭事件
            if (DMSL_ModSettings.settings == null || !DMSL_ModSettings.settings.enableAerialRaid)
            {
                return false;
            }

            // 检查DMS_Army派系是否存在且与玩家敌对
            FactionDef? armyFactionDef = DefDatabase<FactionDef>.GetNamed(RequiredFactionDefName, false);
            if (armyFactionDef == null)
            {
                Log.Warning($"[DMS_Legion]空袭袭击工作器：未找到派系定义：'{RequiredFactionDefName}'");
                return false;
            }

            Faction? armyFaction = Find.FactionManager.FirstFactionOfDef(armyFactionDef);
            if (armyFaction == null)
            {
                Log.Warning($"[DMS_Legion]空袭袭击工作器：派系 '{RequiredFactionDefName}' 不存在");
                return false;
            }

            if (!armyFaction.HostileTo(Faction.OfPlayer))
            {
                return false; // DMS_Army派系不与玩家敌对，不允许触发
            }

            // 奥德赛 DLC 启用时，若玩家地图在太空中则不允许触发
            if (AerialRaidOdysseyUtility.IsMapInSpace(map))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 解析袭击点数
        /// 仅空袭事件仍然需要点数来计算空袭次数
        /// </summary>
        protected override void ResolveRaidPoints(IncidentParms parms)
        {
            if (parms.points <= 0f)
            {
                parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target);
            }
            
            // 点数用于计算空袭次数（在 CalculateAirStrikeCount 方法中）
        }

        /// <summary>
        /// 执行空袭逻辑（仅空袭，不生成地面敌人）
        /// </summary>
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map? map = parms.target as Map;
            if (map == null)
            {
                Log.Error("[DMS_Legion]空袭袭击工作器：目标地图为null");
                return false;
            }

            // 计算空袭次数（基于原始点数）
            int airStrikeCount = CalculateAirStrikeCount(parms.points);
            
            // 创建并初始化空袭前置阶段组件
            var prePhaseComponent = AerialRaidPrePhaseComponent.GetOrCreate(map);
            if (prePhaseComponent == null)
            {
                Log.Error("[DMS_Legion]空袭袭击工作器：无法获取或创建前置阶段组件");
                return false;
            }

            // 设置倒计时时间
            int countdownTicks = Rand.RangeInclusive(MinCountdownTicks, MaxCountdownTicks);
            prePhaseComponent.SetRemainingTicks(countdownTicks);

            // 设置空袭执行次数
            prePhaseComponent.SetExecutionCount(airStrikeCount);

            // 使用原版信件系统发送空袭警告信件
            // SendStandardLetter 会自动使用 def.letterLabel, def.letterText, def.letterDef
            SendStandardLetter(parms, new LookTargets(map.Parent));
            
            // 空袭会在倒计时结束后自动执行（由 AerialRaidPrePhaseComponent 管理）

            return true;
        }

        /// <summary>
        /// 计算空袭次数
        /// 基于原始点数计算：每5000点数触发1次空中支援，至少1次
        /// 例如：原始点数10000 -> 2次空袭，原始点数5000 -> 1次空袭
        /// </summary>
        private int CalculateAirStrikeCount(float originalPoints)
        {
            if (originalPoints <= 0f)
            {
                return MinAirStrikeCount;
            }

            float baseCount = originalPoints / PointsPerAirStrike;
            int count = Mathf.CeilToInt(baseCount);
            count = Mathf.Max(count, MinAirStrikeCount);

            return count;
        }

        // ========== 以下方法是为了满足基类抽象成员要求，但不会被实际使用（因为我们是仅空袭事件） ==========

        /// <summary>
        /// 解析袭击派系
        /// 直接指定为 DMS_Army 派系（与 CanFireNowSub 中的检查保持一致）
        /// </summary>
        protected override bool TryResolveRaidFaction(IncidentParms parms)
        {
            // 获取 DMS_Army 派系定义
            FactionDef? armyFactionDef = DefDatabase<FactionDef>.GetNamed(RequiredFactionDefName, false);
            if (armyFactionDef == null)
            {
                Log.Error($"[DMS_Legion]空袭袭击工作器：TryResolveRaidFaction：未找到派系定义：'{RequiredFactionDefName}'");
                return false;
            }

            // 获取 DMS_Army 派系实例
            Faction? armyFaction = Find.FactionManager.FirstFactionOfDef(armyFactionDef);
            if (armyFaction == null)
            {
                Log.Error($"[DMS_Legion]空袭袭击工作器：TryResolveRaidFaction：派系 '{RequiredFactionDefName}' 不存在");
                return false;
            }

            // 检查派系是否与玩家敌对（虽然在 CanFireNowSub 中已检查，但这里再检查一次以确保一致性）
            if (!armyFaction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            // 设置派系
            parms.faction = armyFaction;
            return true;
        }

        /// <summary>
        /// 获取信件标签（基类要求实现）
        /// </summary>
        protected override string GetLetterLabel(IncidentParms parms)
        {
            // 使用 IncidentDef 中定义的 letterLabel
            if (!string.IsNullOrEmpty(def.letterLabel))
            {
                return def.letterLabel;
            }
            return "DMSL_AerialRaid_Army_LetterLabel".Translate();
        }

        /// <summary>
        /// 获取信件文本（基类要求实现）
        /// </summary>
        protected override string GetLetterText(IncidentParms parms, List<Pawn> pawns)
        {
            // 使用 IncidentDef 中定义的 letterText
            if (!string.IsNullOrEmpty(def.letterText))
            {
                return def.letterText;
            }
            return "DMSL_AerialRaid_Army_LetterText".Translate();
        }

        /// <summary>
        /// 获取信件定义（基类要求实现）
        /// </summary>
        protected override LetterDef GetLetterDef()
        {
            // 使用 IncidentDef 中定义的 letterDef
            if (def.letterDef != null)
            {
                return def.letterDef;
            }
            return LetterDefOf.ThreatBig;
        }

        /// <summary>
        /// 解析袭击策略（基类要求实现，但不会被使用）
        /// </summary>
        public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            // 由于是仅空袭事件，不需要策略
            // 但基类要求实现此方法
            if (parms.raidStrategy == null)
            {
                parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            }
        }

        /// <summary>
        /// 获取相关Pawns信息的信件文本（基类要求实现）
        /// </summary>
        protected override string GetRelatedPawnsInfoLetterText(IncidentParms parms)
        {
            // 仅空袭事件没有相关Pawns，返回空字符串
            return "";
        }





    }
}
