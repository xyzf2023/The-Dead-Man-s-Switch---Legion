// ============================================================================
// 文件：UIEvents.cs
// 说明：UI事件定义，包含各种用户交互和系统状态变化事件
// ============================================================================

using System;

namespace DMS_Legion
{
    /// <summary>
    /// UI事件定义
    /// 每个事件代表一个具体的用户交互或系统状态变化
    /// </summary>
    public static class UIEvents
    {
        // 按钮点击事件
        public class ButtonClicked : IUIEvent
        {
            public string ButtonText { get; }
            public string WindowContext { get; }
            public string? AdditionalInfo { get; }

            public ButtonClicked(string buttonText, string? windowContext = null, string? additionalInfo = null)
            {
                ButtonText = buttonText;
                WindowContext = windowContext ?? "Unknown";
                AdditionalInfo = additionalInfo;
            }
        }

        // 操作开始事件
        public class OperationStarted : IUIEvent
        {
            public string OperationName { get; }
            public string ModuleName { get; }
            public System.Collections.Generic.Dictionary<string, object>? Parameters { get; }

            public OperationStarted(string operationName, string moduleName,
                                  System.Collections.Generic.Dictionary<string, object>? parameters = null)
            {
                OperationName = operationName;
                ModuleName = moduleName;
                Parameters = parameters ?? new System.Collections.Generic.Dictionary<string, object>();
            }
        }

        // 操作完成事件
        public class OperationCompleted : IUIEvent
        {
            public string OperationName { get; }
            public string ModuleName { get; }
            public TimeSpan Duration { get; }
            public object? Result { get; }

            public OperationCompleted(string operationName, string moduleName,
                                    TimeSpan duration, object? result = null)
            {
                OperationName = operationName;
                ModuleName = moduleName;
                Duration = duration;
                Result = result;
            }
        }

        // 操作失败事件
        public class OperationFailed : IUIEvent
        {
            public string OperationName { get; }
            public string ModuleName { get; }
            public string ErrorMessage { get; }
            public Exception? Exception { get; }

            public OperationFailed(string operationName, string moduleName,
                                 string errorMessage, Exception? exception = null)
            {
                OperationName = operationName;
                ModuleName = moduleName;
                ErrorMessage = errorMessage;
                Exception = exception;
            }
        }

        // 状态变化事件
        public class StatusChanged : IUIEvent
        {
            public string Component { get; }
            public string OldStatus { get; }
            public string NewStatus { get; }
            public string ModuleName { get; }

            public StatusChanged(string component, string oldStatus, string newStatus, string moduleName)
            {
                Component = component;
                OldStatus = oldStatus;
                NewStatus = newStatus;
                ModuleName = moduleName;
            }
        }

        // 资源变化事件
        public class ResourceChanged : IUIEvent
        {
            public string ResourceName { get; }
            public int ChangeAmount { get; }
            public int NewTotal { get; }
            public string ModuleName { get; }

            public ResourceChanged(string resourceName, int changeAmount, int newTotal, string moduleName)
            {
                ResourceName = resourceName;
                ChangeAmount = changeAmount;
                NewTotal = newTotal;
                ModuleName = moduleName;
            }
        }

        // 自定义消息事件（通用事件）
        public class CustomMessage : IUIEvent
        {
            public string Message { get; }
            public string Category { get; }
            public string ModuleName { get; }
            public LogLevel Level { get; }

            public CustomMessage(string message, string? category = null,
                               string? moduleName = null, LogLevel level = LogLevel.Info)
            {
                Message = message;
                Category = category ?? "General";
                ModuleName = moduleName ?? "Unknown";
                Level = level;
            }
        }
    }

    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Success
    }
}
