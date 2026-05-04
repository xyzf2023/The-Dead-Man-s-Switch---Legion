using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：地面污染区域（PolluteArea）
    /// 在目标点为中心、指定半径为范围的实心圆内一次性生成生物科技 DLC 的地面污染（类似控制台 Pollute(rect)）。
    /// 仅生成污染，不移除已有污染。需要启用生物科技 DLC。
    /// </summary>
    public class CompProperties_AerialSupportEffect_PolluteArea : CompProperties
    {
        /// <summary>污染区域半径（格），实心圆</summary>
        public float maxRadius = 15f;

        /// <summary>每格添加的污染量（传给 PollutionUtility.GrowPollutionAt 的 amount，int）</summary>
        public int pollutionAmountPerCell = 1;

        public CompProperties_AerialSupportEffect_PolluteArea()
        {
            compClass = typeof(CompAerialSupportEffect_PolluteArea);
        }
    }

    /// <summary>
    /// 空中支援效果组件：地面污染区域。到达时一次性在整片目标区域内生成污染，无扩散、无每 tick 消耗。
    /// </summary>
    public class CompAerialSupportEffect_PolluteArea : ThingComp
    {
        public CompProperties_AerialSupportEffect_PolluteArea Props => (CompProperties_AerialSupportEffect_PolluteArea)props;

        /// <summary>
        /// 执行效果（静态，供渲染器反射调用）：在目标格为中心、maxRadius 为半径的实心圆内一次性生成污染。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_PolluteArea props)
        {
            if (map == null || props == null)
                return;
            if (!ModsConfig.BiotechActive || map.pollutionGrid == null)
                return;

            float maxR = props.maxRadius > 0f ? props.maxRadius : 15f;
            int amount = props.pollutionAmountPerCell > 0 ? props.pollutionAmountPerCell : 1;

            int rCeil = Mathf.CeilToInt(maxR);
            for (int dx = -rCeil; dx <= rCeil; dx++)
            {
                for (int dz = -rCeil; dz <= rCeil; dz++)
                {
                    IntVec3 cell = targetPos + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(map)) continue;
                    if ((cell - targetPos).LengthHorizontal > maxR + 0.01f) continue;

                    try
                    {
                        PollutionUtility.GrowPollutionAt(cell, map, amount, null, false);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[DMS_Legion] PolluteArea: GrowPollutionAt 异常: {ex.Message}");
                    }
                }
            }

            map.pollutionGrid.Drawer.SetDirty();
        }
    }
}
