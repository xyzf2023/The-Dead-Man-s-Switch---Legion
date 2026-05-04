using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using DMS_Legion.GroundSupport;

namespace DMS_Legion
{
    /// <summary>
    /// 空中支援能力效果组件 - 处理完整流程
    /// </summary>
    public class CompAbilityEffect_AerialSupport : CompAbilityEffect
    {
        /// <summary>
        /// 获取空中支援渲染器组件
        /// </summary>
        private AerialSupportRenderer? AerialSupportRenderer
        {
            get
            {
                return parent.pawn?.Map?.GetComponent<AerialSupportRenderer>();
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            // 不需要目标，总是返回 true
            return true;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 不需要目标，即使 target 是 Invalid 也可以应用
            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            var renderer = AerialSupportRenderer;
            if (renderer == null)
                return;

            var selectedType = renderer.GetSelectedSupportType();
            if (selectedType == null)
            {
                // 没有选择支援类型，显示菜单
                ShowAerialSupportMenu();
            }
            else
            {
                // 有选中的支援类型，开始目标选择流程
                StartTargetSelectionProcess();
            }
        }

        /// <summary>
        /// 显示空中支援类型选择菜单
        /// </summary>
        private void ShowAerialSupportMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // 获取所有可用的空中支援类型
            var supportTypes = DefDatabase<AerialSupportTypeDef>.AllDefsListForReading;

            foreach (var supportType in supportTypes)
            {
                // 创建菜单选项
                FloatMenuOption option = new FloatMenuOption(
                    supportType.label,
                    () => SelectAerialSupportType(supportType)
                );

                options.Add(option);
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        /// <summary>
        /// 选择空中支援类型
        /// </summary>
        private void SelectAerialSupportType(AerialSupportTypeDef supportType)
        {
            // 确保空中支援组件存在
            var renderer = AerialSupportRenderer;
            if (renderer == null)
            {
                // 如果组件不存在，创建它
                renderer = new AerialSupportRenderer(parent.pawn.Map);
                parent.pawn.Map.components.Add(renderer);
            }

            // 设置当前选择的支援类型
            renderer.SetSelectedSupportType(supportType);

            // 播放选择音效
            SoundDef.Named("Click").PlayOneShotOnCamera(null);

            // 计算需要选点的数量
            int pointCount = GetPointCountForSupportType(supportType);

            // 显示选择确认消息
            Messages.Message($"已选择{supportType.label}，请选择{pointCount}个目标位置。".Translate(), MessageTypeDefOf.CautionInput);

            // 开始目标选择流程
            StartTargetSelectionProcess();
        }

        /// <summary>
        /// 开始目标选择流程
        /// </summary>
        private void StartTargetSelectionProcess()
        {
            var renderer = AerialSupportRenderer;
            if (renderer == null) return;

            var selectedType = renderer.GetSelectedSupportType();
            if (selectedType == null) return;

            // 根据飞行路径类型选择对应的Job
            // CustomLine和MultiTarget都使用SelectCustomLine Job（因为需要多点选择和coordinator流程）
            JobDef jobDef = (selectedType.flightPathType == "CustomLine" || selectedType.flightPathType == "MultiTarget")
                ? DMSL_JobDefOf.DMSL_AerialSupport_SelectCustomLine
                : DMSL_JobDefOf.DMSL_AerialSupport_SelectTarget;

            // 创建目标选择的Job
            Job job = JobMaker.MakeJob(jobDef);
            job.playerForced = true;

            // 开始Job
            parent.pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        /// <summary>
        /// 根据支援类型获取选点数量
        /// </summary>
        private int GetPointCountForSupportType(AerialSupportTypeDef supportType)
        {
            switch (supportType.flightPathType)
            {
                case "Normal":
                    return 1;                    // 单点打击
                case "CustomLine":
                    return 2;                    // 两点直线
                case "MultiTarget":
                    return supportType.selectionPointCount; // N点多目标
                default:
                    return 1;                    // 默认值
            }
        }

        /// <summary>
        /// 检查能力是否可以被使用
        /// </summary>
        public override bool GizmoDisabled(out string? reason)
        {
            if (base.GizmoDisabled(out reason))
            {
                return true;
            }

            // 能力总是可用
            reason = null;
            return false;
        }
    }
}
