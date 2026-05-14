using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：核冲击波环状扩散（NuclearShockwave）
    /// 以中心格为起点绘制向外扩散的环状冲击波，与 EMP 波纹相同的 XML 接口；接触冲击波的实体受到指定伤害类型伤害，每格使用该伤害类型的爆炸格 Fleck；冲击波不被建筑阻挡，经过建筑时对建筑造成伤害并绘制爆炸特效。
    /// </summary>
    public class CompProperties_AerialSupportEffect_NuclearShockwave : CompProperties
    {
        /// <summary>最大扩散半径（格）</summary>
        public float maxRadius = 15f;

        /// <summary>环带宽度（格），默认 3</summary>
        public int ringThicknessCells = 3;

        /// <summary>扩散速度（格/tick）</summary>
        public float expandSpeedCellsPerTick = 0.5f;

        /// <summary>冲击波造成的伤害量（单次命中时传入 DamageInfo 的 amount）</summary>
        public int damageAmount = 1000;

        /// <summary>伤害类型 defName，默认 DMSL_Damage_BlastWave；用于伤害与每格爆炸 Fleck</summary>
        public string damageDefDefName = "DMSL_Damage_BlastWave";

        /// <summary>伤害冷却时间（tick）：同一实体在此时间内只受一次伤害，超过后若再次被波扫到可再次造成伤害；0 表示整次序列内仅受一次。</summary>
        public int damageCooldownTicks = 120;

        public CompProperties_AerialSupportEffect_NuclearShockwave()
        {
            compClass = typeof(CompAerialSupportEffect_NuclearShockwave);
        }
    }

    /// <summary>
    /// 空中支援效果组件：核冲击波（仅负责在到达时启动序列，实际扩散由 NuclearShockwaveController 驱动）
    /// </summary>
    public class CompAerialSupportEffect_NuclearShockwave : ThingComp
    {
        public CompProperties_AerialSupportEffect_NuclearShockwave Props => (CompProperties_AerialSupportEffect_NuclearShockwave)props;

        /// <summary>
        /// 执行效果（静态，供渲染器反射调用）：在目标格启动核冲击波扩散序列。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_NuclearShockwave props)
        {
            if (map == null || props == null)
                return;

            NuclearShockwaveController controller = map.GetComponent<NuclearShockwaveController>();
            if (controller == null)
            {
                controller = new NuclearShockwaveController(map);
                map.components.Add(controller);
            }
            controller.StartNuclearShockwaveSequence(targetPos, props);
        }
    }

    /// <summary>
    /// 核冲击波专用 MapComponent：持有活跃冲击波序列，每 tick 推进环带并施加伤害与特效。
    /// </summary>
    public class NuclearShockwaveController : MapComponent
    {
        private List<NuclearShockwaveSequence> activeSequences = new List<NuclearShockwaveSequence>();

        public NuclearShockwaveController(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref activeSequences, "activeNuclearShockwaveSequences", LookMode.Deep, Array.Empty<object>());
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                activeSequences?.RemoveAll(seq => seq == null);
        }

        public override void MapComponentTick()
        {
            if (activeSequences != null)
                activeSequences!.RemoveAll(seq => seq.Tick(this));
        }

        public void StartNuclearShockwaveSequence(IntVec3 center, CompProperties_AerialSupportEffect_NuclearShockwave props)
        {
            if (activeSequences == null)
                activeSequences = new List<NuclearShockwaveSequence>();
            activeSequences.Add(new NuclearShockwaveSequence(center, props, map));
        }
    }

    /// <summary>
    /// 核冲击波扩散序列：真实波前半径 <see cref="currentRadius"/> 每 tick 推进；伤害层按扫过径向带处理格子（与视觉厚环解耦），视觉层单独绘制 explosionCellFleck；
    /// damageCooldownTicks ≤ 0 时同一实体整次序列仅受伤一次，大于 0 时按冷却可再次受伤；冲击波不因建筑阻挡而停止。
    /// </summary>
    public class NuclearShockwaveSequence : IExposable
    {
        private const float CellHitPadding = 0.75f;

        private IntVec3 center;
        private float currentRadius;
        /// <summary>本 tick 推进前保存的真实波前半径，与推进后的 currentRadius 共同定义伤害层扫过区间。</summary>
        private float previousRadius;
        /// <summary>本次序列中已对格子施加过实际伤害的地图格索引（padding 与厚环视觉可能造成重复覆盖，用于避免重复处理）。</summary>
        private HashSet<int> processedCellIndices = new HashSet<int>();
        /// <summary>实体 thingIDNumber -> 上次受到本序列伤害的 tick。仅记录实际可受伤且已纳入伤害的实体。</summary>
        private Dictionary<int, int> thingIdToLastHitTick = new Dictionary<int, int>();
        private CompProperties_AerialSupportEffect_NuclearShockwave? props = null;
        private Map? map = null;

        /// <summary>仅用于视觉层枚举厚环带临时格子，勿与伤害层混用。</summary>
        private static readonly List<IntVec3> ringCellsBuffer = new List<IntVec3>();
        private static readonly List<Thing> toDamageBuffer = new List<Thing>();

        public NuclearShockwaveSequence() { }

        public NuclearShockwaveSequence(IntVec3 center, CompProperties_AerialSupportEffect_NuclearShockwave props, Map map)
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
            List<int>? keysList = null;
            List<int>? valuesList = null;
            if (Scribe.mode == LoadSaveMode.Saving && thingIdToLastHitTick != null)
            {
                keysList = new List<int>(thingIdToLastHitTick.Keys);
                valuesList = new List<int>(thingIdToLastHitTick.Values);
            }
            Scribe_Collections.Look(ref keysList, "thingIdToLastHitTickKeys", LookMode.Value);
            Scribe_Collections.Look(ref valuesList, "thingIdToLastHitTickValues", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && keysList != null && valuesList != null)
            {
                thingIdToLastHitTick = new Dictionary<int, int>();
                int n = Math.Min(keysList.Count, valuesList.Count);
                for (int i = 0; i < n; i++)
                    thingIdToLastHitTick[keysList[i]] = valuesList[i];
            }
            Scribe_Deep.Look(ref props, "props");
            Scribe_References.Look(ref map, "map");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (thingIdToLastHitTick == null)
                    thingIdToLastHitTick = new Dictionary<int, int>();
                if (processedCellIndices == null)
                    processedCellIndices = new HashSet<int>();
            }
        }

        private void ApplyShockwaveDamageForSweptCells(
            Map mapVal,
            float fromRadius,
            float toRadius,
            CompProperties_AerialSupportEffect_NuclearShockwave props,
            int now,
            DamageDef damageDef,
            int damageAmountVal)
        {
            int cooldownTicks = props.damageCooldownTicks;
            int rCeil = Mathf.CeilToInt(toRadius + CellHitPadding);

            for (int dx = -rCeil; dx <= rCeil; dx++)
            {
                for (int dz = -rCeil; dz <= rCeil; dz++)
                {
                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(mapVal)) continue;
                    float dist = (cell - center).LengthHorizontal;
                    if (!(fromRadius - CellHitPadding < dist && dist <= toRadius + CellHitPadding))
                        continue;
                    int cellIndex = mapVal.cellIndices.CellToIndex(cell);
                    if (processedCellIndices.Contains(cellIndex))
                        continue;
                    processedCellIndices.Add(cellIndex);

                    toDamageBuffer.Clear();
                    foreach (Thing thing in mapVal.thingGrid.ThingsListAt(cell))
                    {
                        if (thing == null || thing.Destroyed)
                            continue;
                        if (!(thing is Pawn || thing.def?.useHitPoints == true))
                            continue;

                        int id = thing.thingIDNumber;
                        if (thingIdToLastHitTick.TryGetValue(id, out int lastTick))
                        {
                            if (cooldownTicks <= 0)
                                continue;
                            if ((now - lastTick) < cooldownTicks)
                                continue;
                        }

                        thingIdToLastHitTick[id] = now;
                        toDamageBuffer.Add(thing);
                    }

                    DamageInfo dinfo = new DamageInfo(
                        damageDef,
                        damageAmountVal,
                        -1f,
                        -1f,
                        null,
                        null,
                        null,
                        DamageInfo.SourceCategory.ThingOrUnknown,
                        null,
                        true,
                        true,
                        QualityCategory.Normal,
                        true,
                        false);

                    for (int j = 0; j < toDamageBuffer.Count; j++)
                    {
                        Thing thing = toDamageBuffer[j];
                        if (thing != null && !thing.Destroyed && thing.Spawned)
                        {
                            try { thing.TakeDamage(dinfo); }
                            catch { }
                        }
                    }
                }
            }
        }

        private void SpawnShockwaveVisuals(Map mapVal, float visualRadius, int ringThickness, FleckDef? cellFleck)
        {
            float inner = Mathf.Max(0f, visualRadius - ringThickness);
            ringCellsBuffer.Clear();
            int rCeil = Mathf.CeilToInt(visualRadius + ringThickness);
            for (int dx = -rCeil; dx <= rCeil; dx++)
            {
                for (int dz = -rCeil; dz <= rCeil; dz++)
                {
                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(mapVal)) continue;
                    float dist = (cell - center).LengthHorizontal;
                    if (dist < inner - 0.01f || dist > visualRadius + 0.01f)
                        continue;
                    ringCellsBuffer.Add(cell);
                }
            }

            int visualCounter = 0;
            for (int i = 0; i < ringCellsBuffer.Count; i++)
            {
                IntVec3 cell = ringCellsBuffer[i];
                if ((visualCounter++ % 4 == 0) && cellFleck != null)
                {
                    try
                    {
                        FleckMaker.Static(cell.ToVector3Shifted(), mapVal, cellFleck, Rand.Range(0.8f, 1.4f));
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 每 tick 推进波前、施加伤害并绘制视觉。返回 true 表示序列结束（波前已达 maxRadius）。
        /// </summary>
        public bool Tick(NuclearShockwaveController controller)
        {
            if (map == null)
            {
                map = Find.CurrentMap;
                if (map == null) return true;
            }
            if (props == null) return true;

            Map mapVal = map!;
            float maxR = props.maxRadius > 0f ? props.maxRadius : 15f;
            int thickness = props.ringThicknessCells > 0 ? props.ringThicknessCells : 3;
            float speed = props.expandSpeedCellsPerTick > 0f ? props.expandSpeedCellsPerTick : 0.5f;
            int damageAmountVal = props.damageAmount > 0 ? props.damageAmount : 1000;
            DamageDef? damageDef = DefDatabase<DamageDef>.GetNamedSilentFail(props.damageDefDefName);
            if (damageDef == null)
                damageDef = DefDatabase<DamageDef>.GetNamedSilentFail("DMSL_Damage_BlastWave");
            if (damageDef == null)
                damageDef = DamageDefOf.Bomb;
            FleckDef? cellFleck = damageDef.explosionCellFleck;
            int now = Find.TickManager.TicksGame;

            previousRadius = currentRadius;
            currentRadius = Mathf.Min(currentRadius + speed, maxR);

            ApplyShockwaveDamageForSweptCells(mapVal, previousRadius, currentRadius, props, now, damageDef, damageAmountVal);
            SpawnShockwaveVisuals(mapVal, currentRadius, thickness, cellFleck);

            if (currentRadius >= maxR)
                return true;

            return false;
        }
    }
}
