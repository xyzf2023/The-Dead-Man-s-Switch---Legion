// ============================================================================
// 缓存 + 分布式扫描：每 tick 仅扫描 K 格，全图遍历完成后更新缓存；派工仅读缓存，消除全图级峰值。
// K=4；仅在场上有钻井驳机且开启实验性工作逻辑时才执行扫描。缓存随存档保存/加载。
// ============================================================================

using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 按固定速度（K 格/tick）分布式遍历地图，缓存「可钻格」列表；仅在整轮遍历完成时更新缓存。
    /// 仅当本图存在至少一只钻井驳机且设置开启时才执行扫描。缓存通过 ExposeData 随存档保存。
    /// </summary>
    public class DrillingBargeValidDrillCellsCacheComponent : MapComponent
    {
        private const string DrillingBargeRaceDefName = "DMSL_Mech_DrillingBarge";
        private const int CellsPerTick = 4;

        private List<IntVec3> _cache = new List<IntVec3>();
        private List<IntVec3> _building = new List<IntVec3>();
        private int _scanIndex;

        public DrillingBargeValidDrillCellsCacheComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref _cache, "validDrillCellsCache", LookMode.Deep);
            Scribe_Collections.Look(ref _building, "validDrillCellsBuilding", LookMode.Deep);
            Scribe_Values.Look(ref _scanIndex, "scanIndex", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _cache ??= new List<IntVec3>();
                _building ??= new List<IntVec3>();
            }
        }

        public override void MapComponentTick()
        {
            if (map == null || !map.Biome.hasBedrock)
                return;
            if (DMSL_ModSettings.settings?.enableDrillingBargeExperimentalWorkLogic != true)
                return;
            if (!MapHasDrillingBarge())
                return;

            int numCells = map.cellIndices.NumGridCells;
            if (numCells == 0)
                return;

            TerrainAffordanceDef? affordance = ThingDefOf.DeepDrill?.terrainAffordanceNeeded;
            int remaining = CellsPerTick;
            while (remaining > 0 && _scanIndex < numCells)
            {
                IntVec3 c = map.cellIndices.IndexToCell(_scanIndex);
                _scanIndex++;
                remaining--;

                if (!c.InBounds(map) || !c.Walkable(map))
                    continue;
                if (affordance != null && !c.GetAffordances(map).Contains(affordance))
                    continue;
                if (!WorkGiver_DrillingBargeDeepDrill.CellHasValuableDeepResource(c, map))
                    continue;
                _building.Add(c);
            }

            if (_scanIndex >= numCells)
            {
                _cache.Clear();
                _cache.AddRange(_building);
                _building.Clear();
                _scanIndex = 0;
            }
        }

        private bool MapHasDrillingBarge()
        {
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i].def?.defName == DrillingBargeRaceDefName)
                    return true;
            }
            return false;
        }

        /// <summary>当前缓存的可钻格列表（只读）；派工仅遍历此列表。</summary>
        public IReadOnlyList<IntVec3> GetCachedCells()
        {
            return _cache;
        }

        /// <summary>获取或创建本图的缓存组件；若不存在则创建并加入 map.components，以便参与 MapComponentTick。</summary>
        public static DrillingBargeValidDrillCellsCacheComponent? GetOrCreate(Map map)
        {
            if (map == null)
                return null;
            var comp = map.GetComponent<DrillingBargeValidDrillCellsCacheComponent>();
            if (comp == null)
            {
                comp = new DrillingBargeValidDrillCellsCacheComponent(map);
                map.components.Add(comp);
            }
            return comp;
        }
    }
}
