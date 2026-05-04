// ============================================================================
// 文件：CompAgriculturalFrameHay.cs
// 说明：农业框架完成收获/割除后额外掉落干草的逻辑（由 PlantCollected 的 Harmony Postfix 调用）
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 挂载在农业框架种族上，每次完成一株植物的收获或割除后生成额外干草。不参与 Tick，仅由低频的 PlantCollected 钩子触发。
    /// </summary>
    public class CompAgriculturalFrameHay : ThingComp
    {
        public CompProperties_AgriculturalFrameHay Props => (CompProperties_AgriculturalFrameHay)props;

        /// <summary>
        /// 在 Plant.PlantCollected 的 Postfix 中调用：根据配置过滤后，在收集者位置生成额外产物。
        /// </summary>
        public void SpawnExtraYield(Pawn collector, Map map, Plant? plant, PlantDestructionMode mode)
        {
            if (map == null || collector == null || parent != collector || plant == null)
                return;
            if (!ShouldSpawnFor(plant, mode))
                return;

            ThingDef def = Props.extraThingDef ?? ThingDefOf.Hay;
            int count = Rand.RangeInclusive(Props.extraCountMin, Props.extraCountMax);
            if (count <= 0 || def == null)
                return;

            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = System.Math.Min(count, def.stackLimit);
            if (collector.Faction != Faction.OfPlayer)
                thing.SetForbidden(true);
            GenPlace.TryPlaceThing(thing, collector.Position, map, ThingPlaceMode.Near);
        }

        /// <summary>
        /// 根据 onlyWhenCut / onlyWhenHarvest / allowedPlantDefs 判断本次是否应生成额外干草。
        /// </summary>
        private bool ShouldSpawnFor(Plant plant, PlantDestructionMode mode)
        {
            if (plant?.def == null || plant.def.plant == null)
                return false;

            // 收木材、砍树不生成干草，只对非树木（作物、草等）生成
            if (plant.def.plant.IsTree)
                return false;

            if (Props.onlyWhenCut && !Props.onlyWhenHarvest && mode != PlantDestructionMode.Cut)
                return false;
            if (Props.onlyWhenHarvest && !Props.onlyWhenCut && mode != PlantDestructionMode.Chop)
                return false;

            if (Props.allowedPlantDefs != null && Props.allowedPlantDefs.Count > 0 && !Props.allowedPlantDefs.Contains(plant.def))
                return false;

            return true;
        }
    }
}
