// ============================================================================
// 文件：ClusterData.cs
// 说明：集群数据
// 功能：独立的集群数据管理，包含位置信息和储存数据，不依赖WorldObject
// ============================================================================

using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 集群数据
    /// 独立的集群数据管理，包含位置信息和储存数据
    /// 不依赖WorldObject，支持集群移动
    /// </summary>
    public class ClusterData : IExposable
    {
        /// <summary>
        /// 稳定的集群唯一标识符（不会因WorldObject销毁重建而改变）
        /// </summary>
        public string clusterId = string.Empty;
        
        /// <summary>
        /// 当前所在的地图瓦片坐标（-1表示未设置位置）
        /// </summary>
        public int tile = -1;
        
        /// <summary>
        /// 集群的物资储存数据
        /// </summary>
        public ClusterStorage storage = new ClusterStorage();

        /// <summary>
        /// 集群名称（可选，用于显示）
        /// </summary>
        public string clusterName = string.Empty;

        /// <summary>
        /// 初始化集群数据
        /// </summary>
        public void Init()
        {
            if (storage != null)
            {
                storage.clusterId = clusterId;
                storage.Init();
            }
        }

        /// <summary>
        /// 设置集群位置
        /// </summary>
        public void SetTile(int newTile)
        {
            tile = newTile;
        }

        /// <summary>
        /// 获取当前位置的WorldObject（如果存在）
        /// </summary>
        public WorldObject? GetWorldObject()
        {
            if (tile < 0) return null;
            var objects = Find.WorldObjects.ObjectsAt(tile);
            if (objects == null) return null;
            return objects.FirstOrDefault(wo => wo.def.defName == "DMSL_IndustrialHubCluster");
        }

        /// <summary>
        /// 实现IExposable接口（确保存档时保存数据）
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref clusterId, "clusterId", string.Empty);
            Scribe_Values.Look(ref tile, "tile", -1);
            Scribe_Values.Look(ref clusterName, "clusterName", string.Empty);
            Scribe_Deep.Look(ref storage, "storage");
            
            // 读档后需要重新初始化
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (storage != null)
                {
                    storage.clusterId = clusterId;
                    storage.Init();
                }
            }
        }
    }
}

