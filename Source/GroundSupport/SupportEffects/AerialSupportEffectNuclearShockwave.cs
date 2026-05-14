using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：核冲击波环状扩散（NuclearShockwave）
    /// 以中心格为起点向外扩散；伤害由波前扫过区间结算；主体视觉为 MapComponent 每帧绘制的紧凑圆环，可选少量 explosionCellFleck 作装饰。
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

        /// <summary>伤害类型 defName，默认 DMSL_Damage_BlastWave；用于伤害与装饰 Fleck</summary>
        public string damageDefDefName = "DMSL_Damage_BlastWave";

        /// <summary>伤害冷却时间（tick）：同一实体在此时间内只受一次伤害，超过后若再次被波扫到可再次造成伤害；0 表示整次序列内仅受一次。</summary>
        public int damageCooldownTicks = 120;

        /// <summary>是否使用直接绘制的紧凑主体圆环（不产生 Fleck 残留）。</summary>
        public bool drawDirectRing = true;

        /// <summary>主体圆环透明度。</summary>
        public float ringAlpha = 0.45f;

        /// <summary>主体圆环每格方片缩放。</summary>
        public float ringDrawScale = 1f;

        public float ringColorR = 1f;
        public float ringColorG = 0.55f;
        public float ringColorB = 0.15f;

        /// <summary>非空时尝试用该贴图路径作为主体材质；失败则回退纯色。</summary>
        public string ringTexturePath = "";

        /// <summary>是否在波前外缘稀疏生成装饰用 explosionCellFleck。</summary>
        public bool drawDecorativeFlecks = true;

        /// <summary>每隔多少 tick 才尝试生成一批装饰 Fleck。</summary>
        public int decorativeFleckIntervalTicks = 6;

        /// <summary>外缘候选格每隔多少格采样一次装饰 Fleck。</summary>
        public int decorativeFleckSampleEveryCells = 32;

        public float decorativeFleckMinScale = 0.35f;
        public float decorativeFleckMaxScale = 0.65f;

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
    /// 核冲击波专用 MapComponent：持有活跃冲击波序列，每 tick 推进波前并结算伤害；主体圆环在 MapComponentDraw 中每帧绘制。
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

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();

            if (map == null || map != Find.CurrentMap)
                return;

            if (activeSequences == null || activeSequences.Count == 0)
                return;

            try
            {
                for (int i = 0; i < activeSequences.Count; i++)
                {
                    NuclearShockwaveSequence? seq = activeSequences[i];
                    if (seq == null)
                        continue;
                    seq.DrawVisualRing();
                }
            }
            catch
            {
                // 绘制失败不应影响游戏运行
            }
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
    /// 核冲击波扩散序列：真实波前推进与伤害由 <see cref="ApplyShockwaveDamageForSweptCells"/> 处理；主体视觉由 <see cref="DrawVisualRing"/> 每帧绘制，装饰 Fleck 稀疏生成。
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

        private static readonly List<Thing> toDamageBuffer = new List<Thing>();

        /// <summary>非存档：主体圆环材质缓存（不修改 MaterialPool 共享材质颜色）。</summary>
        private Material? cachedRingMaterial;
        private string cachedRingTexturePath = "\0";
        private float cachedRingAlpha = -1f;
        private float cachedRingColorR = -1f;
        private float cachedRingColorG = -1f;
        private float cachedRingColorB = -1f;

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

        private Material? GetOrCreateRingMaterial(CompProperties_AerialSupportEffect_NuclearShockwave p)
        {
            string path = p.ringTexturePath ?? "";
            float a = p.ringAlpha;
            float r = p.ringColorR;
            float g = p.ringColorG;
            float b = p.ringColorB;

            if (cachedRingMaterial != null &&
                cachedRingTexturePath == path &&
                Mathf.Approximately(cachedRingAlpha, a) &&
                Mathf.Approximately(cachedRingColorR, r) &&
                Mathf.Approximately(cachedRingColorG, g) &&
                Mathf.Approximately(cachedRingColorB, b))
            {
                return cachedRingMaterial;
            }

            cachedRingMaterial = null;
            cachedRingTexturePath = path;
            cachedRingAlpha = a;
            cachedRingColorR = r;
            cachedRingColorG = g;
            cachedRingColorB = b;

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    Material? texMat = MaterialPool.MatFrom(path, ShaderDatabase.Transparent);
                    if (texMat != null && texMat.mainTexture != null)
                    {
                        cachedRingMaterial = texMat;
                        return cachedRingMaterial;
                    }
                }
                catch
                {
                    // 回退纯色
                }
            }

            Color solid = new Color(r, g, b, a);
            cachedRingMaterial = SolidColorMaterials.SimpleSolidColorMaterial(solid);
            return cachedRingMaterial;
        }

        /// <summary>每帧由 <see cref="NuclearShockwaveController.MapComponentDraw"/> 调用；仅绘制紧凑主体圆环，不推进逻辑、不造成伤害、不使用 Fleck。</summary>
        public void DrawVisualRing()
        {
            if (map == null || props == null)
                return;

            if (!props.drawDirectRing)
                return;

            if (currentRadius <= 0f)
                return;

            Map mapVal = map!;
            int ringThickness = props.ringThicknessCells > 0 ? props.ringThicknessCells : 3;
            float visualRadius = currentRadius;
            float inner = Mathf.Max(0f, visualRadius - ringThickness);

            Material? mat = GetOrCreateRingMaterial(props);
            if (mat == null)
                return;

            float drawY = AltitudeLayer.MoteOverhead.AltitudeFor();
            int rCeil = Mathf.CeilToInt(visualRadius);
            float scale = props.ringDrawScale > 0f ? props.ringDrawScale : 1f;

            for (int dx = -rCeil; dx <= rCeil; dx++)
            {
                for (int dz = -rCeil; dz <= rCeil; dz++)
                {
                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(mapVal))
                        continue;

                    float dist = (cell - center).LengthHorizontal;
                    if (dist < inner || dist > visualRadius)
                        continue;

                    Vector3 drawPos = cell.ToVector3Shifted();
                    drawPos.y = drawY;

                    Matrix4x4 matrix = Matrix4x4.TRS(
                        drawPos,
                        Quaternion.identity,
                        new Vector3(scale, 1f, scale));

                    Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
                }
            }
        }

        private void SpawnDecorativeShockwaveFlecks(Map mapVal, float visualRadius, FleckDef? cellFleck, int now)
        {
            if (props == null)
                return;

            if (!props.drawDecorativeFlecks)
                return;

            if (cellFleck == null)
                return;

            int interval = props.decorativeFleckIntervalTicks > 0 ? props.decorativeFleckIntervalTicks : 6;
            if (interval > 1 && now % interval != 0)
                return;

            int sampleEvery = props.decorativeFleckSampleEveryCells > 0 ? props.decorativeFleckSampleEveryCells : 32;
            float minScale = props.decorativeFleckMinScale > 0f ? props.decorativeFleckMinScale : 0.35f;
            float maxScale = props.decorativeFleckMaxScale > minScale ? props.decorativeFleckMaxScale : minScale;

            float outerMin = Mathf.Max(0f, visualRadius - 0.75f);
            float outerMax = visualRadius + 0.25f;

            int rCeil = Mathf.CeilToInt(outerMax);
            int candidateCounter = 0;

            for (int dx = -rCeil; dx <= rCeil; dx++)
            {
                for (int dz = -rCeil; dz <= rCeil; dz++)
                {
                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(mapVal))
                        continue;

                    float dist = (cell - center).LengthHorizontal;
                    if (dist < outerMin || dist > outerMax)
                        continue;

                    if ((candidateCounter++ % sampleEvery) != 0)
                        continue;

                    try
                    {
                        FleckMaker.Static(cell.ToVector3Shifted(), mapVal, cellFleck, Rand.Range(minScale, maxScale));
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 每 tick 推进波前并结算伤害；装饰 Fleck 稀疏生成。主体圆环由 Draw 每帧绘制。
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
            SpawnDecorativeShockwaveFlecks(mapVal, currentRadius, cellFleck, now);

            if (currentRadius >= maxR)
                return true;

            return false;
        }
    }
}
