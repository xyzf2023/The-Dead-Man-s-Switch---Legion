// ============================================================================
// 文件：ClusterResourceStorageManager.cs
// 说明：集群资源储存管理器
// 功能：通过GameComponent管理所有集群的数据，不依赖WorldObject
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 集群资源储存管理器
    /// 通过GameComponent管理所有集群的数据，不依赖WorldObject
    /// 支持集群移动，WorldObject仅作为地点占位符
    /// </summary>
    public class ClusterResourceStorageManager : GameComponent
    {
        public static ClusterResourceStorageManager? Instance { get; private set; }

        /// <summary>
        /// 所有集群的数据：key=稳定的clusterId，value=ClusterData
        /// </summary>
        private Dictionary<string, ClusterData> allClusters = new Dictionary<string, ClusterData>();

        /// <summary>
        /// 位置到集群的映射：key=tile坐标，value=clusterId（用于快速查找）
        /// </summary>
        private Dictionary<int, string> tileToClusterId = new Dictionary<int, string>();

        public ClusterResourceStorageManager(Game game) : base()
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Instance = this;
            
            // 初始化时，同步所有集群的位置映射
            SyncTileMapping();
        }

        /// <summary>
        /// 同步位置映射（从集群数据更新tileToClusterId）
        /// </summary>
        private void SyncTileMapping()
        {
            tileToClusterId.Clear();
            foreach (var cluster in allClusters.Values)
            {
                if (cluster.tile >= 0 && !string.IsNullOrEmpty(cluster.clusterId))
                {
                    tileToClusterId[cluster.tile] = cluster.clusterId;
                }
            }
        }

        /// <summary>
        /// 创建新集群（在指定位置）
        /// </summary>
        public ClusterData CreateCluster(int tile, string? clusterName = null)
        {
            // 检查该位置是否已有集群
            if (tileToClusterId.TryGetValue(tile, out var existingId))
            {
                return allClusters[existingId];
            }

            // 生成新的集群ID
            string clusterId = GenerateClusterId();
            
            var clusterData = new ClusterData
            {
                clusterId = clusterId,
                tile = tile,
                clusterName = clusterName ?? $"集群_{clusterId.Substring(0, 8)}"
            };
            clusterData.Init();
            
            allClusters[clusterId] = clusterData;
            tileToClusterId[tile] = clusterId;
            
            return clusterData;
        }

        /// <summary>
        /// 生成唯一的集群ID
        /// </summary>
        private string GenerateClusterId()
        {
            return $"Cluster_{GenTicks.TicksGame}_{Rand.Int}";
        }

        /// <summary>
        /// 通过位置获取集群数据（不存在则创建）
        /// </summary>
        public ClusterData GetOrCreateClusterAtTile(int tile)
        {
            if (tile < 0) return null!;
            
            // 先尝试通过位置查找
            if (tileToClusterId.TryGetValue(tile, out var clusterId))
            {
                if (allClusters.TryGetValue(clusterId, out var cluster))
                {
                    return cluster;
                }
            }

            // 如果不存在，创建新集群
            return CreateCluster(tile);
        }

        /// <summary>
        /// 通过位置获取集群数据（不创建）
        /// </summary>
        public ClusterData? GetClusterAtTile(int tile)
        {
            if (tile < 0) return null;
            
            if (tileToClusterId.TryGetValue(tile, out var clusterId))
            {
                return allClusters.TryGetValue(clusterId, out var cluster) ? cluster : null;
            }
            
            return null;
        }

        /// <summary>
        /// 通过集群ID获取集群数据
        /// </summary>
        public ClusterData? GetClusterById(string clusterId)
        {
            if (string.IsNullOrEmpty(clusterId)) return null;
            return allClusters.TryGetValue(clusterId, out var cluster) ? cluster : null;
        }

        /// <summary>
        /// 通过WorldObject获取集群数据（如果WorldObject存在）
        /// </summary>
        public ClusterData? GetClusterByWorldObject(WorldObject? worldObject)
        {
            if (worldObject == null) return null;
            return GetClusterAtTile(worldObject.Tile);
        }

        /// <summary>
        /// 移动集群到新位置
        /// </summary>
        public void MoveCluster(string clusterId, int newTile)
        {
            if (!allClusters.TryGetValue(clusterId, out var cluster)) return;
            
            // 移除旧位置的映射
            if (cluster.tile >= 0)
            {
                tileToClusterId.Remove(cluster.tile);
            }
            
            // 更新位置
            cluster.SetTile(newTile);
            
            // 添加新位置的映射
            if (newTile >= 0)
            {
                tileToClusterId[newTile] = clusterId;
            }
        }

        /// <summary>
        /// 移除集群（当集群被完全销毁时）
        /// </summary>
        public void RemoveCluster(string clusterId)
        {
            if (!allClusters.TryGetValue(clusterId, out var cluster)) return;
            
            // 移除位置映射
            if (cluster.tile >= 0)
            {
                tileToClusterId.Remove(cluster.tile);
            }
            
            // 移除集群数据
            allClusters.Remove(clusterId);
        }

        /// <summary>
        /// 获取所有集群数据
        /// </summary>
        public IEnumerable<ClusterData> GetAllClusters()
        {
            return allClusters.Values;
        }

        /// <summary>
        /// 获取所有集群的储存数据
        /// </summary>
        public IEnumerable<ClusterStorage> GetAllClusterStorages()
        {
            return allClusters.Values.Select(c => c.storage).Where(s => s != null);
        }

        /// <summary>
        /// 存档/读档
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref allClusters, "allClusters", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref tileToClusterId, "tileToClusterId", LookMode.Value, LookMode.Value);
            
            // 读档后需要重新初始化所有集群数据
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                foreach (var cluster in allClusters.Values)
                {
                    cluster.Init();
                }
                // 同步位置映射
                SyncTileMapping();
            }
        }
    }
}

