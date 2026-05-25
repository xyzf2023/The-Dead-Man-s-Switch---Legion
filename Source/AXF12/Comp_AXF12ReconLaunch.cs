using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DMS_Legion.AerialRaid.AerialRaidComponents;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DMS_Legion.AXF12
{
    public class Comp_AXF12ReconLaunch : ThingComp
    {
        private CompLaunchable? cachedLaunchable;
        private CompTransporter? cachedTransporter;

        // 自定义返航降落：是否启用 & 预设降落格
        private bool useManualReturnLanding;
        private IntVec3 customReturnLandingCell = IntVec3.Invalid;

        // 轰炸模式：false=集束轰炸，true=多点轰炸
        private bool useMultiPointBombing;

        // 多点轰炸选点进行中状态（选满或取消前不扣弹、不发射）
        private List<IntVec3>? pendingMultiBombCells;
        private int pendingMultiBombIndex;
        private PlanetTile pendingMultiBombTargetTile;
        private int pendingMultiBombCount;
        private Map? pendingMultiBombMap;

        /// <summary>轰炸两步选点：世界格选完后跳转目标地图并直接调用同一方法 StartBombingCellTargeter 启动单格选点。以下静态字段仅供 AXF12BombingCellSelectPromptComponent 备用。</summary>
        public static Comp_AXF12ReconLaunch? PendingBombingComp;
        public static PlanetTile PendingBombingTargetTile;
        public static int PendingBombingCount;
        public static string? PendingBombingSupportTypeDefName;

        public static void ClearPendingBombing()
        {
            PendingBombingComp = null;
            PendingBombingTargetTile = default;
            PendingBombingCount = 0;
            PendingBombingSupportTypeDefName = null;
        }

        public CompProperties_AXF12ReconLaunch Props => (CompProperties_AXF12ReconLaunch)props;

        private CompLaunchable? LaunchableComp => cachedLaunchable ??= parent.GetComp<CompLaunchable>();
        private CompTransporter? TransporterComp => cachedTransporter ??= parent.GetComp<CompTransporter>();

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent == null || parent.Faction != Faction.OfPlayer || !parent.Spawned)
            {
                yield break;
            }

            var command = new Command_Action
            {
                defaultLabel = Props.gizmoLabel.Translate(),
                defaultDesc = Props.gizmoDesc.Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath, false),
                action = BeginWorldTargeting
            };

            if (LaunchableComp == null)
            {
                command.Disable("发射组件缺失，无法执行侦察。");
            }
            else if (TransporterComp == null)
            {
                command.Disable("运输组件缺失，无法执行侦察。");
            }
            else if (parent.Faction == null)
            {
                command.Disable("目标阵营无效，无法执行侦察。");
            }
            else if (!LaunchableComp.CanLaunch(null).Accepted)
            {
                command.Disable("DMSL_AXF12_Disable_NotReadyToLaunch".Translate());
            }

            if (parent.Map?.generatorDef?.isUnderground == true)
            {
                command.Disable("CommandCallRoyalAidMapUnreachable".Translate(Faction.OfPlayer.Named("FACTION")));
            }
            else
            {
                if (TransporterComp != null && !TransporterComp.innerContainer.Any(t => t is Pawn))
                {
                    command.Disable("DMSL_AXF12_Disable_NoPilot".Translate());
                }

                var refuelable = parent.GetComp<CompRefuelable>();
                if (refuelable != null && refuelable.Fuel <= 0f)
                {
                    command.Disable("DMSL_AXF12_Disable_NoFuel".Translate());
                }
            }

            yield return command;

            // 拦截 Gizmo：仅当存在空袭倒计时且满足与侦察相同的起飞条件时可用
            var interceptCommand = new Command_Action
            {
                defaultLabel = Props.interceptGizmoLabel.Translate(),
                defaultDesc = Props.interceptGizmoDesc.Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.interceptGizmoIconPath, false),
                action = LaunchIntercept
            };

            if (!HasAnyAerialRaidCountdown())
            {
                interceptCommand.Disable("DMSL_AXF12_InterceptNoTarget".Translate());
            }
            else if (LaunchableComp == null)
            {
                interceptCommand.Disable("发射组件缺失，无法执行。");
            }
            else if (TransporterComp == null)
            {
                interceptCommand.Disable("运输组件缺失，无法执行。");
            }
            else if (parent.Faction == null)
            {
                interceptCommand.Disable("目标阵营无效，无法执行。");
            }
            else if (!LaunchableComp.CanLaunch(null).Accepted)
            {
                interceptCommand.Disable("DMSL_AXF12_Disable_NotReadyToLaunch".Translate());
            }
            else if (parent.Map?.generatorDef?.isUnderground == true)
            {
                interceptCommand.Disable("CommandCallRoyalAidMapUnreachable".Translate(Faction.OfPlayer.Named("FACTION")));
            }
            else
            {
                if (TransporterComp != null && !TransporterComp.innerContainer.Any(t => t is Pawn))
                {
                    interceptCommand.Disable("DMSL_AXF12_Disable_NoPilot".Translate());
                }
                else
                {
                    var refuelable = parent.GetComp<CompRefuelable>();
                    if (refuelable != null && refuelable.Fuel <= 0f)
                    {
                        interceptCommand.Disable("DMSL_AXF12_Disable_NoFuel".Translate());
                    }
                }
            }

            yield return interceptCommand;

            // 轰炸 Gizmo：弹数菜单 → 世界格选择（仅已加载）→ 跳转地图单格选点 → 扣弹并起飞
            var bombCommand = new Command_Action
            {
                defaultLabel = Props.bombGizmoLabel.Translate(),
                defaultDesc = Props.bombGizmoDesc.Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.bombGizmoIconPath, false),
                action = ShowBombOptionMenu
            };

            if (parent.GetComp<CompAXF12AmmoReserve>() == null)
            {
                bombCommand.Disable("航弹储备组件缺失。");
            }
            else if (LaunchableComp == null)
            {
                bombCommand.Disable("发射组件缺失，无法执行轰炸。");
            }
            else if (TransporterComp == null)
            {
                bombCommand.Disable("运输组件缺失，无法执行轰炸。");
            }
            else if (parent.Faction == null)
            {
                bombCommand.Disable("DMSL_AXF12_Disable_InvalidFaction_Bomb".Translate());
            }
            else if (!LaunchableComp.CanLaunch(null).Accepted)
            {
                bombCommand.Disable("DMSL_AXF12_Disable_NotReadyToLaunch".Translate());
            }
            else if (parent.Map?.generatorDef?.isUnderground == true)
            {
                bombCommand.Disable("CommandCallRoyalAidMapUnreachable".Translate(Faction.OfPlayer.Named("FACTION")));
            }
            else
            {
                if (TransporterComp != null && !TransporterComp.innerContainer.Any(t => t is Pawn))
                {
                    bombCommand.Disable("DMSL_AXF12_Disable_NoPilot".Translate());
                }
                else
                {
                    var refuelable = parent.GetComp<CompRefuelable>();
                    if (refuelable != null && refuelable.Fuel <= 0f)
                    {
                        bombCommand.Disable("DMSL_AXF12_Disable_NoFuel".Translate());
                    }
                }
            }

            yield return bombCommand;

            // 轰炸模式切换：显示当前模式（集束 / 多点）
            var bombingModeToggle = new Command_Toggle
            {
                defaultLabel = useMultiPointBombing
                    ? "DMSL_AXF12_BombingMode_MultiPoint_Label".Translate()
                    : "DMSL_AXF12_BombingMode_Concentrated_Label".Translate(),
                defaultDesc = useMultiPointBombing
                    ? "DMSL_AXF12_BombingMode_MultiPoint_Desc".Translate()
                    : "DMSL_AXF12_BombingMode_Concentrated_Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get(
                    useMultiPointBombing ? "UI/Gizmo/MultiPointBombing" : "UI/Gizmo/ConcentratedBombing",
                    false),
                isActive = () => useMultiPointBombing,
                toggleAction = () => useMultiPointBombing = !useMultiPointBombing
            };

            yield return bombingModeToggle;

            // 自定义返航降落点开关（电力开关式 Gizmo）。开启后，在点击侦察/拦截/轰炸时会先在本图选返航落点再进入世界选点。
            var customLandingToggle = new Command_Toggle
            {
                defaultLabel = "DMSL_AXF12_CustomLanding_Label".Translate(),
                defaultDesc = "DMSL_AXF12_CustomLanding_Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmo/CustomLanding", false),
                isActive = () => useManualReturnLanding,
                toggleAction = () => useManualReturnLanding = !useManualReturnLanding
            };

            yield return customLandingToggle;

            // 停止关注所有侦察区域：由设置控制是否显示，无观测时发消息并仍执行一次安全清空
            if (DMS_Legion.DMSL_ModSettings.settings?.enableExtraStopReconOption == true)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DMSL_AXF12_StopObservingAll_Label".Translate(),
                    defaultDesc = "DMSL_AXF12_StopObservingAll_Desc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Unfocus", false),
                    action = () => AXF12ReconMissionManager.StopObservingAllRecon()
                };
            }
        }

        private void ShowBombOptionMenu()
        {
            var ammoReserve = parent.GetComp<CompAXF12AmmoReserve>();
            if (ammoReserve == null)
            {
                return;
            }

            var options = new List<FloatMenuOption>();
            void AddOption(int bombCount, string labelKey, string supportTypeDefName)
            {
                bool disabled = ammoReserve.CurrentCount < bombCount;
                string label = disabled
                    ? (labelKey.Translate() + " (" + "DMSL_AXF12_BombNoAmmo".Translate() + ")").ToString()
                    : labelKey.Translate().ToString();
                var option = new FloatMenuOption(label, () => BeginBombingWorldTargeting(bombCount, supportTypeDefName));
                if (disabled)
                {
                    option.Disabled = true;
                }
                options.Add(option);
            }

            AddOption(1, "DMSL_AXF12_BombOption_One", "DMSL_AerialSupport_AXF12Bombing_Once");
            AddOption(2, "DMSL_AXF12_BombOption_Two", "DMSL_AerialSupport_AXF12Bombing_Twice");
            AddOption(3, "DMSL_AXF12_BombOption_Three", "DMSL_AerialSupport_AXF12Bombing_Thrice");
            AddOption(4, "DMSL_AXF12_BombOption_Four", "DMSL_AerialSupport_AXF12Bombing_FourTimes");
            AddOption(5, "DMSL_AXF12_BombOption_Five", "DMSL_AerialSupport_AXF12Bombing_FiveTimes");

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void BeginBombingWorldTargeting(int bombCount, string supportTypeDefName)
        {
            if (useManualReturnLanding)
            {
                StartReturnLandingCellTargeterOdysseyStyle(() => BeginBombingWorldTargetingInternal(bombCount, supportTypeDefName));
                return;
            }
            BeginBombingWorldTargetingInternal(bombCount, supportTypeDefName);
        }

        private void BeginBombingWorldTargetingInternal(int bombCount, string supportTypeDefName)
        {
            if (parent.Map?.Parent == null)
            {
                Messages.Message("DMSL_AXF12_Message_NoWorldMapPos".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            var launchable = LaunchableComp;
            if (launchable == null)
            {
                Messages.Message("发射组件缺失，无法执行轰炸。", MessageTypeDefOf.RejectInput);
                return;
            }

            TransportShipDef? shipDef = DefDatabase<TransportShipDef>.GetNamed(Props.transportShipDefName, false);
            int maxLaunchDistance = shipDef?.maxLaunchDistance ?? -1;
            PlanetTile originTile = parent.Map.Parent.Tile;
            var refuelable = parent.GetComp<CompRefuelable>();
            int aircraftMax = maxLaunchDistance > 0 ? maxLaunchDistance : int.MaxValue;

            bool ChoseBombingWorldTarget(GlobalTargetInfo target)
            {
                if (!target.IsValid || target.Tile < 0)
                {
                    return false;
                }

                PlanetTile tile = target.Tile;
                if (!IsValidBombingTarget(tile, out string? reason))
                {
                    if (!string.IsNullOrWhiteSpace(reason))
                    {
                        Messages.Message(reason, MessageTypeDefOf.RejectInput);
                    }
                    return false;
                }

                if (!TryGetReconFuelCost(launchable, originTile, tile, out float fuelCost))
                {
                    Messages.Message("DMSL_AXF12_Message_NoFuelCost".Translate(), MessageTypeDefOf.RejectInput);
                    return false;
                }

                if (refuelable != null && refuelable.Fuel < fuelCost)
                {
                    Messages.Message("DMSL_AXF12_Message_NoFuelNeed".Translate(fuelCost.ToString("F0")), MessageTypeDefOf.RejectInput);
                    return false;
                }

                Find.WorldTargeter.StopTargeting();

                MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);
                if (mapParent == null || !mapParent.HasMap)
                {
                    Messages.Message("DMSL_AXF12_Message_MapNotLoaded".Translate(), MessageTypeDefOf.RejectInput);
                    return false;
                }

                Map map = mapParent.Map;
                Current.Game.CurrentMap = map;

                // 使用同一方法启动地图单格选点器（原版 Targeter.BeginTargeting 六参数重载）
                StartBombingCellTargeter(tile, bombCount, supportTypeDefName);
                return true;
            }

            TaggedString ExtraLabelGetter(GlobalTargetInfo target)
            {
                if (!target.IsValid || !target.Tile.Valid)
                {
                    return TaggedString.Empty;
                }
                PlanetTile tile = target.Tile;
                int fuelBasedMax = GetFuelBasedMaxLaunchDistance(launchable, refuelable, tile.Layer);
                int effectiveRange = Mathf.Min(fuelBasedMax, aircraftMax);
                int distance = Find.WorldGrid.TraversalDistanceBetween(originTile, tile, true, int.MaxValue, true);
                if (distance < 0)
                {
                    distance = 0;
                }

                if (distance > effectiveRange)
                {
                    return "DMSL_AXF12_Label_OutOfRange".Translate();
                }
                if (!IsValidBombingTarget(tile, out _))
                {
                    return "DMSL_AXF12_Label_BombNeedVision".Translate();
                }
                if (!TryGetReconFuelCost(launchable, originTile, tile, out float fuelCost))
                {
                    return TaggedString.Empty;
                }
                return "DMSL_AXF12_Label_BombFuel".Translate(fuelCost.ToString("F0"));
            }

            // 先跳转到世界地图，否则玩家仍停留在殖民地地图看不到选格
            CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(originTile)));
            Find.WorldSelector.ClearSelection();
            Find.WorldTargeter.BeginTargeting(
                ChoseBombingWorldTarget,
                true,
                CompLaunchable.TargeterMouseAttachment,
                true,
                () =>
                {
                    if (maxLaunchDistance > 0)
                    {
                        GenDraw.DrawWorldRadiusRing(originTile, maxLaunchDistance, null);
                    }
                },
                ExtraLabelGetter,
                target => IsValidBombingTarget(target.Tile, out _),
                (PlanetTile?)null,
                true);
        }

        private static bool IsValidBombingTarget(PlanetTile tile, out string? reason)
        {
            reason = null;
            if (tile < 0)
            {
                reason = "DMSL_AXF12_Message_InvalidTarget".Translate();
                return false;
            }

            MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);
            if (mapParent == null)
            {
                reason = "DMSL_AXF12_Message_NoSettlement".Translate();
                return false;
            }

            if (!mapParent.HasMap)
            {
                reason = "DMSL_AXF12_Message_MapNotLoaded".Translate();
                return false;
            }

            return true;
        }

        /// <summary>
        /// 轰炸地图单格选点：在当前地图上启动单格选点器。世界格选点回调 ChoseBombingWorldTarget 跳转后直接调用此方法（同一选点逻辑，原版 Targeter.BeginTargeting）。
        /// </summary>
        internal void StartBombingCellTargeter(PlanetTile targetTile, int bombCount, string supportTypeDefName)
        {
            if (bombCount > 1 && useMultiPointBombing)
            {
                StartMultiPointBombingCellTargeter(targetTile, bombCount);
                return;
            }

            Map? map = Current.Game.CurrentMap;
            if (map == null)
            {
                Messages.Message("DMSL_AXF12_Message_NoMapSelect".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            var targetingParams = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetSelf = false,
                canTargetPawns = false,
                canTargetBuildings = true,
                canTargetAnimals = false,
                canTargetHumans = false,
                canTargetMechs = false,
                canTargetItems = false,
                validator = (TargetInfo target) => target.Cell.InBounds(map)
            };

            // 原版 Targeter.BeginTargeting 六参数重载签名（RimWorld 1.6 Targeter.cs:59）：
            // (TargetingParameters targetParams, Action<LocalTargetInfo> action, Pawn caster = null,
            //  Action actionWhenFinished = null, Texture2D mouseAttachment = null, bool requiresCastedSelected = true)
            // 注意：若 caster 非空且 caster.Map != Find.CurrentMap，ConfirmStillValid() 会立即 StopTargeting()。
            // 轰炸时我们在目标地图选点，穿梭机内 Pawn 在殖民地地图，故传 caster=null，requiresCastedSelected=false。
            if (Find.Targeter != null)
            {
                Find.Targeter.BeginTargeting(
                    targetingParams,
                    (LocalTargetInfo target) => OnBombingCellSelected(target, targetTile, map, bombCount, supportTypeDefName),
                    null,
                    null,
                    null,
                    false);
            }
        }

        /// <summary>
        /// 多点轰炸：在目标地图上依次选择 bombCount 个落点，全部选完后再扣弹并发射。
        /// </summary>
        private void StartMultiPointBombingCellTargeter(PlanetTile targetTile, int bombCount)
        {
            Map? map = Current.Game.CurrentMap;
            if (map == null)
            {
                Messages.Message("DMSL_AXF12_Message_NoMapSelect".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            pendingMultiBombCells = new List<IntVec3>();
            pendingMultiBombIndex = 0;
            pendingMultiBombTargetTile = targetTile;
            pendingMultiBombCount = bombCount;
            pendingMultiBombMap = map;
            BeginNextMultiPointBombTargeter();
        }

        private void BeginNextMultiPointBombTargeter()
        {
            Map? map = pendingMultiBombMap ?? Current.Game.CurrentMap;
            if (map == null || pendingMultiBombCells == null)
            {
                ClearMultiPointBombingPending();
                return;
            }

            int currentIndex = pendingMultiBombIndex;
            Messages.Message(
                "DMSL_AXF12_MultiBomb_SelectPoint".Translate(currentIndex + 1, pendingMultiBombCount),
                MessageTypeDefOf.NeutralEvent);

            var targetingParams = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetSelf = false,
                canTargetPawns = false,
                canTargetBuildings = true,
                canTargetAnimals = false,
                canTargetHumans = false,
                canTargetMechs = false,
                canTargetItems = false,
                validator = (TargetInfo target) => target.Cell.InBounds(map)
            };

            if (Find.Targeter == null)
            {
                ClearMultiPointBombingPending();
                return;
            }

            Find.Targeter.BeginTargeting(
                targetingParams,
                OnMultiPointBombCellSelected,
                null,
                OnMultiPointBombingCancelled,
                null,
                false);
        }

        private void OnMultiPointBombCellSelected(LocalTargetInfo target)
        {
            if (pendingMultiBombCells == null)
            {
                return;
            }

            pendingMultiBombCells.Add(target.Cell);
            pendingMultiBombIndex++;

            if (pendingMultiBombIndex < pendingMultiBombCount)
            {
                LongEventHandler.ExecuteWhenFinished(BeginNextMultiPointBombTargeter);
                return;
            }

            var cells = new List<IntVec3>(pendingMultiBombCells);
            PlanetTile tile = pendingMultiBombTargetTile;
            Map? map = pendingMultiBombMap;
            int count = pendingMultiBombCount;
            ClearMultiPointBombingPending();

            OnBombingCellsSelected(
                cells,
                tile,
                map,
                count,
                "DMSL_AerialSupport_AXF12Bombing_Once",
                multiPointBombing: true);
        }

        private void OnMultiPointBombingCancelled()
        {
            if (pendingMultiBombCells == null)
            {
                return;
            }

            if (pendingMultiBombIndex < pendingMultiBombCount)
            {
                Messages.Message("DMSL_AXF12_MultiBomb_Cancelled".Translate(), MessageTypeDefOf.RejectInput);
            }

            ClearMultiPointBombingPending();
        }

        private void ClearMultiPointBombingPending()
        {
            pendingMultiBombCells = null;
            pendingMultiBombIndex = 0;
            pendingMultiBombTargetTile = default;
            pendingMultiBombCount = 0;
            pendingMultiBombMap = null;
        }

        /// <summary>
        /// 奥德赛式返航降落选点：在当前地图上使用穿梭机建筑虚影（DrawShuttleGhost）+ ShuttleCanLandHere 校验，
        /// 选点完成后执行 onConfirmed（例如再打开世界选点、发射等）。仅在“自定义降落点”开启时由侦察/拦截/轰炸入口调用。
        /// </summary>
        private void StartReturnLandingCellTargeterOdysseyStyle(Action onConfirmed)
        {
            Map? map = parent.Map;
            if (map == null)
            {
                onConfirmed();
                return;
            }

            TransportShipDef? shipDef = DefDatabase<TransportShipDef>.GetNamed(Props.transportShipDefName, false);
            ThingDef shuttleDef = shipDef?.shipThing ?? ThingDefOf.Shuttle;
            var shuttleRotation = new Rot4[1] { shuttleDef.defaultPlacingRot };

            void OnCellConfirmed(LocalTargetInfo x)
            {
                customReturnLandingCell = x.Cell;
                onConfirmed();
            }

            void DrawGhost(LocalTargetInfo x)
            {
                RoyalTitlePermitWorker_CallShuttle.DrawShuttleGhost(x, map, shuttleDef, shuttleRotation[0]);
            }

            bool Validator(LocalTargetInfo x)
            {
                AcceptanceReport ar = RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(x, map, shuttleDef, shuttleRotation[0]);
                if (!ar.Accepted)
                {
                    Messages.Message(ar.Reason, new LookTargets(parent), MessageTypeDefOf.RejectInput, historical: false);
                }
                return ar.Accepted;
            }

            void OnGuiRotate(LocalTargetInfo _)
            {
                if (shuttleDef.rotatable)
                {
                    if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
                    {
                        shuttleRotation[0] = shuttleRotation[0].Rotated(RotationDirection.Clockwise);
                    }
                    if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
                    {
                        shuttleRotation[0] = shuttleRotation[0].Rotated(RotationDirection.Counterclockwise);
                    }
                }
            }

            if (Find.Targeter != null)
            {
                Find.Targeter.BeginTargeting(
                    TargetingParameters.ForCell(),
                    OnCellConfirmed,
                    DrawGhost,
                    Validator,
                    null,
                    null,
                    CompLaunchable.TargeterMouseAttachment,
                    playSoundOnAction: true,
                    OnGuiRotate,
                    null);
            }
            else
            {
                onConfirmed();
            }
        }

        private void OnBombingCellSelected(LocalTargetInfo target, PlanetTile targetTile, Map? targetMap, int bombCount, string supportTypeDefName)
        {
            if (targetMap == null)
            {
                return;
            }

            OnBombingCellsSelected(
                new List<IntVec3> { target.Cell },
                targetTile,
                targetMap,
                bombCount,
                supportTypeDefName,
                multiPointBombing: false);
        }

        private void OnBombingCellsSelected(
            List<IntVec3> targetCells,
            PlanetTile targetTile,
            Map? targetMap,
            int bombCount,
            string supportTypeDefName,
            bool multiPointBombing)
        {
            if (targetMap == null || targetCells == null || targetCells.Count == 0)
            {
                return;
            }

            var ammoReserve = parent.GetComp<CompAXF12AmmoReserve>();
            int consumed = ammoReserve?.ConsumeAmmo(bombCount) ?? 0;
            if (consumed < bombCount)
            {
                Messages.Message("DMSL_AXF12_BombNoAmmo".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            var launchable = LaunchableComp;
            if (launchable == null || parent.Map?.Parent == null)
            {
                Messages.Message("发射组件缺失，无法执行轰炸。", MessageTypeDefOf.RejectInput);
                return;
            }

            var transporter = TransporterComp;
            if (transporter == null)
            {
                Messages.Message("运输组件缺失，无法执行轰炸。", MessageTypeDefOf.RejectInput);
                return;
            }

            EnsureTransporterGroup(transporter, parent.Map);

            PlanetTile originTile = parent.Map.Parent.Tile;
            IntVec3 originCell = GetReturnOriginCell();

            if (!TryGetReconFuelCost(launchable, originTile, targetTile, out float fuelCost))
            {
                Messages.Message("DMSL_AXF12_Message_NoFuelCost".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            var refuelable = parent.GetComp<CompRefuelable>();
            if (refuelable != null && refuelable.Fuel < fuelCost)
            {
                Messages.Message("DMSL_AXF12_Message_NoFuelNeed".Translate(fuelCost.ToString("F0")), MessageTypeDefOf.RejectInput);
                return;
            }

            TransportersArrivalAction_AXF12Bombing bombingAction;
            if (multiPointBombing)
            {
                bombingAction = new TransportersArrivalAction_AXF12Bombing(
                    originTile,
                    targetTile,
                    originCell,
                    targetCells,
                    "DMSL_AerialSupport_AXF12Bombing_Once",
                    Props.transportShipDefName,
                    Props.worldObjectDefName,
                    multiPointBombing: true);
            }
            else
            {
                bombingAction = new TransportersArrivalAction_AXF12Bombing(
                    originTile,
                    targetTile,
                    originCell,
                    targetCells[0],
                    supportTypeDefName,
                    Props.transportShipDefName,
                    Props.worldObjectDefName);
            }

            AXF12LaunchContext.CustomFuelCostActive = true;
            AXF12LaunchContext.CustomFuelCost = fuelCost;
            AXF12LaunchContext.AllowedDefName = parent.def.defName;

            bool launched;
            try
            {
                launched = TryLaunchWithArrivalAction(launchable, targetTile, bombingAction);
            }
            finally
            {
                AXF12LaunchContext.Reset();
            }

            if (!launched)
            {
                Messages.Message("DMSL_AXF12_Message_LaunchFailed".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        /// <summary> 是否存在任意地图的空袭倒计时（GetRemainingTicks() &gt; 0）。 </summary>
        private static bool HasAnyAerialRaidCountdown()
        {
            foreach (var map in Find.Maps)
            {
                var comp = map?.GetComponent<AerialRaidPrePhaseComponent>();
                if (comp != null && comp.GetRemainingTicks() > 0)
                    return true;
            }
            return false;
        }

        private void LaunchIntercept()
        {
            if (useManualReturnLanding)
            {
                StartReturnLandingCellTargeterOdysseyStyle(LaunchInterceptCore);
                return;
            }
            LaunchInterceptCore();
        }

        private void LaunchInterceptCore()
        {
            var launchable = LaunchableComp;
            if (launchable == null || parent.Map?.Parent == null)
            {
                Messages.Message("发射组件缺失，无法执行拦截。", MessageTypeDefOf.RejectInput);
                return;
            }
            var transporter = TransporterComp;
            if (transporter == null)
            {
                Messages.Message("运输组件缺失，无法执行拦截。", MessageTypeDefOf.RejectInput);
                return;
            }
            EnsureTransporterGroup(transporter, parent.Map);
            IntVec3 originCell = GetReturnOriginCell();
            PlanetTile originTile = parent.Map.Parent.Tile;

            AXF12LaunchContext.IsInterceptLaunch = true;
            AXF12LaunchContext.OriginMap = parent.Map;
            AXF12LaunchContext.OriginCell = originCell;
            AXF12LaunchContext.AllowedDefName = parent.def.defName;
            AXF12LaunchContext.CustomFuelCostActive = true;
            AXF12LaunchContext.CustomFuelCost = 0f;

            var returnAction = new TransportersArrivalAction_AXF12Return(originCell, Props.transportShipDefName);
            bool launched = false;
            try
            {
                launched = TryLaunchWithArrivalAction(launchable, originTile, returnAction);
            }
            finally
            {
                if (!launched)
                    AXF12LaunchContext.Reset();
                else
                {
                    // 发射成功：仅重置燃料等；IsInterceptLaunch/OriginMap/OriginCell 保留，供飞机离图时 LeaveMap Patch 使用
                    AXF12LaunchContext.CustomFuelCostActive = false;
                    AXF12LaunchContext.CustomFuelCost = 0f;
                    AXF12LaunchContext.AllowedDefName = null;
                }
            }
            if (!launched)
                Messages.Message("DMSL_AXF12_Message_LaunchFailed".Translate(), MessageTypeDefOf.RejectInput);
        }

        private void BeginWorldTargeting()
        {
            if (useManualReturnLanding)
            {
                StartReturnLandingCellTargeterOdysseyStyle(BeginWorldTargetingCore);
                return;
            }
            BeginWorldTargetingCore();
        }

        private void BeginWorldTargetingCore()
        {
            if (parent.Map?.Parent == null)
            {
                Messages.Message("DMSL_AXF12_Message_NoWorldMapPos".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            var launchable = LaunchableComp;
            if (launchable == null)
            {
                Messages.Message("发射组件缺失，无法执行侦察。", MessageTypeDefOf.RejectInput);
                return;
            }

            TransportShipDef? shipDef = DefDatabase<TransportShipDef>.GetNamed(Props.transportShipDefName, false);
            int maxLaunchDistance = shipDef?.maxLaunchDistance ?? -1;
            PlanetTile originTile = parent.Map.Parent.Tile;

            bool ChoseWorldTarget(GlobalTargetInfo target)
            {
                if (!target.IsValid)
                {
                    return false;
                }

                PlanetTile tile = target.Tile;
                if (tile < 0)
                {
                    return false;
                }

                if (!IsValidReconTarget(tile, out string? reason))
                {
                    if (!string.IsNullOrWhiteSpace(reason))
                    {
                        Messages.Message(reason, MessageTypeDefOf.RejectInput);
                    }
                    return false;
                }

                if (!TryGetReconFuelCost(launchable, originTile, tile, out float fuelCost))
                {
                    Messages.Message("DMSL_AXF12_Message_NoFuelCost".Translate(), MessageTypeDefOf.RejectInput);
                    return false;
                }

                var refuelable = parent.GetComp<CompRefuelable>();
                if (refuelable != null && refuelable.Fuel < fuelCost)
                {
                    Messages.Message("DMSL_AXF12_Message_NoFuelNeed".Translate(fuelCost.ToString("F0")), MessageTypeDefOf.RejectInput);
                    return false;
                }

                MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);
                if (mapParent?.Faction != null
                    && mapParent.Faction != Faction.OfPlayer
                    && !mapParent.Faction.HostileTo(Faction.OfPlayer))
                {
                    TaggedString text = "DMSL_AXF12_ReconAirspaceConfirmText".Translate();
                    void OnConfirm()
                    {
                        LaunchTo(tile, originTile);
                        Find.WorldTargeter.StopTargeting();
                    }
                    void OnCancel()
                    {
                        Find.WorldTargeter.StopTargeting();
                    }
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, OnConfirm, OnCancel, false, null, WindowLayer.Dialog));
                    return false;
                }

                LaunchTo(tile, originTile);
                Find.WorldTargeter.StopTargeting();
                return true;
            }

            var refuelable = parent.GetComp<CompRefuelable>();
            int aircraftMax = maxLaunchDistance > 0 ? maxLaunchDistance : int.MaxValue;

            TaggedString ExtraLabelGetter(GlobalTargetInfo target)
            {
                if (!target.IsValid || !target.Tile.Valid)
                {
                    return TaggedString.Empty;
                }
                PlanetTile tile = target.Tile;
                int fuelBasedMax = GetFuelBasedMaxLaunchDistance(launchable, refuelable, tile.Layer);
                int effectiveRange = Mathf.Min(fuelBasedMax, aircraftMax);
                int distance = Find.WorldGrid.TraversalDistanceBetween(originTile, tile, true, int.MaxValue, true);
                if (distance < 0)
                {
                    distance = 0;
                }

                if (distance > effectiveRange)
                {
                    return "DMSL_AXF12_Label_OutOfRange".Translate();
                }
                if (!IsValidReconTarget(tile, out _))
                {
                    return "DMSL_AXF12_Label_NoReconValue".Translate();
                }
                if (!TryGetReconFuelCost(launchable, originTile, tile, out float fuelCost))
                {
                    return TaggedString.Empty;
                }
                return "DMSL_AXF12_Label_ReconFuel".Translate(fuelCost.ToString("F0"));
            }

            CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(originTile)));
            Find.WorldSelector.ClearSelection();
            Find.WorldTargeter.BeginTargeting(
                ChoseWorldTarget,
                true,
                CompLaunchable.TargeterMouseAttachment,
                true,
                () =>
                {
                    if (maxLaunchDistance > 0)
                    {
                        GenDraw.DrawWorldRadiusRing(originTile, maxLaunchDistance, null);
                    }
                },
                ExtraLabelGetter,
                target => IsValidReconTarget(target.Tile, out _),
                (PlanetTile?)null,
                true);
        }

        /// <summary>
        /// 当前燃料支持的飞行距离（格数）。不考虑 fixedLaunchDistanceMax 上限，仅按燃料与每格消耗计算。
        /// </summary>
        private static int GetFuelBasedMaxLaunchDistance(CompLaunchable launchable, CompRefuelable? refuelable, PlanetLayer layer)
        {
            if (launchable?.Props == null || refuelable == null || layer?.Def == null)
            {
                return 0;
            }
            if (refuelable.Fuel < launchable.Props.minFuelCost)
            {
                return 0;
            }
            float factor = launchable.Props.fuelPerTile * layer.Def.rangeDistanceFactor;
            if (factor <= 0f)
            {
                return int.MaxValue;
            }
            return Mathf.FloorToInt(refuelable.Fuel / factor);
        }

        /// <summary>
        /// 获取本次任务使用的返航降落“期望中心格”：优先使用玩家自定义降落点，其次为当前 AXF-12 位置。
        /// </summary>
        private IntVec3 GetReturnOriginCell()
        {
            if (useManualReturnLanding && customReturnLandingCell.IsValid)
            {
                return customReturnLandingCell;
            }

            return parent.Position;
        }

        private void LaunchTo(PlanetTile targetTile, PlanetTile originTile)
        {
            var launchable = LaunchableComp;
            if (launchable == null || parent.Map?.Parent == null)
            {
                Messages.Message("发射组件缺失，无法执行侦察。", MessageTypeDefOf.RejectInput);
                return;
            }

            var transporter = TransporterComp;
            if (transporter == null)
            {
                Messages.Message("运输组件缺失，无法执行侦察。", MessageTypeDefOf.RejectInput);
                return;
            }

            EnsureTransporterGroup(transporter, parent.Map);

            IntVec3 originCell = GetReturnOriginCell();

            var reconAction = new TransportersArrivalAction_AXF12Recon(
                originTile,
                targetTile,
                originCell,
                Props.supportTypeDefName,
                Props.transportShipDefName,
                Props.worldObjectDefName);

            if (!TryGetReconFuelCost(launchable, originTile, targetTile, out float fuelCost))
            {
                Messages.Message("DMSL_AXF12_Message_NoFuelCost".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            var refuelable = parent.GetComp<CompRefuelable>();
            if (refuelable != null && refuelable.Fuel < fuelCost)
            {
                Messages.Message("DMSL_AXF12_Message_NoFuelNeed".Translate(fuelCost.ToString("F0")), MessageTypeDefOf.RejectInput);
                return;
            }

            AXF12LaunchContext.CustomFuelCostActive = true;
            AXF12LaunchContext.CustomFuelCost = fuelCost;
            AXF12LaunchContext.AllowedDefName = parent.def.defName;

            bool launched;
            try
            {
                launched = TryLaunchWithArrivalAction(launchable, targetTile, reconAction);
            }
            finally
            {
                AXF12LaunchContext.Reset();
            }

            if (!launched)
            {
                Messages.Message("DMSL_AXF12_Message_LaunchFailed".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        private static bool TryGetReconFuelCost(
            CompLaunchable launchable,
            PlanetTile originTile,
            PlanetTile targetTile,
            out float fuelCost)
        {
            fuelCost = 0f;
            if (launchable?.Props == null)
            {
                return false;
            }

            int distance = Find.WorldGrid.TraversalDistanceBetween(originTile, targetTile);
            if (distance < 0)
            {
                distance = 0;
            }

            float baseCost = Mathf.Max(launchable.Props.minFuelCost, distance * launchable.Props.fuelPerTile);
            fuelCost = baseCost * 2f;
            return true;
        }

        private static bool IsValidReconTarget(PlanetTile tile, out string? reason)
        {
            reason = null;
            if (tile < 0)
            {
                reason = "DMSL_AXF12_Message_InvalidTarget".Translate();
                return false;
            }

            MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);
            if (mapParent == null)
            {
                reason = "DMSL_AXF12_Message_NoSettlement".Translate();
                return false;
            }

            if (mapParent.def == null || !mapParent.def.canHaveMap)
            {
                reason = "DMSL_AXF12_Reason_NoMapGen".Translate();
                return false;
            }

            return true;
        }

        private static void EnsureTransporterGroup(CompTransporter transporter, Map map)
        {
            int currentGroup = GetTransporterGroupId(transporter);
            if (currentGroup >= 0)
            {
                return;
            }

            int newGroupId = Find.UniqueIDsManager.GetNextTransporterGroupID();
            if (!TrySetTransporterGroupId(transporter, newGroupId))
            {
                Log.Warning("[DMS_Legion][AXF12] 无法设置运输组件分组，可能导致发射失败。");
            }
        }

        private static int GetTransporterGroupId(CompTransporter transporter)
        {
            foreach (var type in EnumerateTypeHierarchy(transporter.GetType()))
            {
                var prop = type.GetProperty("GroupID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.PropertyType == typeof(int))
                {
                    return (int)prop.GetValue(transporter);
                }

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.FieldType == typeof(int) &&
                        field.Name.IndexOf("group", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return (int)field.GetValue(transporter);
                    }
                }
            }

            return -1;
        }

        private static bool TrySetTransporterGroupId(CompTransporter transporter, int groupId)
        {
            foreach (var type in EnumerateTypeHierarchy(transporter.GetType()))
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.Name.IndexOf("group", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                    {
                        method.Invoke(transporter, new object[] { groupId });
                        return true;
                    }
                }

                var prop = type.GetProperty("GroupID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.PropertyType == typeof(int) && prop.CanWrite)
                {
                    prop.SetValue(transporter, groupId);
                    return true;
                }

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.FieldType == typeof(int) &&
                        field.Name.IndexOf("group", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        field.SetValue(transporter, groupId);
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<Type> EnumerateTypeHierarchy(Type type)
        {
            for (Type? current = type; current != null; current = current.BaseType)
            {
                yield return current;
            }
        }

        private static bool TryLaunchWithArrivalAction(CompLaunchable launchable, PlanetTile targetTile, TransportersArrivalAction arrivalAction)
        {
            var launchableType = launchable.GetType();
            var methods = launchableType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (TryInvokeLaunchMethod(methods, "TryLaunch", launchable, targetTile, arrivalAction, out bool invoked))
            {
                return true;
            }
            if (invoked)
            {
                return false;
            }

            Log.Warning("[DMS_Legion][AXF12] 未找到 TryLaunch 重载，尝试调用 Launch。");
            if (TryInvokeLaunchMethod(methods, "Launch", launchable, targetTile, arrivalAction, out invoked))
            {
                return true;
            }
            if (invoked)
            {
                return false;
            }

            Log.Error("[DMS_Legion][AXF12] 未找到可用的发射方法。");
            return false;
        }

        private static bool TryInvokeLaunchMethod(
            MethodInfo[] methods,
            string methodName,
            CompLaunchable launchable,
            PlanetTile targetTile,
            TransportersArrivalAction arrivalAction,
            out bool invoked)
        {
            invoked = false;

            foreach (var method in methods)
            {
                if (method.Name != methodName)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 2 || parameters.Length > 3)
                {
                    continue;
                }

                if (!parameters.Any(p => typeof(TransportersArrivalAction).IsAssignableFrom(p.ParameterType)))
                {
                    continue;
                }

                if (!TryBuildLaunchArgs(parameters, targetTile, arrivalAction, out object?[] args))
                {
                    continue;
                }

                try
                {
                    invoked = true;
                    object? result = method.Invoke(launchable, args);
                    if (result is bool b)
                    {
                        return b;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    invoked = true;
                    Log.Error($"[DMS_Legion][AXF12] 发射调用失败: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        private static bool TryBuildLaunchArgs(ParameterInfo[] parameters, PlanetTile targetTile, TransportersArrivalAction arrivalAction, out object?[] args)
        {
            args = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                Type paramType = parameters[i].ParameterType;

                if (paramType == typeof(PlanetTile))
                {
                    args[i] = targetTile;
                }
                else if (paramType == typeof(int))
                {
                    args[i] = (int)targetTile;
                }
                else if (paramType == typeof(GlobalTargetInfo))
                {
                    args[i] = new GlobalTargetInfo(targetTile);
                }
                else if (typeof(TransportersArrivalAction).IsAssignableFrom(paramType))
                {
                    args[i] = arrivalAction;
                }
                else if (paramType == typeof(bool))
                {
                    args[i] = true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref useManualReturnLanding, "axf12UseManualReturnLanding", false);
            Scribe_Values.Look(ref customReturnLandingCell, "axf12CustomReturnLandingCell", IntVec3.Invalid);
            Scribe_Values.Look(ref useMultiPointBombing, "axf12UseMultiPointBombing", false);
        }
    }
}
