// ============================================================================
// 文件：DMSL_ModSettings.cs
// 说明：DMS Legion MOD的设置类
// 功能：管理MOD的各种设置项，包括自定义UI和字体选项
// ============================================================================

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// DMS Legion MOD设置类
    /// 继承自ModSettings，支持保存和加载设置
    /// </summary>
    public class DMSL_ModSettings : ModSettings
    {
        // 静态设置实例
        public static DMSL_ModSettings? settings;

        // MOD内容包引用（用于获取动态路径）
        public static ModContentPack? modContent;

        // UI设置
        public bool useCustomUI = true;  // 是否使用自定义UI（包括贴图和字体）

        // 事件设置
        public bool enableAerialRaid = true;  // 是否启用空袭事件
        public bool enableAerialRaidPager = true;  // 是否启用海盗空袭先导袭击事件

        // 视听效果设置
        public bool playAirRaidSiren = true;  // 空袭倒计时时是否播放防空警报
        public bool playNuclearStrikeAudioVisual = true;  // 核打击时是否播放白屏与耳鸣
        public bool playIEDDetonateSound = true;  // 点击引爆 IED Gizmo 时是否播放音效

        // 工作相关
        public bool enableDrillingBargeDeepDrill = true;  // 允许钻井驳机直接开采深层矿脉，默认开启

        // 叙事者相关
        public bool enableElectronicAngelSupport = true;          // 允许拉斐尔/艾丽萨将黑衣人替换为电子天使支援，默认开启
        public bool electronicAngelNoStorytellerLimit = false;    // 解除电子天使事件的叙事者限制，默认关闭

        // 未知机兵支援相关
        public bool enableUnknownMechSupport = true;               // 允许拉斐尔叙事者下生成未知机兵支援事件，默认开启
        public bool unknownMechNoStorytellerLimit = false;         // 解除未知机兵支援叙事者限制，默认关闭

        // 拉斐尔额外任务组件
        public bool enableRaphaelExtraQuest = true;                // 启用拉斐尔额外任务组件，默认开启

        // 杂项
        public bool enableDrillingBargeExperimentalWorkLogic = false;  // 启用钻井驳机实验性工作逻辑（缓存+分布式扫描，默认关闭）
        public bool enableExtraStopReconOption = true;  // 启用额外的终止侦察选项（AXF-12 Gizmo 与通讯台「停止侦察」），默认开启
        public bool autoAddDigitalAngelFaction = true;  // 自动添加派系：电子天使（在加载存档时为缺少该派系的世界补生成隐藏派系），默认开启
        public bool enableTankCrushEffect = false;  // 启用坦克碾压效果：超重型机兵近战按体型差追加钝击伤害，默认关闭

        // 字体资源缓存
        private static Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();

        // AssetBundle字体缓存（避免重复加载）
        private static Font? _assetBundleFontCache = null;

        // 字体文件目录路径（动态获取）
        private static string? _fontsDirectory;
        private static string FONTS_DIRECTORY
        {
            get
            {
                if (_fontsDirectory == null)
                {
                    if (modContent != null)
                    {
                        _fontsDirectory = System.IO.Path.Combine(modContent.RootDir, "Content", "Fonts");
                    }
                    else
                    {
                        throw new System.InvalidOperationException("[DMS_Legion] 工业中枢集群UI字体缺失，回退至游戏字体。");
                    }
                }
                return _fontsDirectory;
            }
        }




        /// <summary>
        /// 获取自定义字体（根据UI设置自动选择）
        /// </summary>
        public static Font? GetFont()
        {
            // 检查是否启用了自定义UI（包括字体）
            if (settings == null || !settings.useCustomUI)
            {
                return null; // 使用游戏默认字体
            }

            return GetModFontAlways();
        }

        /// <summary>
        /// 获取 MOD 字体文件中的字体（不检查 useCustomUI，供始终使用自定义字体的 UI 使用）
        /// </summary>
        public static Font? GetModFontAlways()
        {
            const string cacheKey = "mod_font";
            if (_fontCache.TryGetValue(cacheKey, out Font cachedFont))
                return cachedFont;

            Font? font = LoadModFont();
            if (font != null)
                _fontCache[cacheKey] = font;
            return font;
        }

        /// <summary>
        /// 加载MOD文件夹中的字体文件
        /// </summary>
        private static Font? LoadModFont()
        {
            try
            {
                if (!System.IO.Directory.Exists(FONTS_DIRECTORY))
                {
                    return null;
                }

                // 支持常见的字体文件格式（按优先级排序）
                string[] fontExtensions = { "*.ttf", "*.otf", "*.fon", "*.fnt" };

                foreach (var extension in fontExtensions)
                {
                    try
                    {
                        var fontFiles = System.IO.Directory.GetFiles(FONTS_DIRECTORY, extension, System.IO.SearchOption.TopDirectoryOnly);
                        if (fontFiles.Length > 0)
                        {
                            // 使用第一个找到的字体文件
                            string fontPath = fontFiles[0];
                            string fontName = System.IO.Path.GetFileNameWithoutExtension(fontPath);

                            // 尝试从系统字体API加载（基于字体文件名）
                            Font font = Font.CreateDynamicFontFromOSFont(fontName, 12);
                            if (font != null)
                            {
                                Log.Message($"[DMS_Legion] 已从系统字体加载 MOD 字体：{fontName}");
                                return font;
                            }
                        }
                    }
                    catch (System.Exception)
                    {
                        // 忽略单个扩展名的错误，继续尝试其他格式
                    }
                }

                // 如果没找到单独的字体文件，尝试AssetBundle
                if (_assetBundleFontCache == null)
                {
                    string rimfontsPath = System.IO.Path.Combine(FONTS_DIRECTORY, "rimfonts");
                    if (System.IO.File.Exists(rimfontsPath))
                    {
                        try
                        {
                            var assetBundle = UnityEngine.AssetBundle.LoadFromFile(rimfontsPath);
                            if (assetBundle != null)
                            {
                                var fonts = assetBundle.LoadAllAssets<Font>();
                                if (fonts.Length > 0)
                                {
                                    Log.Message("[DMS_Legion] 已从 AssetBundle 加载字体");
                                    assetBundle.Unload(false); // 保留字体资源
                                    _assetBundleFontCache = fonts[0];
                                }
                                else
                                {
                                    assetBundle.Unload(true);
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Log.Warning($"[DMS_Legion] 从 AssetBundle 加载字体失败：{e.Message}");
                        }
                    }
                }

                if (_assetBundleFontCache != null)
                {
                    return _assetBundleFontCache;
                }

                // 如果都没有找到字体文件，返回null
                return null;
            }
            catch (System.Exception e)
            {
                Log.Warning($"[DMS_Legion] 加载 MOD 字体时出错：{e.Message}");
            }

            return null;
        }


        /// <summary>
        /// 从AssetBundle获取所有可用的字体名称
        /// </summary>

        /// <summary>
        /// 清除字体缓存
        /// </summary>
        public static void ClearFontCache()
        {
            foreach (var font in _fontCache.Values)
            {
                if (font != null)
                {
                    Object.Destroy(font);
                }
            }
            _fontCache.Clear();
            _assetBundleFontCache = null;
        }

        /// <summary>
        /// 保存设置数据
        /// </summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref useCustomUI, "useCustomUI", true);  // 默认保持开启自定义UI风格
            Scribe_Values.Look(ref enableAerialRaid, "enableAerialRaid", true);
            Scribe_Values.Look(ref enableAerialRaidPager, "enableAerialRaidPager", true);
            Scribe_Values.Look(ref playAirRaidSiren, "playAirRaidSiren", true);
            Scribe_Values.Look(ref playNuclearStrikeAudioVisual, "playNuclearStrikeAudioVisual", true);
            Scribe_Values.Look(ref playIEDDetonateSound, "playIEDDetonateSound", true);
            Scribe_Values.Look(ref enableDrillingBargeDeepDrill, "enableDrillingBargeDeepDrill", true);
            Scribe_Values.Look(ref enableElectronicAngelSupport, "enableElectronicAngelSupport", true);
            Scribe_Values.Look(ref electronicAngelNoStorytellerLimit, "electronicAngelNoStorytellerLimit", false);
            Scribe_Values.Look(ref enableUnknownMechSupport, "enableUnknownMechSupport", true);
            Scribe_Values.Look(ref unknownMechNoStorytellerLimit, "unknownMechNoStorytellerLimit", false);
            Scribe_Values.Look(ref enableRaphaelExtraQuest, "enableRaphaelExtraQuest", true);
            Scribe_Values.Look(ref enableDrillingBargeExperimentalWorkLogic, "enableDrillingBargeExperimentalWorkLogic", false);
            Scribe_Values.Look(ref enableExtraStopReconOption, "enableExtraStopReconOption", true);
            Scribe_Values.Look(ref autoAddDigitalAngelFaction, "autoAddDigitalAngelFaction", true);
            Scribe_Values.Look(ref enableTankCrushEffect, "enableTankCrushEffect", false);
        }
    }
}
