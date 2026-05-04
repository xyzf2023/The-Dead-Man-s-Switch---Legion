// ============================================================================
// 集群收发器研究完成缓存：仅在「完成科研」时检测一次是否为 DMS_Mechlink，之后只读缓存，不重复调用 ResearchManager。
// 研究完成后存档会保存该标记，读档后无需再检测。
// ============================================================================

using System.Linq;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 缓存「集群收发器」(DMS_Mechlink) 是否已完成研究；仅在研究完成时检测一次，避免每 tick/每间隔调用 ResearchManager。
    /// </summary>
    public class DMSL_GameComponent_ClusterTransceiverResearchCache : GameComponent
    {
        private const string ResearchDefName = "DMS_Mechlink";

        private bool _clusterTransceiverCompleted;

        /// <summary>集群收发器研究是否已完成（缓存，仅在研究完成时更新或创建时同步一次）。</summary>
        public bool ClusterTransceiverCompleted => _clusterTransceiverCompleted;

        public DMSL_GameComponent_ClusterTransceiverResearchCache(Game game) { }

        /// <summary>获取或创建组件；若为新建实例则根据当前存档做一次同步，之后仅依赖 FinishProject 更新。</summary>
        public static DMSL_GameComponent_ClusterTransceiverResearchCache? GetOrCreate()
        {
            Game? game = Current.Game;
            if (game == null) return null;
            var comp = game.components.OfType<DMSL_GameComponent_ClusterTransceiverResearchCache>().FirstOrDefault();
            if (comp == null)
            {
                comp = new DMSL_GameComponent_ClusterTransceiverResearchCache(game);
                game.components.Add(comp);
                EnsureCreatedAndSynced(comp);
            }
            return comp;
        }

        /// <summary>仅在研究完成时由 patch 调用，标记集群收发器已完成。</summary>
        public void MarkClusterTransceiverCompleted()
        {
            _clusterTransceiverCompleted = true;
        }

        /// <summary>创建组件时同步一次（新游戏/老存档首次加载时用当前研究状态填充，之后仅由 FinishProject 更新）。</summary>
        public void SyncFromResearchManagerOnce()
        {
            if (_clusterTransceiverCompleted) return;
            var def = DefDatabase<ResearchProjectDef>.GetNamed(ResearchDefName, false);
            if (def != null && def.IsFinished)
                _clusterTransceiverCompleted = true;
        }

        private static void EnsureCreatedAndSynced(DMSL_GameComponent_ClusterTransceiverResearchCache comp)
        {
            comp.SyncFromResearchManagerOnce();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref _clusterTransceiverCompleted, "clusterTransceiverCompleted", false);
        }
    }
}
