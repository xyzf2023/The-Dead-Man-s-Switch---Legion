using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：对全图没有指定健康状态的 pawn 添加该健康状态（给定严重度）；已有该健康状态的 pawn 跳过。可分批在若干 tick 内完成以避免单帧卡顿。
    /// </summary>
    public class CompProperties_AerialSupportEffect_AddHediff : CompProperties
    {
        /// <summary>健康状态 defName，仅对没有该 hediff 的 pawn 添加</summary>
        public string hediffDefName = "DMSL_Hediff_RadiationCorrosion";

        /// <summary>添加时该健康状态的严重度</summary>
        public float severityToAdd = 0.9f;

        /// <summary>在多少 tick 内分批完成；0 或 1 表示单帧内全部执行</summary>
        public int spreadOverTicks = 60;

        public CompProperties_AerialSupportEffect_AddHediff()
        {
            compClass = typeof(CompAerialSupportEffect_AddHediff);
        }
    }

    /// <summary>
    /// 空中支援效果组件：为没有该 hediff 的 pawn 添加健康状态（供渲染器反射调用）
    /// </summary>
    public class CompAerialSupportEffect_AddHediff : ThingComp
    {
        public CompProperties_AerialSupportEffect_AddHediff Props => (CompProperties_AerialSupportEffect_AddHediff)props;

        /// <summary>
        /// 执行效果（静态）：全图没有指定 hediff 的 pawn 添加该 hediff（给定严重度），已有则跳过；可选在 spreadOverTicks 内分批执行。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_AddHediff props)
        {
            if (map == null || props == null)
                return;

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(props.hediffDefName);
            if (hediffDef == null)
            {
                Log.Warning($"[DMS_Legion] AddHediff: HediffDef 不存在: {props.hediffDefName}");
                return;
            }

            List<Pawn> pawnsWithoutHediff = new List<Pawn>();
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p?.health?.hediffSet == null || p.Dead)
                    continue;
                if (p.health.hediffSet.GetFirstHediffOfDef(hediffDef) == null)
                    pawnsWithoutHediff.Add(p);
            }

            if (pawnsWithoutHediff.Count == 0)
                return;

            float severity = Mathf.Clamp01(props.severityToAdd);
            int spread = props.spreadOverTicks > 0 ? props.spreadOverTicks : 1;

            if (spread <= 1)
            {
                AddHediffToPawns(pawnsWithoutHediff, hediffDef, severity);
                return;
            }

            AddHediffScheduler scheduler = map.GetComponent<AddHediffScheduler>();
            if (scheduler == null)
            {
                scheduler = new AddHediffScheduler(map);
                map.components.Add(scheduler);
            }
            scheduler.Schedule(pawnsWithoutHediff, hediffDef, severity, spread);
        }

        /// <summary>
        /// 对一批 pawn 添加指定 hediff（仅当该 pawn 尚未拥有时添加），严重度为给定值。
        /// </summary>
        internal static void AddHediffToPawns(List<Pawn> pawns, HediffDef hediffDef, float severity)
        {
            if (pawns == null || hediffDef == null)
                return;
            foreach (Pawn p in pawns)
            {
                if (p?.health?.hediffSet == null || p.Dead)
                    continue;
                if (p.health.hediffSet.GetFirstHediffOfDef(hediffDef) != null)
                    continue;
                Hediff hediff = HediffMaker.MakeHediff(hediffDef, p);
                hediff.Severity = severity;
                p.health.AddHediff(hediff);
            }
        }
    }

    /// <summary>
    /// 增加严重度分批调度器：在指定 tick 内每帧处理一批 pawn，避免单帧卡顿。
    /// </summary>
    public class AddHediffScheduler : MapComponent
    {
        private List<AddHediffEntry> pending = new List<AddHediffEntry>();

        public AddHediffScheduler(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "pendingAddHediff", LookMode.Deep, Array.Empty<object>());
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pending?.RemoveAll(e => e == null || e.pawns == null || e.pawns.Count == 0);
            }
        }

        public override void MapComponentTick()
        {
            if (pending == null || pending.Count == 0)
                return;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                AddHediffEntry e = pending[i];
                if (e.pawns == null || e.pawns.Count == 0 || e.hediffDef == null)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                int remainingTicks = e.spreadOverTicks - e.elapsedTicks;
                if (remainingTicks <= 0)
                {
                    CompAerialSupportEffect_AddHediff.AddHediffToPawns(e.pawns, e.hediffDef, e.initialSeverity);
                    pending.RemoveAt(i);
                    continue;
                }

                int batchSize = Mathf.Max(1, (e.pawns.Count + remainingTicks - 1) / remainingTicks);
                int take = Mathf.Min(batchSize, e.pawns.Count);
                List<Pawn> batch = new List<Pawn>();
                for (int j = 0; j < take && j < e.pawns.Count; j++)
                    batch.Add(e.pawns[j]);
                e.pawns.RemoveRange(0, take);
                e.elapsedTicks++;

                CompAerialSupportEffect_AddHediff.AddHediffToPawns(batch, e.hediffDef, e.initialSeverity);

                if (e.pawns.Count == 0)
                    pending.RemoveAt(i);
            }
        }

        public void Schedule(List<Pawn> pawns, HediffDef hediffDef, float initialSeverity, int spreadOverTicks)
        {
            if (pending == null)
                pending = new List<AddHediffEntry>();
            pending.Add(new AddHediffEntry
            {
                pawns = new List<Pawn>(pawns),
                hediffDef = hediffDef,
                initialSeverity = initialSeverity,
                spreadOverTicks = spreadOverTicks,
                elapsedTicks = 0
            });
        }
    }

    /// <summary>
    /// 单条分批任务（可序列化）
    /// </summary>
    public class AddHediffEntry : IExposable
    {
        public List<Pawn>? pawns;
        public HediffDef? hediffDef;
        public float initialSeverity;
        public int spreadOverTicks;
        public int elapsedTicks;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Defs.Look(ref hediffDef, "hediffDef");
            Scribe_Values.Look(ref initialSeverity, "severityToAdd", 0.9f);
            Scribe_Values.Look(ref spreadOverTicks, "spreadOverTicks", 60);
            Scribe_Values.Look(ref elapsedTicks, "elapsedTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawns == null)
                pawns = new List<Pawn>();
        }
    }
}
