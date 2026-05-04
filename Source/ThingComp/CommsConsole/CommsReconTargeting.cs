// ============================================================================
// 文件：CommsReconTargeting.cs
// 说明：通讯台侦察目标世界格选点
// 功能：世界格选点；选完后调度 CommsReconPendingComponent，发 Message，倒计时后生成地图并执行 ArmyRecon
// ============================================================================

using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 通讯台侦察目标选点入口
    /// </summary>
    public static class CommsReconTargeting
    {
        /// <summary>
        /// 启动世界格选点，确认后调度侦察倒计时
        /// </summary>
        public static void BeginWorldTargeting(Faction faction)
        {
            if (faction == null)
            {
                Log.Warning("[DMS_Legion] CommsReconTargeting.BeginWorldTargeting 派系无效");
                return;
            }

            TaggedString ExtraLabelGetter(GlobalTargetInfo target)
            {
                if (!target.IsValid || !target.Tile.Valid)
                    return TaggedString.Empty;
                if (!IsValidReconTarget(target.Tile, out string? reason))
                    return reason ?? "DMSL_Comms_ReconNoTargetIntel".Translate();
                return "DMSL_Comms_ReconExecuteInArea".Translate();
            }

            CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(Find.AnyPlayerHomeMap?.Parent ?? Find.CurrentMap?.Parent)));
            Find.WorldSelector.ClearSelection();
            Find.WorldTargeter.BeginTargeting(
                (GlobalTargetInfo target) => ChoseReconWorldTarget(target, faction),
                true,
                null,
                true,
                null,
                ExtraLabelGetter,
                target => IsValidReconTarget(target.Tile, out _),
                (PlanetTile?)null,
                true);
        }

        private static bool ChoseReconWorldTarget(GlobalTargetInfo target, Faction faction)
        {
            string? reason = null;
            if (!target.Tile.Valid || !IsValidReconTarget(target.Tile, out reason))
            {
                if (!string.IsNullOrWhiteSpace(reason))
                    Messages.Message(reason, MessageTypeDefOf.RejectInput);
                return false;
            }

            Find.WorldTargeter.StopTargeting();
            CommsReconPendingComponent.Instance?.Schedule(target.Tile.tileId);
            return true;
        }

        private static bool IsValidReconTarget(PlanetTile tile, out string? reason)
        {
            reason = null;
            if (tile < 0)
            {
                reason = "目标无效。".Translate();
                return false;
            }

            MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);
            if (mapParent == null)
            {
                reason = "DMSL_Comms_ReconNoTargetIntel".Translate();
                return false;
            }

            if (mapParent.def == null || !mapParent.def.canHaveMap)
            {
                reason = "DMSL_Comms_ReconNoTargetIntel".Translate();
                return false;
            }

            if (mapParent.Map?.generatorDef?.isUnderground == true)
            {
                reason = "DMSL_Comms_ReconNoTargetIntel".Translate();
                return false;
            }

            if (mapParent.Faction != null
                && mapParent.Faction != Faction.OfPlayer
                && !mapParent.Faction.HostileTo(Faction.OfPlayer))
            {
                reason = "DMSL_Comms_ReconNotHostile".Translate();
                return false;
            }

            return true;
        }
    }
}
