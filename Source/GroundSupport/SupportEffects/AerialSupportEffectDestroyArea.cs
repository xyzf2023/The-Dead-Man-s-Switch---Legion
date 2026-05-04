using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：范围内销毁
    /// 触发后对指定半径内所有实体执行销毁效果（等同控制台 Destroy 指令，即从地图移除）。
    /// </summary>
    public class CompProperties_AerialSupportEffect_DestroyArea : CompProperties
    {
        /// <summary>销毁半径（格），仅影响此范围内的实体</summary>
        public float radius = 15f;

        public CompProperties_AerialSupportEffect_DestroyArea()
        {
            compClass = typeof(CompAerialSupportEffect_DestroyArea);
        }
    }

    /// <summary>
    /// 空中支援效果组件：范围内销毁（供渲染器反射调用）
    /// </summary>
    public class CompAerialSupportEffect_DestroyArea : ThingComp
    {
        public CompProperties_AerialSupportEffect_DestroyArea Props => (CompProperties_AerialSupportEffect_DestroyArea)props;

        /// <summary>
        /// 执行效果（静态，供渲染器反射调用）：对目标点半径内所有实体执行 Destroy(Vanish)。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_DestroyArea props)
        {
            if (map == null || props == null)
                return;

            float r = props.radius > 0f ? props.radius : 15f;

            List<Thing> toDestroy = new List<Thing>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(targetPos, r, true))
            {
                if (!cell.InBounds(map))
                    continue;
                foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
                {
                    if (thing != null && !thing.Destroyed)
                        toDestroy.Add(thing);
                }
            }

            // 与控制台 Destroy 一致：允许销毁“不可销毁”物（如蒸汽喷泉），执行后恢复原状
            bool wasAllowed = Thing.allowDestroyNonDestroyable;
            Thing.allowDestroyNonDestroyable = true;
            try
            {
                foreach (Thing thing in toDestroy)
                {
                    if (thing != null && !thing.Destroyed && thing.Spawned)
                        thing.Destroy(DestroyMode.Vanish);
                }
            }
            finally
            {
                Thing.allowDestroyNonDestroyable = wasAllowed;
            }
        }
    }
}
