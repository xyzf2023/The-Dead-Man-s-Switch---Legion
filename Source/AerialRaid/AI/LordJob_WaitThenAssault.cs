using RimWorld;
using Verse;
using Verse.AI.Group;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 等待然后进攻的 LordJob
    /// 流程：等待（DefendPoint）→ 进攻（AssaultColony）
    /// 等待时间由构造函数参数指定
    /// </summary>
    public class LordJob_WaitThenAssault : LordJob
    {
        private Faction? faction;
        private IntVec3 waitSpot;
        private int waitTicks;

        /// <summary>
        /// 构造函数（用于存档加载）
        /// </summary>
        public LordJob_WaitThenAssault()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="faction">派系</param>
        /// <param name="waitSpot">等待位置</param>
        /// <param name="waitTicks">等待时间（tick）</param>
        public LordJob_WaitThenAssault(Faction faction, IntVec3 waitSpot, int waitTicks)
        {
            this.faction = faction;
            this.waitSpot = waitSpot;
            this.waitTicks = waitTicks;
        }

        /// <summary>
        /// 创建状态图
        /// 流程：到达等待位置 → 等待 → 发起进攻
        /// </summary>
        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();

            // 验证等待位置是否有效
            if (!waitSpot.IsValid || waitTicks <= 0)
            {
                Log.Error($"[DMS_Legion]空袭LordJob：waitSpot无效或waitTicks<=0，waitSpot={waitSpot}，waitTicks={waitTicks}，直接进入进攻状态");
                // 如果参数无效，直接进入进攻状态（避免卡住）
                LordToil_AssaultColony fallbackAssaultToil = new LordToil_AssaultColony(false);
                fallbackAssaultToil.useAvoidGrid = false;
                graph.AddToil(fallbackAssaultToil);
                graph.StartingToil = fallbackAssaultToil;
                return graph;
            }

            // 阶段1：到达等待位置（使用Travel LordJob的子图）
            // 这样可以确保敌人先移动到等待位置
            LordToil? travelStartingToil = null;
            try
            {
                // 验证waitSpot是否在lord的地图范围内（如果lord已初始化）
                Map? lordMap = null;
                if (this.lord != null && this.lord.Map != null)
                {
                    lordMap = this.lord.Map;
                    if (!waitSpot.InBounds(lordMap))
                    {
                        Log.Warning($"[DMS_Legion]空袭LordJob：waitSpot不在地图范围内，waitSpot={waitSpot}，直接进入等待状态");
                    }
                }
                
                var travelJob = new LordJob_Travel(waitSpot);
                var travelSubgraph = graph.AttachSubgraph(travelJob.CreateGraph());
                travelStartingToil = travelSubgraph?.StartingToil;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DMS_Legion]空袭LordJob：创建Travel子图失败：{ex.Message}，堆栈：{ex.StackTrace}，直接进入等待状态");
            }

            // 阶段2：等待状态（DefendPoint）
            LordToil_DefendPoint waitToil = new LordToil_DefendPoint(waitSpot, 3f);
            waitToil.useAvoidGrid = false;
            graph.AddToil(waitToil);

            // 阶段3：进攻状态（AssaultColony）
            LordToil_AssaultColony assaultToil = new LordToil_AssaultColony(false); // false = canTimeoutOrFlee
            assaultToil.useAvoidGrid = false;
            graph.AddToil(assaultToil);

            // 如果Travel子图创建失败，直接从等待状态开始
            if (travelStartingToil == null)
            {
                graph.StartingToil = waitToil;
            }
            else
            {
                // 转换1：到达等待位置后开始等待
                Transition travelToWait = new Transition(travelStartingToil, waitToil);
                travelToWait.AddTrigger(new Trigger_Memo("TravelArrived"));
                // 如果超过5秒还没到达，也强制进入等待状态（防止卡住）
                travelToWait.AddTrigger(new Trigger_TicksPassed(300));
                graph.AddTransition(travelToWait);
                
                // 设置起始状态为移动到等待位置
                graph.StartingToil = travelStartingToil;
            }

            // 转换2：等待时间到了就进攻
            Transition waitToAssault = new Transition(waitToil, assaultToil);
            waitToAssault.AddTrigger(new Trigger_TicksPassed(waitTicks));
            
            // 添加消息（如果faction有效）
            if (faction != null && faction.def != null)
            {
                waitToAssault.AddPreAction(new TransitionAction_Message(
                    "DMSL_AerialRaid_AssaultStarted".Translate(),
                    MessageTypeDefOf.ThreatBig, null, 1f));
            }
            
            graph.AddTransition(waitToAssault);

            return graph;
        }

        /// <summary>
        /// 存档/加载数据
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref faction, "faction", false);
            Scribe_Values.Look(ref waitSpot, "waitSpot", IntVec3.Invalid, false);
            Scribe_Values.Look(ref waitTicks, "waitTicks", 0, false);
        }
    }
}
