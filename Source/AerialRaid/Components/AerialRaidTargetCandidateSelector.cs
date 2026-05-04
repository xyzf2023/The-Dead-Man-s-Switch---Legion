using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion.AerialRaid.AerialRaidComponents
{
    public enum AerialRaidTargetSource
    {
        None,
        Bait,
        HighValueHomeBuilding,
        HomeCenter,
        MapCenter
    }

    public struct AerialRaidTargetSnapshot
    {
        public List<IntVec3> TargetCells;
        public AerialRaidTargetSource Source;
        public bool IsValid;
    }

    public class AerialRaidTargetCandidateSelector : MapComponent
    {
        private struct CommittedRaidTarget
        {
            public IntVec3 Cell;
            public Building? Building;
            public float Value;
            public AerialRaidTargetSource Source;
        }

        private const int BuildingsPerScanTick = 1;
        private const int HomeCenterRefreshIntervalTicks = 1250;
        private const int FallbackRandomRadius = 12;
        private const int FallbackRandomTryCount = 8;

        private IntVec3 currentCommittedTarget = IntVec3.Invalid;
        private AerialRaidTargetSource currentTargetSource = AerialRaidTargetSource.None;

        private IntVec3 lastCommittedBuildingTarget = IntVec3.Invalid;
        private Building? lastCommittedBuilding;
        private List<CommittedRaidTarget> committedBuildingTargets = new List<CommittedRaidTarget>();
        private List<IntVec3> committedBuildingTargetCells = new List<IntVec3>();
        private int desiredTargetCount = 1;

        private bool scanInProgress;
        private bool prePhaseActive;
        private int scanIndex;
        private List<CommittedRaidTarget> scanBestBuildings = new List<CommittedRaidTarget>();

        private IntVec3 cachedHomeCenter = IntVec3.Invalid;
        private int lastHomeCenterUpdateTick = -1;

        public AerialRaidTargetCandidateSelector(Map map) : base(map)
        {
        }

        public static AerialRaidTargetCandidateSelector? GetOrCreate(Map map)
        {
            if (map == null)
            {
                return null;
            }

            var selector = map.GetComponent<AerialRaidTargetCandidateSelector>();
            if (selector == null)
            {
                selector = new AerialRaidTargetCandidateSelector(map);
                map.components.Add(selector);
            }

            return selector;
        }

        public void InitializeForPrePhase()
        {
            InitializeForPrePhase(desiredTargetCount);
        }

        public void InitializeForPrePhase(int executionCount)
        {
            if (map == null)
            {
                return;
            }

            SetDesiredTargetCount(executionCount);
            prePhaseActive = true;

            if (TryGetBaitTarget(out IntVec3 baitCell))
            {
                CommitTarget(baitCell, AerialRaidTargetSource.Bait);
                ClearScanState();
                return;
            }

            // 新一轮空袭启动时先维护 committed 结果，但不在该校验中触发启动扫描。
            if (!EnsureCommittedTargetValid(startScanOnFallback: false))
            {
                IntVec3 homeCenter = GetCachedHomeCenter(forceRefresh: true);
                if (homeCenter.IsValid)
                {
                    CommitTarget(homeCenter, AerialRaidTargetSource.HomeCenter);
                }
                else
                {
                    CommitTarget(map.Center, AerialRaidTargetSource.MapCenter);
                }
            }

            // 无 bait 时，始终启动一轮新的渐进扫描以刷新高价值建筑目标。
            StartBuildingScan();
        }

        public void ResumeForActivePrePhase()
        {
            if (map == null)
            {
                return;
            }

            prePhaseActive = true;
            EnsureCommittedTargetValid(startScanOnFallback: true);
        }

        public void SetDesiredTargetCount(int count)
        {
            desiredTargetCount = count > 0 ? count : 1;
            if (committedBuildingTargets.Count > desiredTargetCount)
            {
                committedBuildingTargets.RemoveRange(desiredTargetCount, committedBuildingTargets.Count - desiredTargetCount);
                SyncCommittedBuildingCells();
            }

            if (prePhaseActive && !TryGetBaitTarget(out _))
            {
                StartBuildingScan();
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (map == null)
            {
                return;
            }

            if (!prePhaseActive)
            {
                return;
            }

            if (!EnsureCommittedTargetValid(startScanOnFallback: true))
            {
                StartBuildingScan();
            }

            if (scanInProgress)
            {
                AdvanceBuildingScan();
            }
        }

        public void NotifyBaitTargetChanged(IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            CommitTarget(cell, AerialRaidTargetSource.Bait);
            ClearScanState();
        }

        public void NotifyBaitTargetCleared()
        {
            if (map == null)
            {
                return;
            }

            if (currentTargetSource != AerialRaidTargetSource.Bait)
            {
                return;
            }

            EnsureCommittedTargetValid(startScanOnFallback: prePhaseActive);
        }

        public void AdvanceBuildingScan()
        {
            if (map == null || !scanInProgress)
            {
                return;
            }

            if (TryGetBaitTarget(out IntVec3 baitCell))
            {
                NotifyBaitTargetChanged(baitCell);
                return;
            }

            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            int processed = 0;
            while (processed < BuildingsPerScanTick && scanIndex < buildings.Count)
            {
                Building building = buildings[scanIndex];
                scanIndex++;
                processed++;

                if (!IsValidHomeBuilding(building))
                {
                    continue;
                }

                float value;
                try
                {
                    value = building.GetStatValue(StatDefOf.MarketValue);
                }
                catch
                {
                    continue;
                }

                TryInsertScanBestBuilding(building, value);
            }

            if (scanIndex >= buildings.Count)
            {
                FinishBuildingScan();
            }
        }

        public void StopScanning()
        {
            prePhaseActive = false;
            ClearScanState();
        }

        public AerialRaidTargetSnapshot GetCommittedTargetSnapshot(int executionCount)
        {
            int count = executionCount > 0 ? executionCount : 1;
            EnsureCommittedTargetValid(startScanOnFallback: false);
            var cells = new List<IntVec3>(count);
            AerialRaidTargetSource source = currentTargetSource;

            if (currentTargetSource == AerialRaidTargetSource.Bait && TryGetBaitTarget(out IntVec3 baitCell))
            {
                for (int i = 0; i < count; i++)
                {
                    cells.Add(baitCell);
                }
                return new AerialRaidTargetSnapshot
                {
                    TargetCells = cells,
                    Source = AerialRaidTargetSource.Bait,
                    IsValid = true
                };
            }

            foreach (CommittedRaidTarget target in committedBuildingTargets)
            {
                if (cells.Count >= count)
                {
                    break;
                }

                if (IsValidCommittedBuildingTarget(target) && !ContainsCell(cells, target.Cell))
                {
                    cells.Add(target.Cell);
                    source = AerialRaidTargetSource.HighValueHomeBuilding;
                }
            }

            IntVec3 homeCenter = GetCachedHomeCenter(forceRefresh: false);
            while (cells.Count < count)
            {
                if (!TryAppendUniqueFallbackTarget(cells, homeCenter))
                {
                    IntVec3 fallback = IsValidCell(homeCenter) ? homeCenter : (map != null ? map.Center : IntVec3.Invalid);
                    if (!fallback.IsValid)
                    {
                        break;
                    }
                    cells.Add(fallback);
                    source = IsValidCell(homeCenter) ? AerialRaidTargetSource.HomeCenter : AerialRaidTargetSource.MapCenter;
                }
                else
                {
                    source = IsValidCell(homeCenter) ? AerialRaidTargetSource.HomeCenter : AerialRaidTargetSource.MapCenter;
                }
            }

            return new AerialRaidTargetSnapshot
            {
                TargetCells = cells,
                Source = source,
                IsValid = cells.Count > 0
            };
        }

        public AerialRaidTargetSource GetCurrentTargetSource()
        {
            return currentTargetSource;
        }

        // 兼容旧调用链：仅返回已提交目标快照，不触发扫描。
        public List<IntVec3> GetCandidateTargets(int count, bool excludeBaitTarget = true)
        {
            return GetCommittedTargetSnapshot(count).TargetCells;
        }

        // 兼容旧调用链：仅返回兜底规则点，不触发建筑扫描。
        public IntVec3 GetRuleBasedFallbackTarget()
        {
            IntVec3 homeCenter = GetCachedHomeCenter(forceRefresh: false);
            if (IsValidCell(homeCenter))
            {
                return homeCenter;
            }

            return map != null ? map.Center : IntVec3.Invalid;
        }

        private void StartBuildingScan()
        {
            if (map == null || currentTargetSource == AerialRaidTargetSource.Bait || scanInProgress)
            {
                return;
            }

            scanInProgress = true;
            scanIndex = 0;
            scanBestBuildings.Clear();
        }

        private void FinishBuildingScan()
        {
            scanInProgress = false;

            if (scanBestBuildings.Count > 0)
            {
                committedBuildingTargets = new List<CommittedRaidTarget>(scanBestBuildings);
                SortCommittedTargetsByValueDesc(committedBuildingTargets);
                SyncCommittedBuildingCells();
                lastCommittedBuilding = committedBuildingTargets[0].Building;
                lastCommittedBuildingTarget = committedBuildingTargets[0].Cell;
                CommitTarget(lastCommittedBuildingTarget, AerialRaidTargetSource.HighValueHomeBuilding);
                return;
            }

            if (currentTargetSource != AerialRaidTargetSource.Bait)
            {
                IntVec3 homeCenter = GetCachedHomeCenter(forceRefresh: false);
                if (IsValidCell(homeCenter))
                {
                    CommitTarget(homeCenter, AerialRaidTargetSource.HomeCenter);
                }
                else if (map != null)
                {
                    CommitTarget(map.Center, AerialRaidTargetSource.MapCenter);
                }
            }
        }

        private void CommitTarget(IntVec3 target, AerialRaidTargetSource source)
        {
            if (!IsValidCell(target))
            {
                return;
            }

            bool changed = currentCommittedTarget != target || currentTargetSource != source;
            currentCommittedTarget = target;
            currentTargetSource = source;

            if (changed)
            {
                var prePhase = map?.GetComponent<AerialRaidPrePhaseComponent>();
                prePhase?.NotifyCommittedTargetUpdated();
            }
        }

        private bool TryGetBaitTarget(out IntVec3 baitCell)
        {
            baitCell = IntVec3.Invalid;
            if (map == null)
            {
                return false;
            }

            var baitComp = map.GetComponent<AerialRaidBaitTargetComponent>();
            if (baitComp == null || !baitComp.HasValidBaitTarget())
            {
                return false;
            }

            baitCell = baitComp.GetBaitTarget();
            return IsValidCell(baitCell);
        }

        private IntVec3 GetCachedHomeCenter(bool forceRefresh)
        {
            if (map == null)
            {
                return IntVec3.Invalid;
            }

            int currentTick = Find.TickManager.TicksGame;
            bool shouldRefresh = forceRefresh
                                 || !IsValidCell(cachedHomeCenter)
                                 || lastHomeCenterUpdateTick < 0
                                 || currentTick - lastHomeCenterUpdateTick >= HomeCenterRefreshIntervalTicks;

            if (shouldRefresh)
            {
                cachedHomeCenter = ComputeHomeCenter();
                lastHomeCenterUpdateTick = currentTick;
            }

            return cachedHomeCenter;
        }

        private IntVec3 ComputeHomeCenter()
        {
            if (map == null)
            {
                return IntVec3.Invalid;
            }

            var homeArea = map.areaManager?.Home;
            if (homeArea != null)
            {
                int totalX = 0;
                int totalZ = 0;
                int count = 0;
                foreach (IntVec3 cell in homeArea.ActiveCells)
                {
                    totalX += cell.x;
                    totalZ += cell.z;
                    count++;
                }

                if (count > 0)
                {
                    IntVec3 center = new IntVec3(totalX / count, 0, totalZ / count);
                    if (center.InBounds(map))
                    {
                        return center;
                    }
                }
            }

            return map.Center;
        }

        private bool IsValidHomeBuilding(Building? building)
        {
            if (map == null || building == null)
            {
                return false;
            }

            IntVec3 pos = building.Position;
            if (!pos.IsValid || !pos.InBounds(map))
            {
                return false;
            }

            if (building.Destroyed || building.Map != map)
            {
                return false;
            }

            return map.areaManager?.Home != null && map.areaManager.Home[pos];
        }

        private bool IsValidCell(IntVec3 cell)
        {
            return map != null && cell.IsValid && cell.InBounds(map);
        }

        private bool IsValidHomeBuildingCell(IntVec3 cell)
        {
            if (!IsValidCell(cell) || map == null || map.areaManager?.Home == null || !map.areaManager.Home[cell])
            {
                return false;
            }

            Building? building = cell.GetFirstBuilding(map);
            return IsValidHomeBuilding(building);
        }

        private void ClearScanState()
        {
            scanInProgress = false;
            scanIndex = 0;
            scanBestBuildings.Clear();
        }

        private bool EnsureCommittedTargetValid(bool startScanOnFallback)
        {
            if (map == null)
            {
                return false;
            }

            if (currentTargetSource == AerialRaidTargetSource.Bait && TryGetBaitTarget(out IntVec3 baitCell))
            {
                if (currentCommittedTarget != baitCell)
                {
                    CommitTarget(baitCell, AerialRaidTargetSource.Bait);
                }
                return true;
            }

            if (currentTargetSource == AerialRaidTargetSource.Bait && !TryGetBaitTarget(out _))
            {
                currentCommittedTarget = IntVec3.Invalid;
                currentTargetSource = AerialRaidTargetSource.None;
            }

            ValidateCommittedBuildingTargets();
            if (committedBuildingTargets.Count > 0)
            {
                lastCommittedBuilding = committedBuildingTargets[0].Building;
                lastCommittedBuildingTarget = committedBuildingTargets[0].Cell;
                CommitTarget(lastCommittedBuildingTarget, AerialRaidTargetSource.HighValueHomeBuilding);
                if (startScanOnFallback && !scanInProgress)
                {
                    StartBuildingScan();
                }
                return true;
            }

            IntVec3 homeCenter = GetCachedHomeCenter(forceRefresh: false);
            if (IsValidCell(homeCenter))
            {
                CommitTarget(homeCenter, AerialRaidTargetSource.HomeCenter);
                if (startScanOnFallback && !scanInProgress)
                {
                    StartBuildingScan();
                }
                return true;
            }

            CommitTarget(map.Center, AerialRaidTargetSource.MapCenter);
            if (startScanOnFallback && !scanInProgress)
            {
                StartBuildingScan();
            }
            return true;
        }

        private void TryInsertScanBestBuilding(Building building, float value)
        {
            for (int i = 0; i < scanBestBuildings.Count; i++)
            {
                if (scanBestBuildings[i].Building == building || scanBestBuildings[i].Cell == building.Position)
                {
                    return;
                }
            }

            var candidate = new CommittedRaidTarget
            {
                Cell = building.Position,
                Building = building,
                Value = value,
                Source = AerialRaidTargetSource.HighValueHomeBuilding
            };

            if (scanBestBuildings.Count < desiredTargetCount)
            {
                scanBestBuildings.Add(candidate);
                SortCommittedTargetsByValueDesc(scanBestBuildings);
                return;
            }

            int minIndex = 0;
            float minValue = scanBestBuildings[0].Value;
            for (int i = 1; i < scanBestBuildings.Count; i++)
            {
                if (scanBestBuildings[i].Value < minValue)
                {
                    minValue = scanBestBuildings[i].Value;
                    minIndex = i;
                }
            }

            if (value > minValue)
            {
                scanBestBuildings[minIndex] = candidate;
                SortCommittedTargetsByValueDesc(scanBestBuildings);
            }
        }

        private static void SortCommittedTargetsByValueDesc(List<CommittedRaidTarget> targets)
        {
            targets.Sort((a, b) => b.Value.CompareTo(a.Value));
        }

        private void ValidateCommittedBuildingTargets()
        {
            bool removed = false;
            for (int i = committedBuildingTargets.Count - 1; i >= 0; i--)
            {
                if (!IsValidCommittedBuildingTarget(committedBuildingTargets[i]))
                {
                    committedBuildingTargets.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                SyncCommittedBuildingCells();
                if (committedBuildingTargets.Count > 0)
                {
                    lastCommittedBuilding = committedBuildingTargets[0].Building;
                    lastCommittedBuildingTarget = committedBuildingTargets[0].Cell;
                }
                else
                {
                    lastCommittedBuilding = null;
                    lastCommittedBuildingTarget = IntVec3.Invalid;
                    if (prePhaseActive && !TryGetBaitTarget(out _) && !scanInProgress)
                    {
                        StartBuildingScan();
                    }
                }
            }
        }

        private bool IsValidCommittedBuildingTarget(CommittedRaidTarget target)
        {
            if (!IsValidHomeBuildingCell(target.Cell))
            {
                return false;
            }

            Building? building = target.Building;
            if (building == null || !IsValidHomeBuilding(building))
            {
                if (map == null)
                {
                    return false;
                }
                building = target.Cell.GetFirstBuilding(map);
                return IsValidHomeBuilding(building);
            }

            return true;
        }

        private void SyncCommittedBuildingCells()
        {
            if (committedBuildingTargetCells == null)
            {
                committedBuildingTargetCells = new List<IntVec3>();
            }
            if (committedBuildingTargets == null)
            {
                committedBuildingTargets = new List<CommittedRaidTarget>();
            }

            committedBuildingTargetCells.Clear();
            for (int i = 0; i < committedBuildingTargets.Count; i++)
            {
                committedBuildingTargetCells.Add(committedBuildingTargets[i].Cell);
            }
        }

        private void RebuildCommittedTargetsFromCells()
        {
            if (committedBuildingTargets == null)
            {
                committedBuildingTargets = new List<CommittedRaidTarget>();
            }
            committedBuildingTargets.Clear();
            if (committedBuildingTargetCells == null)
            {
                committedBuildingTargetCells = new List<IntVec3>();
                return;
            }
            if (map == null)
            {
                return;
            }

            foreach (IntVec3 cell in committedBuildingTargetCells)
            {
                if (!IsValidHomeBuildingCell(cell))
                {
                    continue;
                }

                Building? building = cell.GetFirstBuilding(map);
                if (!IsValidHomeBuilding(building))
                {
                    continue;
                }

                float value = float.MinValue;
                try
                {
                    value = building.GetStatValue(StatDefOf.MarketValue);
                }
                catch
                {
                }

                committedBuildingTargets.Add(new CommittedRaidTarget
                {
                    Cell = cell,
                    Building = building,
                    Value = value,
                    Source = AerialRaidTargetSource.HighValueHomeBuilding
                });
            }

            SortCommittedTargetsByValueDesc(committedBuildingTargets);
            SyncCommittedBuildingCells();
        }

        private bool TryAppendUniqueFallbackTarget(List<IntVec3> targets, IntVec3 center)
        {
            if (map == null || !IsValidCell(center))
            {
                return false;
            }

            for (int i = 0; i < FallbackRandomTryCount; i++)
            {
                if (CellFinder.TryFindRandomCellNear(center, map, FallbackRandomRadius, c => c.InBounds(map), out IntVec3 cell))
                {
                    if (!ContainsCell(targets, cell))
                    {
                        targets.Add(cell);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsCell(List<IntVec3> cells, IntVec3 target)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == target)
                {
                    return true;
                }
            }
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref currentCommittedTarget, "currentCommittedTarget", IntVec3.Invalid);
            Scribe_Values.Look(ref currentTargetSource, "currentTargetSource", AerialRaidTargetSource.None);
            Scribe_Values.Look(ref lastCommittedBuildingTarget, "lastCommittedBuildingTarget", IntVec3.Invalid);
            Scribe_Values.Look(ref cachedHomeCenter, "cachedHomeCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref lastHomeCenterUpdateTick, "lastHomeCenterUpdateTick", -1);
            Scribe_Values.Look(ref prePhaseActive, "prePhaseActive", false);
            Scribe_Values.Look(ref desiredTargetCount, "desiredTargetCount", 1);
            Scribe_Collections.Look(ref committedBuildingTargetCells, "committedBuildingTargetCells", LookMode.Value);
            committedBuildingTargetCells ??= new List<IntVec3>();
            committedBuildingTargets ??= new List<CommittedRaidTarget>();
            scanBestBuildings ??= new List<CommittedRaidTarget>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ClearScanState();
                RebuildCommittedTargetsFromCells();
                if (committedBuildingTargets.Count > 0)
                {
                    lastCommittedBuilding = committedBuildingTargets[0].Building;
                    lastCommittedBuildingTarget = committedBuildingTargets[0].Cell;
                }
                else if (IsValidHomeBuildingCell(lastCommittedBuildingTarget))
                {
                    lastCommittedBuilding = lastCommittedBuildingTarget.GetFirstBuilding(map);
                }
                else
                {
                    lastCommittedBuilding = null;
                }
            }
        }
    }
}
