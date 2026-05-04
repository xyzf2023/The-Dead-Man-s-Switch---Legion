// ============================================================================
// 文件：CommsNukeStrikeTargeting.cs
// 说明：核打击世界格 + 地图选点入口（通讯台核打击窗口「传输打击坐标」唤起）
// 功能：关闭窗口后唤起世界地图，选世界格 → 选地图点，调度 ScheduleNukeStrike（2500~7500 tick 倒计时后执行）
// ============================================================================

using System.Collections.Generic;
using DMS_Legion.GroundSupport;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 核打击选点入口：世界格选点 → 地图选点 → 调度核打击倒计时
    /// </summary>
    public static class CommsNukeStrikeTargeting
    {
        private const string NuclearStrikeDefName = "DMSL_AerialSupport_NuclearStrike";

        /// <summary>
        /// 启动世界格选点，确认后跳转地图并启动单点选点，完成后调度核打击倒计时（2500~7500 tick）
        /// </summary>
        public static void BeginWorldTargeting(Faction faction)
        {
            if (faction == null)
            {
                Log.Warning("[DMS_Legion] CommsNukeStrikeTargeting.BeginWorldTargeting 派系无效");
                return;
            }

            var supportType = DefDatabase<AerialSupportTypeDef>.GetNamed(NuclearStrikeDefName, false);
            if (supportType == null)
            {
                Log.Error($"[DMS_Legion] CommsNukeStrikeTargeting 未找到空中支援类型: {NuclearStrikeDefName}");
                return;
            }

            TaggedString ExtraLabelGetter(GlobalTargetInfo target)
            {
                if (!target.IsValid || !target.Tile.Valid)
                    return TaggedString.Empty;
                if (!IsValidNukeTarget(target.Tile, out string? reason))
                    return reason ?? "DMSL_Comms_NoTargetVisibility".Translate();
                return "DMSL_Comms_ExecuteSupportInArea".Translate(supportType.label);
            }

            CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(Find.AnyPlayerHomeMap?.Parent ?? Find.CurrentMap?.Parent)));
            Find.WorldSelector.ClearSelection();
            Find.WorldTargeter.BeginTargeting(
                (GlobalTargetInfo target) => ChoseNukeWorldTarget(target),
                true,
                null,
                true,
                null,
                ExtraLabelGetter,
                target => IsValidNukeTarget(target.Tile, out _),
                (PlanetTile?)null,
                true);
        }

        private static bool ChoseNukeWorldTarget(GlobalTargetInfo target)
        {
            string? reason = null;
            if (!target.Tile.Valid)
                reason = "DMSL_NukeStrike_TargetInvalid".Translate();
            else if (!IsValidNukeTarget(target.Tile, out reason))
                reason ??= "DMSL_Comms_NoTargetVisibility".Translate();
            if (reason != null)
            {
                if (!string.IsNullOrWhiteSpace(reason))
                    Messages.Message(reason, MessageTypeDefOf.RejectInput);
                return false;
            }

            Find.WorldTargeter.StopTargeting();

            MapParent? mapParent = Find.WorldObjects.MapParentAt(target.Tile);
            if (mapParent == null || !mapParent.HasMap)
            {
                Messages.Message("DMSL_NukeStrike_MapNotLoaded".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            Map map = mapParent.Map;
            Current.Game.CurrentMap = map;
            CameraJumper.TryJump(map.Center, map, CameraJumper.MovementMode.Pan);

            int pointCount = AerialSupportCoordinator.GetPointCountForSupportType(
                DefDatabase<AerialSupportTypeDef>.GetNamed(NuclearStrikeDefName, false));
            var selector = new AerialSupportTargetSelector();
            selector.StartSelection(null, map, pointCount,
                points => OnMapSelectionComplete(points, map),
                () => { });
            return true;
        }

        private static void OnMapSelectionComplete(List<IntVec3> points, Map targetMap)
        {
            CommsSupportPendingComponent.GetOrCreate(targetMap)?.ScheduleNukeStrike(NuclearStrikeDefName, points);
        }

        private static bool IsValidNukeTarget(PlanetTile tile, out string? reason)
        {
            reason = null;
            if (tile < 0)
            {
                reason = "DMSL_NukeStrike_TargetInvalid".Translate();
                return false;
            }

            MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);
            if (mapParent == null)
            {
                reason = "DMSL_NukeStrike_NoMapParent".Translate();
                return false;
            }

            if (!mapParent.HasMap)
            {
                reason = "DMSL_NukeStrike_MapNotLoaded".Translate();
                return false;
            }

            if (mapParent.Map?.generatorDef?.isUnderground == true)
            {
                reason = "DMSL_Comms_UndergroundMapRejected".Translate();
                return false;
            }

            return true;
        }
    }
}
