using System.Collections.Generic;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 组件属性：每间隔 tick 对自身随机一个伤势回复一定血量（面向机械体等，参考 MechRepairUtility 的修理逻辑）。
    /// </summary>
    public class HediffCompProperties_SelfMechHeal : HediffCompProperties
    {
        /// <summary>
        /// 每次回复的血量（默认 5）
        /// </summary>
        public float healAmount = 5f;

        /// <summary>
        /// 回复间隔（tick），默认 60
        /// </summary>
        public int intervalTicks = 60;

        public HediffCompProperties_SelfMechHeal()
        {
            compClass = typeof(HediffComp_SelfMechHeal);
        }
    }

    /// <summary>
    /// 每 intervalTicks 对携带者随机选取一个 Hediff_Injury 回复 healAmount 点生命。
    /// </summary>
    public class HediffComp_SelfMechHeal : HediffComp
    {
        private int ticksSinceHeal;

        public HediffCompProperties_SelfMechHeal Props => (HediffCompProperties_SelfMechHeal)props;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref ticksSinceHeal, "ticksSinceHeal", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            Pawn? pawn = parent?.pawn;
            if (pawn == null || pawn.Dead)
                return;

            ticksSinceHeal++;
            if (ticksSinceHeal < Props.intervalTicks)
                return;

            ticksSinceHeal = 0;

            List<Hediff_Injury>? injuries = null;
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h is Hediff_Injury inj)
                {
                    if (injuries == null)
                        injuries = new List<Hediff_Injury>();
                    injuries.Add(inj);
                }
            }

            if (injuries == null || injuries.Count == 0)
                return;

            Hediff_Injury toHeal = injuries.RandomElement();
            float amount = UnityEngine.Mathf.Min(Props.healAmount, toHeal.Severity);
            if (amount <= 0f)
                return;

            toHeal.Heal(amount);
        }
    }
}
