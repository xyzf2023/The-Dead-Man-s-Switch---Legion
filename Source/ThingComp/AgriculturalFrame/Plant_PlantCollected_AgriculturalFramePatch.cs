// ============================================================================
// 文件：Plant_PlantCollected_AgriculturalFramePatch.cs
// 说明：仅在“每完成一株植物收获/割除”时触发一次，属低频逻辑，用于农业框架额外干草掉落
// ============================================================================

using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 钩子：Plant.PlantCollected。每次收获或割除完成时调用一次，由该 Pawn 的 CompAgriculturalFrameHay 生成额外干草。
    /// </summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    [HarmonyPatch(new[] { typeof(Pawn), typeof(PlantDestructionMode) })]
    public static class Plant_PlantCollected_AgriculturalFramePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Plant __instance, Pawn by, PlantDestructionMode plantDestructionMode)
        {
            if (by == null || !by.Spawned)
                return;

            CompAgriculturalFrameHay comp = by.TryGetComp<CompAgriculturalFrameHay>();
            if (comp == null)
                return;

            Map map = __instance?.Map ?? by.Map;
            if (map == null)
                return;

            comp.SpawnExtraYield(by, map, __instance, plantDestructionMode);
        }
    }
}
