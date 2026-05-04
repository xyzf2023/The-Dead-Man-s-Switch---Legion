// ============================================================================
// 拉斐尔「额外任务」独立计时：每 intervalTicks 触发一次白名单任务
// ============================================================================

using System;
using System.Linq;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 记录拉斐尔“额外任务”组件上次触发的 tick，用于按固定 tick 间隔触发。
    /// </summary>
    public class DMSL_GameComponent_RaphaelExtraQuest : GameComponent
    {
        private const string CompId = "DMSL_RaphaelExtraQuest";

        /// <summary>上次触发时的 TicksGame</summary>
        public int lastFireTick = -1;

        public DMSL_GameComponent_RaphaelExtraQuest(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastFireTick, "lastFireTick_" + CompId, -1);
        }

        /// <summary>
        /// 安全调用 QuestScriptDef.CanRun。若任务脚本引用了不存在的 Def（如其他 mod 的 ThingDef 未加载），
        /// 会抛错或触发 Log.Error，此处捕获并返回 false，避免因其它 mod 的坏 def 导致崩溃。
        /// </summary>
        public static bool SafeCanRun(QuestScriptDef def, float points, IIncidentTarget target)
        {
            try
            {
                return def != null && def.CanRun(points, target);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>是否已过 intervalTicks 可再次触发</summary>
        public bool ShouldFireNow(int intervalTicks)
        {
            if (intervalTicks <= 0) return false;
            int now = Find.TickManager.TicksGame;
            if (lastFireTick < 0) return now >= intervalTicks;
            return now - lastFireTick >= intervalTicks;
        }

        /// <summary>标记本次已触发</summary>
        public void MarkFired()
        {
            lastFireTick = Find.TickManager.TicksGame;
        }

        /// <summary>获取或创建组件，保证存档中有且仅有一个实例。Game 未就绪时返回 null。</summary>
        public static DMSL_GameComponent_RaphaelExtraQuest? GetOrCreate()
        {
            Game game = Current.Game;
            if (game == null) return null;
            var comp = game.components.OfType<DMSL_GameComponent_RaphaelExtraQuest>().FirstOrDefault();
            if (comp == null)
            {
                comp = new DMSL_GameComponent_RaphaelExtraQuest(game);
                game.components.Add(comp);
            }
            return comp;
        }
    }
}
