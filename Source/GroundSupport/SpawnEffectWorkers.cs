using System;
using RimWorld;
using Verse;

namespace DMS_Legion.GroundSupport
{
    /// <summary>
    /// 飞机出现时：仅负责静音（不播放任何音效）。在一段时间内将除 allowedSoundDefName 外的所有音效的 ContextVolumeMultiplier 置 0；
    /// 耳鸣由白屏组件在到达时播放。通过 Harmony 对 Sample.get_ContextVolumeMultiplier 的 Postfix 实现（方案 A，不修改 Prefs）。
    /// </summary>
    public class SpawnEffectWorker_MuteExceptTinnitus : SpawnEffectWorker
    {
        public override void ExecuteEffect(IntVec3 spawnPos, AerialSupportTypeDef supportType, Map map)
        {
            if (properties == null) return;
            int durationTicks = properties.durationTicks > 0 ? properties.durationTicks : 300;
            string allowed = string.IsNullOrEmpty(properties.allowedSoundDefName)
                ? "DMSL_AerialRaid_NuclearTinnitus"
                : properties.allowedSoundDefName;
            SoundMuteState.StartMute(Find.TickManager.TicksGame + durationTicks, allowed);

            // 背景音乐：调用原版 API 立即停止并静音 durationTicks 对应时长，结束后自动播下一首
            float durationSec = durationTicks / 60f;
            Find.MusicManagerPlay?.ForceFadeoutAndSilenceFor(durationSec, 0f, true);
        }
    }

    /// <summary>
    /// 全局静音状态：在 muteUntilTick 之前，除 allowedSoundDefName 外的所有音效的 ContextVolumeMultiplier 被置 0（方案 A）。
    /// </summary>
    public static class SoundMuteState
    {
        private static int muteUntilTick;
        private static string allowedSoundDefName = "";

        public static void StartMute(int endTick, string allowedDefName)
        {
            muteUntilTick = endTick;
            allowedSoundDefName = allowedDefName ?? "";
        }

        /// <summary>当前是否处于静音期且应拦截该音效（即：在静音期内且 defName 不是允许的）</summary>
        public static bool ShouldBlockSound(SoundDef? soundDef)
        {
            try
            {
                if (soundDef == null) return false;
                // 从未启动过“静音除耳鸣外”（未进存档或未触发核打击等）：直接放行，不访问 Find/Current，避免主菜单等场景报错
                if (muteUntilTick <= 0) return false;
                // 未进入存档（主菜单、存档加载中等）时不拦截
                if (Current.Game == null || Find.TickManager == null) return false;
                if (Find.TickManager.TicksGame >= muteUntilTick) return false;
                if (string.IsNullOrEmpty(allowedSoundDefName)) return true;
                return !string.Equals(soundDef.defName, allowedSoundDefName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}