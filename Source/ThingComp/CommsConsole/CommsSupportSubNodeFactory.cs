// ============================================================================
// 文件：CommsSupportSubNodeFactory.cs
// 说明：通讯台空中支援子界面公共工厂
// 功能：按 category 生成选项节点、确认节点；关闭对话框、启动世界格选点
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using DMS_Legion.GroundSupport;
using RimWorld;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 通讯台空中支援子界面工厂，按 category 筛选 Def 并生成 DiaNode
    /// </summary>
    public static class CommsSupportSubNodeFactory
    {
        /// <summary>
        /// 创建选项子节点：遍历对应 category 的 CommsAirSupportOptionDef，生成 DiaOption
        /// </summary>
        public static DiaNode CreateOptionsNode(string category, Faction faction, Pawn negotiator)
        {
            string leaderName = faction.leader?.Name?.ToStringFull ?? faction.Name;
            DiaNode optionsNode = new DiaNode("DMSL_Comms_AirSupportPrompt".Translate(leaderName));

            foreach (var optionDef in DefDatabase<CommsAirSupportOptionDef>.AllDefs.Where(d => d.category == category))
            {
                int goodwillCost = -Faction.OfPlayer.CalculateAdjustedGoodwillChange(faction, -optionDef.goodwillCostBase);
                DiaOption opt = new DiaOption(optionDef.label + "（花费: " + goodwillCost + "好感度）");

                if (CommsSupportCooldownTracker.Instance?.IsOnCooldown(optionDef) == true)
                {
                    opt.Disable(CommsSupportCooldownTracker.Instance.GetCooldownDisableReason(optionDef));
                }
                else
                {
                    opt.link = CreateConfirmNode(optionDef, faction, negotiator, () => CreateOptionsNode(category, faction, negotiator));
                }

                optionsNode.options.Add(opt);
            }

            DiaOption goBackOpt = new DiaOption("GoBack".Translate());
            goBackOpt.linkLateBind = () => AirSupportSubNodeBuilder.CreateSubNode(faction, negotiator);
            optionsNode.options.Add(goBackOpt);

            return optionsNode;
        }

        /// <summary>
        /// 创建确认节点：返回、确认；确认后关对话、扣好感、记冷却、启动世界格选点
        /// </summary>
        public static DiaNode CreateConfirmNode(CommsAirSupportOptionDef optionDef, Faction faction, Pawn negotiator, Func<DiaNode> backToOptionsNode)
        {
            var aerialSupportType = DefDatabase<AerialSupportTypeDef>.GetNamed(optionDef.aerialSupportDefName, false);
            string label = aerialSupportType?.label ?? optionDef.label;
            string description = aerialSupportType?.description ?? string.Empty;
            DiaNode confirmNode = new DiaNode("DMSL_Comms_ConfirmSupportPrompt".Translate(label, description));

            // 确定键在右，返回键在左（对称布局）
            DiaOption confirmOpt = new DiaOption("Confirm".Translate());
            confirmOpt.action = () =>
            {
                CloseCommsDialog();

                Faction.OfPlayer.TryAffectGoodwillWith(faction, -optionDef.goodwillCostBase, false, true, null, null);
                CommsSupportCooldownTracker.Instance?.RecordUse(optionDef);
                CommsSupportTargeting.BeginWorldTargeting(optionDef, faction, negotiator);
            };
            confirmNode.options.Add(confirmOpt);

            DiaOption returnOpt = new DiaOption("GoBack".Translate());
            returnOpt.linkLateBind = backToOptionsNode;
            confirmNode.options.Add(returnOpt);

            return confirmNode;
        }

        /// <summary>
        /// 关闭当前通讯台对话框
        /// </summary>
        public static void CloseCommsDialog()
        {
            Find.WindowStack.TryRemoveAssignableFromType(typeof(Dialog_NodeTree), true);
        }
    }
}
