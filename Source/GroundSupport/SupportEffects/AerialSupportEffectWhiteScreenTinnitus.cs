using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 空中支援效果：白屏 + 耳鸣（类似歼星武器到达结局）
    /// 仅当触发时玩家视角在执行地图上时：白屏渐显 → 保持 → 渐隐；同时禁用游戏音频一段时间并播放指定音效。
    /// </summary>
    public class CompProperties_AerialSupportEffect_WhiteScreenTinnitus : CompProperties
    {
        /// <summary>白屏渐显持续时间（tick）</summary>
        public int whiteScreenFadeInTicks = 60;

        /// <summary>白屏到达最大程度后的保持时间（tick）</summary>
        public int whiteScreenHoldTicks = 180;

        /// <summary>白屏渐隐持续时间（tick）</summary>
        public int whiteScreenFadeOutTicks = 120;

        /// <summary>播放的音效 defName，默认 DMSL_AerialRaid_NuclearTinnitus（静音由 SpawnEffect 在贴图出现时触发）</summary>
        public string soundDefDefName = "DMSL_AerialRaid_NuclearTinnitus";

        public CompProperties_AerialSupportEffect_WhiteScreenTinnitus()
        {
            compClass = typeof(CompAerialSupportEffect_WhiteScreenTinnitus);
        }
    }

    /// <summary>
    /// 空中支援效果组件：白屏 + 耳鸣（供渲染器反射调用）
    /// </summary>
    public class CompAerialSupportEffect_WhiteScreenTinnitus : ThingComp
    {
        public CompProperties_AerialSupportEffect_WhiteScreenTinnitus Props => (CompProperties_AerialSupportEffect_WhiteScreenTinnitus)props;

        /// <summary>
        /// 执行效果（静态）：若当前玩家视角在执行地图上，则启动白屏与静音+耳鸣序列。
        /// </summary>
        public static void ExecuteEffect(IntVec3 targetPos, AerialSupportTypeDef supportType, Map map, CompProperties_AerialSupportEffect_WhiteScreenTinnitus props)
        {
            if (map == null || props == null)
                return;
            if (Find.CurrentMap != map)
                return;

            Game? game = Current.Game;
            WhiteScreenTinnitusController? controller = game?.GetComponent<WhiteScreenTinnitusController>();
            if (controller == null && game != null)
            {
                controller = new WhiteScreenTinnitusController(game);
                game.components.Add(controller);
            }
            controller?.StartSequence(props);
        }
    }

    /// <summary>
    /// 白屏 + 静音/耳鸣 全局控制器：持有一个活跃序列，每 tick 更新状态，每帧在 GameComponentOnGUI 中绘制白屏。
    /// </summary>
    public class WhiteScreenTinnitusController : GameComponent
    {
        private WhiteScreenTinnitusRequest? activeRequest;

        public WhiteScreenTinnitusController(Game game) : base() { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref activeRequest, "activeWhiteScreenTinnitusRequest");
        }

        public override void GameComponentTick()
        {
            if (activeRequest == null) return;
            if (activeRequest.Tick())
                activeRequest = null;
        }

        public override void GameComponentOnGUI()
        {
            if (activeRequest == null) return;
            activeRequest.Draw();
        }

        public void StartSequence(CompProperties_AerialSupportEffect_WhiteScreenTinnitus props)
        {
            activeRequest = new WhiteScreenTinnitusRequest(props);
        }
    }

    /// <summary>
    /// 单次白屏+耳鸣请求：记录开始 tick，按 fadeIn / hold / fadeOut 计算当前 alpha 并绘制；到达时播放耳鸣音效（静音由 SpawnEffect 在贴图出现时触发）。
    /// </summary>
    public class WhiteScreenTinnitusRequest : IExposable
    {
        private int startTick;
        private int fadeInTicks;
        private int holdTicks;
        private int fadeOutTicks;
        private string? soundDefDefName;
        private bool soundPlayed;

        public WhiteScreenTinnitusRequest() { }

        public WhiteScreenTinnitusRequest(CompProperties_AerialSupportEffect_WhiteScreenTinnitus props)
        {
            startTick = Find.TickManager.TicksGame;
            fadeInTicks = props.whiteScreenFadeInTicks > 0 ? props.whiteScreenFadeInTicks : 60;
            holdTicks = props.whiteScreenHoldTicks >= 0 ? props.whiteScreenHoldTicks : 180;
            fadeOutTicks = props.whiteScreenFadeOutTicks > 0 ? props.whiteScreenFadeOutTicks : 120;
            soundDefDefName = string.IsNullOrEmpty(props.soundDefDefName) ? "DMSL_AerialRaid_NuclearTinnitus" : props.soundDefDefName;
            soundPlayed = false;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref fadeInTicks, "fadeInTicks", 60);
            Scribe_Values.Look(ref holdTicks, "holdTicks", 180);
            Scribe_Values.Look(ref fadeOutTicks, "fadeOutTicks", 120);
            Scribe_Values.Look(ref soundDefDefName, "soundDefDefName", "DMSL_AerialRaid_NuclearTinnitus");
            Scribe_Values.Look(ref soundPlayed, "soundPlayed", false);
        }

        /// <summary>返回 true 表示序列已结束，可移除</summary>
        public bool Tick()
        {
            int elapsed = Find.TickManager.TicksGame - startTick;
            if (!soundPlayed)
            {
                soundPlayed = true;
                SoundDef? def = DefDatabase<SoundDef>.GetNamedSilentFail(soundDefDefName ?? "DMSL_AerialRaid_NuclearTinnitus");
                if (def != null)
                {
                    try { SoundStarter.PlayOneShotOnCamera(def, null); }
                    catch { }
                }
            }

            int totalTicks = fadeInTicks + holdTicks + fadeOutTicks;
            return elapsed >= totalTicks;
        }

        /// <summary>根据当前 tick 计算白屏 alpha (0~1)，并在 OnGUI 中绘制全屏白底</summary>
        public void Draw()
        {
            int elapsed = Find.TickManager.TicksGame - startTick;
            int totalTicks = fadeInTicks + holdTicks + fadeOutTicks;
            if (elapsed >= totalTicks) return;

            float alpha;
            if (elapsed < fadeInTicks)
                alpha = fadeInTicks > 0 ? (float)elapsed / fadeInTicks : 1f;
            else if (elapsed < fadeInTicks + holdTicks)
                alpha = 1f;
            else
                alpha = fadeOutTicks > 0 ? 1f - (float)(elapsed - fadeInTicks - holdTicks) / fadeOutTicks : 0f;

            if (alpha <= 0f) return;

            Rect fullScreen = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(fullScreen, BaseContent.WhiteTex);
            GUI.color = Color.white;
        }
    }
}
