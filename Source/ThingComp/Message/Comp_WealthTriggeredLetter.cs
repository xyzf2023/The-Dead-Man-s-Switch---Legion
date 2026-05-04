using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 当殖民地财富值达到属性中设定的阈值时，发送一次中性事件信件，之后不再发送。
    /// </summary>
    public class Comp_WealthTriggeredLetter : ThingComp
    {
        private bool letterSent;

        public CompProperties_WealthTriggeredLetter Props => (CompProperties_WealthTriggeredLetter)props;

        public override void CompTick()
        {
            base.CompTick();
            if (letterSent)
                return;
            Map? map = parent?.Map;
            if (map == null || !parent.IsHashIntervalTick(250))
                return;
            if (map.wealthWatcher.WealthTotal < Props.wealthThreshold)
                return;
            string label = Props.letterLabelKey.Translate();
            string text = Props.letterTextKey.Translate();
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NeutralEvent, parent);
            letterSent = true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref letterSent, "letterSent", false);
        }
    }
}
