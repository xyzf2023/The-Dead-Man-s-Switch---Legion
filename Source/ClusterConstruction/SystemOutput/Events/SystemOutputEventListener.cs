// ============================================================================
// 文件：SystemOutputEventListener.cs
// 说明：SystemOutput事件监听器，将UI事件转换为SystemOutput消息
// ============================================================================

namespace DMS_Legion
{
    /// <summary>
    /// SystemOutput事件监听器
    /// 将UI事件转换为SystemOutput消息
    /// </summary>
    public class SystemOutputEventListener
    {
        protected readonly SystemOutputManager _outputManager;

    public SystemOutputEventListener(SystemOutputManager outputManager)
    {
        _outputManager = outputManager;
        RegisterEventHandlers();
    }

    /// <summary>
    /// 添加消息的辅助方法
    /// </summary>
    protected void AddMessage(string message, string source)
    {
        // 添加消息（打字机系统已删除）
        var systemMessage = new SystemMessage(message, source);
        _outputManager.AddMessage(systemMessage.Content, systemMessage.Source);
    }

        /// <summary>
        /// 注册所有事件处理器
        /// </summary>
        protected virtual void RegisterEventHandlers()
        {
            // 按钮点击事件
            UIEventBus.Subscribe<UIEvents.ButtonClicked>(HandleButtonClicked);

            // 操作事件
            UIEventBus.Subscribe<UIEvents.OperationStarted>(HandleOperationStarted);
            UIEventBus.Subscribe<UIEvents.OperationCompleted>(HandleOperationCompleted);
            UIEventBus.Subscribe<UIEvents.OperationFailed>(HandleOperationFailed);

            // 状态变化事件
            UIEventBus.Subscribe<UIEvents.StatusChanged>(HandleStatusChanged);

            // 资源变化事件
            UIEventBus.Subscribe<UIEvents.ResourceChanged>(HandleResourceChanged);

            // 自定义消息事件
            UIEventBus.Subscribe<UIEvents.CustomMessage>(HandleCustomMessage);
        }

    protected virtual void HandleButtonClicked(UIEvents.ButtonClicked e)
    {
        string message = SystemOutputTextManager.UI.ButtonClicked(e.ButtonText);

        // 添加消息（打字机系统已删除）
        var systemMessage = new SystemMessage(message, e.WindowContext);
        _outputManager.AddMessage(systemMessage.Content, systemMessage.Source);
    }

    protected virtual void HandleOperationStarted(UIEvents.OperationStarted e)
    {
        string message = SystemOutputTextManager.UI.OperationStarted(e.OperationName);
        AddMessage(message, e.ModuleName);
    }

    protected virtual void HandleOperationCompleted(UIEvents.OperationCompleted e)
    {
        string durationText = e.Duration.TotalSeconds > 1
            ? $"{e.Duration.TotalSeconds:F1}s"
            : $"{e.Duration.TotalMilliseconds:F0}ms";

        string message = SystemOutputTextManager.UI.OperationCompleted(e.OperationName, durationText);
        AddMessage(message, e.ModuleName);
    }

    protected virtual void HandleOperationFailed(UIEvents.OperationFailed e)
    {
        string message = SystemOutputTextManager.UI.OperationFailed(e.OperationName, e.ErrorMessage);
        AddMessage(message, e.ModuleName);
    }

        protected virtual void HandleStatusChanged(UIEvents.StatusChanged e)
        {
            string message = $"{e.Component} 状态变更: {e.OldStatus} → {e.NewStatus}";
            _outputManager.AddMessage(message, e.ModuleName);
        }

        protected virtual void HandleResourceChanged(UIEvents.ResourceChanged e)
        {
            string direction = e.ChangeAmount > 0 ? "获得" : "消耗";
            string amountText = System.Math.Abs(e.ChangeAmount).ToString();
            string message = $"{direction} {e.ResourceName}: {amountText} (总计: {e.NewTotal})";
            _outputManager.AddMessage(message, e.ModuleName);
        }

        protected virtual void HandleCustomMessage(UIEvents.CustomMessage e)
        {
            string prefix = e.Level switch
            {
                LogLevel.Debug => "[DEBUG]",
                LogLevel.Info => "",
                LogLevel.Warning => "⚠️",
                LogLevel.Error => "❌",
                LogLevel.Success => "✅",
                _ => ""
            };

            string message = string.IsNullOrEmpty(prefix) ? e.Message : $"{prefix} {e.Message}";
            _outputManager.AddMessage(message, e.ModuleName);
        }
    }
}
