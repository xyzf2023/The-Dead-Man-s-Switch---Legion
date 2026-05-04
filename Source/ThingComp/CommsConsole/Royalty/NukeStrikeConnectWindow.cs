// ============================================================================
// 文件：NukeStrikeConnectWindow.cs
// 说明：点击「连接核打击系统」后显示的自定义 UI 窗口
// 参考：ClusterConstruction/ModularOperationWindow（宽度为参考 UI 的一半，高度一致，同背景与关闭按钮）
// ============================================================================

using UnityEngine;
using Verse;
using RimWorld;

namespace DMS_Legion
{
    /// <summary>
    /// 核打击系统连接窗口：宽度为 ModularOperationWindow 的一半，高度一致；
    /// 使用与参考 UI 相同的主窗口背景贴图，在相同相对位置绘制相同贴图的关闭按钮。
    /// </summary>
    [StaticConstructorOnStartup]
    public class NukeStrikeConnectWindow : Window
    {
        static NukeStrikeConnectWindow()
        {
            _cachedWorldMap = ContentFinder<Texture2D>.Get("WorldMap", false)
                ?? ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/WorldMap", false);
        }

        /// <summary>参考窗口宽度 1095，本窗口为一半</summary>
        private const float ReferenceWidth = 1095f;
        /// <summary>与参考窗口一致的高度；窗口实际高度减少 50px 以缩小空白区域</summary>
        private const float ReferenceHeight = 750f;
        private const float WindowHeightOffset = -50f;

        private const float CloseButtonSize = 35f;
        private const float CloseButtonMarginRight = 10f + 3f;
        private const float CloseButtonMarginTop = 10f + 1f;

        private static readonly Color BgColorFallback = new Color(0.05f, 0.05f, 0.05f);
        private static readonly Color ButtonHoverHighlightColor = new Color(0.9f, 0.9f, 0.95f, 0.3f);

        /// <summary>横向无缝滚动：步进间隔（秒）</summary>
        private const float StepInterval = 1f;
        /// <summary>横向无缝滚动：每次步进位移（像素）</summary>
        private const float StepDistance = 12f;

        private static Texture2D? _cachedWorldMap;
        private static Texture2D? WorldMapTexture => _cachedWorldMap;

        /// <summary>本窗口始终使用 MOD 字体文件中的字体，缓存 GUIStyle</summary>
        private GUIStyle? _titleLabelStyle;
        /// <summary>文字区域用标签样式（复刻 ClusterConstruction SystemOutput 显示逻辑）</summary>
        private GUIStyle? _bodyLabelStyle;
        /// <summary>步进计时器（累计真实时间）</summary>
        private float _stepTimer;
        /// <summary>横向偏移（像素），取模回绕</summary>
        private float _scrollX;

        /// <summary>待显示行队列（text, 可选颜色，null 为默认灰）</summary>
        private readonly System.Collections.Generic.Queue<(string text, Color? color)> _pendingLines = new System.Collections.Generic.Queue<(string text, Color? color)>();
        /// <summary>已显示行列表，超出时从顶部移除（向上位移）</summary>
        private readonly System.Collections.Generic.List<(string text, Color? color)> _displayedLines = new System.Collections.Generic.List<(string text, Color? color)>();
        private float _nextLineDisplayTime;
        private const float LineDisplayInterval = 0.5f;
        private const int MaxDisplayLines = 50;

        /// <summary>使用通讯台的人（用于显示身份确认）</summary>
        private readonly Pawn _negotiator;
        /// <summary>DMS_Army 派系（用于获取头衔）</summary>
        private readonly Faction _faction;
        /// <summary>阶段：-1 未开始，0 等 1~3s，1 等 1s，2 结束（此后按钮可用）</summary>
        private int _phase = -1;
        private float _phaseTriggerTime;

        /// <summary>断开连接：显示关闭文案后，在此时间点关闭窗口</summary>
        private float _closeScheduledTime;
        /// <summary>已点击「断开连接」，禁止重复发送</summary>
        private bool _disconnectRequested;
        /// <summary>已点击「执行权限认证」，等待 1~2s 后显示校验结果，此期间禁止重复点击</summary>
        private bool _permissionAuthRequested;
        private float _permissionResultTime;
        /// <summary>权限认证结果已显示，左侧按钮改为「输入密钥」</summary>
        private bool _authComplete;
        /// <summary>已点击「输入密钥」，等待 0.5~1s 后显示密钥确认结果</summary>
        private bool _keyEnteredRequested;
        private float _keyConfirmTime;
        /// <summary>密钥已确认，左侧按钮改为红色「传输打击坐标」</summary>
        private bool _keyComplete;

        public override Vector2 InitialSize => new Vector2(ReferenceWidth * 0.5f, ReferenceHeight + WindowHeightOffset);

        protected override float Margin => 0f;

        public NukeStrikeConnectWindow(Pawn negotiator, Faction faction)
        {
            _negotiator = negotiator;
            _faction = faction;
            closeOnAccept = false;
            closeOnCancel = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        private void EnsureTitleLabelStyle()
        {
            if (_titleLabelStyle != null) return;
            Font? font = DMSL_ModSettings.GetModFontAlways();
            if (font == null) return;
            _titleLabelStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]);
            _titleLabelStyle.font = font;
            _titleLabelStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void EnsureBodyLabelStyle()
        {
            if (_bodyLabelStyle != null) return;
            Font? font = DMSL_ModSettings.GetModFontAlways();
            if (font == null) return;
            _bodyLabelStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]);
            _bodyLabelStyle.font = font;
            _bodyLabelStyle.alignment = TextAnchor.UpperLeft;
        }

        /// <summary>复刻 ClusterConstruction DrawSystemOutputLabel：支持自定义字体、对齐与可选颜色（null 为默认灰）</summary>
        private void DrawSystemOutputLabel(Rect rect, string text, TextAnchor anchor = TextAnchor.UpperLeft, Color? lineColor = null)
        {
            TextAnchor originalAnchor = Text.Anchor;
            Text.Anchor = anchor;
            Color originalColor = GUI.color;
            GUI.color = lineColor ?? new Color(0.7f, 0.7f, 0.7f);
            if (_bodyLabelStyle != null)
            {
                GUIStyle style = new GUIStyle(_bodyLabelStyle) { alignment = anchor };
                GUI.Label(rect, text, style);
            }
            else
            {
                Widgets.Label(rect, text);
            }
            GUI.color = originalColor;
            Text.Anchor = originalAnchor;
        }

        /// <summary>首次打开时加入第一阶段文字并启动 1~3 秒计时</summary>
        private void EnsureInitialContentEnqueued()
        {
            if (_phase >= 0) return;
            _phase = 0;
            _pendingLines.Enqueue(("DMSL_NukeStrike_Connecting".Translate().ToString(), (Color?)null));
            _nextLineDisplayTime = Time.realtimeSinceStartup;
            _phaseTriggerTime = Time.realtimeSinceStartup + Random.Range(1f, 3f);
        }

        /// <summary>阶段到时将下一段文字加入待显示队列；冷却中则将“系统已就绪”替换为冷却提示</summary>
        private void ProcessPhaseTransitions()
        {
            float now = Time.realtimeSinceStartup;
            if (_phase == 0 && now >= _phaseTriggerTime)
            {
                string title = _negotiator?.royalty?.GetCurrentTitle(_faction)?.label ?? "";
                string name = _negotiator?.Name?.ToStringFull ?? _negotiator?.LabelShort ?? "";
                _pendingLines.Enqueue(("DMSL_NukeStrike_ChannelEncrypted".Translate().ToString(), (Color?)null));
                _pendingLines.Enqueue(("DMSL_NukeStrike_SystemOnline".Translate().ToString(), (Color?)null));
                _pendingLines.Enqueue(("DMSL_NukeStrike_IdentityConfirm".Translate(title, name).ToString(), (Color?)null));
                _phase = 1;
                _phaseTriggerTime = now + 3f;//延迟时间
            }
            else if (_phase == 1 && now >= _phaseTriggerTime)
            {
                int cooldownTicks = NukeStrikeCooldownComponent.GetOrCreate()?.GetRemainingCooldownTicks() ?? 0;
                if (cooldownTicks > 0)
                    _pendingLines.Enqueue(("DMSL_NukeStrike_CooldownMessage".Translate(FormatCooldownDays(cooldownTicks)).ToString(), (Color?)null));
                else
                    _pendingLines.Enqueue(("DMSL_NukeStrike_SystemReady".Translate().ToString(), (Color?)null));
                _phase = 2;
            }

            // 权限认证：1~2 秒后显示校验结果
            if (_permissionAuthRequested && now >= _permissionResultTime)
            {
                foreach (string line in "DMSL_NukeStrike_AuthResult".Translate().ToString().Split('\n'))
                    _pendingLines.Enqueue((line, (Color?)null));
                _authComplete = true;
                _permissionAuthRequested = false;
            }

            // 输入密钥：0.5~1 秒后显示密钥已确认
            if (_keyEnteredRequested && now >= _keyConfirmTime)
            {
                foreach (string line in "DMSL_NukeStrike_KeyValidResult".Translate().ToString().Split('\n'))
                    _pendingLines.Enqueue((line, (Color?)null));
                _pendingLines.Enqueue(("DMSL_NukeStrike_AllowCoords".Translate().ToString(), Color.red));
                _keyComplete = true;
                _keyEnteredRequested = false;
            }
        }

        /// <summary>断开连接定时：到点关闭窗口</summary>
        private void ProcessCloseTimer()
        {
            if (_closeScheduledTime <= 0f) return;
            if (Time.realtimeSinceStartup >= _closeScheduledTime)
            {
                _closeScheduledTime = 0f;
                Close();
            }
        }

        /// <summary>左下角功能按钮（参考 UI：贴图、按下态、悬停高光；未就绪时显示按下态且不可点；labelColor 为 null 时用黑色）</summary>
        private bool DrawBottomLeftButton(Rect rect, string labelKey, bool enabled, Color? labelColor = null)
        {
            Texture2D? tex = DMSL_CustomUIAssets.Button;
            bool showPressed = !enabled || (Mouse.IsOver(rect) && Input.GetMouseButton(0));
            if (showPressed && tex != null)
            {
                Texture2D? pressedTex = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/Button_pressed", false);
                if (pressedTex != null) tex = pressedTex;
            }
            if (tex != null)
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            else
                Widgets.DrawBoxSolid(rect, new Color(0.25f, 0.25f, 0.25f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Color prevColor = GUI.color;
            Color textColor = labelColor ?? Color.black;
            if (_bodyLabelStyle != null)
            {
                GUIStyle style = new GUIStyle(_bodyLabelStyle) { alignment = TextAnchor.MiddleCenter };
                GUI.color = textColor;
                GUI.Label(rect, labelKey.Translate(), style);
            }
            else
            {
                GUI.color = textColor;
                Widgets.Label(rect, labelKey.Translate());
            }
            GUI.color = prevColor;
            Text.Anchor = TextAnchor.UpperLeft;
            if (enabled && Mouse.IsOver(rect) && !Input.GetMouseButton(0))
            {
                GUI.color = ButtonHoverHighlightColor;
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = Color.white;
            }
            return enabled && Widgets.ButtonInvisible(rect);
        }

        /// <summary>逐行显示：到时间则从待显示队列取一行加入已显示，超出则从顶部移除（向上位移）</summary>
        private void ProcessLineDisplay()
        {
            if (_pendingLines.Count == 0 || Time.realtimeSinceStartup < _nextLineDisplayTime) return;
            _displayedLines.Add(_pendingLines.Dequeue());
            _nextLineDisplayTime = Time.realtimeSinceStartup + LineDisplayInterval;
            if (_displayedLines.Count > MaxDisplayLines)
                _displayedLines.RemoveAt(0);
        }

        /// <summary>剩余 tick 转“天”显示：≥1 天向下取整整数，&lt;1 天保留一位小数向下取整</summary>
        private static string FormatCooldownDays(int remainingTicks)
        {
            const float TicksPerDay = 60000f;
            float daysRaw = remainingTicks / TicksPerDay;
            if (daysRaw >= 1f)
                return Mathf.FloorToInt(daysRaw).ToString();
            return (Mathf.Floor(daysRaw * 10f) / 10f).ToString("0.0");
        }

        /// <summary>复刻 ClusterConstruction DrawSystemMessages：仅绘制已显示行；冷却中仅将“系统已就绪/暂无系统消息”替换为冷却提示，其余流程照常</summary>
        private void DrawSystemMessages(Rect rect)
        {
            int cooldownTicks = NukeStrikeCooldownComponent.GetOrCreate()?.GetRemainingCooldownTicks() ?? 0;
            bool inCooldown = cooldownTicks > 0;

            EnsureInitialContentEnqueued();
            ProcessPhaseTransitions();

            float lineHeight = Text.CalcHeight("测试", rect.width) * 1.15f;
            int maxTotalLines = Mathf.Max(1, Mathf.FloorToInt(rect.height / lineHeight));

            if (_displayedLines.Count == 0 && _pendingLines.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                if (inCooldown)
                    DrawSystemOutputLabel(rect, "DMSL_NukeStrike_CooldownMessage".Translate(FormatCooldownDays(cooldownTicks)), TextAnchor.MiddleCenter);
                else
                    DrawSystemOutputLabel(rect, "DMSL_NukeStrike_NoMessage".Translate(), TextAnchor.MiddleCenter);
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            ProcessLineDisplay();

            int startIndex = Mathf.Max(0, _displayedLines.Count - maxTotalLines);
            int count = Mathf.Min(maxTotalLines, _displayedLines.Count);
            float y = rect.y;
            for (int i = 0; i < count; i++)
            {
                var (lineText, lineColor) = _displayedLines[startIndex + i];
                Rect lineRect = new Rect(rect.x, y, rect.width, lineHeight);
                DrawSystemOutputLabel(lineRect, lineText, TextAnchor.MiddleLeft, lineColor);
                y += lineHeight;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 本窗口始终使用当前贴图与 MOD 字体文件中的字体，不受 MOD 设置（如 useCustomUI）影响
            EnsureTitleLabelStyle();
            EnsureBodyLabelStyle();

            // 1. 绘制整体背景（与参考 UI 一致贴图）
            if (DMSL_CustomUIAssets.MainWindowBackground != null)
            {
                GUI.DrawTexture(inRect, DMSL_CustomUIAssets.MainWindowBackground, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(inRect, BgColorFallback);
            }

            // 2. 绘制关闭按钮（与参考 UI 相同位置、相同贴图）
            float closeButtonBottom;
            if (DMSL_CustomUIAssets.CloseButton != null)
            {
                Rect closeButtonRect = new Rect(
                    inRect.xMax - CloseButtonSize - CloseButtonMarginRight,
                    inRect.y + CloseButtonMarginTop,
                    CloseButtonSize,
                    CloseButtonSize
                );
                closeButtonBottom = closeButtonRect.yMax;

                Texture2D? currentTex = DMSL_CustomUIAssets.CloseButton;
                if (Mouse.IsOver(closeButtonRect) && Input.GetMouseButton(0))
                {
                    Texture2D? pressedTex = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/CloseButton_pressed", false);
                    if (pressedTex != null)
                        currentTex = pressedTex;
                }

                if (currentTex != null)
                    GUI.DrawTexture(closeButtonRect, currentTex, ScaleMode.StretchToFill);

                if (Mouse.IsOver(closeButtonRect) && !Input.GetMouseButton(0))
                {
                    GUI.color = ButtonHoverHighlightColor;
                    GUI.DrawTexture(closeButtonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                    GUI.color = Color.white;
                }

                if (Widgets.ButtonInvisible(closeButtonRect))
                    Close();
            }
            else
            {
                Rect closeButtonRect = new Rect(inRect.xMax - 30f, inRect.y + 5f, 20f, 20f);
                closeButtonBottom = closeButtonRect.yMax;
                if (Widgets.ButtonText(closeButtonRect, "×"))
                    Close();
            }

            // 3. 关闭按钮下方：标题栏 + 文字区域贴图（缩小 0.85 倍，左右各外扩 10px，整体下移 2px；高度按基准宽计算，不随外扩变高）
            float panelGap = 5f;
            float panelMarginH = 10f;
            float panelTop = closeButtonBottom + panelGap + 2f;
            float titleHeight = 20f;
            const float panelScale = 0.85f;
            const float blockExpandH = 35f; // 左右各外扩 10px
            float baseBlockWidth = (inRect.width - panelMarginH * 2f) * panelScale; // 外扩前的宽度，用于算高度
            float blockWidth = baseBlockWidth + blockExpandH * 2f; // 外扩后绘制宽度
            float blockX = inRect.x + (inRect.width - blockWidth) * 0.5f;

            // 3.1 标题栏（与参考 UI 相同贴图）
            Rect titleRect = new Rect(blockX, panelTop, blockWidth, titleHeight);
            if (DMSL_CustomUIAssets.SystemOutputTitleBackground != null)
                GUI.DrawTexture(titleRect, DMSL_CustomUIAssets.SystemOutputTitleBackground, ScaleMode.StretchToFill);
            else
                Widgets.DrawBoxSolid(titleRect, new Color(0.15f, 0.15f, 0.15f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            Color prevColor = GUI.color;
            GUI.color = Color.black;
            if (_titleLabelStyle != null)
                GUI.Label(titleRect, "DMSL_Comms_NukeStrike_Terminal".Translate(), _titleLabelStyle);
            else
                Widgets.Label(titleRect, "DMSL_Comms_NukeStrike_Terminal".Translate());
            GUI.color = prevColor;
            Text.Anchor = TextAnchor.UpperLeft;

            // 3.2 文字区域贴图（高度按基准宽与贴图比例，不随左右外扩变高；宽度用外扩后 blockWidth）
            float panelY = titleRect.yMax + 3f;
            float textBlockBottom = panelY;
            if (DMSL_CustomUIAssets.SystemOutputPanelBackground != null)
            {
                Texture2D tex = DMSL_CustomUIAssets.SystemOutputPanelBackground;
                float aspectRatio = tex.width / (float)tex.height;
                float panelHeight = baseBlockWidth / aspectRatio; // 用外扩前宽度定高，避免越扩越大
                textBlockBottom = panelY + panelHeight;
                Rect panelRect = new Rect(blockX, panelY, blockWidth, panelHeight);
                GUI.DrawTexture(panelRect, tex, ScaleMode.StretchToFill);
            }
            else
            {
                textBlockBottom = panelY + baseBlockWidth; // 无贴图时给默认高度以便显示文字
            }

            // 3.3 文字显示（复刻 ClusterConstruction：逐行显示、向上位移，翻译键，区域向右 5px、行距缩小）
            const float textAreaPadding = 5f;
            const float textAreaShiftRight = 5f;
            Rect textAreaRect = new Rect(
                blockX + textAreaPadding + textAreaShiftRight,
                panelY + textAreaPadding,
                blockWidth - textAreaPadding * 2f - textAreaShiftRight,
                (textBlockBottom - panelY) - textAreaPadding * 2f
            );
            DrawSystemMessages(textAreaRect);

            // 3.4 左侧区域竖排两按钮：在「界面左侧」与「正方形区域左侧」之间居中，以文字窗口下边界为上限、上下排列
            ProcessCloseTimer();
            bool buttonsEnabled = _phase >= 2;
            const float btnW = 160f;
            const float btnH = 36f;
            const float btnGap = 32f; // 两按钮上下间距（原 12 + 20）
            const float leftMargin = 15f;
            const float leftSpacePaddingV = 15f;
            float squareLeft = inRect.xMax - 15f - 300f; // 与下方 squareRect 左缘一致
            float leftSpaceX = inRect.x + leftMargin;
            float leftSpaceW = squareLeft - leftSpaceX - leftMargin; // 界面左侧到正方形左侧之间的宽度
            float leftSpaceTop = textBlockBottom + leftSpacePaddingV;
            float leftSpaceBottom = inRect.yMax - leftSpacePaddingV;
            float leftSpaceH = leftSpaceBottom - leftSpaceTop;
            float totalBtnH = btnH * 2f + btnGap;
            float btnStartY = leftSpaceTop + (leftSpaceH - totalBtnH) * 0.5f;
            float btnX = leftSpaceX + (leftSpaceW - btnW) * 0.5f; // 在该区域内水平居中
            Rect authRect = new Rect(btnX, btnStartY, btnW, btnH);
            Rect disconnectRect = new Rect(btnX, btnStartY + btnH + btnGap, btnW, btnH);
            bool inCooldown = (NukeStrikeCooldownComponent.GetOrCreate()?.GetRemainingCooldownTicks() ?? 0) > 0;
            bool authBtnEnabled = buttonsEnabled && !inCooldown && !_permissionAuthRequested && !_keyEnteredRequested;
            bool disconnectBtnEnabled = buttonsEnabled && !_disconnectRequested;
            if (buttonsEnabled)
            {
                if (!inCooldown)
                {
                string authLabelKey = _keyComplete ? "DMSL_NukeStrike_TransmitCoords" : (_authComplete ? "DMSL_NukeStrike_EnterKey" : "DMSL_NukeStrike_PermissionAuth");
                Color? authLabelColor = _keyComplete ? Color.red : (Color?)null;
                if (DrawBottomLeftButton(authRect, authLabelKey, authBtnEnabled, authLabelColor))
                {
                    if (_keyComplete)
                    {
                        Close();
                        CommsSupportSubNodeFactory.CloseCommsDialog();
                        CommsNukeStrikeTargeting.BeginWorldTargeting(_faction);
                        return;
                    }
                    if (!_authComplete && !_permissionAuthRequested)
                    {
                        foreach (string line in "DMSL_NukeStrike_AuthRequest".Translate().ToString().Split('\n'))
                            _pendingLines.Enqueue((line, (Color?)null));
                        _permissionAuthRequested = true;
                        _permissionResultTime = Time.realtimeSinceStartup + Random.Range(1f, 2f);
                    }
                    else if (_authComplete && !_keyComplete && !_keyEnteredRequested)
                    {
                        foreach (string line in "DMSL_NukeStrike_KeyTransferConfirm".Translate().ToString().Split('\n'))
                            _pendingLines.Enqueue((line, (Color?)null));
                        _keyEnteredRequested = true;
                        _keyConfirmTime = Time.realtimeSinceStartup + Random.Range(0.5f, 1f);
                    }
                }
                }
                if (DrawBottomLeftButton(disconnectRect, "DMSL_NukeStrike_Disconnect", disconnectBtnEnabled))
                {
                    if (!_disconnectRequested)
                    {
                        _disconnectRequested = true;
                        _pendingLines.Enqueue(("DMSL_NukeStrike_DisconnectMsg".Translate().ToString(), (Color?)null));
                        _closeScheduledTime = Time.realtimeSinceStartup + 0.5f;
                    }
                }
            }

            // 4. 预留内容区域（后续可在此绘制核打击相关 UI）
            Rect contentRect = new Rect(
                inRect.x + 10f,
                inRect.y + 50f,
                inRect.width - 20f,
                inRect.height - 60f
            );
            // 占位：暂无内容

            // 5. 右下角正方形区域：距右 15px、距下 20px；该区域为多层绘制，WorldMap 离散步进无缝滚动，MapBackground 为最上层
            const float squareMarginRight = 15f;
            const float squareMarginBottom = 20f;
            const float squareSide = 300f;
            Rect squareRect = new Rect(inRect.xMax - squareMarginRight - squareSide, inRect.yMax - squareMarginBottom - squareSide, squareSide, squareSide);

            Texture2D? worldMap = WorldMapTexture;
            if (worldMap != null)
            {
                float drawH = squareRect.height;
                float drawW = drawH * (worldMap.width / (float)worldMap.height);
                if (drawW <= 0f) drawW = 1f;

                // 使用真实时间累计，否则暂停时 Time.deltaTime 为 0，步进不会触发
                _stepTimer += RealTime.deltaTime;
                while (_stepTimer >= StepInterval)
                {
                    _stepTimer -= StepInterval;
                    _scrollX += StepDistance;
                }
                while (_scrollX >= drawW)
                    _scrollX -= drawW;

                GUI.BeginGroup(squareRect);
                GUI.DrawTexture(new Rect(-_scrollX, 0f, drawW, drawH), worldMap, ScaleMode.StretchToFill);
                GUI.DrawTexture(new Rect(-_scrollX + drawW, 0f, drawW, drawH), worldMap, ScaleMode.StretchToFill);
                GUI.EndGroup();
            }

            if (DMSL_CustomUIAssets.MapBackground != null)
                GUI.DrawTexture(squareRect, DMSL_CustomUIAssets.MapBackground, ScaleMode.StretchToFill);
        }
    }
}
