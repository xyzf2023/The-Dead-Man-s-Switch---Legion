// ============================================================================
// 确保半数倒地支援请求的 GameComponent 在游戏初始化时被加入
// ============================================================================

using HarmonyLib;
using Verse;

namespace DMS_Legion.Incidents.UnknownMechSupport
{
    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    public static class Game_FinalizeInit_HalfDownedLetter_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Game __instance)
        {
            if (__instance?.components == null)
                return;
            for (int i = 0; i < __instance.components.Count; i++)
            {
                if (__instance.components[i] is DMSL_GameComponent_HalfDownedLetter)
                    return;
            }
            __instance.components.Add(new DMSL_GameComponent_HalfDownedLetter(__instance));
        }
    }
}
