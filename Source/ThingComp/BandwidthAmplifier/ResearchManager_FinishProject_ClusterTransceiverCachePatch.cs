// ============================================================================
// 仅在「完成科研」时检测本次完成的是否为集群收发器，若是则更新缓存，不做多余检测。
// 并在游戏初始化时确保缓存组件存在，以便正确存档/读档。
// ============================================================================

using HarmonyLib;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    [HarmonyPatch(new[] { typeof(ResearchProjectDef), typeof(bool), typeof(Pawn), typeof(bool) })]
    public static class ResearchManager_FinishProject_ClusterTransceiverCachePatch
    {
        private const string ResearchDefName = "DMS_Mechlink";

        [HarmonyPostfix]
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null || proj.defName != ResearchDefName)
                return;
            var comp = DMSL_GameComponent_ClusterTransceiverResearchCache.GetOrCreate();
            if (comp == null || comp.ClusterTransceiverCompleted)
                return;
            comp.MarkClusterTransceiverCompleted();
        }
    }

    /// <summary>确保集群收发器研究缓存在新游戏/读档后即存在并参与存档，避免首次使用才创建。</summary>
    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    public static class Game_FinalizeInit_ClusterTransceiverCachePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            DMSL_GameComponent_ClusterTransceiverResearchCache.GetOrCreate();
        }
    }
}
