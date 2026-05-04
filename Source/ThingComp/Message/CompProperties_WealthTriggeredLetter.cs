using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 组件属性：当殖民地财富达到指定阈值时发送一次中性事件信件（如“空袭先导袭击”介绍信）。
    /// </summary>
    public class CompProperties_WealthTriggeredLetter : CompProperties
    {
        /// <summary>财富阈值，达到后发送信件。</summary>
        public float wealthThreshold = 3000f;

        /// <summary>信件标题的翻译键。</summary>
        public string letterLabelKey = "DMSL_AerialRaidPagerIntro_LetterLabel";

        /// <summary>信件正文的翻译键。</summary>
        public string letterTextKey = "DMSL_AerialRaidPagerIntro_LetterText";

        public CompProperties_WealthTriggeredLetter()
        {
            compClass = typeof(Comp_WealthTriggeredLetter);
        }
    }
}
