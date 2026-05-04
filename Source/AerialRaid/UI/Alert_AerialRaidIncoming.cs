using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using DMS_Legion.AerialRaid.AerialRaidComponents;

namespace DMS_Legion.AerialRaid
{
    /// <summary>
    /// 空袭即将到达的持续警告Alert
    /// 红底白字显示在屏幕顶部
    /// 参考原版Alert_Critical的实现方式
    /// </summary>
    public class Alert_AerialRaidIncoming : Alert_Critical
    {

        /// <summary>
        /// 获取Alert的标签文本
        /// </summary>
        public override string GetLabel()
        {
            var components = GetActiveComponents();
            if (components.Count == 0)
            {
                return "";
            }

            var first = components[0];
            int remainingTicks = first.GetRemainingTicks();
            float hours = remainingTicks / 2500f;
            string hoursText = hours >= 1f
                ? Mathf.RoundToInt(hours).ToString()
                : hours.ToString("0.0");

            if (components.Count == 1)
            {
                return "DMSL_Alert_AerialRaidIncoming_LabelSingle".Translate(hoursText);
            }

            return "DMSL_Alert_AerialRaidIncoming_LabelMultiple".Translate(hoursText, components.Count);
        }

        /// <summary>
        /// 获取Alert的详细说明
        /// </summary>
        public override TaggedString GetExplanation()
        {
            var components = GetActiveComponents();
            if (components.Count == 0)
            {
                return "DMSL_Alert_AerialRaidIncoming_ExplanationIntro".Translate();
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("DMSL_Alert_AerialRaidIncoming_ExplanationIntro".Translate());
            sb.AppendLine();
            sb.AppendLine("DMSL_Alert_AerialRaidIncoming_CountdownList".Translate());

            for (int i = 0; i < components.Count; i++)
            {
                int remainingTicks = components[i].GetRemainingTicks();
                float hours = remainingTicks / 2500f;
                string hoursText = hours >= 1f
                    ? Mathf.RoundToInt(hours).ToString()
                    : hours.ToString("0.0");
                sb.AppendLine("DMSL_Alert_AerialRaidIncoming_CountdownLine".Translate(hoursText));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取Alert报告，决定是否显示Alert
        /// </summary>
        public override AlertReport GetReport()
        {
            var components = GetActiveComponents();
            if (components.Count == 0)
            {
                return AlertReport.Inactive;
            }

            return AlertReport.Active;
        }

        /// <summary>
        /// 获取当前活动的空袭前置阶段组件
        /// </summary>
        private List<AerialRaidPrePhaseComponent> GetActiveComponents()
        {
            var result = new List<AerialRaidPrePhaseComponent>();

            foreach (var map in Find.Maps)
            {
                if (map == null) continue;
                var component = map.GetComponent<AerialRaidPrePhaseComponent>();
                if (component == null) continue;

                int remainingTicks = component.GetRemainingTicks();
                if (remainingTicks <= 0) continue;

                result.Add(component);
            }

            result.Sort((a, b) => a.GetRemainingTicks().CompareTo(b.GetRemainingTicks()));
            return result;
        }
    }
}
