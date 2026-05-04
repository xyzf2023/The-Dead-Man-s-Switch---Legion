// ============================================================================
// 为 WorkGiver ShouldSkip 提供 O(1)“当前地图是否存在可钻深矿”查询，避免无矿时全图扫描。
// 由 DeepResourceGrid_SetAt_Patch 在 SetAt 时维护 NonZeroCellCount；首次使用时按格初始化。
// 若曾因读档/初始化顺序导致计数为 0 而实际有矿，则 HasAnyDeepResource 会在计数为 0 时重算一次（仅一次）。
// ============================================================================

using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 按 Map 维护 deepResourceGrid 非零格数量，用于 O(1) HasAnyDeepResource 查询。
    /// </summary>
    public class DeepResourceGridTrackerComponent : MapComponent
    {
        private int _nonZeroCellCount;

        /// <summary>计数为 0 时已做过一次重算（防止误判“无矿”导致驳机不工作）。</summary>
        private bool _hasReinitedWhenZero;

        public DeepResourceGridTrackerComponent(Map map) : base(map)
        {
        }

        /// <summary>当前地图上 deepResourceGrid 中数量大于 0 的格子数。</summary>
        public int NonZeroCellCount
        {
            get => _nonZeroCellCount;
            set => _nonZeroCellCount = value < 0 ? 0 : value;
        }

        /// <summary>是否存在至少一格深矿（O(1)），仅供本类静态方法使用。</summary>
        private bool HasAnyResource => _nonZeroCellCount > 0;

        /// <summary>
        /// 获取或创建该 Map 的追踪组件；若不存在则添加并做一次全图扫描初始化（仅首次）。
        /// </summary>
        public static DeepResourceGridTrackerComponent? GetOrCreate(Map map)
        {
            if (map == null)
                return null;
            var comp = map.GetComponent<DeepResourceGridTrackerComponent>();
            if (comp == null)
            {
                comp = new DeepResourceGridTrackerComponent(map);
                comp.InitFromGrid();
                map.components.Add(comp);
            }
            return comp;
        }

        /// <summary>当前地图是否存在任意可钻深矿（O(1)，无矿时避免全图扫描）。计数为 0 时会重算一次以防读档/初始化顺序导致误判。</summary>
        public static bool HasAnyDeepResource(Map map)
        {
            var comp = GetOrCreate(map);
            if (comp == null)
                return false;
            if (comp.HasAnyResource)
                return true;
            if (!comp._hasReinitedWhenZero)
            {
                comp._hasReinitedWhenZero = true;
                comp.InitFromGrid();
            }
            return comp.HasAnyResource;
        }

        /// <summary>遍历当前地图 deepResourceGrid 一次，统计非零格数量。组件首次创建或“计数为 0 重算一次”时调用。</summary>
        public void InitFromGrid()
        {
            _nonZeroCellCount = 0;
            if (map?.deepResourceGrid == null)
                return;
            CellIndices ci = map.cellIndices;
            for (int i = 0; i < ci.NumGridCells; i++)
            {
                if (map.deepResourceGrid.CountAt(ci.IndexToCell(i)) > 0)
                    _nonZeroCellCount++;
            }
        }
    }
}
