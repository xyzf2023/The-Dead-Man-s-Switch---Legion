// ============================================================================
// 文件：IncidentWorker_GiftFromArmy.cs
// 说明：来自武装殖民舰队馈赠事件 Worker，在安全位置空投按好感价值生成的物品
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace DMS_Legion.Incidents.GiftFromArmy
{
    /// <summary>
    /// 来自武装殖民舰队馈赠：仅在与 DMS_Army 盟友时可触发，
    /// 根据当前好感计算总价值（好感×valuePerGoodwill，上限 maxTotalValue），
    /// 在组件配置的物品 def 中随机分配生成，空投到贸易空投点。
    /// </summary>
    public class IncidentWorker_GiftFromArmy : IncidentWorker
    {
        private const string LetterLabelKey = "DMSL_GiftFromArmy_LetterLabel";
        private const string LetterTextKey = "DMSL_GiftFromArmy_LetterText";
        private const string FactionDefName = "DMS_Army";

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
                return false;

            if (parms.target is not Map map || !map.IsPlayerHome)
                return false;

            Faction? army = GetArmyFaction();
            if (army == null || army.PlayerRelationKind != FactionRelationKind.Ally)
                return false;

            var props = def.GetModExtension<CompProperties_GiftFromArmyIncident>();
            if (props?.thingDefNames == null || props.thingDefNames.Count == 0)
                return false;

            int totalValue = GetGiftValue(army, props);
            if (totalValue <= 0)
                return false;

            List<ThingDef> allowed = ResolveAllowedThingDefs(props);
            if (allowed.Count == 0)
                return false;

            float minValue = allowed.Min(td => GetUnitValue(td));
            if (totalValue < minValue)
                return false;

            return DropCellFinder.TradeDropSpot(map).IsValid;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.target is not Map map || !map.IsPlayerHome)
                return false;

            Faction? army = GetArmyFaction();
            if (army == null || army.PlayerRelationKind != FactionRelationKind.Ally)
                return false;

            var props = def.GetModExtension<CompProperties_GiftFromArmyIncident>();
            if (props?.thingDefNames == null || props.thingDefNames.Count == 0)
                return false;

            int totalValue = GetGiftValue(army, props);
            List<ThingDef> allowed = ResolveAllowedThingDefs(props);
            if (allowed.Count == 0 || totalValue <= 0)
                return false;

            List<Thing> things = GenerateThingsUpToValue(allowed, totalValue);
            if (things == null || things.Count == 0)
                return false;

            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            if (!dropSpot.IsValid)
                return false;

            DropPodUtility.DropThingsNear(
                dropSpot,
                map,
                things,
                110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: true,
                forbid: false
            );

            SendStandardLetter(
                LetterLabelKey.Translate(),
                LetterTextKey.Translate(),
                LetterDefOf.PositiveEvent,
                parms,
                new TargetInfo(dropSpot, map)
            );

            return true;
        }

        private static Faction? GetArmyFaction()
        {
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (f?.def != null && f.def.defName == FactionDefName)
                    return f;
            }
            return null;
        }

        private static int GetGiftValue(Faction army, CompProperties_GiftFromArmyIncident props)
        {
            int goodwill = army?.GoodwillWith(Faction.OfPlayer) ?? 0;
            if (goodwill < 0)
                goodwill = 0;
            int value = goodwill * props.valuePerGoodwill;
            return Mathf.Min(value, props.maxTotalValue);
        }

        private static List<ThingDef> ResolveAllowedThingDefs(CompProperties_GiftFromArmyIncident props)
        {
            var list = new List<ThingDef>();
            foreach (string name in props.thingDefNames)
            {
                ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (td != null && td.BaseMarketValue > 0f && (td.category == ThingCategory.Item || td.Minifiable))
                    list.Add(td);
            }
            return list;
        }

        private static float GetUnitValue(ThingDef def)
        {
            return def.BaseMarketValue;
        }

        /// <summary>
        /// 在允许的 ThingDef 中随机选取并生成物品，使总价值接近 totalValue；
        /// 当剩余价值不足以生成任意一件物品时停止。
        /// </summary>
        private static List<Thing> GenerateThingsUpToValue(List<ThingDef> allowed, int totalValue)
        {
            if (allowed.Count == 0 || totalValue <= 0)
                return new List<Thing>();

            float remaining = totalValue;
            float minUnitValue = allowed.Min(GetUnitValue);
            var result = new List<Thing>();

            while (remaining >= minUnitValue)
            {
                ThingDef chosen = allowed.RandomElement();
                float unitValue = GetUnitValue(chosen);
                if (unitValue <= 0f || remaining < unitValue)
                    continue;

                int stackCount = 1;
                if (chosen.stackLimit > 1)
                {
                    int maxByValue = Mathf.FloorToInt(remaining / unitValue);
                    stackCount = Mathf.Clamp(maxByValue, 1, chosen.stackLimit);
                }

                Thing thing = ThingMaker.MakeThing(chosen);
                thing.stackCount = stackCount;
                result.Add(thing);
                remaining -= thing.MarketValue;
            }

            return result;
        }
    }
}
