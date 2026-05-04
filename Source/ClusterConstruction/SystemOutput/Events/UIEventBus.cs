// ============================================================================
// 文件：UIEventBus.cs
// 说明：轻量级事件总线，负责事件发布和订阅管理
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace DMS_Legion
{
    /// <summary>
    /// UI事件接口
    /// </summary>
    public interface IUIEvent { }

    /// <summary>
    /// 轻量级事件总线
    /// 负责事件发布和订阅管理
    /// </summary>
    public static class UIEventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        /// <summary>
        /// 发布事件
        /// </summary>
        public static void Publish<T>(T eventData) where T : IUIEvent
        {
            var eventType = typeof(T);
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers.ToList()) // 创建副本避免并发修改
                {
                    try
                    {
                        ((Action<T>)handler)(eventData);
                    }
                    catch (Exception e)
                    {
                        Verse.Log.Error($"UIEventBus: 事件处理失败 {eventType.Name}: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : IUIEvent
        {
            var eventType = typeof(T);
            if (!_handlers.ContainsKey(eventType))
            {
                _handlers[eventType] = new List<Delegate>();
            }
            _handlers[eventType].Add(handler);
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : IUIEvent
        {
            var eventType = typeof(T);
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }
}
