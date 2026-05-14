using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：EMP 波纹扩散（EmpRipple）
    /// 以中心格为起点生成向外扩散的近似圆形 EMP 环带，对机械体/炮台/用电建筑施加眩晕或临时停摆。
    /// </summary>
    public class CompProperties_AerialSupportEffect_EmpRipple : CompProperties
    {
        /// <summary>最大扩散半径（格）</summary>
        public float maxRadius = 15f;

        /// <summary>环带宽度（格），默认 3</summary>
        public int ringThicknessCells = 3;

        /// <summary>扩散速度（格/tick）</summary>
        public float expandSpeedCellsPerTick = 0.5f;

        /// <summary>机械体/炮台/建筑禁用时长（tick），默认 1800 = 30 秒</summary>
        public int disableTicks = 1800;

        /// <summary>是否对无 CompStunnable 的用电建筑造成临时断电；true 时触发，false 时不触发</summary>
        public bool disablePowerBuildings = false;

        /// <summary>效果冷却时间（tick）：同一实体在此时间内只受一次 EMP 效果，超过后若再次被波扫到可再次施加；0 表示整次序列内仅一次。</summary>
        public int effectCooldownTicks = 0;

        /// <summary>是否对被波扫到的机械体的意识处理部位（ConsciousnessSource，如数据处理）造成额外伤害；true 时造成，false 时不造成。</summary>
        public bool damageConsciousnessPart = false;

        /// <summary>意识处理部位受到的伤害量（仅当 damageConsciousnessPart 为 true 时生效）；伤害类型为 DMSL_Damage_NuclearEMP。</summary>
        public int consciousnessPartDamageAmount = 1000;

        /// <summary>机械体眩晕延迟（tick）：先造成伤害，经此 tick 后再尝试施加眩晕；被伤害击杀的机械体届时已不存在，不会重复处理。0 表示立即施加眩晕。</summary>
        public int mechanoidStunDelayTicks = 10;

        public bool drawDirectRing = true;
        public int ringFadeOutTicks = 18;
        public float ringAlpha = 0.55f;
        public float ringDrawScale = 1f;
        public float ringColorR = 0.65f;
        public float ringColorG = 0.85f;
        public float ringColorB = 1f;
        public string ringTexturePath = "";
        public string ringFleckDefName = "";
        public bool useFleckMaterialForDirectRing = true;
        public bool drawDecorativeFlecks = true;
        public string decorativeFleckDefName = "";
        public int decorativeFleckIntervalTicks = 6;
        public int decorativeFleckSampleEveryCells = 32;
        public float decorativeFleckMinScale = 0.35f;
        public float decorativeFleckMaxScale = 0.65f;
        public bool ringCellRandomRotation = true;
        public float ringCellMinScale = 0.75f;
        public float ringCellMaxScale = 1.15f;
        public float ringCellMinAlphaFactor = 0.65f;
        public float ringCellMaxAlphaFactor = 1f;
        public float ringCellMinBrightnessFactor = 0.85f;
        public float ringCellMaxBrightnessFactor = 1.15f;
        public bool ringCellSubtlePulse = true;
        public float ringCellPulseAmplitude = 0.12f;

        public CompProperties_AerialSupportEffect_EmpRipple()
        {
            compClass = typeof(CompAerialSupportEffect_EmpRipple);
        }
    }

    /// <summary>
    /// 空中支援效果组件：EMP 波纹扩散（仅负责在到达时启动序列，实际扩散由 EmpRippleController 驱动）
    /// </summary>
    public class CompAerialSupportEffect_EmpRipple : ThingComp
    {
        public CompProperties_AerialSupportEffect_EmpRipple Props => (CompProperties_AerialSupportEffect_EmpRipple)props;

        /// <summary>
        /// 执行效果（静态，供渲染器反射调用）：在目标格启动 EMP 波纹扩散序列。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_EmpRipple props)
        {
            if (map == null || props == null)
                return;

            EmpRippleController controller = map.GetComponent<EmpRippleController>();
            if (controller == null)
            {
                Log.Error("[DMS_Legion] EmpRipple: EmpRippleController not found on map.");
                return;
            }

            controller.StartEmpRippleSequence(targetPos, props);
        }
    }

    /// <summary>
    /// EMP 波纹专用 MapComponent：每 tick 推进序列；主体圆环在 MapComponentDraw 中绘制。
    /// </summary>
    public class EmpRippleController : MapComponent
    {
        private List<EmpRippleSequence> activeEmpRippleSequences = new List<EmpRippleSequence>();
        /// <summary>EMP 波纹对无 CompStunnable 的用电建筑的临时停摆：thingID -> 恢复 tick（不篡改电网，仅在此表内记录）</summary>
        private Dictionary<int, int> powerSuppressedUntilTick = new Dictionary<int, int>();
        private int lastPowerSuppressionCleanupTick = -1;
        private const int PowerSuppressionCleanupIntervalTicks = 60;
        public static bool AnyPowerSuppressionActive { get; private set; }

        public EmpRippleController(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref activeEmpRippleSequences, "activeEmpRippleSequences", LookMode.Deep, Array.Empty<object>());
            Scribe_Collections.Look(ref powerSuppressedUntilTick, "powerSuppressedUntilTick", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                activeEmpRippleSequences?.RemoveAll(seq => seq == null);
                if (powerSuppressedUntilTick == null)
                    powerSuppressedUntilTick = new Dictionary<int, int>();
                RecalculateGlobalPowerSuppressionFlag();
            }
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();

            if (map == null || map != Find.CurrentMap)
                return;

            if (activeEmpRippleSequences == null || activeEmpRippleSequences.Count == 0)
                return;

            try
            {
                for (int i = 0; i < activeEmpRippleSequences.Count; i++)
                {
                    EmpRippleSequence? seq = activeEmpRippleSequences[i];
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
            if (activeEmpRippleSequences != null)
                activeEmpRippleSequences!.RemoveAll(seq => seq.Tick(this));

            int now = Find.TickManager.TicksGame;
            if (powerSuppressedUntilTick != null && powerSuppressedUntilTick.Count > 0)
            {
                if (lastPowerSuppressionCleanupTick < 0 ||
                    now - lastPowerSuppressionCleanupTick >= PowerSuppressionCleanupIntervalTicks)
                {
                    lastPowerSuppressionCleanupTick = now;
                    CleanupExpiredPowerSuppressions(now);
                }
            }
        }

        public void StartEmpRippleSequence(IntVec3 center, CompProperties_AerialSupportEffect_EmpRipple props)
        {
            if (activeEmpRippleSequences == null)
                activeEmpRippleSequences = new List<EmpRippleSequence>();
            activeEmpRippleSequences.Add(new EmpRippleSequence(center, props, map));
        }

        /// <summary>当前是否存在任何未过期的用电停摆（供补丁快速路径：无停摆时跳过 GetComponent + 字典查询）</summary>
        public bool HasAnyPowerSuppression => powerSuppressedUntilTick != null && powerSuppressedUntilTick.Count > 0;

        /// <summary>判断某物是否处于 EMP 波纹临时停摆中（供 Harmony 补丁查询）</summary>
        public bool IsPowerSuppressed(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map != map || powerSuppressedUntilTick == null)
                return false;
            if (!powerSuppressedUntilTick.TryGetValue(thing.thingIDNumber, out int until))
                return false;
            return Find.TickManager.TicksGame < until;
        }

        /// <summary>将某物登记为临时停摆直到 untilTick</summary>
        public void RegisterPowerSuppressed(Thing thing, int untilTick)
        {
            if (thing == null || powerSuppressedUntilTick == null) return;
            powerSuppressedUntilTick[thing.thingIDNumber] = untilTick;
            AnyPowerSuppressionActive = true;
        }

        public static void RecalculateGlobalPowerSuppressionFlag()
        {
            AnyPowerSuppressionActive = false;
            if (Find.Maps == null)
                return;

            foreach (Map currentMap in Find.Maps)
            {
                EmpRippleController controller = currentMap.GetComponent<EmpRippleController>();
                if (controller != null && controller.HasAnyPowerSuppression)
                {
                    AnyPowerSuppressionActive = true;
                    return;
                }
            }
        }

        private void CleanupExpiredPowerSuppressions(int now)
        {
            if (powerSuppressedUntilTick == null || powerSuppressedUntilTick.Count == 0)
                return;

            List<int>? toRemove = null;
            foreach (KeyValuePair<int, int> kv in powerSuppressedUntilTick)
            {
                if (kv.Value <= now)
                {
                    toRemove ??= new List<int>();
                    toRemove.Add(kv.Key);
                }
            }

            if (toRemove == null)
                return;

            foreach (int key in toRemove)
                powerSuppressedUntilTick.Remove(key);

            if (powerSuppressedUntilTick.Count == 0)
                RecalculateGlobalPowerSuppressionFlag();
        }
    }

    /// <summary>
    /// 延迟施加机械体眩晕的待办项：先造成伤害，若干 tick 后再尝试眩晕；若机械体已被击杀则不再施加。
    /// </summary>
    public class PendingMechanoidStun : IExposable
    {
        public Pawn? pawn;
        public int applyAtTick;

        public PendingMechanoidStun() { }

        public PendingMechanoidStun(Pawn pawn, int applyAtTick)
        {
            this.pawn = pawn;
            this.applyAtTick = applyAtTick;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref applyAtTick, "applyAtTick");
        }
    }

    /// <summary>
    /// EMP 波纹扩散序列：效果层由 <see cref="ApplyRippleEffectsForSweptCells"/> 按波前扫过处理；主体视觉为 Draw 中直接绘制圆环；装饰 BlastEMP 稀疏外缘生成；可选淡出阶段仅视觉。
    /// </summary>
    public class EmpRippleSequence : IExposable
    {
        private const float CellHitPadding = 0.75f;

        private IntVec3 center;
        private float currentRadius;
        /// <summary>本 tick 推进前保存的真实波前半径，与推进后的 currentRadius 共同定义效果层扫过区间。</summary>
        private float previousRadius;
        /// <summary>本次序列中已对格子施加过实际 EMP 效果的地图格索引（padding 与厚环视觉可能造成重复覆盖，用于避免重复处理）。</summary>
        private HashSet<int> processedCellIndices = new HashSet<int>();
        private Dictionary<int, int> thingIdToLastHitTick = new Dictionary<int, int>();
        private CompProperties_AerialSupportEffect_EmpRipple? props = null;
        private Map? map = null;
        /// <summary>机械体眩晕延迟队列：到 applyAtTick 时对仍存活的 pawn 施加眩晕。</summary>
        private List<PendingMechanoidStun> pendingMechanoidStuns = new List<PendingMechanoidStun>();

        /// <summary>遍历每格物品时先复制到此列表，避免 TakeDamage/ApplyDamage 修改原集合导致 InvalidOperationException。</summary>
        private static readonly List<Thing> thingsAtCellBuffer = new List<Thing>();

        private readonly MaterialPropertyBlock empRingCellMatPropertyBlock = new MaterialPropertyBlock();

        private enum EmpRingDrawMatKind
        {
            None,
            FleckGraphic,
            TexturePath,
            SolidColor
        }

        private Material? empRingDrawSharedMaterial;
        private string empRingDrawCacheKey = "";
        private EmpRingDrawMatKind empRingDrawMatKind;
        private Color empRingDrawFleckGraphicColor = Color.white;
        private float empRingDrawFleckSizeAvg = 1f;

        private bool isFadingOut;
        private int fadeOutElapsedTicks;

        public EmpRippleSequence() { }

        public EmpRippleSequence(IntVec3 center, CompProperties_AerialSupportEffect_EmpRipple props, Map map)
        {
            this.center = center;
            this.props = props;
            this.map = map;
            this.currentRadius = 0f;
            this.previousRadius = 0f;
            this.processedCellIndices = new HashSet<int>();
            this.isFadingOut = false;
            this.fadeOutElapsedTicks = 0;
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
            Scribe_Collections.Look(ref pendingMechanoidStuns, "pendingMechanoidStuns", LookMode.Deep);
            Scribe_Values.Look(ref isFadingOut, "isFadingOut", false);
            Scribe_Values.Look(ref fadeOutElapsedTicks, "fadeOutElapsedTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (thingIdToLastHitTick == null)
                    thingIdToLastHitTick = new Dictionary<int, int>();
                if (pendingMechanoidStuns == null)
                    pendingMechanoidStuns = new List<PendingMechanoidStun>();
                if (processedCellIndices == null)
                    processedCellIndices = new HashSet<int>();
            }
        }

        private void ProcessPendingMechanoidStuns(int tickGame, int disableTicks)
        {
            if (pendingMechanoidStuns == null || pendingMechanoidStuns.Count == 0)
                return;
            DamageInfo empDinfoForStun = new DamageInfo(DamageDefOf.EMP, (float)disableTicks / 30f, -1f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
            pendingMechanoidStuns.RemoveAll(entry =>
            {
                if (entry.applyAtTick > tickGame) return false;
                if (entry.pawn == null || entry.pawn.Destroyed || !entry.pawn.Spawned) return true;
                try
                {
                    entry.pawn.stances?.stunner?.Notify_DamageApplied(empDinfoForStun);
                }
                catch { }
                return true;
            });
        }

        private void FlushPendingMechanoidStuns(int disableTicks)
        {
            if (pendingMechanoidStuns == null || pendingMechanoidStuns.Count == 0)
                return;
            DamageInfo empDinfoEnd = new DamageInfo(DamageDefOf.EMP, (float)disableTicks / 30f, -1f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
            foreach (PendingMechanoidStun entry in pendingMechanoidStuns)
            {
                if (entry.pawn == null || entry.pawn.Destroyed || !entry.pawn.Spawned) continue;
                try { entry.pawn.stances?.stunner?.Notify_DamageApplied(empDinfoEnd); }
                catch { }
            }
            pendingMechanoidStuns.Clear();
        }

        private void ApplyRippleEffectsForSweptCells(
            EmpRippleController controller,
            Map mapVal,
            float fromRadius,
            float toRadius,
            CompProperties_AerialSupportEffect_EmpRipple props,
            int tickGame,
            int disableTicks)
        {
            DamageInfo empDinfo = new DamageInfo(DamageDefOf.EMP, (float)disableTicks / 30f, -1f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
            int untilTick = tickGame + disableTicks;
            int cooldownTicks = props.effectCooldownTicks;

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

                    thingsAtCellBuffer.Clear();
                    thingsAtCellBuffer.AddRange(mapVal.thingGrid.ThingsListAt(cell));
                    foreach (Thing thing in thingsAtCellBuffer)
                    {
                        if (thing == null || thing.Destroyed) continue;
                        int id = thing.thingIDNumber;
                        if (thingIdToLastHitTick.TryGetValue(id, out int lastTick))
                        {
                            if (cooldownTicks <= 0)
                                continue;
                            if ((tickGame - lastTick) < cooldownTicks)
                                continue;
                        }
                        thingIdToLastHitTick[id] = tickGame;

                        if (thing is Pawn pawn && pawn.RaceProps != null && pawn.RaceProps.IsMechanoid)
                        {
                            if (props.damageConsciousnessPart && pawn.health?.hediffSet != null)
                            {
                                BodyPartRecord? consciousnessPart = pawn.health.hediffSet.GetBrain();
                                if (consciousnessPart != null)
                                {
                                    DamageDef? nuclearEmpDef = DefDatabase<DamageDef>.GetNamedSilentFail("DMSL_Damage_NuclearEMP") ?? DamageDefOf.EMP;
                                    int amount = props.consciousnessPartDamageAmount > 0 ? props.consciousnessPartDamageAmount : 1000;
                                    if (nuclearEmpDef != null)
                                    {
                                        DamageInfo dinfo = new DamageInfo(nuclearEmpDef, amount, -1f, -1f, null, consciousnessPart, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
                                        try { pawn.TakeDamage(dinfo); }
                                        catch { }
                                    }
                                }
                            }
                            int delay = props.mechanoidStunDelayTicks > 0 ? props.mechanoidStunDelayTicks : 0;
                            if (delay > 0)
                            {
                                if (pendingMechanoidStuns == null) pendingMechanoidStuns = new List<PendingMechanoidStun>();
                                pendingMechanoidStuns.Add(new PendingMechanoidStun(pawn, tickGame + delay));
                            }
                            else if (pawn.stances?.stunner != null)
                            {
                                pawn.stances.stunner.Notify_DamageApplied(empDinfo);
                            }
                            continue;
                        }

                        if (thing is Building)
                        {
                            CompStunnable? stun = thing.TryGetComp<CompStunnable>();
                            if (stun != null && stun.CanBeStunnedByDamage(DamageDefOf.EMP))
                            {
                                stun.ApplyDamage(empDinfo);
                                continue;
                            }
                            if (props.disablePowerBuildings && thing.TryGetComp<CompPowerTrader>() != null && controller != null)
                                controller.RegisterPowerSuppressed(thing, untilTick);
                        }
                    }
                }
            }
        }

        private float StableRandom01(IntVec3 cell, int salt)
        {
            unchecked
            {
                int h = cell.x * 73856093 ^ cell.z * 19349663 ^ center.x * 83492791 ^ center.z * 297121507 ^ salt * 1396312589;
                h = (h << 13) ^ h;
                h = h * (h * h * 15731 + 789221) + 961748927;
                uint u = (uint)h;
                return Mathf.Clamp01(u / (float)uint.MaxValue);
            }
        }

        private FleckDef? ResolveRingFleckDef()
        {
            if (props == null)
                return null;
            if (!string.IsNullOrEmpty(props.ringFleckDefName))
            {
                FleckDef? named = DefDatabase<FleckDef>.GetNamedSilentFail(props.ringFleckDefName);
                if (named != null)
                    return named;
            }
            return DefDatabase<FleckDef>.GetNamedSilentFail("BlastEMP");
        }

        private FleckDef? ResolveDecorativeEmpFleckDef()
        {
            if (props == null)
                return DefDatabase<FleckDef>.GetNamedSilentFail("BlastEMP");
            if (!string.IsNullOrEmpty(props.decorativeFleckDefName))
            {
                FleckDef? named = DefDatabase<FleckDef>.GetNamedSilentFail(props.decorativeFleckDefName);
                if (named != null)
                    return named;
            }
            return DefDatabase<FleckDef>.GetNamedSilentFail("BlastEMP");
        }

        private void RefreshRingDrawCacheIfNeeded(CompProperties_AerialSupportEffect_EmpRipple p, FleckDef? ringFleckDef)
        {
            string fleckName = ringFleckDef?.defName ?? "";
            string texPath = p.ringTexturePath ?? "";
            string key = $"{p.useFleckMaterialForDirectRing}|{fleckName}|{texPath}|{p.ringColorR}|{p.ringColorG}|{p.ringColorB}|{p.ringAlpha}";
            if (empRingDrawSharedMaterial != null && empRingDrawCacheKey == key)
                return;

            empRingDrawCacheKey = key;
            empRingDrawSharedMaterial = null;
            empRingDrawMatKind = EmpRingDrawMatKind.None;
            empRingDrawFleckGraphicColor = Color.white;
            empRingDrawFleckSizeAvg = 1f;

            if (p.useFleckMaterialForDirectRing && ringFleckDef != null)
            {
                try
                {
                    GraphicData? gd = ringFleckDef.GetGraphicData(0);
                    Graphic? graphic = gd?.Graphic;
                    Material? m = graphic?.MatSingle;
                    if (m != null && m != BaseContent.BadMat)
                    {
                        empRingDrawSharedMaterial = m;
                        empRingDrawFleckGraphicColor = graphic!.Color;
                        Vector2 ds = gd!.drawSize;
                        empRingDrawFleckSizeAvg = (ds.x + ds.y) * 0.5f;
                        if (empRingDrawFleckSizeAvg < 0.01f)
                            empRingDrawFleckSizeAvg = 1f;
                        empRingDrawMatKind = EmpRingDrawMatKind.FleckGraphic;
                        return;
                    }
                }
                catch
                {
                    // 回退
                }
            }

            if (!string.IsNullOrEmpty(texPath))
            {
                try
                {
                    Material? texMat = MaterialPool.MatFrom(texPath, ShaderDatabase.Transparent);
                    if (texMat != null && texMat.mainTexture != null)
                    {
                        empRingDrawSharedMaterial = texMat;
                        empRingDrawMatKind = EmpRingDrawMatKind.TexturePath;
                        return;
                    }
                }
                catch
                {
                    // 回退纯色
                }
            }

            Color solid = new Color(p.ringColorR, p.ringColorG, p.ringColorB, p.ringAlpha);
            empRingDrawSharedMaterial = SolidColorMaterials.SimpleSolidColorMaterial(solid);
            empRingDrawMatKind = EmpRingDrawMatKind.SolidColor;
        }

        private float GetFadeAlphaMultiplier()
        {
            if (!isFadingOut || props == null)
                return 1f;
            int fadeTicks = props.ringFadeOutTicks;
            if (fadeTicks <= 0)
                return 0f;
            float t = Mathf.Clamp01(fadeOutElapsedTicks / (float)fadeTicks);
            float eased = t * t * (3f - 2f * t);
            return 1f - eased;
        }

        /// <summary>每帧由 <see cref="EmpRippleController.MapComponentDraw"/> 调用；仅绘制主体圆环，不施加 EMP、不生成主体 Fleck。</summary>
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

            FleckDef? ringFleckDef = ResolveRingFleckDef();
            RefreshRingDrawCacheIfNeeded(props, ringFleckDef);

            if (empRingDrawSharedMaterial == null || empRingDrawMatKind == EmpRingDrawMatKind.None)
                return;

            float drawY = AltitudeLayer.MoteOverhead.AltitudeFor();
            int rCeil = Mathf.CeilToInt(visualRadius);
            float baseDrawScale = props.ringDrawScale > 0f ? props.ringDrawScale : 1f;

            float minSc = props.ringCellMinScale;
            float maxSc = props.ringCellMaxScale > minSc ? props.ringCellMaxScale : minSc;
            float minAf = props.ringCellMinAlphaFactor;
            float maxAf = props.ringCellMaxAlphaFactor > minAf ? props.ringCellMaxAlphaFactor : minAf;
            float minBf = props.ringCellMinBrightnessFactor;
            float maxBf = props.ringCellMaxBrightnessFactor > minBf ? props.ringCellMaxBrightnessFactor : minBf;
            float pulseAmp = props.ringCellPulseAmplitude > 0f ? props.ringCellPulseAmplitude : 0.12f;
            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            float fadeAlpha = GetFadeAlphaMultiplier();

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

                    float scaleFactor = Mathf.Lerp(minSc, maxSc, StableRandom01(cell, 92001));
                    float sizeMul = empRingDrawMatKind == EmpRingDrawMatKind.FleckGraphic ? empRingDrawFleckSizeAvg : 1f;
                    float finalScale = baseDrawScale * scaleFactor * sizeMul;

                    float alphaFactor = Mathf.Lerp(minAf, maxAf, StableRandom01(cell, 92003));
                    float brightFactor = Mathf.Lerp(minBf, maxBf, StableRandom01(cell, 92004));

                    float pulse = 1f;
                    if (props.ringCellSubtlePulse)
                    {
                        float phase = StableRandom01(cell, 92002) * 100f;
                        pulse = 1f + Mathf.Sin((ticksGame + phase) * 0.15f) * pulseAmp;
                        pulse = Mathf.Clamp(pulse, 1f - pulseAmp, 1f + pulseAmp);
                    }

                    Quaternion rot = Quaternion.identity;
                    if (props.ringCellRandomRotation)
                    {
                        float ang = StableRandom01(cell, 92000) * 360f;
                        rot = Quaternion.AngleAxis(ang, Vector3.up);
                    }

                    Vector3 drawPos = cell.ToVector3Shifted();
                    drawPos.y = drawY;

                    Matrix4x4 matrix = Matrix4x4.TRS(drawPos, rot, new Vector3(finalScale, 1f, finalScale));

                    Color c;
                    switch (empRingDrawMatKind)
                    {
                        case EmpRingDrawMatKind.FleckGraphic:
                            c = empRingDrawFleckGraphicColor;
                            c.r = Mathf.Clamp01(c.r * brightFactor);
                            c.g = Mathf.Clamp01(c.g * brightFactor);
                            c.b = Mathf.Clamp01(c.b * brightFactor);
                            c.a = Mathf.Clamp01(c.a * props.ringAlpha * alphaFactor * pulse);
                            break;
                        case EmpRingDrawMatKind.TexturePath:
                            c = new Color(props.ringColorR, props.ringColorG, props.ringColorB, props.ringAlpha);
                            c.r = Mathf.Clamp01(c.r * brightFactor);
                            c.g = Mathf.Clamp01(c.g * brightFactor);
                            c.b = Mathf.Clamp01(c.b * brightFactor);
                            c.a = Mathf.Clamp01(c.a * alphaFactor * pulse);
                            break;
                        default:
                            c = empRingDrawSharedMaterial.color;
                            c.r = Mathf.Clamp01(c.r * brightFactor);
                            c.g = Mathf.Clamp01(c.g * brightFactor);
                            c.b = Mathf.Clamp01(c.b * brightFactor);
                            c.a = Mathf.Clamp01(c.a * alphaFactor * pulse);
                            break;
                    }

                    c.a = Mathf.Clamp01(c.a * fadeAlpha);

                    empRingCellMatPropertyBlock.SetColor(ShaderPropertyIDs.Color, c);
                    Graphics.DrawMesh(MeshPool.plane10, matrix, empRingDrawSharedMaterial, 0, null, 0, empRingCellMatPropertyBlock);
                }
            }
        }

        private void SpawnDecorativeEmpFlecks(Map mapVal, float visualRadius, FleckDef? fleckDef, int now)
        {
            if (props == null)
                return;

            if (!props.drawDecorativeFlecks)
                return;

            if (fleckDef == null)
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
                        FleckMaker.Static(cell.ToVector3Shifted(), mapVal, fleckDef, Rand.Range(minScale, maxScale));
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 每 tick 推进波前、施加效果；装饰 Fleck 稀疏生成；主体圆环由 MapComponentDraw 绘制。返回 true 表示序列结束。
        /// </summary>
        public bool Tick(EmpRippleController controller)
        {
            if (map == null)
            {
                map = Find.CurrentMap;
                if (map == null) return true;
            }
            if (props == null) return true;

            Map mapVal = map!;
            float maxR = props.maxRadius > 0f ? props.maxRadius : 15f;
            int disableTicks = props.disableTicks > 0 ? props.disableTicks : 1800;
            int tickGame = Find.TickManager.TicksGame;

            if (isFadingOut)
            {
                fadeOutElapsedTicks++;
                if (props.ringFadeOutTicks <= 0)
                    return true;
                if (fadeOutElapsedTicks >= props.ringFadeOutTicks)
                    return true;
                return false;
            }

            ProcessPendingMechanoidStuns(tickGame, disableTicks);

            float speed = props.expandSpeedCellsPerTick > 0f ? props.expandSpeedCellsPerTick : 0.5f;

            previousRadius = currentRadius;
            currentRadius = Mathf.Min(currentRadius + speed, maxR);

            ApplyRippleEffectsForSweptCells(controller, mapVal, previousRadius, currentRadius, props, tickGame, disableTicks);

            FleckDef? decorativeFleck = ResolveDecorativeEmpFleckDef();
            SpawnDecorativeEmpFlecks(mapVal, currentRadius, decorativeFleck, tickGame);

            if (currentRadius >= maxR)
            {
                FlushPendingMechanoidStuns(disableTicks);
                if (props.ringFadeOutTicks > 0)
                {
                    isFadingOut = true;
                    fadeOutElapsedTicks = 0;
                    return false;
                }
                return true;
            }

            return false;
        }
    }
}
