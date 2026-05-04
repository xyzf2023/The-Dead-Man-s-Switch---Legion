// ============================================================================
// 文件：SystemOutputText.cs
// 说明：SystemOutput文本系统，包含键值定义、内容映射和管理接口
// ============================================================================

using System.Collections.Generic;

namespace DMS_Legion
{
    #region 文本键值定义

    /// <summary>
    /// SystemOutput文本资源
    /// 所有系统输出文本的集中定义
    /// </summary>
    public static class SystemOutputText
    {
        // ================================
        // UI交互相关
        // ================================

        public static class UI
        {
            // 按钮点击
            public const string BUTTON_CLICKED = "UI.Button.Clicked";
            public const string SLOT_SELECTED = "UI.Slot.Selected";
            public const string WINDOW_OPENED = "UI.Window.Opened";
            public const string WINDOW_CLOSED = "UI.Window.Closed";

            // 操作提示
            public const string OPERATION_STARTED = "UI.Operation.Started";
            public const string OPERATION_COMPLETED = "UI.Operation.Completed";
            public const string OPERATION_FAILED = "UI.Operation.Failed";
            public const string OPERATION_CANCELLED = "UI.Operation.Cancelled";

            public const string SYSTEM_INFO = "UI.SystemInfo";
            public const string USER_SESSION = "UI.UserSession";
            public const string WELCOME_MESSAGE = "UI.WelcomeMessage";
        }

        // ================================
        // 建筑系统相关
        // ================================

        public static class Construction
        {
            // 建筑操作
            public const string BUILDING_PLACED = "Construction.Building.Placed";
            public const string BUILDING_DESTROYED = "Construction.Building.Destroyed";
            public const string BUILDING_UPGRADED = "Construction.Building.Upgraded";
            public const string BUILDING_REPAIRED = "Construction.Building.Repaired";

            // 槽位操作
            public const string SLOT_EMPTY_CLICKED = "Construction.Slot.EmptyClicked";
            public const string SLOT_OCCUPIED_CLICKED = "Construction.Slot.OccupiedClicked";
            public const string SLOT_BUILDING_ASSIGNED = "Construction.Slot.BuildingAssigned";
            public const string SLOT_BUILDING_REMOVED = "Construction.Slot.BuildingRemoved";

            // 建造状态
            public const string CONSTRUCTION_STARTED = "Construction.Process.Started";
            public const string CONSTRUCTION_COMPLETED = "Construction.Process.Completed";
            public const string CONSTRUCTION_FAILED = "Construction.Process.Failed";
            public const string CONSTRUCTION_CANCELLED = "Construction.Process.Cancelled";
        }
    }

    #endregion

    #region 文本内容映射

    /// <summary>
    /// SystemOutput文本内容映射
    /// 将键值映射到实际的显示文本
    /// </summary>
    public static class SystemOutputTextContent
    {
        // 文本映射字典
        private static readonly Dictionary<string, string> _textMap = new Dictionary<string, string>
        {
            // ================================
            // UI交互相关
            // ================================

            [SystemOutputText.UI.BUTTON_CLICKED] = "系统信息查询\n{0}\n操作已执行\n完成时间: {1}",
            [SystemOutputText.UI.SLOT_SELECTED] = "选中了 {0}",
            [SystemOutputText.UI.WINDOW_OPENED] = "单行消息测试",
            [SystemOutputText.UI.WINDOW_CLOSED] = "关闭了 {0} 窗口",

            [SystemOutputText.UI.OPERATION_STARTED] = "开始执行: {0}",
            [SystemOutputText.UI.OPERATION_COMPLETED] = "完成: {0} ({1})",
            [SystemOutputText.UI.OPERATION_FAILED] = "{0} 失败: {1}",
            [SystemOutputText.UI.OPERATION_CANCELLED] = "{0} 已取消",

            [SystemOutputText.UI.SYSTEM_INFO] = "系统信息\n{0}\n查询时间: {1}",
            [SystemOutputText.UI.USER_SESSION] = "用户会话\n{0}\n登录时间: {1}",
            [SystemOutputText.UI.WELCOME_MESSAGE] = ">您好，{0}。\n 当前时间 {1} {2}，{3}。\n 请下达指令。",

            // ================================
            // 建筑系统相关
            // ================================

            [SystemOutputText.Construction.BUILDING_PLACED] = "建造完成: {0} (位置: {1})",
            [SystemOutputText.Construction.BUILDING_DESTROYED] = "拆除完成: {0}",
            [SystemOutputText.Construction.BUILDING_UPGRADED] = "升级完成: {0} → {1}",
            [SystemOutputText.Construction.BUILDING_REPAIRED] = "修复完成: {0} ({1} HP)",

            [SystemOutputText.Construction.SLOT_EMPTY_CLICKED] = "点击了空建筑槽位 {0}",
            [SystemOutputText.Construction.SLOT_OCCUPIED_CLICKED] = "点击了建筑槽位 {0} ({1})",
            [SystemOutputText.Construction.SLOT_BUILDING_ASSIGNED] = "已分配建筑: {0} → 槽位 {1}",
            [SystemOutputText.Construction.SLOT_BUILDING_REMOVED] = "已移除建筑: {0} 从槽位 {1}",

            [SystemOutputText.Construction.CONSTRUCTION_STARTED] = "开始建造: {0}",
            [SystemOutputText.Construction.CONSTRUCTION_COMPLETED] = "建造完成: {0} ({1})",
            [SystemOutputText.Construction.CONSTRUCTION_FAILED] = "建造失败: {0} - {1}",
            [SystemOutputText.Construction.CONSTRUCTION_CANCELLED] = "建造取消: {0}",
        };

        /// <summary>
        /// 获取文本内容
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            if (_textMap.TryGetValue(key, out string template))
            {
                try
                {
                    return args.Length > 0 ? string.Format(template, args) : template;
                }
                catch (System.FormatException)
                {
                    // 参数不匹配，返回模板本身并记录错误
                    Verse.Log.Warning($"SystemOutput文本格式化失败: {key}");
                    return template;
                }
            }

            // 键不存在，返回键名本身并记录警告
            Verse.Log.Warning($"SystemOutput文本键不存在: {key}");
            return $"[{key}]";
        }

        /// <summary>
        /// 检查文本键是否存在
        /// </summary>
        public static bool ContainsKey(string key)
        {
            return _textMap.ContainsKey(key);
        }

        /// <summary>
        /// 获取所有文本键（用于调试和验证）
        /// </summary>
        public static IEnumerable<string> GetAllKeys()
        {
            return _textMap.Keys;
        }
    }

    #endregion


    /// <summary>
    /// SystemOutput文本管理器
    /// 提供文本获取和管理的统一接口
    /// </summary>
    public static class SystemOutputTextManager
    {
        /// <summary>
        /// 获取格式化的文本
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            return SystemOutputTextContent.Get(key, args);
        }

        /// <summary>
        /// 获取UI相关的文本
        /// </summary>
        public static class UI
        {
            public static string ButtonClicked(string buttonText)
                => Get(SystemOutputText.UI.BUTTON_CLICKED,
                       DMS_Legion.SystemOutputInfoProvider.GetUserInfo(),
                       DMS_Legion.SystemOutputInfoProvider.GetCurrentRealTime());

            public static string SlotSelected(string slotInfo)
                => Get(SystemOutputText.UI.SLOT_SELECTED, slotInfo);

            public static string OperationStarted(string operationName)
                => Get(SystemOutputText.UI.OPERATION_STARTED, operationName);

            public static string OperationCompleted(string operationName, string duration)
                => Get(SystemOutputText.UI.OPERATION_COMPLETED, operationName, duration);

            public static string OperationFailed(string operationName, string reason)
                => Get(SystemOutputText.UI.OPERATION_FAILED, operationName, reason);

            public static string SystemInfo()
                => Get(SystemOutputText.UI.SYSTEM_INFO,
                       DMS_Legion.SystemOutputInfoProvider.GetUserInfo(),
                       DMS_Legion.SystemOutputInfoProvider.GetCurrentRealTime());

            public static string UserSession()
                => Get(SystemOutputText.UI.USER_SESSION,
                       DMS_Legion.SystemOutputInfoProvider.GetSystemInfoSummary(),
                       DMS_Legion.SystemOutputInfoProvider.GetCurrentRealDate());

            public static string WelcomeMessage()
                => Get(SystemOutputText.UI.WELCOME_MESSAGE,
                       DMS_Legion.SystemOutputInfoProvider.GetDisplayUsername(),
                       DMS_Legion.SystemOutputInfoProvider.GetFormattedDateForWelcome(),
                       DMS_Legion.SystemOutputInfoProvider.GetFormattedTimeForWelcome(),
                       DMS_Legion.SystemOutputInfoProvider.GetTimeBasedGreeting());
        }

        /// <summary>
        /// 获取建筑相关的文本
        /// </summary>
        public static class Construction
        {
            public static string BuildingPlaced(string buildingName, string position)
                => Get(SystemOutputText.Construction.BUILDING_PLACED, buildingName, position);

            public static string SlotEmptyClicked(int slotIndex)
                => Get(SystemOutputText.Construction.SLOT_EMPTY_CLICKED, slotIndex);

            public static string SlotOccupiedClicked(int slotIndex, string buildingName)
                => Get(SystemOutputText.Construction.SLOT_OCCUPIED_CLICKED, slotIndex, buildingName);

            public static string ConstructionStarted(string buildingName)
                => Get(SystemOutputText.Construction.CONSTRUCTION_STARTED, buildingName);

            public static string ConstructionCompleted(string buildingName, string duration)
                => Get(SystemOutputText.Construction.CONSTRUCTION_COMPLETED, buildingName, duration);
        }
    }
}