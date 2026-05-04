// ============================================================================
// 叙事者组件：艾丽萨的带宽认可
// 若玩家与武装殖民舰队（DMS_Army）非敌对，按间隔扫描机械师使用带宽，增加好感并发送信件；皇权下可授予荣誉
// ============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    public class StorytellerComp_ElisaBandwidthApproval : StorytellerComp
    {
        private StorytellerCompProperties_ElisaBandwidthApproval Props =>
            (StorytellerCompProperties_ElisaBandwidthApproval)props;

        private DMSL_GameComponent_ElisaBandwidth? GetComponent()
        {
            return Current.Game?.GetComponent<DMSL_GameComponent_ElisaBandwidth>();
        }

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            Map? map = target as Map;
            if (map == null || !map.IsPlayerHome)
                yield break;

            var comp = GetComponent();
            if (comp == null || !comp.ShouldRunNow(Props.intervalDays))
                yield break;

            if (!TryRunOnce(map, Props, markRun: true))
                yield break;

            comp.MarkRun();
        }

        /// <summary>
        /// 执行一次“艾丽萨的青睐”逻辑：检查条件、加好感、发信。供叙事者与调试指令共用。
        /// </summary>
        /// <returns>是否成功执行（已加好感并发信）</returns>
        public static bool TryRunOnce(Map map, StorytellerCompProperties_ElisaBandwidthApproval? props = null, bool markRun = false)
        {
            var p = props ?? new StorytellerCompProperties_ElisaBandwidthApproval();

            FactionDef? armyDef = DefDatabase<FactionDef>.GetNamedSilentFail(p.factionDefName);
            if (armyDef == null)
                return false;
            Faction? army = Find.FactionManager.FirstFactionOfDef(armyDef);
            if (army == null || army.HostileTo(Faction.OfPlayer))
                return false;

            if (!ModsConfig.BiotechActive)
                return false;

            int totalBandwidth = SumColonyMechanitorUsedBandwidth();
            if (totalBandwidth <= 0)
                return false;

            float goodwillPer = Math.Max(0.001f, p.goodwillPerBandwidth);
            int goodwillChange = Math.Max(0, (int)Math.Floor(totalBandwidth / goodwillPer));
            if (goodwillChange <= 0)
                return false;

            HistoryEventDef? reasonDef = DefDatabase<HistoryEventDef>.GetNamedSilentFail(p.historyEventDefName);
            Faction.OfPlayer.TryAffectGoodwillWith(army, goodwillChange, true, true, reasonDef, null);

            string letterTitle = "DMSL_ElisaFavor_LetterTitle".Translate().ToString();
            string letterText = "DMSL_ElisaFavor_LetterText".Translate(goodwillChange.Named("INCREASE")).ToString();

            if (ModsConfig.RoyaltyActive && army.def.royalFavorLabel != null)
            {
                float honorPer = Math.Max(0.001f, p.honorPerBandwidth);
                int honorAmount = (int)Math.Floor(totalBandwidth / honorPer);
                if (honorAmount >= 1)
                {
                    letterText += "\n\n" + "DMSL_ElisaFavor_LetterTextRoyalty".Translate(honorAmount.Named("AMOUNT")).ToString();
                    List<Pawn> colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists;
                    if (colonists.Count > 0)
                    {
                        var choiceLetter = new ChoiceLetter_ElisaHonorChooseColonist
                        {
                            def = LetterDefOf.PositiveEvent,
                            ID = Find.UniqueIDsManager.GetNextLetterID(),
                            Label = letterTitle,
                            Text = letterText,
                            colonists = new List<Pawn>(colonists),
                            faction = army,
                            honorAmount = honorAmount,
                            relatedFaction = army,
                            lookTargets = new LookTargets(map.Center, map)
                        };
                        Find.LetterStack.ReceiveLetter(choiceLetter, null, 0, true);
                    }
                    else
                    {
                        Find.LetterStack.ReceiveLetter(letterTitle, letterText, LetterDefOf.PositiveEvent, null, army, null, null, null, 0, true);
                    }
                }
                else
                {
                    Find.LetterStack.ReceiveLetter(letterTitle, letterText, LetterDefOf.PositiveEvent, null, army, null, null, null, 0, true);
                }
            }
            else
            {
                Find.LetterStack.ReceiveLetter(letterTitle, letterText, LetterDefOf.PositiveEvent, null, army, null, null, null, 0, true);
            }

            if (markRun)
            {
                var comp = Current.Game?.GetComponent<DMSL_GameComponent_ElisaBandwidth>();
                comp?.MarkRun();
            }
            return true;
        }

        /// <summary>
        /// 殖民地带宽总和：殖民者机械师 UsedBandwidth + 玩家地图上挂载了机械师带宽组件的 Pawn（如正义/隐者、带宽协调模块）提供的额外带宽。
        /// </summary>
        private static int SumColonyMechanitorUsedBandwidth()
        {
            int sum = 0;
            foreach (Pawn p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
            {
                if (p?.mechanitor != null)
                    sum += p.mechanitor.UsedBandwidth;
            }
            foreach (Map map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome)
                    continue;
                sum += SumBandwidthFromMapPawns(map);
            }
            return sum;
        }

        /// <summary>
        /// 仅从玩家地图上的 Pawn 汇总“机械师带宽组件”提供的带宽：机械体上的 ThingComp（如正义/隐者的 CompMechanitorBandwidth）+ 任意 Pawn 上的 HediffComp（如带宽协调模块）。
        /// 殖民者不重复计其 ThingComp，避免与 mechanitor.UsedBandwidth 重复。
        /// </summary>
        private static int SumBandwidthFromMapPawns(Map map)
        {
            int sum = 0;
            List<Thing> pawns = map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] is not Pawn pawn)
                    continue;
                // 非殖民者机械体上的带宽 comp（如正义/隐者）
                if (!pawn.IsColonist && pawn is ThingWithComps twc)
                {
                    foreach (ThingComp c in twc.AllComps)
                    {
                        if (c != null)
                            sum += TryGetBandwidthFromObject(c);
                    }
                }
                // 任意 Pawn 的 Hediff 带宽（如带宽协调模块）
                sum += SumBandwidthFromPawnHediffs(pawn);
            }
            return sum;
        }

        private static int SumBandwidthFromPawnHediffs(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                return 0;
            int sum = 0;
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h is not HediffWithComps hwc || hwc.comps == null)
                    continue;
                foreach (HediffComp hc in hwc.comps)
                {
                    if (hc != null)
                        sum += TryGetBandwidthFromObject(hc);
                }
            }
            return sum;
        }

        /// <summary>从机械师带宽组件（ThingComp/HediffComp）读带宽，约定：GetBandwidth() 或属性 ExtraBandwidth 等（int/float，向下取整）。</summary>
        private static int TryGetBandwidthFromObject(object? comp)
        {
            if (comp == null)
                return 0;
            Type t = comp.GetType();
            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                MethodInfo? mi = t.GetMethod("GetBandwidth", Type.EmptyTypes);
                if (mi != null && (mi.ReturnType == typeof(int) || mi.ReturnType == typeof(float)))
                {
                    object? v = mi.Invoke(comp, null);
                    if (v is int i)
                        return Math.Max(0, i);
                    if (v is float f)
                        return Math.Max(0, (int)Math.Floor(f));
                }
                foreach (string propName in new[] { "Bandwidth", "UsedBandwidth", "TotalBandwidth", "ExtraBandwidth" })
                {
                    PropertyInfo? pi = t.GetProperty(propName, flags);
                    if (pi == null || !pi.CanRead)
                        continue;
                    object? v = pi.GetValue(comp);
                    if (v is int i)
                        return Math.Max(0, i);
                    if (v is float f)
                        return Math.Max(0, (int)Math.Floor(f));
                }
            }
            catch
            {
                // 忽略反射或调用异常，视为 0
            }
            return 0;
        }
    }
}
