// ============================================================================
// 半数倒地支援请求：每 600 tick 检测（仅拉斐尔），满足则发「未知讯号」信
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DMS_Legion.Incidents.UnknownMechSupport
{
    /// <summary>
    /// 仅当叙事者为拉斐尔时，根据「负面信件」触发未知机兵支援请求；每张地图 30000 tick 冷却一次。
    /// </summary>
    public class DMSL_GameComponent_HalfDownedLetter : GameComponent
    {
        private const int CheckIntervalTicks = 600;
        internal const int LetterCooldownTicks = 30000;
        internal const string StorytellerDefName = "DMSL_Storyteller_Raphael";

        /// <summary>
        /// 从检测到负面信件到真正发送「未知讯号」自定义信件之间的延迟。
        /// </summary>
        private const int DelayTicksAfterNegativeLetter = 120;

        private struct PendingLetter
        {
            public Map map;
            public int sendTick;
        }

        private readonly List<PendingLetter> pendingLetters = new List<PendingLetter>();

        public DMSL_GameComponent_HalfDownedLetter(Game game) { }

        public override void GameComponentTick()
        {
            // 触发逻辑主要由 LetterStack.ReceiveLetter 的 Harmony Patch 负责；
            // 此处仅用于在检测到负面信件后，延迟若干 tick 再实际发送「未知讯号」信件。
            if (pendingLetters.Count == 0)
                return;

            int now = Find.TickManager.TicksGame;
            for (int i = pendingLetters.Count - 1; i >= 0; i--)
            {
                PendingLetter pending = pendingLetters[i];

                if (pending.map == null || !Find.Maps.Contains(pending.map) || !pending.map.IsPlayerHome)
                {
                    pendingLetters.RemoveAt(i);
                    continue;
                }

                if (now >= pending.sendTick)
                {
                    TrySendLetterForMap(pending.map);
                    pendingLetters.RemoveAt(i);
                }
            }
        }

        internal static void EnqueueDelayedLetter(Map map)
        {
            if (map == null || !map.IsPlayerHome)
                return;

            Game game = Current.Game;
            if (game == null)
                return;

            var comp = game.GetComponent<DMSL_GameComponent_HalfDownedLetter>();
            if (comp == null)
                return;

            int sendTick = Find.TickManager.TicksGame + DelayTicksAfterNegativeLetter;
            comp.pendingLetters.Add(new PendingLetter
            {
                map = map,
                sendTick = sendTick
            });
        }

        internal static void TrySendLetterForMap(Map map)
        {
            if (map == null || !map.IsPlayerHome)
                return;

            // 未开启未知机兵支援开关时，不发送信件
            var settings = DMS_Legion.DMSL_ModSettings.settings;
            if (settings == null || !settings.enableUnknownMechSupport)
                return;

            // 若未解除叙事者限制，则仅在拉斐尔叙事者下发送
            if (!settings.unknownMechNoStorytellerLimit)
            {
                if (Find.Storyteller?.def?.defName != StorytellerDefName)
                    return;
            }

            var state = map.GetComponent<DMSL_MapComponent_HalfDownedLetterState>();
            if (state == null)
            {
                state = new DMSL_MapComponent_HalfDownedLetterState(map);
                map.components.Add(state);
            }
            if (!state.CanSendNow(LetterCooldownTicks))
                return;

            var letter = new ChoiceLetter_UnknownSignal(map);
            letter.Send();
            state.MarkSent();
        }
    }
}
