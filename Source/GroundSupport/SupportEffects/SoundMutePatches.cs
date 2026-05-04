using HarmonyLib;
using Verse;
using Verse.Sound;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.GroundSupport.SupportEffects
{
    /// <summary>
    /// 方案 A：静音期内将“非允许音效”的 ContextVolumeMultiplier 置 0，允许的音效保留原版乘数（仍受总音量、对应档位音量控制）。
    /// 不修改 Prefs，不拦截播放；仅改乘数，其它音效×0，我们的音效走原版逻辑。
    /// </summary>
    [HarmonyPatch(typeof(Sample), "ContextVolumeMultiplier", MethodType.Getter)]
    public static class SampleContextVolumeMutePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Sample __instance, ref float __result)
        {
            if (__instance?.subDef?.parentDef == null) return;
            if (!SoundMuteState.ShouldBlockSound(__instance.subDef.parentDef)) return;
            __result = 0f;
        }
    }
}
