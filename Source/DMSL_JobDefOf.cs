using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// DMS Legion MOD的JobDef引用类
    /// </summary>
    [DefOf]
    public static class DMSL_JobDefOf
    {
        public static JobDef DMSL_AerialSupport_SelectTarget = null!;
        public static JobDef DMSL_AerialSupport_SelectCustomLine = null!;
        public static JobDef DMSL_RaidCallAirSupport = null!;
        public static JobDef DMSL_StaticDefenseDirectiveChant = null!;
        public static JobDef DMSL_Job_HighVoltageShockStrike = null!;

        static DMSL_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DMSL_JobDefOf));
        }
    }
}
