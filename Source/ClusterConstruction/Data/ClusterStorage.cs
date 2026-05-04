// ============================================================================
// 文件：ClusterStorage.cs
// 说明：集群储存数据
// 功能：记录一个集群的物资储存状态，与集群绑定，支持存档/读档
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 集群储存数据
    /// 记录一个集群的物资储存状态，与集群绑定
    /// </summary>
    public class ClusterStorage : IExposable
    {
        /// <summary>
        /// 集群唯一标识（WorldObject的ThingID）
        /// </summary>
        public string clusterId = string.Empty;
        
        /// <summary>
        /// 物资存储数据：key=thingDefName，value=当前数量
        /// </summary>
        private Dictionary<string, int> storedThings = new Dictionary<string, int>();

        /// <summary>
        /// 初始化：从XML配置的可储存物资列表中加载，初始数量为0
        /// </summary>
        public void Init()
        {
            foreach (var storableDef in DefDatabase<IndustrialHubClusterStorage>.AllDefs)
            {
                if (!storedThings.ContainsKey(storableDef.thingDefName))
                {
                    storedThings[storableDef.thingDefName] = 0;
                }
            }
        }

        /// <summary>
        /// 获取当前数量（带验证）
        /// </summary>
        public int GetAmount(string thingDefName)
        {
            return storedThings.TryGetValue(thingDefName, out int amount) ? amount : 0;
        }

        /// <summary>
        /// 获取当前数量（通过ThingDef）
        /// </summary>
        public int GetAmount(ThingDef thingDef)
        {
            return thingDef != null ? GetAmount(thingDef.defName) : 0;
        }

        /// <summary>
        /// 修改数量（确保不超过上限）
        /// </summary>
        /// <param name="thingDefName">物资的defName</param>
        /// <param name="delta">变化量（正数为增加，负数为减少）</param>
        /// <returns>实际修改的数量（可能因上限限制而小于delta）</returns>
        public int ModifyAmount(string thingDefName, int delta)
        {
            var storableDef = DefDatabase<IndustrialHubClusterStorage>.AllDefs
                .FirstOrDefault(d => d.thingDefName == thingDefName);
            if (storableDef == null) return 0; // 不是可储存物资，直接忽略

            int current = GetAmount(thingDefName);
            int newAmount = Mathf.Clamp(current + delta, 0, storableDef.maxStorage);
            int actualDelta = newAmount - current;
            storedThings[thingDefName] = newAmount;
            return actualDelta;
        }

        /// <summary>
        /// 修改数量（通过ThingDef）
        /// </summary>
        public int ModifyAmount(ThingDef thingDef, int delta)
        {
            return thingDef != null ? ModifyAmount(thingDef.defName, delta) : 0;
        }

        /// <summary>
        /// 获取储存上限
        /// </summary>
        public int GetMaxAmount(string thingDefName)
        {
            var storableDef = DefDatabase<IndustrialHubClusterStorage>.AllDefs
                .FirstOrDefault(d => d.thingDefName == thingDefName);
            return storableDef?.maxStorage ?? 0;
        }

        /// <summary>
        /// 获取储存上限（通过ThingDef）
        /// </summary>
        public int GetMaxAmount(ThingDef thingDef)
        {
            return thingDef != null ? GetMaxAmount(thingDef.defName) : 0;
        }

        /// <summary>
        /// 检查是否可以添加指定数量的物资
        /// </summary>
        public bool CanAdd(string thingDefName, int amount)
        {
            int current = GetAmount(thingDefName);
            int max = GetMaxAmount(thingDefName);
            return current + amount <= max;
        }

        /// <summary>
        /// 检查是否可以添加指定数量的物资（通过ThingDef）
        /// </summary>
        public bool CanAdd(ThingDef thingDef, int amount)
        {
            return thingDef != null && CanAdd(thingDef.defName, amount);
        }

        /// <summary>
        /// 实现IExposable接口（确保存档时保存数据）
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref clusterId, "clusterId", string.Empty);
            Scribe_Collections.Look(ref storedThings, "storedThings", LookMode.Value, LookMode.Value);
            
            // 读档后需要重新初始化（确保新增的配置项被包含）
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Init();
            }
        }
    }
}

