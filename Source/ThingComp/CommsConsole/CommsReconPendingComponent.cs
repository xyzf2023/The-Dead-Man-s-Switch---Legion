// ============================================================================
// 文件：CommsReconPendingComponent.cs
// 说明：通讯台侦察待执行 GameComponent
// 功能：倒计时 300～900 tick；到期后生成地图、SetObservedMap、执行 DMSL_AerialSupport_ArmyRecon
// ============================================================================

using System.Collections.Generic;
using DMS_Legion.AXF12;
using DMS_Legion.GroundSupport;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 通讯台侦察待执行组件（GameComponent）
    /// </summary>
    public class CommsReconPendingComponent : GameComponent
    {
        private static CommsReconPendingComponent? instance;
        public static CommsReconPendingComponent? Instance => instance;

        private List<PendingRecon> pendingRecons = new List<PendingRecon>();

        private const string ArmyReconDefName = "DMSL_AerialSupport_ArmyRecon";

        public CommsReconPendingComponent(Game game)
        {
            instance = this;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            instance = this;
        }

        /// <summary>
        /// 调度侦察：remainingTicks 随机 300～900，立即发 Message
        /// </summary>
        public void Schedule(PlanetTile targetTile)
        {
            Schedule(targetTile.tileId);
        }

        public void Schedule(int targetTileId)
        {
            int remainingTicks = Rand.Range(300, 901);
            pendingRecons.Add(new PendingRecon
            {
                targetTileId = targetTileId,
                remainingTicks = remainingTicks
            });

            int seconds = Mathf.CeilToInt(remainingTicks / 60f);
            Messages.Message("DMSL_Comms_ReconEtaMessage".Translate(seconds), MessageTypeDefOf.NeutralEvent);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (pendingRecons.Count == 0)
                return;

            for (int i = pendingRecons.Count - 1; i >= 0; i--)
            {
                var entry = pendingRecons[i];
                entry.remainingTicks--;
                if (entry.remainingTicks > 0)
                {
                    pendingRecons[i] = entry;
                    continue;
                }

                pendingRecons.RemoveAt(i);
                ExecuteRecon(entry.targetTileId);
            }
        }

        private static void ExecuteRecon(int targetTileId)
        {
            MapParent? mapParent = Find.WorldObjects.MapParentAt(targetTileId);
            if (mapParent == null)
            {
                Log.Error("[DMS_Legion] CommsReconPendingComponent 目标地点没有可生成地图的世界对象。");
                return;
            }

            AXF12ReconMissionManager.Instance?.SetObservedMap(mapParent);

            if (mapParent.HasMap)
            {
                ExecuteReconOnMap(mapParent);
                return;
            }

            LongEventHandler.QueueLongEvent(
                () => ExecuteReconOnMap(mapParent),
                "GeneratingMapForNewEncounter",
                false,
                GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        private static void ExecuteReconOnMap(MapParent mapParent)
        {
            bool hadMap = mapParent.HasMap;
            Map map = mapParent.HasMap
                ? mapParent.Map
                : GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, mapParent.def);
            if (map == null)
            {
                Log.Error("[DMS_Legion] CommsReconPendingComponent 生成目标地图失败。");
                return;
            }

            AXF12ReconMissionManager.Instance?.SetObservedMap(map.Parent);

            var supportType = DefDatabase<AerialSupportTypeDef>.GetNamed(ArmyReconDefName, false);
            if (supportType == null)
            {
                Log.Error($"[DMS_Legion] CommsReconPendingComponent 未找到空中支援类型: {ArmyReconDefName}");
                return;
            }

            if (!hadMap)
            {
                CameraJumper.TryJump(map.Center, map, CameraJumper.MovementMode.Pan);
            }

            map.GetComponent<AXF12ReconSupportDelayComponent>()?
                .Schedule(map.Center, supportType.defName, clearFog: hadMap, delayTicks: 20);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingRecons, "commsReconPending", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingRecons ??= new List<PendingRecon>();
            }
        }

        private struct PendingRecon : IExposable
        {
            public int targetTileId;
            public int remainingTicks;

            public void ExposeData()
            {
                Scribe_Values.Look(ref targetTileId, "targetTileId");
                Scribe_Values.Look(ref remainingTicks, "remainingTicks");
            }
        }
    }
}
