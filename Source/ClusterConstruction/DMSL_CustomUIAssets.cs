// ============================================================================
// 文件：DMSL_CustomUIAssets.cs
// 说明：DMS Legion自定义UI资源加载类
// 功能：在游戏启动时加载自定义UI贴图资源
// ============================================================================

using UnityEngine;
using Verse;

namespace DMS_Legion
{
    /// <summary>
    /// 自定义UI资源加载类
    /// 在游戏启动时静态初始化所有自定义UI贴图
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DMSL_CustomUIAssets
    {
        // ===== 背景贴图 =====
        public static readonly Texture2D? MainWindowBackground; // 主窗口整体背景
        public static readonly Texture2D? PanelBackground;     // 左侧面板背景
        public static readonly Texture2D? BuildingSlotBackground; // 建筑槽位窗口背景
        public static readonly Texture2D? ResourcePanelBackground; // 资源储备面板背景
        public static readonly Texture2D? TaskQueuePanelBackground; // 任务队列面板背景
        public static readonly Texture2D? TaskQueueTitleBackground; // 任务队列标题栏背景
        public static readonly Texture2D? SystemOutputPanelBackground; // 系统输出面板背景
        public static readonly Texture2D? SystemOutputTitleBackground; // 系统输出标题栏背景
        public static readonly Texture2D? MapBackground; // 核打击窗口右下角地图/背景贴图

        // ===== 按钮相关 =====
        public static readonly Texture2D? CloseButton;         // 关闭窗口按钮
        public static readonly Texture2D? BuildingSlotButton;  // 建筑槽位按钮
        public static readonly Texture2D? Button;              // 通用按钮（功能按钮使用）

        // ===== 按钮图标 =====
        public static readonly Texture2D? ProductionIcon;      // 生产规划图标
        public static readonly Texture2D? TacticalIcon;        // 战术支援图标
        public static readonly Texture2D? MechanoidIcon;       // 机械体管理图标
        public static readonly Texture2D? StrategicIcon;       // 战略部署图标
        public static readonly Texture2D? TitleIcon;           // 标题图标
        public static readonly Texture2D? TaskListTitleIcon;   // 任务列表标题图标

        /// <summary>
        /// 静态构造函数
        /// 在游戏启动时加载所有自定义UI贴图
        /// </summary>
        static DMSL_CustomUIAssets()
        {
            // ===== 加载背景贴图 =====
            MainWindowBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/MainWindowBackground", false);
            PanelBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/PanelBackground", false);
            BuildingSlotBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/BuildingSlotBackground", false);  // jpg文件
            ResourcePanelBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/ResourcePanelBackground", false);  // 占位符 - 文件不存在
            TaskQueuePanelBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/TaskQueuePanelBackground", false);
            TaskQueueTitleBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/TaskQueueTitleBackground", false);
            SystemOutputPanelBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/SystemOutputPanelBackground", false);
            SystemOutputTitleBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/SystemOutputTitleBackground", false);
            MapBackground = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Panels/MapBackground", false);

            // ===== 加载按钮相关 =====
            CloseButton = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/CloseButton", false);
            BuildingSlotButton = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/BuildingSlotButton", false);
            Button = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Buttons/Button", false);

            // ===== 加载按钮图标 =====
            ProductionIcon = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Icons/ProductionIcon", false);
            TacticalIcon = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Icons/TacticalIcon", false);
            MechanoidIcon = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Icons/MechanoidIcon", false);
            StrategicIcon = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Icons/StrategicIcon", false);
            TitleIcon = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Icons/TitleIcon", false);
            TaskListTitleIcon = ContentFinder<Texture2D>.Get("UI/ClusterConstruction/Icons/TaskListTitleIcon", false);
        }

        /// <summary>
        /// 检查自定义UI资源是否完全加载
        /// </summary>
        /// <returns>如果所有必要资源都已加载返回true</returns>
        public static bool AreCustomUIAssetsLoaded()
        {
            // 检查关键资源是否加载成功
            return PanelBackground != null &&
                   BuildingSlotBackground != null;
        }

    }
}
