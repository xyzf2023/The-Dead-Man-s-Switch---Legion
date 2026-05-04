// ============================================================================
// 文件：DMSL_Mod.cs
// 说明：DMS Legion MOD主类
// 功能：MOD的入口点，处理设置页面和初始化
// ============================================================================

using System.Collections.Generic;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace DMS_Legion
{
    /// <summary>
    /// DMS Legion MOD主类
    /// </summary>
    public class DMSL_Mod : Mod
    {
        /// <summary>
        /// MOD设置实例
        /// </summary>
        private DMSL_ModSettings settings;

        /// <summary>
        /// 设置窗口滚动位置
        /// </summary>
        private Vector2 settingsScrollPosition = Vector2.zero;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="content">MOD内容包</param>
        public DMSL_Mod(ModContentPack content) : base(content)
        {
            // 获取或创建设置实例
            settings = GetSettings<DMSL_ModSettings>();
            // 设置静态引用
            DMSL_ModSettings.settings = settings;
            // 设置MOD内容包引用（用于动态路径）
            DMSL_ModSettings.modContent = content;
        }

        /// <summary>
        /// 绘制设置窗口内容
        /// </summary>
        /// <param name="inRect">设置窗口区域</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // 为设置界面添加滚动条，避免在小分辨率或较多选项时内容被裁切
            Rect scrollRect = inRect;
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, scrollRect.height + 400f);

            Widgets.BeginScrollView(scrollRect, ref settingsScrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // UI设置部分（已注释：保持默认自定义UI风格，不再在设置中显示）
            // listing.Label("UI 设置");
            // listing.GapLine();
            // listing.CheckboxLabeled("使用自定义UI",
            //     ref settings.useCustomUI,
            //     "启用后将使用自定义贴图替换按钮和背景样式，并自动使用MOD文件夹中的字体文件。关闭后使用游戏默认UI和字体。需要重启游戏才能完全生效。");
            // listing.Gap(20f);

            // 事件设置部分
            listing.Label("DMSL_Settings_EventSection".Translate());
            listing.GapLine();

            // 空袭事件设置
            listing.CheckboxLabeled("DMSL_Settings_EnableAerialRaid".Translate(),
                ref settings.enableAerialRaid,
                "DMSL_Settings_EnableAerialRaid_Desc".Translate());

            // 海盗空袭先导袭击事件设置
            listing.CheckboxLabeled("DMSL_Settings_EnableAerialRaidPager".Translate(),
                ref settings.enableAerialRaidPager,
                "DMSL_Settings_EnableAerialRaidPager_Desc".Translate());

            listing.Gap(20f);

            // 视听效果设置
            listing.Label("DMSL_Settings_AudioVisualSection".Translate());
            listing.GapLine();
            listing.CheckboxLabeled("DMSL_Settings_PlayAirRaidSiren".Translate(),
                ref settings.playAirRaidSiren,
                "DMSL_Settings_PlayAirRaidSiren_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_PlayNuclearStrikeAudioVisual".Translate(),
                ref settings.playNuclearStrikeAudioVisual,
                "DMSL_Settings_PlayNuclearStrikeAudioVisual_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_PlayIEDDetonateSound".Translate(),
                ref settings.playIEDDetonateSound,
                "DMSL_Settings_PlayIEDDetonateSound_Desc".Translate());

            listing.Gap(20f);

            // 工作相关
            listing.Label("DMSL_Settings_WorkSection".Translate());
            listing.GapLine();
            listing.CheckboxLabeled("DMSL_Settings_EnableDrillingBargeDeepDrill".Translate(),
                ref settings.enableDrillingBargeDeepDrill,
                "DMSL_Settings_EnableDrillingBargeDeepDrill_Desc".Translate());

            listing.Gap(20f);

            // 叙事者
            listing.Label("DMSL_Settings_StorytellerSection".Translate());
            listing.GapLine();
            listing.CheckboxLabeled("DMSL_Settings_EnableElectronicAngelSupport".Translate(),
                ref settings.enableElectronicAngelSupport,
                "DMSL_Settings_EnableElectronicAngelSupport_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_ElectronicAngelNoStorytellerLimit".Translate(),
                ref settings.electronicAngelNoStorytellerLimit,
                "DMSL_Settings_ElectronicAngelNoStorytellerLimit_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_EnableUnknownMechSupport".Translate(),
                ref settings.enableUnknownMechSupport,
                "DMSL_Settings_EnableUnknownMechSupport_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_UnknownMechNoStorytellerLimit".Translate(),
                ref settings.unknownMechNoStorytellerLimit,
                "DMSL_Settings_UnknownMechNoStorytellerLimit_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_EnableRaphaelExtraQuest".Translate(),
                ref settings.enableRaphaelExtraQuest,
                "DMSL_Settings_EnableRaphaelExtraQuest_Desc".Translate());

            listing.Gap(20f);

            // 杂项
            listing.Label("DMSL_Settings_MiscSection".Translate());
            listing.GapLine();
            listing.CheckboxLabeled("DMSL_Settings_EnableDrillingBargeExperimentalWorkLogic".Translate(),
                ref settings.enableDrillingBargeExperimentalWorkLogic,
                "DMSL_Settings_EnableDrillingBargeExperimentalWorkLogic_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_EnableExtraStopReconOption".Translate(),
                ref settings.enableExtraStopReconOption,
                "DMSL_Settings_EnableExtraStopReconOption_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_AutoAddDigitalAngelFaction".Translate(),
                ref settings.autoAddDigitalAngelFaction,
                "DMSL_Settings_AutoAddDigitalAngelFaction_Desc".Translate());
            listing.CheckboxLabeled("DMSL_Settings_EnableTankCrushEffect".Translate(),
                ref settings.enableTankCrushEffect,
                "DMSL_Settings_EnableTankCrushEffect_Desc".Translate());

            listing.Gap(20f);

            // 重置按钮（useCustomUI 保持默认 true，不再在设置中暴露）
            if (listing.ButtonText("DMSL_Settings_ResetToDefault".Translate()))
            {
                // settings.useCustomUI = true;  // 始终为 true，不显示在设置中
                settings.enableAerialRaid = true;
                settings.enableAerialRaidPager = true;
                settings.playAirRaidSiren = true;
                settings.playNuclearStrikeAudioVisual = true;
                settings.playIEDDetonateSound = true;
                settings.enableDrillingBargeDeepDrill = true;
                settings.enableRaphaelExtraQuest = true;
                settings.enableDrillingBargeExperimentalWorkLogic = false;
                settings.enableExtraStopReconOption = true;
                settings.autoAddDigitalAngelFaction = true;
                settings.enableTankCrushEffect = false;
                DMSL_ModSettings.ClearFontCache();
            }

            listing.End();

            Widgets.EndScrollView();
        }

        /// <summary>
        /// 设置类别名称
        /// </summary>
        /// <returns>设置类别显示名称</returns>
        public override string SettingsCategory()
        {
            return "DMS Legion";
        }
    }
}
