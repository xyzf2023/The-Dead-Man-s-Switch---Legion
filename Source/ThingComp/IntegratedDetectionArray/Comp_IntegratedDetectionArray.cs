using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.IntegratedDetectionArray
{
    /// <summary>
    /// 综合探测阵列：诱饵信号 Gizmo + 本地/远程矿脉扫描（固定 12 小时进度，通电时增加），
    /// 100% 时执行原版地质扫描仪或远距离矿物扫描仪效果。
    /// </summary>
    public class Comp_IntegratedDetectionArray : ThingComp
    {
        private const string GizmoIconPath = "UI/Gizmo/GenerateDecoyCoords";
        private const string GizmoIconLocalScan = "UI/Gizmo/LocalVeinScan";
        private const string GizmoIconLongRangeScan = "UI/Gizmo/Long-RangeVeinScan";
        private const string GizmoIconLocalTargetedScan = "UI/Commands/LaunchReport";
        /// <summary>12 小时 = 30000 tick</summary>
        private const float ProgressTicksPerCycle = 30000f;

        private float scanProgress;
        private bool isLocalMode = true;
        private bool useSelectedMineralForLocalScan;
        private ThingDef? targetMineable;
        private CompPowerTrader? powerComp;

        public CompProperties_IntegratedDetectionArray Props => (CompProperties_IntegratedDetectionArray)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
            SetDefaultTargetMineralIfNeeded();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref scanProgress, "scanProgress", 0f);
            Scribe_Values.Look(ref isLocalMode, "isLocalMode", true);
            Scribe_Values.Look(ref useSelectedMineralForLocalScan, "useSelectedMineralForLocalScan", false);
            Scribe_Defs.Look(ref targetMineable, "targetMineable");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                SetDefaultTargetMineralIfNeeded();
        }

        /// <summary>选中建筑时在详细信息栏显示当前扫描进度（类似蓄电池显示蓄电量）。</summary>
        public override string CompInspectStringExtra()
        {
            float pct = Mathf.Clamp01(scanProgress) * 100f;
            string text = "DMSL_IntegratedDetectionArray_ScanProgress".Translate(pct.ToString("F0"));
            string baseStr = base.CompInspectStringExtra();
            return baseStr.NullOrEmpty() ? text : text + "\n" + baseStr;
        }

        /// <summary>选中阵列且为本地模式、通电时，像地质扫描仪一样显示本地深矿矿脉位置。</summary>
        public override void PostDrawExtraSelectionOverlays()
        {
            if (ShouldShowDeepResourceOverlay())
                parent.Map.deepResourceGrid.MarkForDraw();
        }

        /// <summary>是否应显示深矿覆盖层（供 Harmony 补丁 DeepResourceGrid_DeepResourcesOnGUI_IntegratedDetectionArray_Patch 悬停时显示资源信息）。</summary>
        public bool ShouldShowDeepResourceOverlay()
        {
            if (parent.Map == null)
                return false;
            if (parent.Map.Biome == null || !parent.Map.Biome.hasBedrock)
                return false;
            return true;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (powerComp == null || !powerComp.PowerOn)
                return;
            // 被禁止时（玩家点了「禁止」）不增加扫描进度
            var forbiddable = parent.GetComp<CompForbiddable>();
            if (forbiddable != null && forbiddable.Forbidden)
                return;
            scanProgress += 1f / ProgressTicksPerCycle;
            if (scanProgress >= 1f)
            {
                ExecuteScanComplete();
                scanProgress = 0f;
            }
        }

        private void SetDefaultTargetMineralIfNeeded()
        {
            if (targetMineable != null)
                return;
            targetMineable = ThingDefOf.MineableGold;
        }

        private void ExecuteScanComplete()
        {
            Map map = parent.Map;
            if (map == null)
                return;
            Pawn worker = PawnsFinder.AllMaps_FreeColonists.FirstOrDefault();
            if (isLocalMode)
                DoLocalFind(worker, map);
            else
                DoRemoteFind(worker, map);
        }

        /// <summary>原版地质扫描仪 100% 效果：在本地地图生成深矿矿脉。</summary>
        private void DoLocalFind(Pawn worker, Map map)
        {
            if (!map.Biome.hasBedrock)
            {
                Messages.Message("MessageGroundPenetratingScannerNoBedrock".Translate(parent.Named("THING")), parent, MessageTypeDefOf.NegativeEvent, historical: false);
                return;
            }
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(10, (IntVec3 x) => CanScatterDeepAt(x, map), map, out IntVec3 result))
            {
                Log.Error("[DMS_Legion] IntegratedDetectionArray: Could not find a center cell for deep scanning lump generation!");
                return;
            }
            ThingDef thingDef = ChooseLocalDeepLumpThingDef();
            int numCells = Mathf.CeilToInt(thingDef.deepLumpSizeRange.RandomInRange);
            foreach (IntVec3 item in GridShapeMaker.IrregularLump(result, map, numCells))
            {
                if (CanScatterDeepAt(item, map) && !item.InNoBuildEdgeArea(map))
                    map.deepResourceGrid.SetAt(item, thingDef, thingDef.deepCountPerCell);
            }
            string key = ("LetterDeepScannerFoundLump".CanTranslate() ? "LetterDeepScannerFoundLump" : "DeepScannerFoundLump");
            Find.LetterStack.ReceiveLetter(
                "LetterLabelDeepScannerFoundLump".Translate() + ": " + thingDef.LabelCap,
                key.Translate(thingDef.label, (worker != null ? worker.Named("FINDER") : parent.Named("FINDER"))),
                LetterDefOf.PositiveEvent,
                new LookTargets(result, map));
        }

        private bool CanScatterDeepAt(IntVec3 pos, Map map)
        {
            TerrainDef terrainDef = map.terrainGrid.BaseTerrainAt(pos);
            if ((terrainDef != null && terrainDef.IsWater && terrainDef.passability == Traversability.Impassable)
                || !pos.GetAffordances(map).Contains(ThingDefOf.DeepDrill.terrainAffordanceNeeded))
                return false;
            return !map.deepResourceGrid.GetCellBool(CellIndicesUtility.CellToIndex(pos, map.Size.x));
        }

        private ThingDef ChooseLocalDeepLumpThingDef()
        {
            if (useSelectedMineralForLocalScan)
            {
                ThingDef? selectedResource = targetMineable?.building?.mineableThing;
                if (CanUseAsLocalDeepResource(selectedResource))
                    return selectedResource!;
                Log.Warning("[DMS_Legion] IntegratedDetectionArray: Selected mineable \""
                    + (targetMineable?.defName ?? "null")
                    + "\" / resource \""
                    + (selectedResource?.defName ?? "null")
                    + "\" is not suitable for local deep resource generation; falling back to random selection.");
            }
            return ChooseDeepLumpThingDef();
        }

        private static bool CanUseAsLocalDeepResource(ThingDef? def)
        {
            if (def == null)
                return false;
            if (def.deepLumpSizeRange == IntRange.Zero)
                return false;
            if (def.deepCountPerCell <= 0)
                return false;
            return true;
        }

        private static ThingDef ChooseDeepLumpThingDef()
        {
            return DefDatabase<ThingDef>.AllDefs.RandomElementByWeight((ThingDef def) => def.deepCommonality);
        }

        /// <summary>原版远距离矿物扫描仪 100% 效果：生成远距离矿脉任务。</summary>
        private void DoRemoteFind(Pawn worker, Map map)
        {
            if (targetMineable?.building?.mineableThing == null)
            {
                SetDefaultTargetMineralIfNeeded();
                if (targetMineable?.building?.mineableThing == null)
                    return;
            }
            Slate slate = new Slate();
            slate.Set("map", map);
            slate.Set("targetMineable", targetMineable);
            slate.Set("targetMineableThing", targetMineable.building.mineableThing);
            slate.Set("worker", worker);
            if (QuestScriptDefOf.LongRangeMineralScannerLump.CanRun(slate, map))
            {
                Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(QuestScriptDefOf.LongRangeMineralScannerLump, slate);
                Find.LetterStack.ReceiveLetter(quest.name, quest.description, LetterDefOf.PositiveEvent, null, null, quest);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
                yield return g;

            Map? map = parent?.Map;
            if (map == null)
                yield break;

            // 扫描模式切换按钮（进度仅显示在详细信息栏）
            bool powered = powerComp != null && powerComp.PowerOn;

            var modeCmd = new Command_Action
            {
                defaultLabel = isLocalMode
                    ? "DMSL_IntegratedDetectionArray_ModeLocal".Translate()
                    : "DMSL_IntegratedDetectionArray_ModeLongRange".Translate(),
                defaultDesc = isLocalMode
                    ? "DMSL_IntegratedDetectionArray_ModeLocalDesc".Translate()
                    : "DMSL_IntegratedDetectionArray_ModeLongRangeDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get(isLocalMode ? GizmoIconLocalScan : GizmoIconLongRangeScan, true),
                action = () => { isLocalMode = !isLocalMode; }
            };
            if (!powered)
                modeCmd.Disable("DMSL_IntegratedDetectionArray_Disable_NoPower".Translate());
            yield return modeCmd;

            // 本地定向扫描开关（仅本地模式显示）
            if (isLocalMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = useSelectedMineralForLocalScan
                        ? "DMSL_IntegratedDetectionArray_LocalTargetedScan_On".Translate()
                        : "DMSL_IntegratedDetectionArray_LocalTargetedScan_Off".Translate(),
                    defaultDesc = useSelectedMineralForLocalScan
                        ? "DMSL_IntegratedDetectionArray_LocalTargetedScan_OnDesc".Translate()
                        : "DMSL_IntegratedDetectionArray_LocalTargetedScan_OffDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get(GizmoIconLocalTargetedScan, true),
                    action = () => { useSelectedMineralForLocalScan = !useSelectedMineralForLocalScan; }
                };
            }

            // 选择目标矿物（远程模式始终显示；本地模式仅在定向扫描开启时显示）
            if (!isLocalMode || useSelectedMineralForLocalScan)
            {
                Command_Action? mineralCmd = CreateSelectMineralGizmo();
                if (mineralCmd != null)
                    yield return mineralCmd;
            }

            // 生成诱饵信号
            var decoyCmd = new Command_Action
            {
                defaultLabel = "DMSL_IntegratedDetectionArray_GenerateDecoyCoords".Translate(),
                defaultDesc = "DMSL_IntegratedDetectionArray_GenerateDecoyCoordsDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get(GizmoIconPath, true),
                action = () => StartDecoyTargeter(map)
            };
            if (!powered)
                decoyCmd.Disable("DMSL_IntegratedDetectionArray_Disable_NoPower".Translate());
            yield return decoyCmd;

            // DEV: 进度直接到 100%（仅开发模式且开启上帝模式时显示，与原版 ShowDevGizmos 一致）
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: 增加扫描进度至100%",
                    action = () =>
                    {
                        scanProgress = 1f;
                        ExecuteScanComplete();
                        scanProgress = 0f;
                    }
                };
            }
        }

        private Command_Action? CreateSelectMineralGizmo()
        {
            if (parent?.Faction != Faction.OfPlayer)
                return null;
            if (targetMineable?.building?.mineableThing is not ThingDef mineableThing)
                return null;

            return new Command_Action
            {
                defaultLabel = "CommandSelectMineralToScanFor".Translate() + ": " + mineableThing.LabelCap,
                defaultDesc = "CommandSelectMineralToScanForDesc".Translate(),
                icon = mineableThing.uiIcon,
                iconAngle = mineableThing.uiIconAngle,
                iconOffset = mineableThing.uiIconOffset,
                action = () =>
                {
                    List<ThingDef> mineables = ((GenStep_PreciousLump)GenStepDefOf.PreciousLump.genStep).mineables;
                    var list = new List<FloatMenuOption>();
                    foreach (ThingDef d in mineables)
                    {
                        if (d.building?.mineableThing == null)
                            continue;
                        ThingDef localD = d;
                        list.Add(new FloatMenuOption(localD.building.mineableThing.LabelCap, () =>
                        {
                            targetMineable = localD;
                        }, MenuOptionPriority.Default, null, null, 29f, (Rect rect) => Widgets.InfoCardButton(rect.x + 5f, rect.y + (rect.height - 24f) / 2f, localD.building.mineableThing)));
                    }
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            };
        }

        private void StartDecoyTargeter(Map targetMap)
        {
            var targetingParams = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetSelf = false,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => target.Cell.IsValid && target.Cell.InBounds(targetMap)
            };
            Find.Targeter.BeginTargeting(
                targetingParams,
                (LocalTargetInfo target) => OnDecoyCellSelected(targetMap, target.Cell),
                null, null, null,
                () => { });
        }

        private void OnDecoyCellSelected(Map map, IntVec3 cell)
        {
            var baitComponent = AerialRaidBaitTargetComponent.GetOrCreate(map);
            if (baitComponent == null)
            {
                Messages.Message("DMSL_IntegratedDetectionArray_NoBaitComponent".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            int durationTicks = Props?.decoyDurationTicks ?? 12500;
            baitComponent.SetBaitTarget(cell, durationTicks);
            Messages.Message("DMSL_IntegratedDetectionArray_DecoySet".Translate(cell), MessageTypeDefOf.PositiveEvent);
        }
    }
}
