using HarmonyLib;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// Harmony补丁初始化 - 自动扫描并应用所有Harmony补丁
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("DMS_Legion");
            
            // 应用所有补丁
            harmony.PatchAll();
            
            Log.Message("[DMS_Legion] Harmony补丁已加载");
        }
    }

}

