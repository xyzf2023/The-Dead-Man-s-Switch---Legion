// ============================================================================
// 文件：CommsSupportTargeting.cs
// 说明：通讯台空中支援选点入口（世界格选择 + 地图选点）
// 功能：BeginWorldTargeting 启动世界格选点；回调中跳转地图并启动 AerialSupportTargetSelector
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using DMS_Legion.GroundSupport;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 通讯台空中支援选点入口
    /// </summary>
    public static class CommsSupportTargeting
    {
        /// <summary>
        /// 启动世界格选点，确认后跳转地图并启动地图选点
        /// </summary>
        public static void BeginWorldTargeting(CommsAirSupportOptionDef optionDef, Faction faction, Pawn negotiator)
        {
            if (optionDef == null || faction == null)
            {
                Log.Warning("[DMS_Legion] CommsSupportTargeting.BeginWorldTargeting 参数无效");
                return;
            }

            var aerialSupportType = DefDatabase<AerialSupportTypeDef>.GetNamed(optionDef.aerialSupportDefName, false);
            if (aerialSupportType == null)
            {
                Log.Error($"[DMS_Legion] CommsSupportTargeting 未找到空中支援类型: {optionDef.aerialSupportDefName}");
                return;
            }

            TaggedString ExtraLabelGetter(GlobalTargetInfo target)
            {
                if (!target.IsValid || !target.Tile.Valid)
                    return TaggedString.Empty;
                if (!IsValidCommsSupportTarget(target.Tile, out _))
                    return "DMSL_Comms_NoTargetVisibility".Translate();
                return "DMSL_Comms_ExecuteSupportInArea".Translate(aerialSupportType.label);
            }

            CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(Find.AnyPlayerHomeMap?.Parent ?? Find.CurrentMap?.Parent)));
            Find.WorldSelector.ClearSelection();
            Find.WorldTargeter.BeginTargeting(
                (GlobalTargetInfo target) => ChoseCommsSupportWorldTarget(target, optionDef, aerialSupportType, faction),
                true,
                null,
                true,
                null,
                ExtraLabelGetter,
                target => IsValidCommsSupportTarget(target.Tile, out _),
                (PlanetTile?)null,
                true);
        }

        private static bool ChoseCommsSupportWorldTarget(GlobalTargetInfo target, CommsAirSupportOptionDef optionDef, AerialSupportTypeDef aerialSupportType, Faction faction)
        {
            string? reason = null;
            if (!target.Tile.Valid || !IsValidCommsSupportTarget(target.Tile, out reason))
            {
                if (!string.IsNullOrWhiteSpace(reason))
                    Messages.Message(reason, MessageTypeDefOf.RejectInput);
                return false;
            }

            Find.WorldTargeter.StopTargeting();

            MapParent? mapParent = Find.WorldObjects.MapParentAt(target.Tile);
            if (mapParent == null || !mapParent.HasMap)
            {
                Messages.Message("该地图未加载。".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            Map map = mapParent.Map;
            Current.Game.CurrentMap = map;

            StartMapTargeting(map, optionDef, aerialSupportType);
            return true;
        }

        private static bool IsValidCommsSupportTarget(PlanetTile tile, out string? reason)
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
                reason = "该位置没有可进入的地点。".Translate();
                return false;
            }

            if (!mapParent.HasMap)
            {
                reason = "该地图未加载。".Translate();
                return false;
            }

            if (mapParent.Map?.generatorDef?.isUnderground == true)
            {
                reason = "DMSL_Comms_UndergroundMapRejected".Translate();
                return false;
            }

            return true;
        }

        private static void StartMapTargeting(Map targetMap, CommsAirSupportOptionDef optionDef, AerialSupportTypeDef aerialSupportType)
        {
            int pointCount = AerialSupportCoordinator.GetPointCountForSupportType(aerialSupportType);
            var selector = new AerialSupportTargetSelector();
            selector.StartSelection(null, targetMap, pointCount,
                points => OnMapSelectionComplete(points, targetMap, optionDef),
                () => { });
        }

        private static void OnMapSelectionComplete(List<IntVec3> points, Map targetMap, CommsAirSupportOptionDef optionDef)
        {
            CommsSupportPendingComponent.GetOrCreate(targetMap)?.Schedule(optionDef.aerialSupportDefName, points);
        }
    }
}
