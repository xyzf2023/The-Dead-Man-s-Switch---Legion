// ============================================================================
// 文件：ModularOperationWindow.cs
// 说明：模块化操作界面主窗口
// 功能：实现基础布局框架，包含建筑槽位、功能按钮、资源展示和任务队列
// ============================================================================

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 模块化操作界面主窗口
    /// 实现左右两栏布局（3:1比例），包含建筑槽位、功能按钮、资源展示和任务队列
    /// </summary>
    public class ModularOperationWindow : Window
    {
        /* ===============================
         * Window 基本设置
         * =============================== */

        public override Vector2 InitialSize => new Vector2(1095f, 750f);

        // 自定义UI时完全接管边距，未启用时使用默认边距
        protected override float Margin => UseCustomUI ? 0f : base.Margin;

        /* ===============================
         * 背景 → 内容 的安全边距（px）
         * 与背景图结构一一对应
         * =============================== */

        private const float ContentMarginTop = 40f;
        private const float ContentMarginBottom = 10f;
        private const float ContentMarginLeft = 10f;
        private const float ContentMarginRight = 10f;

        /* ===============================
         * 内部状态
         * =============================== */

        private Vector2 rightScrollPosition = Vector2.zero;
        private Vector2 resourceScrollPosition = Vector2.zero;

        // 状态数据
        private int selectedBuildingIndex = -1;
        private int? selectedSlotIndex = null;  // 当前选中的槽位索引（用于展开额外窗口）

        // 建筑槽位数据（12个槽位，初始为空）
        private List<BuildingSlot> buildingSlots = new List<BuildingSlot>();

        // ================================
        // ================================
        // SystemOutput UI 相关字段
        // ================================

        // SystemOutput缓存（可选，用于性能优化）
        private List<SystemMessage>? _cachedMessages;
        private float _lastCacheUpdateTime;

        // SystemOutput逐行显示控制
        private Queue<SystemOutputLine> _pendingLines = new Queue<SystemOutputLine>(); // 等待显示的行队列
        private List<SystemOutputLine> _displayedLines = new List<SystemOutputLine>(); // 已显示的行列表
        private float _nextLineDisplayTime = 0f; // 下一次显示行的时间
        private const float LINE_DISPLAY_INTERVAL = 0.5f; // 行显示间隔（秒）

        // UI颜色定义（参考RimWorld风格）
        private static readonly Color bgColor = new Color(0.05f, 0.05f, 0.05f);
        private static readonly Color panelColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Color buttonColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color highlightedButtonColor = new Color(0.1f, 0.1f, 0.1f);
        // 鼠标悬停高亮效果颜色 - 偏冷的浅灰色，透明度较低
        private static readonly Color buttonHoverHighlightColor = new Color(0.9f, 0.9f, 0.95f, 0.3f);

        // 自定义UI设置检查
        private bool UseCustomUI => DMSL_ModSettings.settings?.useCustomUI ?? false;

        // 欢迎消息发送标志
        private bool _welcomeMessageSent = false;

        // 自定义字体设置检查（与自定义UI联动）
        private bool UseCustomFonts => DMSL_ModSettings.settings?.useCustomUI ?? false;

        // 当前使用的自定义字体
        private Font? _currentCustomFont = null;
        private GUIStyle? _customLabelStyle = null;
        private GUIStyle? _customButtonStyle = null;

        // 添加调试属性 - 只在界面首次打开时输出
        private bool UseCustomUI_Debug
        {
            get
            {
                bool result = DMSL_ModSettings.settings?.useCustomUI ?? false;
                // 只在开发者模式且界面首次检查时输出，避免重复日志
                if (Prefs.DevMode && !hasLoggedUISetting)
                {
                    Verse.Log.Message($"[DMS Legion] UseCustomUI check: settings={DMSL_ModSettings.settings != null}, useCustomUI={result}");
                    hasLoggedUISetting = true;
                }
                return result;
            }
        }

        // 防止重复日志的标志
        private bool hasLoggedUISetting = false;


        /// <summary>
        /// 建筑槽位数据结构
        /// </summary>
        private class BuildingSlot
        {
            public ThingDef? buildingDef;  // null表示空槽
            public int slotIndex;        // 槽位索引（0-11）
            public bool isEmpty => buildingDef == null;

            // 调试日志标志，避免重复输出
            public bool hasLoggedSuccess = false;
            public bool hasLoggedFailure = false;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /* ===============================
         * 构造
         * =============================== */

        public ModularOperationWindow()
        {
            closeOnAccept = false;
            closeOnCancel = true;
            doCloseX = true;           // ★ 总是显示原版叉号，在自定义UI时会被自定义按钮覆盖
            absorbInputAroundWindow = true;
            forcePause = true;

            // 重置日志标志，允许重新记录调试信息
            hasLoggedUISetting = false;

            // 初始化12个空槽位（6×2网格）
            InitializeBuildingSlots();

            // 初始化SystemOutput系统
            SystemOutputInitializer.Initialize();
        }

        /// <summary>
        /// 窗口关闭前的清理工作
        /// </summary>
        public override void PreClose()
        {
            base.PreClose();

            // 清除SystemOutput的所有缓存消息，确保下次打开时是全新的状态
            SystemOutputManager.Instance.ClearAllMessages();

            // 清除显示队列
            _pendingLines.Clear();
            _displayedLines.Clear();
            _nextLineDisplayTime = 0f;

            // 重置欢迎消息标志，下次打开时会重新发送
            _welcomeMessageSent = false;
        }

        /// <summary>
        /// 初始化建筑槽位
        /// </summary>
        private void InitializeBuildingSlots()
        {
            buildingSlots = new List<BuildingSlot>();
            for (int i = 0; i < 12; i++)  // 6×2网格，共12个槽位
            {
                buildingSlots.Add(new BuildingSlot { slotIndex = i, buildingDef = null });
            }
        }

        /// <summary>
        /// 初始化SystemOutput缓存
        /// </summary>
        private void InitializeSystemOutputCache()
        {
            // 每0.25秒更新一次缓存，减少消息显示延迟
            if (Time.time - _lastCacheUpdateTime > 0.25f)
            {
                var newMessages = SystemOutputManager.Instance.GetAllMessages();
                _lastCacheUpdateTime = Time.time;

                // 检查是否有新消息
                if (_cachedMessages == null || newMessages.Count != _cachedMessages.Count)
                {
                    // 有新消息，添加到待显示队列
                    AddNewMessagesToQueue(newMessages);
                    _cachedMessages = newMessages;
                }
            }

            // 处理逐行显示逻辑
            ProcessLineDisplay();
        }

        /// <summary>
        /// 将新消息添加到待显示队列
        /// </summary>
        private void AddNewMessagesToQueue(List<SystemMessage> newMessages)
        {
            if (_cachedMessages == null)
            {
                // 首次加载，添加所有消息
                foreach (var message in newMessages)
                {
                    var lines = SplitMessageIntoLines(message, 400f); // 使用估算宽度，后续会调整
                    foreach (var line in lines)
                    {
                        _pendingLines.Enqueue(line);
                    }
                }
            }
            else
            {
                // 只添加新增的消息
                int existingCount = _cachedMessages.Count;
                for (int i = existingCount; i < newMessages.Count; i++)
                {
                    var message = newMessages[i];
                    var lines = SplitMessageIntoLines(message, 400f); // 使用估算宽度，后续会调整
                    foreach (var line in lines)
                    {
                        _pendingLines.Enqueue(line);
                    }
                }
            }

            // 如果这是第一批行，立即开始显示
            if (_pendingLines.Count > 0 && _displayedLines.Count == 0)
            {
                _nextLineDisplayTime = Time.time;
            }
        }

        /// <summary>
        /// 处理逐行显示逻辑
        /// </summary>
        private void ProcessLineDisplay()
        {
            // 如果有待显示的行且时间到了，显示下一行
            if (_pendingLines.Count > 0 && Time.time >= _nextLineDisplayTime)
            {
                var nextLine = _pendingLines.Dequeue();
                _displayedLines.Add(nextLine);

                // 设置下一行的显示时间
                _nextLineDisplayTime = Time.time + LINE_DISPLAY_INTERVAL;

                // 限制显示的行数（防止内存泄漏）
                int maxDisplayLines = 50; // 最多显示50行
                if (_displayedLines.Count > maxDisplayLines)
                {
                    _displayedLines.RemoveAt(0); // 移除最旧的行
                }
            }
        }

        /// <summary>
        /// 初始化自定义字体
        /// </summary>
        private void InitializeCustomFonts()
        {
            if (!UseCustomFonts || DMSL_ModSettings.settings == null)
            {
                _currentCustomFont = null;
                _customLabelStyle = null;
                _customButtonStyle = null;
                return;
            }

            // 获取自定义字体
            _currentCustomFont = DMSL_ModSettings.GetFont();

            if (_currentCustomFont != null)
            {
                // 创建自定义样式
                _customLabelStyle = new GUIStyle(Text.fontStyles[(int)Text.Font]);
                _customLabelStyle.font = _currentCustomFont;
                _customLabelStyle.alignment = TextAnchor.MiddleCenter;

                _customButtonStyle = new GUIStyle(Text.fontStyles[(int)Text.Font]);
                _customButtonStyle.font = _currentCustomFont;
                _customButtonStyle.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                _customLabelStyle = null;
                _customButtonStyle = null;
            }
        }

        /// <summary>
        /// 绘制带自定义字体的标签
        /// </summary>
        private void DrawCustomLabel(Rect rect, string text, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            TextAnchor originalAnchor = Text.Anchor;
            Text.Anchor = anchor;

            // 在启用自定义UI时，使用黑色文字
            if (UseCustomUI)
            {
                Color originalColor = GUI.color;
                GUI.color = Color.black;

                if (UseCustomFonts && _customLabelStyle != null)
                {
                    GUI.Label(rect, text, _customLabelStyle);
                }
                else
                {
                    Widgets.Label(rect, text);
                }

                GUI.color = originalColor;
            }
            else
            {
                if (UseCustomFonts && _customLabelStyle != null)
                {
                    GUI.Label(rect, text, _customLabelStyle);
                }
                else
                {
                    Widgets.Label(rect, text);
                }
            }

            Text.Anchor = originalAnchor;
        }

        /// <summary>
        /// 绘制带自定义字体的按钮标签
        /// </summary>
        private void DrawCustomButtonLabel(Rect rect, string text, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            TextAnchor originalAnchor = Text.Anchor;
            Text.Anchor = anchor;

            // 在启用自定义UI时，使用黑色文字
            if (UseCustomUI)
            {
                Color originalColor = GUI.color;
                GUI.color = Color.black;

                if (UseCustomFonts && _customButtonStyle != null)
                {
                    GUI.Label(rect, text, _customButtonStyle);
                }
                else
                {
                    Widgets.Label(rect, text);
                }

                GUI.color = originalColor;
            }
            else
            {
                if (UseCustomFonts && _customButtonStyle != null)
                {
                    GUI.Label(rect, text, _customButtonStyle);
                }
                else
                {
                    Widgets.Label(rect, text);
                }
            }

            Text.Anchor = originalAnchor;
        }

        /* ===============================
         * 主绘制入口
         * =============================== */

        public override void DoWindowContents(Rect inRect)
        {
            // 初始化自定义字体
            InitializeCustomFonts();

            // 初始化SystemOutput缓存
            InitializeSystemOutputCache();

            // 发送欢迎消息（仅在首次打开时）
            SendWelcomeMessageIfNeeded();

            // 检查是否启用自定义UI
            if (UseCustomUI)
            {
                /* ===============================
                 * 自定义UI模式：使用自定义背景和边距
                 * =============================== */

                /* ===============================
                 * 1. 绘制【整个窗口】背景
                 *    —— 仅装饰，不参与布局
                 * =============================== */

                if (DMSL_CustomUIAssets.MainWindowBackground != null)
                {
                    GUI.DrawTexture(inRect, DMSL_CustomUIAssets.MainWindowBackground, ScaleMode.StretchToFill);
                }
                else
                {
                    Widgets.DrawBoxSolid(inRect, bgColor);
                }

                /* ===============================
                 * 1.5. 绘制【关闭按钮】（右上角）
                 *    —— 仅在自定义UI模式下显示，覆盖原版叉号
                 * =============================== */

                if (UseCustomUI && DMSL_CustomUIAssets.CloseButton != null)
                {
                    // 计算关闭按钮位置（右上角，60x60像素）
                    float closeButtonSize = 35f;
                    Rect closeButtonRect = new Rect(
                        inRect.xMax - closeButtonSize - 10f - 3f,  // 右边缘-按钮宽度-边距 + 1px（左移）
                        inRect.y + 10f + 1f,                        // 上边缘+边距 + 1px（下移）
                        closeButtonSize,                            // 按钮宽度
                        closeButtonSize                             // 按钮高度
                    );

                    // 1. 选择纹理（参考DrawCustomButton的逻辑）
                    Texture2D? currentTex = DMSL_CustomUIAssets.CloseButton; // 默认使用普通纹理

                    // 实时检测鼠标状态，决定是否使用按下纹理
                    if (Mouse.IsOver(closeButtonRect) && UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                    {
                        Texture2D? pressedTex = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/CloseButton_pressed", false);
                        if (pressedTex != null)
                        {
                            currentTex = pressedTex;
                        }
                    }

                    // 2. 绘制选中的纹理
                    GUI.DrawTexture(closeButtonRect, currentTex, ScaleMode.StretchToFill);

                    // 3. 鼠标悬停高亮效果（仅当鼠标悬停时，且按钮不是按下状态）
                    if (Mouse.IsOver(closeButtonRect) && !UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                    {
                        GUI.color = buttonHoverHighlightColor;
                        GUI.DrawTexture(closeButtonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                        GUI.color = Color.white; // 恢复默认颜色
                    }

                    // 4. 处理关闭按钮点击
                    if (Widgets.ButtonInvisible(closeButtonRect))
                    {
                        Close();
                    }
                }

                /* ===============================
                 * 1.6. 绘制【标题图标】（左上角，与关闭按钮对称）
                 *    —— 仅在自定义UI模式下显示
                 * =============================== */

                if (UseCustomUI && DMSL_CustomUIAssets.TitleIcon != null)
                {
                    // 计算标题图标位置（左上角，与关闭按钮对称）
                    float titleIconSize = 36f; // 标题图标尺寸
                    Rect titleIconRect = new Rect(
                        inRect.x + 10f + 4f,                  // 左边缘+边距 + 2px（右移）
                        inRect.y + 10f + 1f,                   // 上边缘+边距 + 1px（下移，与关闭按钮对称）
                        titleIconSize,                         // 图标宽度
                        titleIconSize                          // 图标高度
                    );

                    // 绘制标题图标
                    GUI.DrawTexture(titleIconRect, DMSL_CustomUIAssets.TitleIcon, ScaleMode.StretchToFill);
                }

                /* ===============================
                 * 2. 计算【内容区域】Rect（关键）
                 * =============================== */

                Rect contentRect = new Rect(
                    inRect.x + ContentMarginLeft,
                    inRect.y + ContentMarginTop,
                    inRect.width - ContentMarginLeft - ContentMarginRight,
                    inRect.height - ContentMarginTop - ContentMarginBottom
                );

                /* ===============================
                 * 3. 原有布局逻辑：使用内容区域
                 * =============================== */

                CalculateLayout(
                    contentRect,
                    out Rect leftRect,
                    out Rect rightRect,
                    out Rect? extraWindowRect
                );

                // 计算命令窗口位置
                float closeButtonBottom = inRect.y + 46f;  // 叉号下边线位置
                float commandWindowTop = closeButtonBottom + 5f;  // 上边线：叉号下边线 + 5px
                float commandWindowBottom = rightRect.y - 5f;  // 下边线：右侧窗口上边线 - 5px
                float commandWindowHeight = commandWindowBottom - commandWindowTop;  // 高度

                Rect commandWindowRect = new Rect(
                    rightRect.x,  // 与右侧窗口左对齐
                    commandWindowTop,
                    rightRect.width,  // 与右侧窗口等宽
                    commandWindowHeight
                );

                if (extraWindowRect.HasValue)
                {
                    DrawExtraWindow(extraWindowRect.Value);
                }

                DrawLeftPanel(leftRect);
                DrawRightPanel(rightRect);

                // 绘制命令窗口
                DrawCommandWindow(commandWindowRect);            }
            else
            {
                /* ===============================
                 * 默认UI模式：使用原版窗口行为
                 * =============================== */

                // 绘制默认背景
                Widgets.DrawBoxSolid(inRect, bgColor);

                // 整体边框由Window基类处理，不再额外绘制内侧边框

                // 使用原始的inRect进行布局计算
            CalculateLayout(inRect, out Rect leftRect, out Rect rightRect, out Rect? extraWindowRect);

            // 计算命令窗口位置（标题栏上边线与左侧窗口上边线平齐）
            float leftPanelTop = inRect.y + 10f;  // 左侧窗口上边线
            float commandWindowTop = leftPanelTop;  // 上边线：与左侧窗口上边线平齐
            float commandWindowBottom = rightRect.y - 5f;  // 下边线：右侧窗口上边线 - 5px
            float commandWindowHeight = commandWindowBottom - commandWindowTop;  // 高度

            Rect commandWindowRect = new Rect(
                rightRect.x,  // 与右侧窗口左对齐
                commandWindowTop,
                rightRect.width,  // 与右侧窗口等宽
                commandWindowHeight
            );

            // 绘制额外窗口（如果展开）
            if (extraWindowRect.HasValue)
            {
                DrawExtraWindow(extraWindowRect.Value);
            }

            // 绘制左右两个主要区域
            DrawLeftPanel(leftRect);
            DrawRightPanel(rightRect);

            // 绘制命令窗口
            DrawCommandWindow(commandWindowRect);
            }
        }

        /// <summary>
        /// 计算布局（包含额外窗口）
        /// 额外窗口在左侧区域的左侧额外添加，不挤占原有内容
        /// </summary>
        private void CalculateLayout(Rect inRect, out Rect leftRect, out Rect rightRect, out Rect? extraWindowRect)
        {
            float padding = 10f;
            float totalWidth = inRect.width - padding * 2;

            // 左侧面板尺寸保持不变，右侧缩短30px
            float leftWidth = 735f;  // 保持左侧面板宽度不变
            float rightWidth = leftWidth * (1.5f / 3f) - 30f;  // 右侧缩短30px ≈ 337.5px
            float extraWindowWidth = 215f;  // 额外窗口宽度设置为215px

            // 判断是否展开额外窗口
            bool isExtraWindowOpen = selectedSlotIndex.HasValue;

            if (isExtraWindowOpen)
            {
                // 额外窗口在左侧区域的左侧额外添加
                // 计算可用宽度（总宽度减去右侧区域和间距）
                float availableWidth = totalWidth - rightWidth - padding;
                
                // 额外窗口在左侧
                extraWindowRect = new Rect(
                    inRect.x + padding,
                    inRect.y + padding,
                    extraWindowWidth,
                    inRect.height - padding * 2  // 与总窗口等高
                );

                // 左侧区域在额外窗口右侧，宽度相应减小
                leftRect = new Rect(
                    extraWindowRect.Value.xMax + padding,
                    inRect.y + padding,
                    availableWidth - extraWindowWidth - padding,
                    inRect.height - padding * 2
                );
            }
            else
            {
                extraWindowRect = null;
                leftRect = new Rect(
                    inRect.x + padding,
                    inRect.y + padding,
                    totalWidth - rightWidth - padding,
                    inRect.height - padding * 2
                );
            }

            // 右侧区域（与任务列表窗口边界等价）
            rightRect = new Rect(
                leftRect.xMax + ContentMarginLeft,  // 使用ContentMarginLeft保持对称
                inRect.y + padding + 250f,  // 顶部向下移动
                rightWidth,
                inRect.height - padding * 2 - 250f  // 下边线向下降低5px
            );
        }

        /// <summary>
        /// 绘制左侧面板
        /// 布局：左上建筑槽位，右上决策按钮，中部功能按钮，底部资源展示
        /// </summary>
        private void DrawLeftPanel(Rect rect)
        {
            // 左侧面板背景已在内容区级别绘制，无需重复绘制
            if (!UseCustomUI_Debug)
            {
            Widgets.DrawBoxSolid(rect, panelColor);
            }

            // 仅在未开启自定义UI时绘制白线边框
            if (!UseCustomUI_Debug)
            {
            Widgets.DrawBox(rect, 1);
            }

            // 资源展示区域固定在底部
            float resourceDisplayHeight = 240f;  // 资源展示区域高度（增大至两倍）
            float resourceDisplayBottomMargin = 1f;  // 资源展示区域距离底部边距（下边界线向下移动5px）

            Rect resourceDisplayRect = new Rect(
                rect.x + 10f,
                rect.yMax - resourceDisplayHeight - resourceDisplayBottomMargin,  // 固定在底部
                rect.width - 20f,
                resourceDisplayHeight
            );
            DrawResourceDisplay(resourceDisplayRect);

            // 计算可用空间高度
            float availableHeight = resourceDisplayRect.y - rect.y - 20f;

            // 上部建筑槽位和决策按钮区域
            float buildingSectionHeight = 2 * 70f + 15f + 60f;  // 建筑格高度 + 决策按钮高度 + 间距
            Rect buildingRect = new Rect(
                rect.x + 10f,
                rect.y + 20f,
                rect.width - 20f,
                buildingSectionHeight
            );
            DrawTopSection(buildingRect);

            // 下部功能按钮区域（建筑区域下方）
            float functionAreaTop = buildingRect.yMax + 15f;

            // 如果额外窗口展开，向下平移20px
            if (selectedSlotIndex.HasValue)
            {
                functionAreaTop += 20f;
            }

            float functionAreaHeight = availableHeight - buildingSectionHeight - 15f;

            if (functionAreaHeight > 100f)  // 确保有足够空间
            {
                Rect functionRect = new Rect(
                    rect.x + 10f,
                    functionAreaTop,
                    rect.width - 20f,
                    functionAreaHeight
                );
                DrawMiddleFunctionButtons(functionRect);
            }
        }

        /// <summary>
        /// 绘制自定义按钮（支持贴图、图标+文字和默认样式）
        /// </summary>
        /// <param name="rect">按钮区域</param>
        /// <param name="label">按钮文本</param>
        /// <param name="buttonType">按钮类型（用于选择自定义贴图）</param>
        /// <param name="showIcon">是否显示图标（仅对功能按钮）</param>
        /// <returns>是否被点击</returns>
        /// <summary>
        /// 绘制自定义按钮
        /// </summary>
        /// <param name="rect">按钮区域</param>
        /// <param name="label">按钮文字</param>
        /// <param name="buttonType">按钮类型，用于获取对应贴图</param>
        /// <param name="showIcon">是否显示图标</param>
        /// <param name="fillMode">填充模式：true=图片完全填充按钮，false=图片作为背景</param>
        /// <returns>是否被点击</returns>
        private bool DrawCustomButton(Rect rect, string label, string buttonType = "default", bool showIcon = false, bool fillMode = false)
        {
            
            bool clicked = false;

            bool useCustom = UseCustomUI;

            Texture2D? tex = null;
            if (useCustom)
            {
                // 直接使用通用Button纹理
                tex = DMSL_CustomUIAssets.Button;

                // 如果Button纹理不存在，报错并回退到原版
                if (tex == null)
                {
                    Verse.Log.Error("[DMS_Legion]Button按钮贴图缺失");
                    // 继续执行，tex为null会自动回退到原版按钮
                }
                else if (buttonType == "production" && Prefs.DevMode)
                {
                    // 生产规划按钮纹理加载成功（已移除日志）
                }
            }

            if (useCustom && tex != null)
            {
                // 1. 选择纹理（与原版DrawButtonGraphic相同的逻辑）
                Texture2D? currentTex = tex; // 默认使用普通纹理

                // 实时检测鼠标状态，决定是否使用按下纹理
                if (Mouse.IsOver(rect) && UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                {
                    Texture2D? pressedTex = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/Button_pressed", false);
                    if (pressedTex != null)
                    {
                        currentTex = pressedTex;
                    }
                }

                // 2. 绘制选中的纹理
                if (fillMode)
                    GUI.DrawTexture(rect, currentTex, ScaleMode.StretchToFill);
                else
                    GUI.DrawTexture(rect, currentTex, ScaleMode.StretchToFill); // 作为背景

                // 3. 叠加文字 / 图标（如果需要）
                if (!fillMode)
                {
                    if (showIcon && ShouldShowIconForButton(buttonType))
                    {
                        DrawButtonWithIconAndText(rect, label, buttonType);
                    }
                    else
                    {
                        // 普通文字按钮 - 在贴图上绘制文字
                        Text.Anchor = TextAnchor.MiddleCenter;
                        DrawCustomButtonLabel(rect, label, TextAnchor.MiddleCenter);
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                }

                // 4. 鼠标悬停高亮效果（仅当鼠标悬停时，且按钮不是按下状态）
                if (Mouse.IsOver(rect) && !UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                {
                    GUI.color = buttonHoverHighlightColor;
                    GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                    GUI.color = Color.white; // 恢复默认颜色
                }

                // 5. 点击检测（必须）
                clicked = Widgets.ButtonInvisible(rect);

                // 调试信息（仅保留生产规划按钮的点击事件）
                if (Prefs.DevMode)
                {
                    // 特别关注生产规划按钮的点击事件（这个可以多次输出，因为点击是事件）
                    if (buttonType == "production" && clicked)
                    {
                        Verse.Log.Message($"[DMS Legion] 生产规划按钮 - 检测到点击事件!");
                    }
                }
            }
            else
            {
                // 回退到 RimWorld 默认按钮
                clicked = Widgets.ButtonText(rect, label);

                // 调试信息
                if (Prefs.DevMode && useCustom)
                {
                    Verse.Log.Message($"[DMS Legion] Button '{buttonType}' using default style (texture not found)");
                }
            }

            return clicked;
        }

        /// <summary>
        /// 判断按钮是否应该显示图标
        /// </summary>
        private bool ShouldShowIconForButton(string buttonType)
        {
            return buttonType == "production" || buttonType == "tactical" ||
                   buttonType == "mechanoid" || buttonType == "strategic";
            // 四个主要功能按钮都显示图标
        }

        /// <summary>
        /// 绘制带有图标和文字的按钮
        /// </summary>
        private void DrawButtonWithIconAndText(Rect rect, string label, string buttonType)
        {
            // 获取对应的图标
            Texture2D? icon = GetIconForButtonType(buttonType);
            if (icon == null)
            {
                // 如果没有图标，回退到普通文字
                Text.Anchor = TextAnchor.MiddleCenter;
                DrawCustomButtonLabel(rect, label, TextAnchor.MiddleCenter);
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // 图标尺寸 (1.5倍放大)
            float iconSize = 20f * 1.5f; // 30f
            float padding = 2f; // 减少padding，让图标和文字更紧凑

            // 计算文字宽度（确保足够宽，一行显示）
            float textWidth = Mathf.Max(label.Length * 14f, 80f); // 中文字符大约14像素宽度，最小80像素确保完整显示
            float totalWidth = iconSize + padding + textWidth; // 图标+间隔+文字的总宽度

            // 计算整体居中位置
            float centerX = rect.x + rect.width / 2f;
            float startX = centerX - totalWidth / 2f;

            // 图标位置
            Rect iconRect = new Rect(
                startX,
                rect.y + (rect.height - iconSize) / 2f,
                iconSize,
                iconSize
            );
            Widgets.DrawTextureFitted(iconRect, icon, 1f);

            // 文字位置（紧跟图标右侧）
            Rect textRect = new Rect(
                iconRect.xMax + padding, // 图标右侧+小间隔
                rect.y,
                textWidth + 10f, // 文字宽度+缓冲
                rect.height
            );

            Text.Anchor = TextAnchor.MiddleLeft; // 文字左对齐，与图标对齐
            DrawCustomButtonLabel(textRect, label, TextAnchor.MiddleLeft);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 根据按钮类型获取对应的图标
        /// </summary>
        private Texture2D? GetIconForButtonType(string buttonType)
        {
            switch (buttonType)
            {
                case "production": return DMSL_CustomUIAssets.ProductionIcon;
                case "tactical": return DMSL_CustomUIAssets.TacticalIcon;
                case "mechanoid": return DMSL_CustomUIAssets.MechanoidIcon;
                case "strategic": return DMSL_CustomUIAssets.StrategicIcon;
                // decision 和 specialization 按钮不需要图标
                default: return null;
            }
        }

        /// <summary>
        /// 绘制上部区域（建筑槽位和决策按钮）
        /// </summary>
        private void DrawTopSection(Rect rect)
        {
            // 建筑槽位区域（6列）
            float buildingGridWidth = 6 * 70f + 5 * 15f + 30f;  // 6列槽位 + 5个间距 + 边距
            float buildingGridHeight = 2 * 70f + 15f + 30f;     // 2行槽位 + 间距 + 边距

            // 根据是否展开额外窗口调整建筑窗口位置
            float buildingGridX;
            if (selectedSlotIndex.HasValue)
            {
                // 展开状态：3×3窗口距离左侧边界3px
                buildingGridX = rect.x + 3f;
            }
            else
            {
                // 未展开时，建筑窗口位于左上角
                buildingGridX = rect.x;
            }

            Rect buildingGridRect = new Rect(
                buildingGridX,
                rect.y,
                buildingGridWidth,
                buildingGridHeight
            );
            DrawLeftBuildingGrid(buildingGridRect);

            // 决策按钮逻辑
            if (!selectedSlotIndex.HasValue)
            {
                // 未展开状态：决策按钮位于左侧窗口右边缘与建筑槽位窗口右侧边缘的居中位置
                float leftWindowRightEdge = rect.xMax;  // 左侧窗口右边缘
                float buildingGridRightEdge = buildingGridRect.xMax;  // 建筑槽位窗口右侧边缘
                float decisionAreaCenterX = (leftWindowRightEdge + buildingGridRightEdge) / 2f;  // 居中位置

                // 缩小按钮宽度以适配增大的建筑槽位窗口
                float decisionButtonWidth = 120f;  // 缩小宽度
                float decisionAreaLeft = decisionAreaCenterX - decisionButtonWidth / 2f;
                float decisionAreaWidth = decisionButtonWidth;

                if (decisionAreaWidth > 100f)  // 确保有足够空间显示按钮（建筑变宽后调整）
                {
                    // 决策按钮与建筑槽位窗口上下对齐
                    Rect decisionAreaRect = new Rect(
                        decisionAreaLeft,
                        buildingGridRect.y,  // 与建筑窗口上方对齐
                        decisionAreaWidth,
                        buildingGridRect.height  // 与建筑窗口高度相同
                    );
                    DrawDecisionButtonsTopRight(decisionAreaRect);
                }
            }
            else
            {
                // 展开状态：在3×3窗口右侧添加决策按钮
                // 计算3×3窗口的实际尺寸
                float buttonSize = 70f;
                float gap = 15f;
                float framePadding = 15f;
                int rows = 3, columns = 3;
                float gridWidth = columns * buttonSize + (columns - 1) * gap;  // 240
                float gridHeight = rows * buttonSize + (rows - 1) * gap;      // 240
                float squareSize = Mathf.Max(gridWidth, gridHeight) + framePadding * 2;  // 270

                // 决策按钮位于3×3窗口右侧的可用空间内居中，保持与未展开状态相同的大小
                float availableSpaceLeft = buildingGridRect.x + squareSize;  // 3×3窗口右侧
                float availableSpaceRight = rect.xMax;  // 左侧窗口右边缘
                float availableSpaceWidth = availableSpaceRight - availableSpaceLeft;  // 可用空间宽度

                float decisionButtonWidth = 120f;  // 保持与未展开状态相同的大小
                float decisionButtonLeft = availableSpaceLeft + (availableSpaceWidth - decisionButtonWidth) / 2f;  // 在可用空间内居中
                float decisionButtonHeight = buildingGridRect.height;  // 与建筑窗口高度相同

                if (decisionButtonLeft >= availableSpaceLeft && decisionButtonLeft + decisionButtonWidth <= availableSpaceRight)  // 确保按钮完全在可用空间内
                {
                    Rect decisionAreaRect = new Rect(
                        decisionButtonLeft,
                        buildingGridRect.y,
                        decisionButtonWidth,
                        decisionButtonHeight
                    );
                    DrawDecisionButtonsTopRight(decisionAreaRect);
                }
            }
        }

        /// <summary>
        /// 绘制决策按钮区域（垂直排列，与建筑槽位窗口对齐）
        /// </summary>
        private void DrawDecisionButtonsTopRight(Rect rect)
        {
            // 计算建筑槽位的实际内容区域（去掉白框边距）
            float buildingContentTop = rect.y + 15f;  // 建筑白框上边距
            float buildingContentHeight = rect.height - 30f;  // 建筑白框上下边距
            float buttonSpacing = 15f;  // 两行建筑槽位之间的间距

            // 计算每行建筑槽位的高度
            float slotRowHeight = 70f;  // 建筑槽位高度

            // 决策按钮（与第一行建筑槽位对齐）
            float decisionButtonTop = buildingContentTop;
            float decisionButtonHeight = slotRowHeight;

            // 集群特化按钮（与第二行建筑槽位对齐）
            float specializationButtonTop = decisionButtonTop + slotRowHeight + buttonSpacing;
            float specializationButtonHeight = slotRowHeight;

            // 决策按钮
            Rect decisionButtonRect = new Rect(
                rect.x,
                decisionButtonTop,
                rect.width,
                decisionButtonHeight
            );

            // 集群特化按钮
            Rect specializationButtonRect = new Rect(
                rect.x,
                specializationButtonTop,
                rect.width,
                specializationButtonHeight
            );

            if (DrawCustomButton(decisionButtonRect, "决策", "decision", true))
            {
                // 发布操作开始事件
                UIEventBus.Publish(new UIEvents.OperationStarted(
                    "决策分析",
                    "DecisionModule"
                ));
                // TODO: 实现决策功能
            }

            if (DrawCustomButton(specializationButtonRect, "集群特化", "specialization", true))
            {
                // 发布操作开始事件
                UIEventBus.Publish(new UIEvents.OperationStarted(
                    "集群特化",
                    "SpecializationModule"
                ));
                // TODO: 实现集群特化功能
            }
        }

        /// <summary>
        /// 绘制功能按钮区域（建筑窗口下方）
        /// </summary>
        private void DrawMiddleFunctionButtons(Rect rect)
        {
            // 四个功能按钮，2行2列布局，放大宽度接近左右白线
            float buttonWidth = (rect.width - 30f) / 2f;  // 占据大部分宽度，只留小边距
            float buttonHeight = Mathf.Min(45f, (rect.height - 20f) / 2f); // 稍微增高

            // 按钮组靠近左右边缘
            float startX = rect.x + 10f;  // 左边距10px
            float startY = rect.y + (rect.height - (buttonHeight * 2 + 10f)) / 2f;  // 垂直居中

            // 第一行
            Rect prodButton = new Rect(
                startX,
                startY,
                buttonWidth,
                buttonHeight
            );

            Rect supportButton = new Rect(
                prodButton.xMax + 10f,
                startY,
                buttonWidth,
                buttonHeight
            );

            // 第二行
            Rect mechButton = new Rect(
                startX,
                prodButton.yMax + 10f,
                buttonWidth,
                buttonHeight
            );

            Rect strategyButton = new Rect(
                mechButton.xMax + 10f,
                prodButton.yMax + 10f,
                buttonWidth,
                buttonHeight
            );

            if (DrawCustomButton(prodButton, "生产规划", "production", true))
            {
                // 发布操作开始事件
                UIEventBus.Publish(new UIEvents.OperationStarted(
                    "生产规划",
                    "ConstructionUI"
                ));
            }

            if (DrawCustomButton(supportButton, "战术支援", "tactical", true))
            {
                // 发布操作开始事件
                UIEventBus.Publish(new UIEvents.OperationStarted(
                    "战术支援",
                    "TacticalModule"
                ));
            }

            if (DrawCustomButton(mechButton, "机械体管理", "mechanoid", true))
            {
                // 发布操作开始事件
                UIEventBus.Publish(new UIEvents.OperationStarted(
                    "机械体管理",
                    "MechanoidModule"
                ));
            }

            if (DrawCustomButton(strategyButton, "战略部署", "strategic", true))
            {
                // 发布操作开始事件
                UIEventBus.Publish(new UIEvents.OperationStarted(
                    "战略部署",
                    "StrategicModule"
                ));
            }
        }



        /// <summary>
        /// 绘制左侧建筑槽位网格（2行5列，共10个槽位）
        /// 白框紧贴建筑槽位，减少不必要的空间
        /// </summary>
        private void DrawLeftBuildingGrid(Rect rect)
        {
            // 根据是否展开额外窗口调整网格参数
            int rows, columns;
            if (selectedSlotIndex.HasValue)
            {
                // 展开状态：3行3列，共9个槽位，形成正方形布局
                rows = 3;
                columns = 3;
            }
            else
            {
                // 未展开状态：2行6列，共12个槽位
                rows = 2;
                columns = 6;
            }
            float buttonSize = 70f;  // 正方形槽位
            float gap = 15f;  // 间距

            // 计算网格尺寸
            float gridWidth = columns * buttonSize + (columns - 1) * gap;
            float gridHeight = rows * buttonSize + (rows - 1) * gap;

            // 计算白框区域
            float framePadding = 15f;  // 白框内边距
            Rect frameRect;

            if (selectedSlotIndex.HasValue)
            {
                // 展开状态：正方形窗口，使用传入的rect位置，顶部位置与未展开时相同
                float squareSize = Mathf.Max(gridWidth, gridHeight) + framePadding * 2;  // 取较大的尺寸作为正方形边长
                float frameTop = rect.y;  // 与左侧窗口上方边界线距离相同（未展开时也是rect.y）

                frameRect = new Rect(
                    rect.x,  // 使用传入的rect的x位置
                    frameTop,
                    squareSize,
                    squareSize
                );
            }
            else
            {
                // 未展开状态：紧贴网格，上下边距相等
                float frameTop = rect.y + (rect.height - gridHeight) / 2f - framePadding;  // 确保上下边距相等
                float frameHeight = gridHeight + framePadding * 2;

                frameRect = new Rect(
                    rect.x + (rect.width - gridWidth) / 2f - framePadding,
                    frameTop,
                    gridWidth + framePadding * 2,
                    frameHeight
                );
            }

            // 绘制建筑格区域背景（填充整个白线框住的空间，高于面板背景，低于按钮）
            if (UseCustomUI_Debug && DMSL_CustomUIAssets.BuildingSlotBackground != null)
            {
                GUI.DrawTexture(frameRect, DMSL_CustomUIAssets.BuildingSlotBackground, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(frameRect, panelColor);
            }

            // 仅在未开启自定义UI时绘制白线边框
            if (!UseCustomUI_Debug)
            {
                Widgets.DrawBox(frameRect, 1);
            }

            // 网格居中显示在白框内部
            float startX, startY;
            if (selectedSlotIndex.HasValue)
            {
                // 展开状态：在正方形窗口中水平和垂直居中
                startX = frameRect.x + (frameRect.width - gridWidth) / 2f;
                startY = frameRect.y + (frameRect.height - gridHeight) / 2f;
            }
            else
            {
                // 未展开状态：使用标准的framePadding
                startX = frameRect.x + framePadding;
                startY = frameRect.y + framePadding;
            }

            // 绘制网格：展开时3×3，未展开时2×6
            int slotIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    Rect buttonRect = new Rect(
                        startX + col * (buttonSize + gap),
                        startY + row * (buttonSize + gap),
                        buttonSize,
                        buttonSize
                    );

                    // 获取对应的槽位数据
                    BuildingSlot slot = buildingSlots[slotIndex];
                    bool isSelected = slotIndex == selectedBuildingIndex;

                    if (DrawBuildingButton(buttonRect, slot, isSelected))
                    {
                        if (slot.isEmpty)
                        {
                            // 空槽：展开额外窗口
                            UIEventBus.Publish(new UIEvents.ButtonClicked(
                                $"空建筑槽位 {slotIndex + 1}",
                                "ConstructionUI"
                            ));
                            selectedSlotIndex = slotIndex;
                        }
                        else
                        {
                            // 已填充槽位：选中建筑
                            selectedBuildingIndex = slotIndex;
                            UIEventBus.Publish(new UIEvents.ButtonClicked(
                                $"建筑槽位 {slotIndex + 1}",
                                "ConstructionUI",
                                "选中操作"
                            ));
                        }
                    }

                    slotIndex++;
                }
            }
        }

        /// <summary>
        /// 绘制建筑按钮（支持空槽）
        /// </summary>
        private bool DrawBuildingButton(Rect rect, BuildingSlot slot, bool isSelected)
        {
            // 空槽处理 - 使用BuildingSlotButton填充
            if (slot.isEmpty)
            {
                if (UseCustomUI && DMSL_CustomUIAssets.BuildingSlotButton != null)
                {
                    // 1. 选择纹理（与原版DrawButtonGraphic相同的逻辑）
                    Texture2D? currentTex = DMSL_CustomUIAssets.BuildingSlotButton; // 默认使用普通纹理

                    // 实时检测鼠标状态，决定是否使用按下纹理
                    if (Mouse.IsOver(rect) && UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                    {
                        Texture2D? pressedTex = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/BuildingSlotButton_pressed", false);
                        if (pressedTex != null)
                        {
                            currentTex = pressedTex;
                        }
                    }

                    // 2. 绘制选中的纹理
                    GUI.DrawTexture(rect, currentTex, ScaleMode.ScaleToFit);

                    // 3. 选中高亮（在贴图之上）
                    if (isSelected)
                    {
                        Widgets.DrawHighlightSelected(rect);
                    }

                    // 4. 鼠标悬停高亮效果（仅当鼠标悬停时，且按钮不是按下状态）
                    if (Mouse.IsOver(rect) && !UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                    {
                        GUI.color = buttonHoverHighlightColor;
                        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                        GUI.color = Color.white; // 恢复默认颜色
                    }
                }
                else
                {
                    // 回退到默认样式
                Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
                Widgets.DrawBox(rect, 1);

                    // 在中心绘制"+"符号
                Text.Anchor = TextAnchor.MiddleCenter;
                    Text.Font = GameFont.Medium; // 使用中等字体
                    GUI.color = Color.white; // 白色文字
                Widgets.Label(rect, "+");
                    GUI.color = Color.white; // 恢复默认颜色
                    Text.Font = GameFont.Small; // 恢复默认字体
                    Text.Anchor = TextAnchor.UpperLeft; // 恢复默认对齐

                    // 悬停高亮效果（灰白色，亮度为默认的一半）
                    if (Mouse.IsOver(rect))
                    {
                        Color defaultHoverColor = buttonHoverHighlightColor; // (0.9f, 0.9f, 0.95f, 0.3f)
                        Color dimmedGrayHoverColor = new Color(0.5f, 0.5f, 0.5f, defaultHoverColor.a * 0.5f); // 灰白色，透明度减半
                        GUI.color = dimmedGrayHoverColor;
                        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                        GUI.color = Color.white; // 恢复默认颜色
                    }
                }

                // 检测点击（不绘制加号）
                return Widgets.ButtonInvisible(rect);
            }

            // 非空槽的绘制逻辑 - 使用DrawCustomButton的填充模式
            bool clicked = false;

            if (UseCustomUI)
            {
                // 使用自定义贴图填充模式
                Texture2D? buttonTexture = DMSL_CustomUIAssets.BuildingSlotButton;

                if (buttonTexture != null)
                {
                    // 1. 选择纹理（与原版DrawButtonGraphic相同的逻辑）
                    Texture2D? currentTex = buttonTexture; // 默认使用普通纹理

                    // 实时检测鼠标状态，决定是否使用按下纹理
                    if (Mouse.IsOver(rect) && UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                    {
                        Texture2D? pressedTex = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/BuildingSlotButton_pressed", false);
                        if (pressedTex != null)
                        {
                            currentTex = pressedTex;
                        }
                    }

                    // 2. 绘制建筑槽位贴图（填充模式）
                    GUI.DrawTexture(rect, currentTex, ScaleMode.ScaleToFit);

                    // 3. 选中高亮（在贴图之上）
            if (isSelected)
            {
                Widgets.DrawHighlightSelected(rect);
            }

                    // 4. 鼠标悬停高亮效果（仅当鼠标悬停时，且按钮不是按下状态）
                    if (Mouse.IsOver(rect) && !UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                    {
                        GUI.color = buttonHoverHighlightColor;
                        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                        GUI.color = Color.white; // 恢复默认颜色
                    }

                    // 调试信息 - 只在首次成功绘制每个槽位时输出
                    if (Prefs.DevMode && !slot.hasLoggedSuccess)
                    {
                        Verse.Log.Message($"[DMS Legion] Building slot {slot.slotIndex + 1} SUCCESS: using custom texture (fill mode)");
                        slot.hasLoggedSuccess = true;
                    }
                }
                else
                {
                    // 回退到默认样式
            Widgets.DrawBoxSolid(rect, isSelected ? highlightedButtonColor : buttonColor);
                    Widgets.DrawBox(rect, 1);

            // 绘制"建筑"标签
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            DrawCustomLabel(rect, "建筑", TextAnchor.MiddleCenter);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

                    // 调试信息 - 只在首次检测到贴图缺失时输出每个槽位
                    if (Prefs.DevMode && !slot.hasLoggedFailure)
                    {
                        Verse.Log.Message($"[DMS Legion] Building slot {slot.slotIndex + 1} FAILED: BuildingSlotButton texture not found, using default style");
                        slot.hasLoggedFailure = true;
                    }

                    // 调试信息
                    if (Prefs.DevMode) Verse.Log.Message($"[DMS Legion] Building slot {slot.slotIndex + 1} using default style (texture not found)");
                }

                // 检测点击
                clicked = Widgets.ButtonInvisible(rect);
            }
            else
            {
                // 使用默认RimWorld样式
                Widgets.DrawBoxSolid(rect, isSelected ? highlightedButtonColor : buttonColor);
            Widgets.DrawBox(rect, 1);

                // 绘制"建筑"标签
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
                DrawCustomLabel(rect, "建筑", TextAnchor.MiddleCenter);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 检测点击
                clicked = Widgets.ButtonInvisible(rect);
            }

            return clicked;
        }

        /// <summary>
        /// 绘制左侧功能按钮区域（4个按钮，2行2列）
        /// </summary>
        private void DrawLeftFunctionButtons(Rect rect)
        {
            float buttonHeight = 40f;
            float buttonGap = 10f;
            float buttonWidth = (rect.width - buttonGap * 3) / 2f;  // 两列按钮的宽度

            // 第一排：生产规划、战术支援
            float startY = rect.y;
            Rect prodButton = new Rect(rect.x + buttonGap, startY, buttonWidth, buttonHeight);
            Rect supportButton = new Rect(prodButton.xMax + buttonGap, startY, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(prodButton, "生产规划"))
            {
                Messages.Message("点击了生产规划按钮", MessageTypeDefOf.NeutralEvent);
            }

            if (Widgets.ButtonText(supportButton, "战术支援"))
            {
                Messages.Message("点击了战术支援按钮", MessageTypeDefOf.NeutralEvent);
            }

            startY += buttonHeight + buttonGap;

            // 第二排：机械体管理、战略部署
            Rect mechButton = new Rect(rect.x + buttonGap, startY, buttonWidth, buttonHeight);
            Rect strategyButton = new Rect(mechButton.xMax + buttonGap, startY, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(mechButton, "机械体管理"))
            {
                Messages.Message("点击了机械体管理按钮", MessageTypeDefOf.NeutralEvent);
            }

            if (Widgets.ButtonText(strategyButton, "战略部署"))
            {
                Messages.Message("点击了战略部署按钮", MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// 绘制资源展示区域
        /// </summary>
        private void DrawResourceDisplay(Rect rect)
        {
            // 绘制背景
            if (UseCustomUI && DMSL_CustomUIAssets.BuildingSlotBackground != null)
            {
                GUI.DrawTexture(rect, DMSL_CustomUIAssets.BuildingSlotBackground, ScaleMode.StretchToFill);
            }
            else
            {
            Widgets.DrawBoxSolid(rect, panelColor);
            }

            // 仅在未开启自定义UI时绘制白线边框
            if (!UseCustomUI_Debug)
            {
            Widgets.DrawBox(rect, 1);
            }

            // 标题（确保在最上层，不被遮挡）
            Text.Font = GameFont.Medium; // 稍微调大字号
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect titleRect = new Rect(rect.x + 5f, rect.y + 15f, rect.width - 10f, 25f); // 调整高度适应更大字体

            // 在启用自定义UI时，使用黑色文字
            Color originalGUIColor = GUI.color;
            if (UseCustomUI)
            {
                GUI.color = Color.black;
            }
            if (UseCustomFonts && _customLabelStyle != null)
            {
                GUI.Label(titleRect, "DMSL_ResourceStorage".Translate(), _customLabelStyle);
            }
            else
            {
            Widgets.Label(titleRect, "DMSL_ResourceStorage".Translate());
            }
            GUI.color = originalGUIColor;

            Text.Anchor = TextAnchor.UpperLeft;

            // 内容区域
            Rect contentRect = new Rect(
                rect.x + 5f, 
                titleRect.yMax + 10f, // 增加到标题的间距，从5f增加到10f
                rect.width - 10f, 
                rect.height - titleRect.height - 25f // 调整高度计算（15f + 10f = 25f）
            );

            // 获取当前集群的储存数据
            var storage = GetCurrentClusterStorage();
            if (storage == null)
            {
                // 没有集群数据时，创建一个临时的空储存对象来显示0/上限
                storage = new ClusterStorage();
                storage.Init();
            }

            // 绘制物资列表（支持滚动）
            DrawResourceList(contentRect, storage);
        }

        /// <summary>
        /// 获取当前集群的储存数据
        /// </summary>
        private ClusterStorage? GetCurrentClusterStorage()
        {
            // 获取第一个集群的储存数据
            // TODO: 根据实际需求修改获取方式（如从窗口参数传入、从选中的WorldObject获取等）
            if (ClusterResourceStorageManager.Instance == null) return null;
            
            // 方案1：通过WorldObject获取（如果存在）
            var worldObject = Find.WorldObjects.AllWorldObjects
                .FirstOrDefault(wo => wo.def.defName == "DMSL_IndustrialHubCluster");
            if (worldObject != null)
            {
                var clusterData = ClusterResourceStorageManager.Instance.GetClusterByWorldObject(worldObject);
                return clusterData?.storage;
            }
            
            // 方案2：获取第一个集群数据（如果没有WorldObject）
            var firstCluster = ClusterResourceStorageManager.Instance.GetAllClusters().FirstOrDefault();
            return firstCluster?.storage;
        }

        /// <summary>
        /// 绘制物资列表
        /// </summary>
        private void DrawResourceList(Rect rect, ClusterStorage storage)
        {
            // 获取所有可储存的物资配置
            var storableDefs = DefDatabase<IndustrialHubClusterStorage>.AllDefs
                .Where(def => def.ThingDef != null)
                .ToList();

            // 如果没有可储存的物资配置，也正常显示（数量为0/0）
            // 注释掉原来的"暂无资源"显示

            // 根据窗口宽度动态调整布局参数
            float padding = 5f;
            float spacing = 12f;     // 基础间隔

            // 判断窗口是否足够宽以显示4个物品
            float minWidthFor4Items = padding * 2 + 4 * (32f + 100f) + 3 * spacing;  // 4个物品需要的宽度
            bool canShow4Items = rect.width >= minWidthFor4Items;

            // 根据宽度设置不同的参数
            float itemHeight, iconSize, textWidth;
            if (canShow4Items)
            {
                // 窗口较宽：一行显示4个，使用较大尺寸
                itemHeight = 40f;
                iconSize = 32f;
                textWidth = 100f;
                spacing = 15f;
            }
            else
            {
                // 窗口较窄：一行显示3个，使用较小尺寸
                itemHeight = 35f;
                iconSize = 28f;
                textWidth = 85f;
                spacing = 12f;
            }

            // 计算每行可显示的物资数量
            int itemsPerRow = canShow4Items ? 4 : 3;
            
            // 使用滚动视图
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, 
                Mathf.CeilToInt((float)storableDefs.Count / itemsPerRow) * (itemHeight + spacing));
            
            Widgets.BeginScrollView(rect, ref resourceScrollPosition, viewRect);
            
            float currentY = 0f;
            int currentIndex = 0;
            
            foreach (var storableDef in storableDefs)
            {
                int row = currentIndex / itemsPerRow;
                int col = currentIndex % itemsPerRow;
                
                // 计算一行中所有项的总宽度
                float rowTotalWidth = itemsPerRow * (iconSize + textWidth) + (itemsPerRow - 1) * spacing;
                // 计算居中时的起始X坐标
                float rowStartX = (rect.width - 16f - rowTotalWidth) / 2f;  // 16f是滚动条宽度

                float x = rowStartX + col * (iconSize + textWidth + spacing);
                float y = currentY + row * (itemHeight + spacing);
                
                Rect itemRect = new Rect(x, y, iconSize + textWidth, itemHeight);
                DrawResourceItem(itemRect, storableDef, storage, iconSize);
                
                currentIndex++;
            }
            
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 绘制单个物资项
        /// </summary>
        private void DrawResourceItem(Rect rect, IndustrialHubClusterStorage storableDef, ClusterStorage storage, float iconSize)
        {
            // 绘制物资贴图（动态图标尺寸）
            Rect iconRect = new Rect(rect.x, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
            Widgets.ThingIcon(iconRect, storableDef.ThingDef);
            
            // 获取数量和上限
            int currentAmount = storage.GetAmount(storableDef.thingDefName);
            int maxAmount = storage.GetMaxAmount(storableDef.thingDefName);
            
            // 绘制文本：只显示数量信息（增大文本区域）
            Rect textRect = new Rect(iconRect.xMax + 8f, rect.y, rect.width - iconRect.width - 8f, rect.height);  // 增大图标和文本间距
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            
            // 只显示数量，不显示物品名称
            string text = $"{currentAmount}/{maxAmount}";
            
            // 如果数量接近上限，使用不同颜色
            Color textColor = currentAmount >= maxAmount * 0.9f ? Color.yellow : Color.white;
            GUI.color = textColor;
            DrawCustomLabel(textRect, text, TextAnchor.MiddleLeft);
            GUI.color = Color.white;
            
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 绘制额外窗口（点击空槽后展开）
        /// 额外窗口在左侧区域的左侧额外添加，右上角有关闭按钮
        /// </summary>
        private void DrawExtraWindow(Rect rect)
        {
            if (!selectedSlotIndex.HasValue) return;

            // 绘制窗口背景
            Widgets.DrawBoxSolid(rect, panelColor);

            // 绘制建筑列表背景（在白线之内，高于背景，低于内容）
            if (UseCustomUI_Debug && DMSL_CustomUIAssets.TaskQueuePanelBackground != null)
            {
                GUI.DrawTexture(rect, DMSL_CustomUIAssets.TaskQueuePanelBackground, ScaleMode.StretchToFill);
            }

            // 绘制白线边框（仅在未开启自定义UI时）
            if (!UseCustomUI_Debug)
            {
            Widgets.DrawBox(rect, 2);
            }

            // 标题栏
            float titleHeight = 30f;
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, titleHeight);

            // 使用TaskQueueTitleBackground填充标题栏
            if (UseCustomUI_Debug && DMSL_CustomUIAssets.TaskQueueTitleBackground != null)
            {
                GUI.DrawTexture(titleRect, DMSL_CustomUIAssets.TaskQueueTitleBackground, ScaleMode.StretchToFill);
            }
            else
            {
            Widgets.DrawBoxSolid(titleRect, new Color(0.15f, 0.15f, 0.15f));
            }

            // 标题图标（左侧，仅在自定义UI时显示）
            if (UseCustomUI && DMSL_CustomUIAssets.ProductionIcon != null)
            {
                float iconSize = 25f;
                Rect iconRect = new Rect(
                    rect.x + 15f,                          // 左边缘+边距
                    rect.y + (titleHeight - iconSize) / 2f, // 垂直居中
                    iconSize,                               // 图标宽度
                    iconSize                                // 图标高度
                );
                GUI.DrawTexture(iconRect, DMSL_CustomUIAssets.ProductionIcon, ScaleMode.StretchToFill);
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            DrawCustomLabel(new Rect(rect.x + 10f, rect.y, rect.width - 10f, titleHeight), "选择建筑", TextAnchor.MiddleLeft);
            Text.Anchor = TextAnchor.UpperLeft;

            // 关闭按钮（右上角，仅关闭额外窗口）
            if (UseCustomUI && DMSL_CustomUIAssets.CloseButton != null)
            {
                float closeButtonSize = 20f;
                Rect closeButtonRect = new Rect(
                    rect.xMax - closeButtonSize - 10f,  // 右边缘-按钮宽度-边距
                    rect.y + 5f,                         // 上边缘+边距
                    closeButtonSize,                     // 按钮宽度
                    closeButtonSize                      // 按钮高度
                );

                // 1. 选择纹理（参考DrawCustomButton的逻辑）
                Texture2D? currentTex = DMSL_CustomUIAssets.CloseButton; // 默认使用普通纹理

                // 实时检测鼠标状态，决定是否使用按下纹理
                if (Mouse.IsOver(closeButtonRect) && UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                {
                    Texture2D? pressedTex = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/CloseButton_pressed", false);
                    if (pressedTex != null)
                    {
                        currentTex = pressedTex;
                    }
                }

                // 2. 绘制选中的纹理
                GUI.DrawTexture(closeButtonRect, currentTex, ScaleMode.StretchToFill);

                // 3. 鼠标悬停高亮效果（仅当鼠标悬停时，且按钮不是按下状态）
                if (Mouse.IsOver(closeButtonRect) && !UnityGUIBugsFixer.IsLeftMouseButtonPressed())
                {
                    GUI.color = buttonHoverHighlightColor;
                    GUI.DrawTexture(closeButtonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                    GUI.color = Color.white; // 恢复默认颜色
                }

                // 4. 处理关闭按钮点击
                if (Widgets.ButtonInvisible(closeButtonRect))
                {
                    selectedSlotIndex = null;  // 关闭额外窗口
                    Messages.Message("关闭了建筑选择窗口", MessageTypeDefOf.NeutralEvent);
                    return;
                }
            }
            else
            {
                // 回退到简单文本按钮
            Rect closeButtonRect = new Rect(rect.xMax - 30f, rect.y + 5f, 20f, 20f);
            if (Widgets.ButtonText(closeButtonRect, "×"))
            {
                selectedSlotIndex = null;  // 关闭额外窗口
                Messages.Message("关闭了建筑选择窗口", MessageTypeDefOf.NeutralEvent);
                return;
                }
            }

            // 占位内容（暂时显示提示信息）
            Rect contentRect = new Rect(rect.x + 5f, titleRect.yMax + 5f, rect.width - 10f, rect.height - titleHeight - 10f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            DrawCustomLabel(contentRect, "建筑列表将在此显示", TextAnchor.MiddleCenter);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 绘制右侧面板（任务队列）
        /// </summary>
        private void DrawRightPanel(Rect rect)
        {
            // 绘制右侧面板背景（任务队列）
            Widgets.DrawBoxSolid(rect, panelColor);  // 始终绘制基础背景

            // 绘制任务队列背景贴图（叠加显示，与建筑选择窗口同步）
            if (UseCustomUI_Debug && DMSL_CustomUIAssets.TaskQueuePanelBackground != null)
            {
                GUI.DrawTexture(rect, DMSL_CustomUIAssets.TaskQueuePanelBackground, ScaleMode.StretchToFill);
            }

            // 绘制白线边框（仅在未开启自定义UI时）
            if (!UseCustomUI_Debug)
            {
            Widgets.DrawBox(rect, 2);
            }

            // 标题栏
            float titleHeight = 30f; // 微调高度以匹配建筑列表的视觉效果
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, titleHeight);

            // 标题栏背景
            if (UseCustomUI_Debug && DMSL_CustomUIAssets.TaskQueueTitleBackground != null)
            {
                GUI.DrawTexture(titleRect, DMSL_CustomUIAssets.TaskQueueTitleBackground, ScaleMode.StretchToFill);
            }
            else
            {
            Widgets.DrawBoxSolid(titleRect, new Color(0.15f, 0.15f, 0.15f));
            }

            // 标题图标（左侧，仅在自定义UI时显示）
            if (UseCustomUI && DMSL_CustomUIAssets.TaskListTitleIcon != null)
            {
                float iconSize = 25f;
                Rect iconRect = new Rect(
                    rect.x + 15f,                          // 左边缘+边距
                    rect.y + (titleHeight - iconSize) / 2f, // 垂直居中
                    iconSize,                               // 图标宽度
                    iconSize                                // 图标高度
                );
                GUI.DrawTexture(iconRect, DMSL_CustomUIAssets.TaskListTitleIcon, ScaleMode.StretchToFill);
            }

            // 标题文本
            Text.Anchor = TextAnchor.MiddleLeft;
            DrawCustomLabel(new Rect(rect.x + 10f, rect.y, rect.width - 10f, titleHeight), "任务队列", TextAnchor.MiddleLeft);
            Text.Anchor = TextAnchor.UpperLeft;

            // 任务列表区域（占位，与右侧窗口边线距离10px，下边线与窗口对齐）
            Rect taskListRect = new Rect(
                rect.x + 10f,
                titleRect.yMax + 5f,
                rect.width - 20f,
                rect.height - titleHeight - 5f  // 下边距调整为5px，与窗口下边线对齐
            );

            // 任务列表区域背景已在面板级别绘制，无需重复绘制

            // 占位文本
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            DrawCustomLabel(taskListRect, "任务列表将在此显示", TextAnchor.MiddleCenter);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 绘制命令窗口
        /// 直接用SystemOutput文字填充窗口内容
        /// </summary>
        private void DrawCommandWindow(Rect rect)
        {
            // 绘制背景
            if (UseCustomUI && DMSL_CustomUIAssets.SystemOutputPanelBackground != null)
            {
                GUI.DrawTexture(rect, DMSL_CustomUIAssets.SystemOutputPanelBackground, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f)); // 深色背景
            }

            // 仅在未开启自定义UI时绘制白线边框
            if (!UseCustomUI_Debug)
            {
                Widgets.DrawBox(rect, 1);
            }

            // 添加紧凑的标题栏 (20px高度)
            float titleHeight = 20f;
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, titleHeight);

            // 标题栏背景 (根据UI设置使用贴图或默认颜色)
            if (UseCustomUI && DMSL_CustomUIAssets.SystemOutputTitleBackground != null)
            {
                GUI.DrawTexture(titleRect, DMSL_CustomUIAssets.SystemOutputTitleBackground, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(titleRect, new Color(0.15f, 0.15f, 0.15f));
            }

            // 标题文字 (根据UI设置调整样式)
            Text.Anchor = TextAnchor.MiddleCenter;
            if (UseCustomUI)
            {
                DrawCustomLabel(titleRect, "System Output", TextAnchor.MiddleCenter);
            }
            else
            {
                Text.Font = GameFont.Small;
                Widgets.Label(titleRect, "System Output");
                Text.Font = GameFont.Small;
            }
            Text.Anchor = TextAnchor.UpperLeft;

            // 文字显示区域 (标题栏下方)
            Rect textArea = new Rect(
                rect.x + 5f,
                titleRect.yMax + 3f,
                rect.width - 10f,
                rect.height - titleHeight - 8f
            );

            // 绘制系统输出消息
            DrawSystemMessages(textArea);
        }

        /// <summary>
        /// 绘制系统消息列表
        /// UI层只读访问消息数据，不拥有或修改任何消息
        /// </summary>
        private void DrawSystemMessages(Rect rect)
        {
            if (_displayedLines.Count == 0 && _pendingLines.Count == 0)
            {
                // 无消息时的占位显示（居中对齐）
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                DrawSystemOutputLabel(rect, "暂无系统消息", TextAnchor.MiddleCenter);
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // 限制总行数，充分利用可用空间
            float lineHeight = Text.CalcHeight("测试", rect.width) * 1.5f; // 增大行间距
            int maxTotalLines = Mathf.FloorToInt(rect.height / lineHeight);

            // 获取应该显示的行（最新的maxTotalLines行）
            int startIndex = Mathf.Max(0, _displayedLines.Count - maxTotalLines);
            var linesToShow = _displayedLines.GetRange(startIndex, Mathf.Min(maxTotalLines, _displayedLines.Count));

            // 从顶部开始绘制行
            float currentY = rect.y;
            for (int i = 0; i < linesToShow.Count; i++)
            {
                var line = linesToShow[i];
                Rect lineRect = new Rect(rect.x, currentY, rect.width, lineHeight);

                // 绘制单行
                DrawSingleSystemOutputLine(lineRect, line);

                currentY += lineHeight;
            }
        }

        /// <summary>
        /// SystemOutput行数据结构，用于按行管理显示内容
        /// </summary>
        private class SystemOutputLine
        {
            /// <summary>
            /// 行文本内容
            /// </summary>
            public string Text { get; }

            /// <summary>
            /// 所属消息的引用（用于样式和元数据）
            /// </summary>
            public SystemMessage Message { get; }

            /// <summary>
            /// 是否是消息中的最后一行
            /// </summary>
            public bool IsLastLineOfMessage { get; }

            public SystemOutputLine(string text, SystemMessage message, bool isLastLineOfMessage)
            {
                Text = text ?? "";
                Message = message;
                IsLastLineOfMessage = isLastLineOfMessage;
            }
        }

        /// <summary>
        /// 将消息拆分为独立的行
        /// </summary>
        private List<SystemOutputLine> SplitMessageIntoLines(SystemMessage message, float maxWidth)
        {
            var lines = new List<SystemOutputLine>();

            if (string.IsNullOrEmpty(message.Content))
            {
                // 空消息也需要一行
                lines.Add(new SystemOutputLine("", message, true));
                return lines;
            }

            // 设置正确的字体
            Text.Font = GameFont.Small;

            // 先按主动换行符分割
            string[] manualLines = message.Content.Split('\n');

            for (int i = 0; i < manualLines.Length; i++)
            {
                string line = manualLines[i];
                bool isLastManualLine = (i == manualLines.Length - 1);

                if (string.IsNullOrEmpty(line))
                {
                    // 空行
                    lines.Add(new SystemOutputLine("", message, isLastManualLine));
                    continue;
                }

                // 计算这一行是否需要自动换行
                float textWidth = Text.CalcSize(line).x;

                if (textWidth <= maxWidth)
                {
                    // 不需要换行，直接添加
                    lines.Add(new SystemOutputLine(line, message, isLastManualLine));
                }
                else
                {
                    // 需要自动换行，将文本拆分成多行
                    var wrappedLines = WrapTextToLines(line, maxWidth);
                    for (int j = 0; j < wrappedLines.Count; j++)
                    {
                        bool isLastWrappedLine = isLastManualLine && (j == wrappedLines.Count - 1);
                        lines.Add(new SystemOutputLine(wrappedLines[j], message, isLastWrappedLine));
                    }
                }
            }

            return lines;
        }

        /// <summary>
        /// 将长文本按宽度限制拆分成多行
        /// </summary>
        private List<string> WrapTextToLines(string text, float maxWidth)
        {
            var lines = new List<string>();
            Text.Font = GameFont.Small;

            string remainingText = text;

            while (!string.IsNullOrEmpty(remainingText))
            {
                // 找到能容纳的最大字符数
                int charCount = FindMaxCharsForWidth(remainingText, maxWidth);

                if (charCount >= remainingText.Length)
                {
                    // 剩余文本都能容纳
                    lines.Add(remainingText);
                    break;
                }
                else
                {
                    // 需要拆分
                    string lineText = remainingText.Substring(0, charCount);
                    lines.Add(lineText.TrimEnd());

                    // 移除已处理的文本
                    remainingText = remainingText.Substring(charCount).TrimStart();
                }
            }

            return lines;
        }

        /// <summary>
        /// 找到在给定宽度限制下能容纳的最大字符数
        /// </summary>
        private int FindMaxCharsForWidth(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            Text.Font = GameFont.Small;

            // 二分查找最合适的字符数
            int left = 1;
            int right = text.Length;
            int bestFit = 0;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                string testText = text.Substring(0, mid);
                float width = Text.CalcSize(testText).x;

                if (width <= maxWidth)
                {
                    bestFit = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return bestFit;
        }

        /// <summary>
        /// 绘制单行SystemOutput内容
        /// </summary>
        private void DrawSingleSystemOutputLine(Rect rect, SystemOutputLine line)
        {
            // 只输出文字内容，不显示时间和来源
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            DrawSystemOutputLabel(rect, line.Text, TextAnchor.MiddleLeft);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 绘制单条系统消息
        /// 严格只读访问消息属性，不修改任何数据
        /// </summary>

        /// <summary>
        /// 绘制SystemOutput专用标签（始终使用灰色文字）
        /// </summary>
        private void DrawSystemOutputLabel(Rect rect, string text, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            TextAnchor originalAnchor = Text.Anchor;
            Text.Anchor = anchor;

            // SystemOutput始终使用灰色文字，确保在任何UI模式下都有良好对比度
            Color originalColor = GUI.color;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);  // 统一的灰色调

            if (UseCustomFonts && _customLabelStyle != null)
            {
                // 创建临时样式，使用正确的对齐方式
                GUIStyle tempStyle = new GUIStyle(_customLabelStyle);
                tempStyle.alignment = anchor;
                GUI.Label(rect, text, tempStyle);
            }
            else
            {
                Widgets.Label(rect, text);
            }

            GUI.color = originalColor;
            Text.Anchor = originalAnchor;
        }

        /// <summary>
        /// 发送欢迎消息（仅在首次打开窗口时）
        /// </summary>
        private void SendWelcomeMessageIfNeeded()
        {
            // 立即标记已发送，防止在同一帧内的重复调用
            if (!_welcomeMessageSent)
            {
                _welcomeMessageSent = true; // 立即设置标志

                try
                {
                    // 一次性获取所有时间信息，确保时间固定不变
                    string username = DMS_Legion.SystemOutputInfoProvider.GetDisplayUsername();
                    string date = DMS_Legion.SystemOutputInfoProvider.GetFormattedDateForWelcome();
                    string time = DMS_Legion.SystemOutputInfoProvider.GetFormattedTimeForWelcome();
                    string greeting = DMS_Legion.SystemOutputInfoProvider.GetTimeBasedGreeting();

                    // 手动构造欢迎消息内容
                    string welcomeMessage = string.Format(">通信链路状态：稳定\n>您好，{0}。\n 当前时间 {1} {2}，{3}。\n 请下达指令。",
                        username, date, time, greeting);

                    // 创建系统消息并添加到管理器
                    var systemMessage = new SystemMessage(welcomeMessage, "System");
                    SystemOutputManager.Instance.AddMessage(systemMessage.Content, systemMessage.Source);
                }
                catch (Exception ex)
                {
                    Verse.Log.Warning($"SystemOutput: 发送欢迎消息失败: {ex.Message}");
                    // 即使失败，标志也保持为true，避免反复尝试
                }
            }
        }
    }
}



