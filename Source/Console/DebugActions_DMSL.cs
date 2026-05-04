using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using DMS_Legion.AerialRaid.AerialRaidComponents;
using DMS_Legion.GroundSupport;

namespace DMS_Legion.Console
{
    /// <summary>
    /// DMS Legion 控制台指令类
    /// 用于放置所有 DMSL 相关的调试和控制台指令
    /// </summary>
    public static class DebugActions_DMSL
    {
        /// <summary>
        /// 设置诱饵坐标
        /// 点击地图任意位置，在该位置生成/更改诱饵坐标
        /// </summary>
        [DebugAction("DMS 军团", name = "设置诱饵坐标", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SetBaitTarget()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("需要在地图中", MessageTypeDefOf.RejectInput);
                return;
            }

            IntVec3 cell = UI.MouseCell();
            if (!cell.IsValid || !cell.InBounds(map))
            {
                Messages.Message("无效的坐标位置", MessageTypeDefOf.RejectInput);
                return;
            }

            // 获取或创建诱饵目标组件
            var baitComponent = AerialRaidBaitTargetComponent.GetOrCreate(map);
            if (baitComponent == null)
            {
                Messages.Message("无法创建诱饵目标组件", MessageTypeDefOf.RejectInput);
                return;
            }

            // 设置诱饵坐标
            baitComponent.SetBaitTarget(cell);
            
            Messages.Message($"已在 {cell} 设置诱饵坐标", MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// 终止空袭计时
        /// 如果正在执行空袭倒计时，则终止倒计时流程，后续空袭不再触发
        /// </summary>
        [DebugAction("DMS 军团", name = "终止空袭计时", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CancelAerialRaid()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("需要在地图中", MessageTypeDefOf.RejectInput);
                return;
            }

            // 获取空袭前置阶段组件
            var prePhaseComponent = map.GetComponent<AerialRaidPrePhaseComponent>();
            
            // 如果组件不存在或已完成，说明当前无空袭
            if (prePhaseComponent == null || prePhaseComponent.GetCurrentState() == AerialRaidPrePhaseState.Completed)
            {
                Messages.Message("当前无空袭", MessageTypeDefOf.NeutralEvent);
                return;
            }

            // 使用反射访问私有字段来终止倒计时流程
            Type componentType = typeof(AerialRaidPrePhaseComponent);
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            
            // 设置状态为 Completed
            FieldInfo stateField = componentType.GetField("currentState", flags);
            stateField?.SetValue(prePhaseComponent, AerialRaidPrePhaseState.Completed);
            
            // 设置剩余时间为 0
            FieldInfo ticksField = componentType.GetField("remainingTicks", flags);
            ticksField?.SetValue(prePhaseComponent, 0);
            
            // 清除目标坐标
            FieldInfo targetField = componentType.GetField("targetCell", flags);
            targetField?.SetValue(prePhaseComponent, IntVec3.Invalid);
            
            // 重置决策 Tick
            FieldInfo decisionField = componentType.GetField("lastDecisionTick", flags);
            decisionField?.SetValue(prePhaseComponent, -1);
            
            // 重置显示状态（避免下次复用残留）
            FieldInfo displayPosField = componentType.GetField("crosshairHasDisplayPos", flags);
            displayPosField?.SetValue(prePhaseComponent, false);
            
            FieldInfo visualTargetField = componentType.GetField("lastVisualTargetCell", flags);
            visualTargetField?.SetValue(prePhaseComponent, IntVec3.Invalid);

            Messages.Message("空袭倒计时已终止", MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// 调用空中支援
        /// 返回 List&lt;DebugActionNode&gt; 时，原版会作为子菜单：点击后留在调试窗口内展示子项（进入子界面），
        /// 选择某一支援类型后再关闭窗口并唤起选点器。
        /// </summary>
        [DebugAction("DMS 军团", name = "调用空中支援", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> CallAerialSupport()
        {
            var list = new List<DebugActionNode>();
            var coordinator = AerialSupportCoordinator.Instance;
            if (coordinator == null)
                return list;

            var supportTypes = DefDatabase<AerialSupportTypeDef>.AllDefsListForReading;
            foreach (var supportType in supportTypes)
            {
                AerialSupportTypeDef st = supportType;
                int pointCount = GetPointCountForAerialSupport(st);
                string label = $"{st.label}（需选{pointCount}个点）";
                var node = new DebugActionNode(label, DebugActionType.Action, () =>
                {
                    // 在点击支援类型时再取当前地图，避免多地图时用错（子菜单打开时可能不是目标地图）
                    Map map = Find.CurrentMap;
                    if (map == null)
                    {
                        Messages.Message("需要在地图中", MessageTypeDefOf.RejectInput);
                        return;
                    }
                    coordinator.ExecuteAerialSupport(map, st);
                }, null);
                list.Add(node);
            }
            return list;
        }

        private static int GetPointCountForAerialSupport(AerialSupportTypeDef supportType)
        {
            switch (supportType.flightPathType)
            {
                case "Normal":
                    return 1;
                case "CustomLine":
                    return 2;
                case "MultiTarget":
                    return supportType.selectionPointCount;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// 清除核打击冷却（仅启用皇权 DLC 时生效），使核打击系统冷却时间归零。
        /// </summary>
        [DebugAction("DMS 军团", name = "清除核打击冷却", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ClearNukeStrikeCooldown()
        {
            if (!ModsConfig.RoyaltyActive)
            {
                Messages.Message("需要启用皇权 DLC", MessageTypeDefOf.RejectInput);
                return;
            }
            var comp = NukeStrikeCooldownComponent.GetOrCreate();
            if (comp == null)
            {
                Messages.Message("无法获取核打击冷却组件", MessageTypeDefOf.RejectInput);
                return;
            }
            comp.ClearCooldown();
            Messages.Message("核打击冷却已归零", MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// 生成培育配方所需物品
        /// 进入子界面展示所有机械体培育配方 defName，选中后视角转移至当前地图并可选点，
        /// 左键点击在所选格子里生成该配方全部所需物品及数量；存在可选材料时全部生成。
        /// </summary>
        [DebugAction("DMS 军团", name = "生成培育配方所需物品", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnGestationRecipeIngredients()
        {
            var gestationRecipes = DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(r => r.gestationCycles > 0)
                .OrderBy(r => r.defName)
                .ToList();
            if (gestationRecipes.Count == 0)
            {
                Messages.Message("未找到机械体培育配方（gestationCycles > 0）", MessageTypeDefOf.RejectInput);
                return;
            }
            var list = new List<DebugMenuOption>();
            foreach (var recipe in gestationRecipes)
            {
                RecipeDef localRecipe = recipe;
                list.Add(new DebugMenuOption(localRecipe.defName, DebugMenuOptionMode.Action, () =>
                {
                    Map map = Find.CurrentMap;
                    if (map == null)
                    {
                        Messages.Message("需要在地图中", MessageTypeDefOf.RejectInput);
                        return;
                    }
                    // 转移视角至当前地图（参考皇权“强制设置爵位”）
                    CameraJumper.TryHideWorld();
                    CameraJumper.TryJump(map.Center, map, CameraJumper.MovementMode.Cut);
                    // 设置选点工具：左键点击生成该配方所需物品
                    string toolLabel = "生成: " + localRecipe.defName;
                    DebugTools.curTool = new DebugTool(toolLabel, () =>
                    {
                        Map curMap = Find.CurrentMap;
                        if (curMap == null)
                        {
                            Messages.Message("需要在地图中", MessageTypeDefOf.RejectInput);
                            return;
                        }
                        IntVec3 cell = UI.MouseCell();
                        if (!cell.IsValid || !cell.InBounds(curMap))
                        {
                            Messages.Message("无效的坐标位置", MessageTypeDefOf.RejectInput);
                            return;
                        }
                        SpawnRecipeIngredientsAt(localRecipe, curMap, cell);
                    }, () => { });
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list, null));
        }

        /// <summary>
        /// 在指定格子里生成配方的全部所需物品：每个固定材料按数量生成；
        /// 若某材料为可选（filter 多个 thingDef），则每种可选材料各生成对应数量。
        /// </summary>
        private static void SpawnRecipeIngredientsAt(RecipeDef recipe, Map map, IntVec3 baseCell)
        {
            if (recipe.ingredients == null || recipe.ingredients.Count == 0)
            {
                Messages.Message("该配方无材料需求", MessageTypeDefOf.NeutralEvent);
                return;
            }
            int spawned = 0;
            int offset = 0;
            foreach (IngredientCount ing in recipe.ingredients)
            {
                foreach (ThingDef thingDef in ing.filter.AllowedThingDefs)
                {
                    int count = ing.CountRequiredOfFor(thingDef, recipe, null);
                    if (count <= 0)
                        continue;
                    int stackLimit = thingDef.stackLimit > 0 ? thingDef.stackLimit : 1;
                    int remaining = count;
                    while (remaining > 0)
                    {
                        int stackCount = Mathf.Min(remaining, stackLimit);
                        Thing thing = ThingMaker.MakeThing(thingDef, null);
                        thing.stackCount = stackCount;
                        IntVec3 cell = baseCell;
                        if (offset != 0)
                            cell = baseCell + new IntVec3(offset % 5 - 2, 0, offset / 5);
                        if (GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near, null))
                            spawned++;
                        remaining -= stackCount;
                        offset++;
                    }
                }
            }
        }

        /// <summary>
        /// 触发艾丽萨的青睐（测试）
        /// 直接执行一次效果（加好感、发信），不依赖叙事者 tick 或“Enable Storyteller”。
        /// 需满足：与武装殖民舰队（DMS_Army）非敌对、有机械师且使用带宽＞0、已开启生物科技。
        /// </summary>
        [DebugAction("DMS 军团", name = "触发艾丽萨的青睐", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TriggerElisaBandwidthFavor()
        {
            Map? map = Find.CurrentMap;
            if (map == null || !map.IsPlayerHome)
            {
                Messages.Message("需要处于主地图", MessageTypeDefOf.RejectInput);
                return;
            }
            bool ok = StorytellerComp_ElisaBandwidthApproval.TryRunOnce(map, null, markRun: false);
            if (ok)
                Messages.Message("已触发艾丽萨的青睐", MessageTypeDefOf.PositiveEvent);
            else
                Messages.Message("触发失败，条件未满足。", MessageTypeDefOf.RejectInput);
        }

        /// <summary>
        /// 触发艾丽萨额外事件（测试）
        /// 立即执行一次艾丽萨事件循环组件效果：遍历 incidents 列表寻找可执行事件并触发。
        /// 不检查 5-10 天间隔，不更新下次触发计时。
        /// </summary>
        [DebugAction("DMS 军团", name = "触发艾丽萨额外事件", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TriggerElisaIncidentCycle()
        {
            IIncidentTarget? target = Find.CurrentMap;
            if (target == null)
            {
                Messages.Message("需要在地图中", MessageTypeDefOf.RejectInput);
                return;
            }
            bool ok = StorytellerComp_ElisaIncidentCycle.TryRunOnce(target, null);
            if (ok)
                Messages.Message("已触发艾丽萨额外事件", MessageTypeDefOf.PositiveEvent);
            else
                Messages.Message("触发失败：无满足条件的事件。", MessageTypeDefOf.RejectInput);
        }

        /// <summary>
        /// 打开集群UI
        /// 点击后关闭调试控制台界面并打开集群建设管理界面（ModularOperationWindow）。
        /// </summary>
        [DebugAction("DMS 军团", name = "打开集群UI", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenClusterUI()
        {
            TryCloseDebugDialog();
            Find.WindowStack.Add(new ModularOperationWindow());
        }

        /// <summary>
        /// 尝试关闭调试控制台窗口（通过反射查找 LudeonTK 调试对话框类型并移除）。
        /// </summary>
        private static void TryCloseDebugDialog()
        {
            try
            {
                Type? debugDialogType = Type.GetType("LudeonTK.Dialog_DebugActionsMenu, Assembly-CSharp")
                    ?? Type.GetType("LudeonTK.Dialog_DebugActionsMenu, LudeonTK");
                if (debugDialogType != null)
                    Find.WindowStack.TryRemoveAssignableFromType(debugDialogType, true);
            }
            catch
            {
                // 忽略：无法关闭时仅打开集群UI即可
            }
        }
    }
}
