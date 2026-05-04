using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：触发事件（Incident）
    /// 到达时按配置的 IncidentDef defName 调用对应事件，支持多个事件、可配置延迟 tick。
    /// </summary>
    public class CompProperties_AerialSupportEffect_TriggerEvent : CompProperties
    {
        /// <summary>要触发的事件（Incident）defName 列表，可填多个同时触发</summary>
        public List<string> incidentDefNames = new List<string>();

        /// <summary>从唤起本效果到实际调用事件的延迟（tick）；0 表示立即触发</summary>
        public int delayTicks = 0;

        public CompProperties_AerialSupportEffect_TriggerEvent()
        {
            compClass = typeof(CompAerialSupportEffect_TriggerEvent);
        }
    }

    /// <summary>
    /// 空中支援效果组件：触发事件（供渲染器反射调用）
    /// </summary>
    public class CompAerialSupportEffect_TriggerEvent : ThingComp
    {
        public CompProperties_AerialSupportEffect_TriggerEvent Props => (CompProperties_AerialSupportEffect_TriggerEvent)props;

        /// <summary>
        /// 执行效果（静态）：立即或延迟触发配置的 Incident。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_TriggerEvent props)
        {
            if (map == null || props == null || props.incidentDefNames == null || props.incidentDefNames.Count == 0)
                return;

            int delay = props.delayTicks > 0 ? props.delayTicks : 0;
            List<string> defNames = new List<string>(props.incidentDefNames);

            if (delay <= 0)
            {
                FireIncidents(map, defNames);
                return;
            }

            TriggerEventScheduler scheduler = map.GetComponent<TriggerEventScheduler>();
            if (scheduler == null)
            {
                scheduler = new TriggerEventScheduler(map);
                map.components.Add(scheduler);
            }
            scheduler.Schedule(Find.TickManager.TicksGame + delay, defNames);
        }

        /// <summary>
        /// 对当前地图触发一批 Incident（参考原版“触发事件”逻辑：IncidentParms.target = map，Worker.TryExecute）
        /// </summary>
        internal static void FireIncidents(Map map, List<string> incidentDefNames)
        {
            if (map == null || incidentDefNames == null)
                return;
            foreach (string defName in incidentDefNames)
            {
                if (string.IsNullOrEmpty(defName)) continue;
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    Log.Warning($"[DMS_Legion] TriggerEvent: IncidentDef 不存在: {defName}");
                    continue;
                }
                IncidentParms parms = new IncidentParms();
                parms.target = map;
                try
                {
                    if (def.Worker != null)
                        def.Worker.TryExecute(parms);
                }
                catch (Exception ex)
                {
                    Log.Error($"[DMS_Legion] TriggerEvent 执行失败 [{defName}]: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 延迟触发事件调度器：按 tick 触发待执行的事件列表。
    /// </summary>
    public class TriggerEventScheduler : MapComponent
    {
        private List<TriggerEventSchedulerEntry> pending = new List<TriggerEventSchedulerEntry>();

        public TriggerEventScheduler(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "pendingTriggerEvents", LookMode.Deep, Array.Empty<object>());
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                pending?.RemoveAll(e => e == null);
        }

        public override void MapComponentTick()
        {
            if (pending == null || pending.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].fireTick <= now)
                {
                    CompAerialSupportEffect_TriggerEvent.FireIncidents(map, pending[i].incidentDefNames);
                    pending.RemoveAt(i);
                }
            }
        }

        public void Schedule(int fireTick, List<string> incidentDefNames)
        {
            if (pending == null) pending = new List<TriggerEventSchedulerEntry>();
            pending.Add(new TriggerEventSchedulerEntry { fireTick = fireTick, incidentDefNames = incidentDefNames ?? new List<string>() });
        }
    }

    /// <summary>
    /// 单条延迟触发条目（可序列化）
    /// </summary>
    public class TriggerEventSchedulerEntry : IExposable
    {
        public int fireTick;
        public List<string> incidentDefNames = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref fireTick, "fireTick");
            Scribe_Collections.Look(ref incidentDefNames, "incidentDefNames", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && incidentDefNames == null)
                incidentDefNames = new List<string>();
        }
    }
}
