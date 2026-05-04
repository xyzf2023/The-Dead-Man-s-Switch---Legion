// ============================================================================
// 文件：SystemOutputManager.cs
// 说明：SystemOutput核心组件，包含数据结构、管理器和初始化逻辑
// ============================================================================

using System;
using System.Collections.Generic;

namespace DMS_Legion
{
    #region 数据结构

    /// <summary>
    /// SystemOutput消息的最小数据结构
    /// 仅包含输出文本、消息来源和时间顺序信息
    /// </summary>
    public class SystemMessage
    {
        /// <summary>
        /// 消息唯一标识符（用于打字机效果）
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 输出文本内容
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// 消息来源标识符（如"ConstructionModule", "ResourceManager"等）
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// 消息创建时间戳，用于保持时间顺序
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="content">消息内容</param>
        /// <param name="source">消息来源</param>
        public SystemMessage(string content, string source)
        {
            Id = Guid.NewGuid().ToString(); // 生成唯一ID
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Source = source ?? "Unknown";
            Timestamp = DateTime.Now;
        }
    }

    #endregion

    #region 核心管理器

    /// <summary>
    /// 系统输出管理器
    /// 负责集中管理系统输出消息，维护消息列表和容量限制
    /// </summary>
    public class SystemOutputManager
    {
        // 单例实例
        private static SystemOutputManager? _instance;
        private static readonly object _lock = new object();

        // 消息列表（线程安全）
        private readonly List<SystemMessage> _messages = new List<SystemMessage>();

        // 配置参数
        private const int DefaultMaxMessages = 10;  // 存储更多消息，为按行显示预留空间

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static SystemOutputManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SystemOutputManager();
                    }
                }
                return _instance;
            }
        }

        // 私有构造函数，确保单例模式
        private SystemOutputManager() { }

        /// <summary>
        /// 添加新消息
        /// </summary>
        /// <param name="content">消息内容</param>
        /// <param name="source">消息来源</param>
        public void AddMessage(string content, string source)
        {
            if (string.IsNullOrWhiteSpace(content))
                return; // 忽略空消息

            lock (_messages)
            {
                // 创建新消息
                var message = new SystemMessage(content, source);

                // 添加到列表
                _messages.Add(message);

                // 检查容量限制，如果超出则移除最旧的消息
                if (_messages.Count > DefaultMaxMessages)
                {
                    _messages.RemoveAt(0); // 移除最旧的消息
                }
            }
        }

        /// <summary>
        /// 获取所有消息（只读访问）
        /// </summary>
        /// <returns>消息列表的副本</returns>
        public List<SystemMessage> GetAllMessages()
        {
            lock (_messages)
            {
                return new List<SystemMessage>(_messages); // 返回副本，确保外部无法修改
            }
        }

        /// <summary>
        /// 获取消息数量
        /// </summary>
        public int GetMessageCount()
        {
            lock (_messages)
            {
                return _messages.Count;
            }
        }

        /// <summary>
        /// 清空所有消息
        /// </summary>
        public void ClearAllMessages()
        {
            lock (_messages)
            {
                _messages.Clear();
            }
        }
    }

    #endregion

    #region 初始化器

    /// <summary>
    /// SystemOutput初始化器
    /// 负责设置事件监听器和其他初始化工作
    /// </summary>
    public static class SystemOutputInitializer
    {
        private static bool _initialized = false;

        /// <summary>
        /// 初始化SystemOutput系统
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            // 创建SystemOutput监听器并注册到事件总线
            var listener = new SystemOutputEventListener(SystemOutputManager.Instance);

            _initialized = true;
            Verse.Log.Message("[SystemOutput] 初始化完成");
        }
    }

    #endregion
}