using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：消防泡沫波纹扩散（FirefoamRipple）
    /// 以中心格为起点向外扩散，逐格灭火并可选留下消防泡沫污渍；无可见波纹绘制。
    /// </summary>
    public class CompProperties_AerialSupportEffect_FirefoamRipple : CompProperties
    {
        /// <summary>最大扩散半径（格）</summary>
        public float maxRadius = 15f;

        /// <summary>扩散速度（格/tick）</summary>
        public float expandSpeedCellsPerTick = 0.5f;

        /// <summary>灭火强度（Extinguish 伤害量）</summary>
        public float extinguishDamage = DMSL_FirefoamUtility.DefaultExtinguishDamage;

        /// <summary>是否在扫过的格子上尝试生成消防泡沫污渍</summary>
        public bool placeFilth = true;

        public CompProperties_AerialSupportEffect_FirefoamRipple()
        {
            compClass = typeof(CompAerialSupportEffect_FirefoamRipple);
        }
    }

    /// <summary>
    /// 空中支援效果组件：消防泡沫波纹（仅负责在到达时启动序列，实际扩散由 FirefoamRippleController 驱动）
    /// </summary>
    public class CompAerialSupportEffect_FirefoamRipple : ThingComp
    {
        public CompProperties_AerialSupportEffect_FirefoamRipple Props => (CompProperties_AerialSupportEffect_FirefoamRipple)props;

        /// <summary>
        /// 执行效果（静态，供渲染器反射调用）：在目标格启动消防泡沫波纹扩散序列。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_FirefoamRipple props)
        {
            if (map == null || props == null)
                return;

            FirefoamRippleController? controller = map.GetComponent<FirefoamRippleController>();
            if (controller == null)
            {
                Log.Error("[DMS_Legion] FirefoamRipple: FirefoamRippleController not found on map.");
                return;
            }

            controller.StartFirefoamRippleSequence(targetPos, props);
        }
    }

    /// <summary>
    /// 消防泡沫波纹专用 MapComponent：每 tick 推进序列。
    /// </summary>
    public class FirefoamRippleController : MapComponent
    {
        private List<FirefoamRippleSequence> activeFirefoamRippleSequences = new List<FirefoamRippleSequence>();

        public FirefoamRippleController(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref activeFirefoamRippleSequences, "activeFirefoamRippleSequences", LookMode.Deep, Array.Empty<object>());
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                activeFirefoamRippleSequences?.RemoveAll(seq => seq == null);
        }

        public override void MapComponentTick()
        {
            if (activeFirefoamRippleSequences != null)
                activeFirefoamRippleSequences.RemoveAll(seq => seq.Tick(this));
        }

        public void StartFirefoamRippleSequence(IntVec3 center, CompProperties_AerialSupportEffect_FirefoamRipple props)
        {
            if (map == null || props == null)
                return;
            if (activeFirefoamRippleSequences == null)
                activeFirefoamRippleSequences = new List<FirefoamRippleSequence>();
            activeFirefoamRippleSequences.Add(new FirefoamRippleSequence(center, props, map));
        }
    }

    /// <summary>
    /// 消防泡沫波纹扩散序列：按波前扫过区间逐格灭火并可选留渍。
    /// </summary>
    public class FirefoamRippleSequence : IExposable
    {
        private const float CellHitPadding = 0.75f;

        private IntVec3 center;
        private float currentRadius;
        private float previousRadius;
        private HashSet<int> processedCellIndices = new HashSet<int>();
        private CompProperties_AerialSupportEffect_FirefoamRipple? props;
        private Map? map;

        public FirefoamRippleSequence() { }

        public FirefoamRippleSequence(IntVec3 center, CompProperties_AerialSupportEffect_FirefoamRipple props, Map map)
        {
            this.center = center;
            this.props = props;
            this.map = map;
            this.currentRadius = 0f;
            this.previousRadius = 0f;
            this.processedCellIndices = new HashSet<int>();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref currentRadius, "currentRadius", 0f);
            Scribe_Values.Look(ref previousRadius, "previousRadius", 0f);
            List<int>? processedCellIndicesList = null;
            if (Scribe.mode == LoadSaveMode.Saving && processedCellIndices != null)
                processedCellIndicesList = new List<int>(processedCellIndices);
            Scribe_Collections.Look(ref processedCellIndicesList, "processedCellIndices", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                processedCellIndices = processedCellIndicesList != null
                    ? new HashSet<int>(processedCellIndicesList)
                    : new HashSet<int>();
            Scribe_Deep.Look(ref props, "props");
            Scribe_References.Look(ref map, "map");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && processedCellIndices == null)
                processedCellIndices = new HashSet<int>();
        }

        private void ApplyRippleEffectsForSweptCells(
            Map mapVal,
            float fromRadius,
            float toRadius,
            CompProperties_AerialSupportEffect_FirefoamRipple rippleProps)
        {
            float extinguishDamage = rippleProps.extinguishDamage > 0f
                ? rippleProps.extinguishDamage
                : DMSL_FirefoamUtility.DefaultExtinguishDamage;
            bool placeFilth = rippleProps.placeFilth;

            int rCeil = Mathf.CeilToInt(toRadius + CellHitPadding);
            for (int dx = -rCeil; dx <= rCeil; dx++)
            {
                for (int dz = -rCeil; dz <= rCeil; dz++)
                {
                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(mapVal))
                        continue;

                    float dist = (cell - center).LengthHorizontal;
                    if (!(fromRadius - CellHitPadding < dist && dist <= toRadius + CellHitPadding))
                        continue;

                    int cellIndex = mapVal.cellIndices.CellToIndex(cell);
                    if (processedCellIndices.Contains(cellIndex))
                        continue;
                    processedCellIndices.Add(cellIndex);

                    DMSL_FirefoamUtility.ExtinguishFiresAtCell(cell, mapVal, extinguishDamage);
                    DMSL_FirefoamUtility.TryPlaceFirefoamFilth(cell, mapVal, placeFilth);
                }
            }
        }

        /// <summary>
        /// 每 tick 推进波前并处理扫过的格子。返回 true 表示序列结束。
        /// </summary>
        public bool Tick(FirefoamRippleController controller)
        {
            if (map == null)
            {
                map = Find.CurrentMap;
                if (map == null)
                    return true;
            }

            if (props == null)
                return true;

            Map mapVal = map;
            float maxR = props.maxRadius > 0f ? props.maxRadius : 15f;
            float speed = props.expandSpeedCellsPerTick > 0f ? props.expandSpeedCellsPerTick : 0.5f;

            previousRadius = currentRadius;
            currentRadius = Mathf.Min(currentRadius + speed, maxR);

            ApplyRippleEffectsForSweptCells(mapVal, previousRadius, currentRadius, props);

            if (currentRadius >= maxR)
                return true;

            return false;
        }
    }
}
