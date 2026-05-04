// ============================================================================
// 电子天使派系关系对齐：提供接口，被调用时将指定派系与玩家改为中立，
// 与对玩家敌对的派系改为敌对，与对玩家非敌对的派系（中立/盟友）全部改为中立。
// ============================================================================

using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 电子天使（或任意隐藏派系）与世界上各派系关系的对齐逻辑。
    /// 每次调用时：自身与玩家改为中立；与所有对玩家敌对的派系改为敌对；
    /// 与所有对玩家非敌对的派系（中立或盟友）全部改为中立（不将玩家盟友同步为盟友）。
    /// </summary>
    public static class DigitalAngelRelationAligner
    {
        /// <summary>
        /// 将对齐派系 <paramref name="self"/> 与玩家设为中立，并按其与玩家的关系对齐与其它所有派系的关系。
        /// 与玩家敌对的派系 → 与 self 设为敌对；与玩家中立或同盟的派系 → 与 self 均设为中立。
        /// </summary>
        /// <param name="self">要执行对齐的派系（例如电子天使）。若为 null 或玩家派系则直接返回。</param>
        /// <param name="sendLetters">是否在关系变化时发送敌对/关系变化信件，默认 false 避免刷屏。</param>
        public static void AlignRelations(Faction self, bool sendLetters = false)
        {
            if (self == null || Faction.OfPlayer == null || self == Faction.OfPlayer)
                return;

            // 自身与玩家派系 → 中立
            self.SetRelationDirect(Faction.OfPlayer, FactionRelationKind.Neutral, canSendHostilityLetter: sendLetters, reason: null, lookTarget: null);

            List<Faction> all = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                Faction other = all[i];
                if (other == self || other == Faction.OfPlayer)
                    continue;

                if (other.HostileTo(Faction.OfPlayer))
                    self.SetRelationDirect(other, FactionRelationKind.Hostile, canSendHostilityLetter: sendLetters, reason: null, lookTarget: null);
                else
                    self.SetRelationDirect(other, FactionRelationKind.Neutral, canSendHostilityLetter: sendLetters, reason: null, lookTarget: null);
            }
        }
    }
}
