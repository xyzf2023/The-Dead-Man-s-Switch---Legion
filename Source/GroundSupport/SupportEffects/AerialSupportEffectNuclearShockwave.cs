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
    /// 核冲击波扩散序列：每 tick 向外推进一环带，环带内每格绘制爆炸 Fleck，对环带内实体造成指定伤害类型伤害；同一实体在 damageCooldownTicks 内只受一次伤害，超过后若再次被波扫到可再次造成伤害；冲击波不因建筑阻挡而停止。
    /// </summary>
    public class NuclearShockwaveSequence : IExposable
    {
        private IntVec3 center;
        private float currentRadius;
        /// <summary>实体 thingIDNumber -> 上次受到本序列伤害的 tick。用于冷却：仅在超过 damageCooldownTicks 后再次被波扫到才可再次造成伤害。</summary>
        private Dictionary<int, int> thingIdToLastHitTick = new Dictionary<int, int>();
        private CompProperties_AerialSupportEffect_NuclearShockwave? props = null;
        private Map? map = null;

        private static readonly List<IntVec3> ringCellsBuffer = new List<IntVec3>();
        private static readonly List<Thing> toDamageBuffer = new List<Thing>();

        public NuclearShockwaveSequence() { }

        public NuclearShockwaveSequence(IntVec3 center, CompProperties_AerialSupportEffect_NuclearShockwave props, Map map)
        {
            this.center = center;
            this.props = props;
            this.map = map;
            this.currentRadius = 0f;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref currentRadius, "currentRadius", 0f);
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
        }

        /// <summary>
        /// 每 tick 推进半径、计算环带、施加伤害并绘制每格爆炸 Fleck。返回 true 表示序列结束。
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
            FleckDef? cellFleck = damageDef?.explosionCellFleck;

            currentRadius += speed;
            if (currentRadius > maxR)
                return true;

            float inner = Mathf.Max(0f, currentRadius - thickness);
            ringCellsBuffer.Clear();
            int rCeil = Mathf.CeilToInt(currentRadius + thickness);
            for (int dx = -rCeil; dx <= rCeil; dx++)
            {
                for (int dz = -rCeil; dz <= rCeil; dz++)
                {
                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(mapVal)) continue;
                    float dist = (cell - center).LengthHorizontal;
                    if (dist >= inner - 0.01f && dist <= currentRadius + 0.01f)
                        ringCellsBuffer.Add(cell);
                }
            }

            int now = Find.TickManager.TicksGame;
            int cooldownTicks = props.damageCooldownTicks;
            int visualCounter = 0;
            for (int i = 0; i < ringCellsBuffer.Count; i++)
            {
                IntVec3 cell = ringCellsBuffer[i];

                // 每格绘制爆炸格特效（来自伤害类型的 explosionCellFleck），采样以控制性能
                if ((visualCounter++ % 4 == 0) && cellFleck != null)
                {
                    try
                    {
                        FleckMaker.Static(cell.ToVector3Shifted(), mapVal, cellFleck, Rand.Range(0.8f, 1.4f));
                    }
                    catch { }
                }

                // 先收集本格需造成伤害的实体，避免 TakeDamage 修改 ThingsListAt 导致枚举异常
                toDamageBuffer.Clear();
                foreach (Thing thing in mapVal.thingGrid.ThingsListAt(cell))
                {
                    if (thing == null || thing.Destroyed) continue;
                    int id = thing.thingIDNumber;
                    if (thingIdToLastHitTick.TryGetValue(id, out int lastTick))
                    {
                        if (cooldownTicks > 0 && (now - lastTick) < cooldownTicks)
                            continue;
                    }
                    // Pawn 使用身体部位生命值（useHitPoints 通常为 false），需显式包含；其余有 useHitPoints 的实体（建筑、物品等）一并伤害
                    if (damageDef != null && (thing is Pawn || (thing.def?.useHitPoints == true)))
                        toDamageBuffer.Add(thing);
                    thingIdToLastHitTick[id] = now;
                }
                if (damageDef != null)
                {
                    DamageInfo dinfo = new DamageInfo(damageDef, damageAmountVal, -1f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
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

            return false;
        }
    }
}
