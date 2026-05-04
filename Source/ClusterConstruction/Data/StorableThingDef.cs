// ============================================================================
// 文件：StorableThingDef.cs
// 说明：可储存物资的配置定义（映射XML）
// 功能：用于定义哪些ThingDef可以作为物资储存，以及储存上限
// ============================================================================

using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 可储存物资的配置定义（映射XML）
    /// 用于定义哪些ThingDef可以作为物资储存，以及储存上限
    /// </summary>
    public class IndustrialHubClusterStorage : Def
    {
        /// <summary>
        /// 对应的ThingDef的defName（如"Steel"）
        /// </summary>
        public string thingDefName = string.Empty;
        
        /// <summary>
        /// 该物资的储存上限
        /// </summary>
        public int maxStorage;

        /// <summary>
        /// 缓存对应的ThingDef（避免重复查询）
        /// </summary>
        public ThingDef ThingDef => DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
    }
}

