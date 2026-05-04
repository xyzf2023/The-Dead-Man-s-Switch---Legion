using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using DMS_Legion;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 空袭支援袭击（携带传呼器，地面等待 -> 呼叫空袭 -> 冲锋）
    /// </summary>
    public class IncidentWorker_PagerRaid : IncidentWorker_Raid
    {
        /// <summary>
        /// 检查事件是否可以在当前条件下触发
        /// </summary>
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            // 检查MOD设置：是否启用海盗空袭先导袭击事件
            if (DMSL_ModSettings.settings == null || !DMSL_ModSettings.settings.enableAerialRaidPager)
            {
                return false;
            }

            // 奥德赛 DLC 启用时，若玩家地图在太空中则不允许触发
            Map? map = parms.target as Map;
            if (AerialRaidOdysseyUtility.IsMapInSpace(map))
            {
                return false;
            }

            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // 确保设置了到达方式（地图边缘生成）
            if (parms.raidArrivalMode == null)
            {
                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            }

            // 使用原版的 TryGenerateRaidInfo 方法生成敌人（完全按照原版流程）
            // TryGenerateRaidInfo 会调用 raidArrivalMode.Worker.Arrive，所以敌人已经到达地图
            List<Pawn> pawns;
            if (!TryGenerateRaidInfo(parms, out pawns, false))
            {
                Log.Warning("[DMS_Legion]空袭支援袭击：TryGenerateRaidInfo 失败");
                return false;
            }

            if (pawns.Count == 0)
            {
                Log.Warning("[DMS_Legion]空袭支援袭击：未生成任何敌人");
                return false;
            }

            Map map = (Map)parms.target;
            
            // TryGenerateRaidInfo 已经调用了 Arrive，所以敌人应该已经在地图上
            // 使用 spawnCenter 作为集合点，如果无效则使用第一个敌人的位置
            IntVec3 rallyPoint = parms.spawnCenter.IsValid ? parms.spawnCenter : pawns[0].Position;

            // 确保所有 pawns 都没有加入其他 Lord（策略的 SpawnThreats 可能已经创建了 Lord）
            int removedFromLordCount = 0;
            foreach (var pawn in pawns)
            {
                Lord? existingLord = pawn.GetLord();
                if (existingLord != null)
                {
                    existingLord.RemovePawn(pawn);
                    removedFromLordCount++;
                }
            }
            // 创建自定义 Lord（替换原版策略创建的 Lord）
            LordJob lordJob = new LordJob_PagerRaid(rallyPoint);
            Lord lord = LordMaker.MakeNewLord(parms.faction, lordJob, map, pawns);
            
            // 验证所有 pawns 都已加入 Lord
            foreach (var pawn in pawns)
            {
                if (pawn.GetLord() != lord)
                {
                    Log.Warning($"[DMS_Legion]空袭支援袭击：警告：{pawn.Name} 未正确加入 Lord");
                }
            }

            // 分配传呼器并立即下达 Job
            Pawn? caller = TryAssignPagerToPawn(pawns, map);
            if (caller != null)
            {
                // 创建 Job，设置 targetA 为 pawn 自身（用于进度条），targetB 为集合点（用于移动）
                Job callJob = JobMaker.MakeJob(DMSL_JobDefOf.DMSL_RaidCallAirSupport);
                callJob.targetA = caller;
                callJob.targetB = rallyPoint;
                caller.jobs.TryTakeOrderedJob(callJob, JobTag.Misc);
            }
            else
            {
                Log.Warning("[DMS_Legion]空袭支援袭击：无法分配传呼器，直接进入冲锋");
                // 无携带者时直接冲锋
                lord.ReceiveMemo(LordJob_PagerRaid.MemoCallFailed);
            }

            // 发送信件（仅一次）
            SendStandardLetter(parms, new LookTargets(pawns));
            return true;
        }

        /// <summary>
        /// 随机选一个 pawn，给予传呼器（若没有）并返回该 pawn
        /// </summary>
        private Pawn? TryAssignPagerToPawn(List<Pawn> pawns, Map map)
        {
            if (pawns.Count == 0)
            {
                Log.Warning("[DMS_Legion]空袭支援袭击：TryAssignPagerToPawn - pawns 列表为空");
                return null;
            }

            Pawn pawn = pawns.RandomElement();
            ThingDef? pagerDef = DefDatabase<ThingDef>.GetNamed("DMSL_AirSupportPager", false);
            if (pagerDef == null)
            {
                Log.Error("[DMS_Legion]空袭支援袭击：未找到 DMSL_AirSupportPager 定义");
                return null;
            }
            Thing pager = ThingMaker.MakeThing(pagerDef);

            // 尝试穿戴/放入装备槽；若失败则放入物品栏
            if (pager is Apparel apparel && pawn.apparel != null && ApparelUtility.HasPartsToWear(pawn, pagerDef))
            {
                pawn.apparel.Wear(apparel, dropReplacedApparel: false);
            }
            else
            {
                if (pager.stackCount > 1) pager.stackCount = 1;
                if (!pawn.inventory.innerContainer.TryAdd(pager))
                {
                    pager.Destroy(DestroyMode.Vanish);
                    return null;
                }
            }

            // 给持有传呼器的 pawn 添加标记 Hediff
            AddPagerMarkerHediff(pawn);

            return pawn;
        }

        /// <summary>
        /// 给 pawn 添加传呼器携带者标记 Hediff
        /// </summary>
        private void AddPagerMarkerHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            HediffDef? markerDef = DefDatabase<HediffDef>.GetNamed("DMSL_PagerCarrierMarker", false);
            if (markerDef == null)
            {
                Log.Warning("[DMS_Legion]空袭支援袭击：未找到 DMSL_PagerCarrierMarker HediffDef");
                return;
            }

            // 检查是否已经有这个 Hediff
            if (pawn.health.hediffSet.GetFirstHediffOfDef(markerDef) != null)
            {
                return; // 已经有了，不需要重复添加
            }

            // 添加 Hediff
            Hediff markerHediff = HediffMaker.MakeHediff(markerDef, pawn);
            pawn.health.AddHediff(markerHediff);
        }

        // ========== 以下方法是为了满足基类抽象成员要求 ==========

        /// <summary>
        /// 解析袭击点数
        /// </summary>
        protected override void ResolveRaidPoints(IncidentParms parms)
        {
            if (parms.points <= 0f)
            {
                parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target);
            }
        }

        /// <summary>
        /// 解析袭击派系
        /// 选择海盗或其他敌对的工业及以上科技派系
        /// </summary>
        protected override bool TryResolveRaidFaction(IncidentParms parms)
        {
            // 如果已经指定了派系，直接使用
            if (parms.faction != null)
            {
                return true;
            }

            // 查找符合条件的派系：敌对、工业及以上科技
            Faction? targetFaction = null;
            foreach (Faction faction in Find.FactionManager.AllFactions)
            {
                if (faction.def.hidden || faction.defeated || !faction.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }

                // 检查科技等级：工业及以上
                TechLevel techLevel = faction.def.techLevel;
                if (techLevel >= TechLevel.Industrial)
                {
                    // 优先选择海盗派系
                    if (faction.def.defName == "Pirate")
                    {
                        targetFaction = faction;
                        break;
                    }
                    else if (targetFaction == null)
                    {
                        targetFaction = faction;
                    }
                }
            }

            if (targetFaction == null)
            {
                return false;
            }

            parms.faction = targetFaction;
            return true;
        }

        /// <summary>
        /// 获取信件标签（基类要求实现，由 IncidentDef 配置）
        /// </summary>
        protected override string GetLetterLabel(IncidentParms parms)
        {
            return def.letterLabel ?? "";
        }

        /// <summary>
        /// 获取信件文本（基类要求实现，由 IncidentDef 配置）
        /// </summary>
        protected override string GetLetterText(IncidentParms parms, List<Pawn> pawns)
        {
            return def.letterText ?? "";
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
        /// 解析袭击策略（基类要求实现）
        /// </summary>
        public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            // 如果未指定策略，使用默认策略
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
            // 返回空字符串（信件文本已在 GetLetterText 中处理）
            return "";
        }
    }
}
