// ============================================================================
// 文件：CommsSupportCooldownTracker.cs
// 说明：通讯台空中支援选项冷却 GameComponent，持久化各选项上次使用 tick
// 功能：IsOnCooldown、RecordUse、GetCooldownDisableReason（格式化剩余小时供 DMSL_Comms_OptionCooldown 显示）
// ============================================================================

using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 通讯台空中支援选项冷却追踪器
    /// </summary>
    public class CommsSupportCooldownTracker : GameComponent
    {
        private static CommsSupportCooldownTracker? instance;
        public static CommsSupportCooldownTracker? Instance => instance;

        private Dictionary<string, int> lastUseTickByOptionDefName = new Dictionary<string, int>();

        private const float TicksPerHour = 2500f;

        public CommsSupportCooldownTracker(Game game)
        {
            instance = this;
        }

        /// <summary>
        /// 是否处于冷却中
        /// </summary>
        public bool IsOnCooldown(CommsAirSupportOptionDef optionDef)
        {
            if (optionDef == null || optionDef.cooldownTicks <= 0)
                return false;
            if (!lastUseTickByOptionDefName.TryGetValue(optionDef.defName, out int lastTick))
                return false;
            int remaining = optionDef.cooldownTicks - (Find.TickManager.TicksGame - lastTick);
            return remaining > 0;
        }

        /// <summary>
        /// 记录该选项被使用
        /// </summary>
        public void RecordUse(CommsAirSupportOptionDef optionDef)
        {
            if (optionDef == null)
                return;
            lastUseTickByOptionDefName[optionDef.defName] = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// 获取冷却禁用理由文案，用于 DiaOption.Disable。
        /// 格式：剩余 tick / 2500 向下取整；若结果在 0～1 之间则显示一位小数（向下取整）。
        /// </summary>
        public string GetCooldownDisableReason(CommsAirSupportOptionDef optionDef)
        {
            if (optionDef == null || !lastUseTickByOptionDefName.TryGetValue(optionDef.defName, out int lastTick))
                return "DMSL_Comms_OptionCooldown".Translate("0");
            int remaining = optionDef.cooldownTicks - (Find.TickManager.TicksGame - lastTick);
            if (remaining <= 0)
                return "DMSL_Comms_OptionCooldown".Translate("0");
            float hoursRaw = remaining / TicksPerHour;
            string formattedHours;
            if (hoursRaw >= 1f)
            {
                formattedHours = Mathf.FloorToInt(hoursRaw).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                float hoursFloored = Mathf.Floor(hoursRaw * 10f) / 10f;
                formattedHours = hoursFloored.ToString("0.0", CultureInfo.InvariantCulture);
            }
            return "DMSL_Comms_OptionCooldown".Translate(formattedHours);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastUseTickByOptionDefName, "commsSupportLastUseTickByOptionDefName", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lastUseTickByOptionDefName ??= new Dictionary<string, int>();
            }
        }
    }
}
