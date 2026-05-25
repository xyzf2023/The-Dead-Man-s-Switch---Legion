using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DMS_Legion.AXF12
{
    /// <summary>
    /// 延迟到下一帧（或下一 GUI 帧）执行的 Action 队列。用于多点轰炸等在 Targeter 结束后再启动下一轮选点，
    /// 避免在 actionWhenFinished 调用链内直接 BeginTargeting 导致选点器被覆盖。
    /// GameComponentUpdate 与 GameComponentOnGUI 均会排空队列，以便暂停状态下 UI 仍能推进。
    /// </summary>
    public class AXF12DeferredActionComponent : GameComponent
    {
        private static readonly Queue<Action> Pending = new Queue<Action>();

        public AXF12DeferredActionComponent(Game game)
        {
        }

        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            Pending.Enqueue(action);
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            Game? game = Current.Game;
            if (game == null)
            {
                return;
            }

            if (game.GetComponent<AXF12DeferredActionComponent>() != null)
            {
                return;
            }

            game.components.Add(new AXF12DeferredActionComponent(game));
        }

        public override void GameComponentUpdate()
        {
            DrainQueue();
        }

        public override void GameComponentOnGUI()
        {
            DrainQueue();
        }

        private static int lastProcessedFrame = -1;

        /// <summary>每 Unity 帧最多执行队列中一项，避免 Update 与 OnGUI 同帧重复排空。</summary>
        private static void DrainQueue()
        {
            int frame = Time.frameCount;
            if (frame == lastProcessedFrame)
            {
                return;
            }

            lastProcessedFrame = frame;
            if (Pending.Count > 0)
            {
                Pending.Dequeue()?.Invoke();
            }
        }
    }
}
