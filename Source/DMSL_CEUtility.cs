using Verse;

namespace DMS_Legion
{
    public static class DMSL_CEUtility
    {
        private const string CEId = "CETeam.CombatExtended";
        private const string CEIdSteam = "CETeam.CombatExtended_steam";

        public static bool IsCEActive
        {
            get
            {
                if (ModLister.GetActiveModWithIdentifier(CEId, true) != null)
                    return true;
                if (ModLister.GetActiveModWithIdentifier(CEIdSteam, true) != null)
                    return true;
                return false;
            }
        }
    }
}
