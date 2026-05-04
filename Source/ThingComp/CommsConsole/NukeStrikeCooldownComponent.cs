// ============================================================================
// 文件：NukeStrikeCooldownComponent.cs
// 说明：核打击系统冷却 GameComponent，调度核打击时启动 900000 tick 冷却
// 功能：ExposeData 持久化；供 NukeStrikeConnectWindow 判断是否显示“准备流程尚未结束”及隐藏执行权限认证按钮
// ============================================================================

using System.Linq;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 核打击系统冷却：每次调度核打击时开始 900000 tick 冷却，冷却期间打开 UI 显示准备中文案且不显示执行权限认证按钮。
    /// </summary>
    public class NukeStrikeCooldownComponent : GameComponent
    {
        private static NukeStrikeCooldownComponent? _instance;
        public static NukeStrikeCooldownComponent? Instance => _instance;

        /// <summary>获取或创建全局冷却组件（若游戏未自动创建则手动加入）</summary>
        public static NukeStrikeCooldownComponent? GetOrCreate()
        {
            Game? game = Current.Game;
            if (game == null) return null;
            var comp = game.components.OfType<NukeStrikeCooldownComponent>().FirstOrDefault();
            if (comp == null)
            {
                comp = new NukeStrikeCooldownComponent(game);
                game.components.Add(comp);
            }
            return comp;
        }

        /// <summary>冷却结束时的游戏 tick；0 表示未在冷却</summary>
        private int _cooldownEndTick;

        /// <summary>冷却时长（tick）</summary>
        public const int CooldownTicks = 900000;

        public NukeStrikeCooldownComponent(Game game)
        {
            _instance = this;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            _instance = this;
        }

        /// <summary>
        /// 开始冷却（在 ScheduleNukeStrike 时调用）
        /// </summary>
        public void StartCooldown()
        {
            _cooldownEndTick = Find.TickManager.TicksGame + CooldownTicks;
        }

        /// <summary>
        /// 剩余冷却 tick；0 表示未在冷却或已结束。若在冷却中则返回 &gt; 0。
        /// </summary>
        public int GetRemainingCooldownTicks()
        {
            if (_cooldownEndTick <= 0) return 0;
            int remaining = _cooldownEndTick - Find.TickManager.TicksGame;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>
        /// 清除冷却（调试用），使核打击系统立即可用。
        /// </summary>
        public void ClearCooldown()
        {
            _cooldownEndTick = 0;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _cooldownEndTick, "nukeStrikeCooldownEndTick", 0);
        }
    }
}
