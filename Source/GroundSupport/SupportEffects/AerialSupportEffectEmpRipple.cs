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
    /// EMP 波纹专用 MapComponent：持有活跃波纹序列与临时停摆表，每 tick 推进序列并供 Harmony 查询停摆状态。
    /// 与渲染器解耦，保持 AerialSupportRenderer 只负责飞行与轰炸序列。
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
    /// EMP 波纹扩散序列：每 tick 向外推进一环带，对环带内单位施加 EMP 眩晕/停摆；同一实体在 effectCooldownTicks 内只受一次效果，超过后若再次被波扫到可再次施加。
    /// 机械体先受伤害，经 mechanoidStunDelayTicks 后再尝试眩晕，避免“边枚举边修改”且被击杀者不再被眩晕。
    /// </summary>
    public class EmpRippleSequence : IExposable
    {
        private IntVec3 center;
        private float currentRadius;
        private Dictionary<int, int> thingIdToLastHitTick = new Dictionary<int, int>();
        private CompProperties_AerialSupportEffect_EmpRipple? props = null;
        private Map? map = null;
        /// <summary>机械体眩晕延迟队列：到 applyAtTick 时对仍存活的 pawn 施加眩晕。</summary>
        private List<PendingMechanoidStun> pendingMechanoidStuns = new List<PendingMechanoidStun>();

        private static readonly List<IntVec3> ringCellsBuffer = new List<IntVec3>();
        /// <summary>遍历每格物品时先复制到此列表，避免 TakeDamage/ApplyDamage 修改原集合导致 InvalidOperationException。</summary>
        private static readonly List<Thing> thingsAtCellBuffer = new List<Thing>();

        public EmpRippleSequence() { }

        public EmpRippleSequence(IntVec3 center, CompProperties_AerialSupportEffect_EmpRipple props, Map map)
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
            Scribe_Collections.Look(ref pendingMechanoidStuns, "pendingMechanoidStuns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pendingMechanoidStuns == null)
                pendingMechanoidStuns = new List<PendingMechanoidStun>();
        }

        /// <summary>
        /// 每 tick 推进半径、计算环带、施加效果并播放采样特效。返回 true 表示序列结束（半径已超过 maxRadius）
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
            int thickness = props.ringThicknessCells > 0 ? props.ringThicknessCells : 3;
            float speed = props.expandSpeedCellsPerTick > 0f ? props.expandSpeedCellsPerTick : 0.5f;
            int disableTicks = props.disableTicks > 0 ? props.disableTicks : 1800;
            int tickGame = Find.TickManager.TicksGame;

            // 先处理到期的延迟眩晕：仅对仍存活的机械体施加，被伤害击杀的已不在列表中或已 Destroyed
            if (pendingMechanoidStuns != null && pendingMechanoidStuns.Count > 0)
            {
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

            currentRadius += speed;
            if (currentRadius > maxR)
            {
                // 序列结束前把尚未到期的延迟眩晕全部施加，避免遗漏
                if (pendingMechanoidStuns != null && pendingMechanoidStuns.Count > 0)
                {
                    DamageInfo empDinfoEnd = new DamageInfo(DamageDefOf.EMP, (float)disableTicks / 30f, -1f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
                    foreach (var entry in pendingMechanoidStuns)
                    {
                        if (entry.pawn == null || entry.pawn.Destroyed || !entry.pawn.Spawned) continue;
                        try { entry.pawn.stances?.stunner?.Notify_DamageApplied(empDinfoEnd); }
                        catch { }
                    }
                    pendingMechanoidStuns.Clear();
                }
                return true;
            }

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

            DamageInfo empDinfo = new DamageInfo(DamageDefOf.EMP, (float)disableTicks / 30f, -1f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true, false);
            int untilTick = tickGame + disableTicks;
            int cooldownTicks = props.effectCooldownTicks;
            int visualCounter = 0;
            // 原版 EMP 手雷爆炸时每格绘制的是 DamageDef.EMP 的 explosionCellFleck，即 Fleck BlastEMP（FleckDefOf 可能无此成员，故用 DefDatabase 按 defName 查找）
            FleckDef? blastEmpFleck = DefDatabase<FleckDef>.GetNamedSilentFail("BlastEMP");

            for (int i = 0; i < ringCellsBuffer.Count; i++)
            {
                IntVec3 cell = ringCellsBuffer[i];
                thingsAtCellBuffer.Clear();
                thingsAtCellBuffer.AddRange(mapVal.thingGrid.ThingsListAt(cell));
                foreach (Thing thing in thingsAtCellBuffer)
                {
                    if (thing == null || thing.Destroyed) continue;
                    int id = thing.thingIDNumber;
                    if (thingIdToLastHitTick.TryGetValue(id, out int lastTick))
                    {
                        if (cooldownTicks > 0 && (tickGame - lastTick) < cooldownTicks)
                            continue;
                    }
                    thingIdToLastHitTick[id] = tickGame;

                    if (thing is Pawn pawn && pawn.RaceProps != null && pawn.RaceProps.IsMechanoid)
                    {
                        // 先造成伤害（意识部位等），可能直接击杀机械体
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
                        // 眩晕延后施加：经 mechanoidStunDelayTicks 后再尝试，被伤害击杀的机械体届时已不存在
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

                if ((visualCounter++ % 4 == 0) && blastEmpFleck != null)
                {
                    try
                    {
                        FleckMaker.Static(cell.ToVector3Shifted(), mapVal, blastEmpFleck, Rand.Range(0.6f, 1f));
                    }
                    catch { }
                }
            }

            return false;
        }
    }
}
