using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 启动时为所有 Pawn 种族 Def 动态注入体型近战加伤逻辑组件，使任意 Pawn 受击时均可参与体型差判定。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BodySizeMeleeBonusInjector
    {
        static BodySizeMeleeBonusInjector()
        {
            InjectToAllPawnRaces();
        }

        private static void InjectToAllPawnRaces()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.race == null)
                    continue;
                if (def.comps == null)
                    def.comps = new List<CompProperties>();
                if (def.comps.Any(c => c.compClass == typeof(Comp_BodySizeMeleeBonus)))
                    continue;
                def.comps.Add(new CompProperties_BodySizeMeleeBonus());
            }
        }
    }
}
