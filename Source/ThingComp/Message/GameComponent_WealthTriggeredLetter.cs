using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 游戏级组件：当任意玩家殖民地财富首次达到 3000 时发送一次“空袭先导袭击”中性信；
    /// 当任意玩家殖民地财富达到 3000 且与 DMS_Army 首次结盟时发送一次“来自武装殖民舰队的讯息”正面信。
    /// 每个存档每种信只发送一次（参考原版 GameComponent_OnetimeNotification / 异象 DLC 介绍信逻辑），
    /// 避免奥德赛逆重飞船等多殖民地场景下每次到达新殖民地重复收信。
    /// </summary>
    public class GameComponent_WealthTriggeredLetter : GameComponent
    {
        private const float WealthThreshold = 3000f;
        private const string PagerIntroLabelKey = "DMSL_AerialRaidPagerIntro_LetterLabel";
        private const string PagerIntroTextKey = "DMSL_AerialRaidPagerIntro_LetterText";
        private const string ArmyAllyLabelKey = "DMSL_ArmyAllyIntro_LetterLabel";
        private const string ArmyAllyTextKey = "DMSL_ArmyAllyIntro_LetterText";

        /// <summary> 本存档是否已发过“空袭先导”介绍信（整局只发一次）。 </summary>
        private bool sentPagerIntro;
        /// <summary> 本存档是否已发过“武装殖民舰队结盟”介绍信（整局只发一次）。 </summary>
        private bool sentArmyAlly;

        public GameComponent_WealthTriggeredLetter(Game game) { }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;
            if (Find.TickManager.TicksGame % 7500 != 0)
                return;

            Map? firstQualifyingMap = GetFirstPlayerHomeMapWithWealthAtLeast(WealthThreshold);
            if (firstQualifyingMap == null)
                return;

            if (!sentPagerIntro)
            {
                sentPagerIntro = true;
                Find.LetterStack.ReceiveLetter(
                    PagerIntroLabelKey.Translate(),
                    PagerIntroTextKey.Translate(),
                    LetterDefOf.NeutralEvent,
                    new TargetInfo(firstQualifyingMap.Center, firstQualifyingMap));
            }

            Faction? dmsArmy = GetDmsArmyFaction();
            if (!sentArmyAlly
                && dmsArmy != null
                && dmsArmy.PlayerRelationKind == FactionRelationKind.Ally)
            {
                sentArmyAlly = true;
                Find.LetterStack.ReceiveLetter(
                    ArmyAllyLabelKey.Translate(),
                    ArmyAllyTextKey.Translate(),
                    LetterDefOf.PositiveEvent,
                    new TargetInfo(firstQualifyingMap.Center, firstQualifyingMap));
            }
        }

        /// <summary>
        /// 立即检查一次是否满足“武装殖民舰队结盟信”的发送条件并发送（用于在好感度/关系变化时补检，避免只依赖定时检测）。
        /// 每个存档只发送一次。
        /// </summary>
        public void TrySendArmyAllyLetterNow()
        {
            if (Current.ProgramState != ProgramState.Playing || sentArmyAlly)
                return;
            Faction? dmsArmy = GetDmsArmyFaction();
            if (dmsArmy == null || dmsArmy.PlayerRelationKind != FactionRelationKind.Ally)
                return;
            Map? map = GetFirstPlayerHomeMapWithWealthAtLeast(WealthThreshold);
            if (map == null)
                return;
            sentArmyAlly = true;
            Find.LetterStack.ReceiveLetter(
                ArmyAllyLabelKey.Translate(),
                ArmyAllyTextKey.Translate(),
                LetterDefOf.PositiveEvent,
                new TargetInfo(map.Center, map));
        }

        /// <summary> 返回第一个财富达到阈值的玩家家园地图（用于信件 lookTarget），若无则 null。 </summary>
        private static Map? GetFirstPlayerHomeMapWithWealthAtLeast(float threshold)
        {
            for (int i = 0; i < Current.Game.Maps.Count; i++)
            {
                Map map = Current.Game.Maps[i];
                if (map != null && map.IsPlayerHome && map.wealthWatcher.WealthTotal >= threshold)
                    return map;
            }
            return null;
        }

        private static Faction? GetDmsArmyFaction()
        {
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (f?.def != null && f.def.defName == "DMS_Army")
                    return f;
            }
            return null;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sentPagerIntro, "sentPagerIntro", false);
            Scribe_Values.Look(ref sentArmyAlly, "sentArmyAlly", false);
        }
    }
}
