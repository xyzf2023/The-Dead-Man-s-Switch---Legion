using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace DMS_Legion
{
    public class WorkGiver_DrillingBargeDeepDrill : WorkGiver_Scanner
    {
        private const string DrillingBargeRaceDefName = "DMSL_Mech_DrillingBarge";

        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn == null || pawn.def?.defName != DrillingBargeRaceDefName)
                return true;
            if (DMSL_ModSettings.settings?.enableDrillingBargeDeepDrill != true)
                return true;
            Map? map = pawn.Map;
            if (map == null || map.Biome == null || !map.Biome.hasBedrock)
                return true;
            // 无矿时 O(1) 跳过，避免 PotentialWorkCellsGlobal(pawn).Any() 触发全图扫描
            if (!DeepResourceGridTrackerComponent.HasAnyDeepResource(map))
                return true;
            return !PotentialWorkCellsGlobal(pawn).Any();
        }

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.def?.defName != DrillingBargeRaceDefName)
                yield break;

            Map map = pawn.Map;
            if (DMSL_ModSettings.settings?.enableDrillingBargeExperimentalWorkLogic == true)
            {
                var cacheComp = DrillingBargeValidDrillCellsCacheComponent.GetOrCreate(map);
                IReadOnlyList<IntVec3>? cached = cacheComp?.GetCachedCells();
                if (cached != null && cached.Count > 0)
                {
                    for (int i = 0; i < cached.Count; i++)
                    {
                        IntVec3 cell = cached[i];
                        if (!cell.InAllowedArea(pawn))
                            continue;
                        if (pawn.CanReach(cell, PathEndMode.OnCell, MaxPathDanger(pawn))
                            && !cell.IsForbidden(pawn)
                            && CellHasValuableDeepResource(cell, map, pawn)
                            && pawn.CanReserve(cell, 1, -1, null, false))
                            yield return cell;
                    }
                }
                yield break;
            }

            // 未开启实验性逻辑：使用原来的直接全图遍历
            foreach (IntVec3 cell in GetValidDrillCells(map))
            {
                if (!cell.InAllowedArea(pawn))
                    continue;
                if (pawn.CanReach(cell, PathEndMode.OnCell, MaxPathDanger(pawn))
                    && !cell.IsForbidden(pawn)
                    && CellHasValuableDeepResource(cell, map, pawn)
                    && pawn.CanReserve(cell, 1, -1, null, false))
                    yield return cell;
            }
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            if (pawn == null || pawn.def?.defName != DrillingBargeRaceDefName)
                return false;
            if (pawn.Map == null || c.IsForbidden(pawn))
                return false;
            if (!c.InAllowedArea(pawn))
                return false;
            if (!pawn.CanReach(c, PathEndMode.OnCell, MaxPathDanger(pawn)))
                return false;
            if (!pawn.CanReserve(c, 1, -1, null, forced))
                return false;
            return CellHasValuableDeepResource(c, pawn.Map, pawn);
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
        {
            if (pawn == null)
                return null!;
            JobDef? jobDef = DefDatabase<JobDef>.GetNamed("DMSL_Job_DrillingBargeDeepDrill", false);
            if (jobDef == null)
                return null!;
            return JobMaker.MakeJob(jobDef, cell);
        }

        /// <summary>
        /// 该格或周围 21 格在 deepResourceGrid 中是否有矿（地质扫描仪扫出的矿点）；不含仅基岩石头的情况。
        /// </summary>
        public static bool CellHasValuableDeepResource(IntVec3 center, Map map)
        {
            return CellHasValuableDeepResource(center, map, null);
        }

        /// <summary>
        /// 该格或周围 21 格在 deepResourceGrid 中是否有可开采深矿；可选按驳机 XML 排除名单过滤矿物。
        /// </summary>
        public static bool CellHasValuableDeepResource(IntVec3 center, Map map, Pawn? pawn)
        {
            if (map == null || !center.InBounds(map))
                return false;
            bool hasResource = DeepDrillUtility.GetNextResource(center, map, out ThingDef resourceDef, out _, out _);
            if (!hasResource || resourceDef == null)
                return false;
            if (pawn?.def?.defName == DrillingBargeRaceDefName
                && CompDrillingBargeDeepResourceOverlay.IsExcludedDeepResource(resourceDef))
                return false;
            return true;
        }

        private static IEnumerable<IntVec3> GetValidDrillCells(Map map)
        {
            if (map == null || !map.Biome.hasBedrock)
                yield break;

            TerrainAffordanceDef? affordance = ThingDefOf.DeepDrill?.terrainAffordanceNeeded;
            for (int i = 0; i < map.cellIndices.NumGridCells; i++)
            {
                IntVec3 c = map.cellIndices.IndexToCell(i);
                if (!c.InBounds(map) || !c.Walkable(map))
                    continue;
                if (affordance != null && !c.GetAffordances(map).Contains(affordance))
                    continue;
                if (!CellHasValuableDeepResource(c, map))
                    continue;
                yield return c;
            }
        }
    }
}
