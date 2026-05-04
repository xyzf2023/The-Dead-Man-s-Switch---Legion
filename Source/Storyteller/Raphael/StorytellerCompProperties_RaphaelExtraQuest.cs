// ============================================================================
// 拉斐尔叙事者组件属性：独立计时，每 intervalTicks 额外生成一个的随机任务
// ============================================================================

using System.Collections.Generic;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 拉斐尔“额外任务”叙事者组件的 XML 可配置属性。
    /// </summary>
    public class StorytellerCompProperties_RaphaelExtraQuest : RimWorld.StorytellerCompProperties
    {
        /// <summary>触发间隔（tick），60000 = 1 游戏日</summary>
        public int intervalTicks = 60000;

        /// <summary>开局至少经过多少天后才开始</summary>
        public new float minDaysPassed;

        /// <summary>
        /// 可选：任务脚本 defName 白名单。若不为空，则仅在此列表中按条件随机选择 QuestScriptDef；
        /// 若为空，则退回到“所有的自然随机任务池”逻辑。
        /// </summary>
        public List<string> questDefs = new List<string>();

        public StorytellerCompProperties_RaphaelExtraQuest()
        {
            compClass = typeof(StorytellerComp_RaphaelExtraQuest);
        }
    }
}
