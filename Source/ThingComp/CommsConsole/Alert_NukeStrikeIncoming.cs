// ============================================================================
// 文件：Alert_NukeStrikeIncoming.cs
// 说明：核打击即将到达的持续严重警告 Alert，逻辑参考 AerialRaid/UI/Alert_AerialRaidIncoming
// 文本：预计核打击将于{剩余tick/2500，保留一位小数向下取整}小时后达到
// ============================================================================

using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 核打击即将到达的持续警告 Alert，红底白字显示在屏幕顶部（Alert_Critical）
    /// </summary>
    public class Alert_NukeStrikeIncoming : Alert_Critical
    {
        private const float TicksPerHour = 2500f;

        /// <summary>剩余 tick 转小时：保留一位小数，向下取整</summary>
        private static float TicksToHoursRoundedDown(int remainingTicks)
        {
            return Mathf.Floor(remainingTicks / TicksPerHour * 10f) / 10f;
        }

        private static string HoursText(float hours)
        {
            return hours >= 1f ? Mathf.RoundToInt(hours).ToString() : hours.ToString("0.0");
        }

        public override string GetLabel()
        {
            if (!ModsConfig.RoyaltyActive) return "";
            int? soonestTicks = GetSoonestNukeStrikeRemainingTicks();
            if (soonestTicks == null)
                return "";
            float hours = TicksToHoursRoundedDown(soonestTicks.Value);
            return "DMSL_NukeStrike_AlertLabel".Translate(HoursText(hours));
        }

        public override TaggedString GetExplanation()
        {
            if (!ModsConfig.RoyaltyActive) return "";
            int? soonestTicks = GetSoonestNukeStrikeRemainingTicks();
            if (soonestTicks == null)
                return "DMSL_NukeStrike_AlertExplanationIntro".Translate();
            float hours = TicksToHoursRoundedDown(soonestTicks.Value);
            return "DMSL_NukeStrike_AlertExplanationIntro".Translate() + "\n\n" + "DMSL_NukeStrike_AlertLabel".Translate(HoursText(hours));
        }

        public override AlertReport GetReport()
        {
            if (!ModsConfig.RoyaltyActive) return AlertReport.Inactive;
            return GetSoonestNukeStrikeRemainingTicks() != null ? AlertReport.Active : AlertReport.Inactive;
        }

        /// <summary>
        /// 返回所有地图上待执行核打击中剩余 tick 最小的一个（最早到达），无则 null。
        /// </summary>
        private static int? GetSoonestNukeStrikeRemainingTicks()
        {
            int? min = null;
            foreach (var map in Find.Maps)
            {
                if (map == null) continue;
                var comp = map.GetComponent<CommsSupportPendingComponent>();
                if (comp == null) continue;
                foreach (int t in comp.GetPendingNukeStrikeRemainingTicks())
                {
                    if (t <= 0) continue;
                    if (min == null || t < min.Value) min = t;
                }
            }
            return min;
        }
    }
}
