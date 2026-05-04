using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion.Incidents.UnknownMechSupport
{
    /// <summary>
    /// 在任意信件进入 LetterStack 时，检测是否为负面信件；
    /// 若是且满足叙事者与冷却条件，则对对应地图发送「未知讯号」信。
    /// </summary>
    [HarmonyPatch(typeof(LetterStack))]
    [HarmonyPatch(nameof(LetterStack.ReceiveLetter))]
    [HarmonyPatch(new Type[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class LetterStack_ReceiveLetter_HalfDownedLetter_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Letter let)
        {
            TryTriggerUnknownSignalFromNegativeLetter(let);
        }

        private static void TryTriggerUnknownSignalFromNegativeLetter(Letter let)
        {
            if (let == null)
                return;

            // 仅在游戏正常进行时工作
            if (Current.ProgramState != ProgramState.Playing)
                return;

            // 读取 MOD 设置：未开启未知机兵支援时，直接跳过
            var settings = DMS_Legion.DMSL_ModSettings.settings;
            if (settings == null || !settings.enableUnknownMechSupport)
                return;

            // 若未解除叙事者限制，则仅在拉斐尔叙事者时工作
            if (!settings.unknownMechNoStorytellerLimit)
            {
                if (Find.Storyteller?.def?.defName != DMSL_GameComponent_HalfDownedLetter.StorytellerDefName)
                    return;
            }

            // 只对负面事件信件起效
            if (!IsNegativeLetter(let))
                return;

            Map? map = null;

            // 优先使用信件自带的目标地图
            if (let.lookTargets.IsValid())
            {
                GlobalTargetInfo primary = let.lookTargets.PrimaryTarget;
                if (primary.IsValid && primary.Map != null)
                {
                    map = primary.Map;
                }
            }

            // 若没有目标地图，则使用当前玩家地图
            if (map == null)
                map = Find.CurrentMap;

            if (map == null || !map.IsPlayerHome)
                return;

            // 在检测到负面信件后，不立刻发送自定义信件，而是加入一个 120 tick 延迟队列。
            DMSL_GameComponent_HalfDownedLetter.EnqueueDelayedLetter(map);
        }

        private static bool IsNegativeLetter(Letter letter)
        {
            LetterDef def = letter.def;
            if (def == null)
                return false;

            // “红色信封”判断（基于原版 LetterDef 定义）：
            return def == LetterDefOf.ThreatBig
                   || def == LetterDefOf.ThreatSmall
                   || def == LetterDefOf.Bossgroup;
        }
    }
}

