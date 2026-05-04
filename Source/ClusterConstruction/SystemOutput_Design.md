# SystemOutput 系统设计文档

## 概述

SystemOutput 是为 RimWorld MOD "The Dead Man's Switch - Legion" 设计的系统文本输出通道，专门用于在 ModularOperationWindow 的命令窗口显示系统反馈信息。

## 设计目标

- **非阻断式**：不影响游戏主循环性能
- **面向玩家**：消息内容适合玩家阅读
- **模块化**：独立部署，不与游戏核心耦合
- **可扩展**：支持新功能和消息类型的添加

## 职责边界

### ✅ 核心职责
- **消息接收**：接收来自其他系统的文本消息输入
- **消息存储**：维护消息的历史记录和队列
- **读取接口**：为UI层提供访问消息数据的接口

### ❌ 排除职责
- UI绘制和渲染逻辑
- 游戏核心逻辑处理
- 弹窗或异常处理机制
- 消息格式化或本地化
- 持久化存储（存档相关）

## 核心组件

### 0. SystemMessage 最小数据结构（已实现）

### 0.5. SystemOutputManager 管理器类（已实现）

**设计目标**：最简洁的消息表示，只包含必要字段

```csharp
/// <summary>
/// 系统输出消息的最小数据结构
/// 仅包含输出文本、消息来源和时间顺序信息
/// </summary>
public class SystemMessage
{
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
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Source = source ?? "Unknown"; // 默认来源
        Timestamp = DateTime.Now;
    }
}
```

**设计原则**：
- **最小化**：只包含用户要求的三个必要字段
- **不可变性**：所有属性只读，确保数据一致性
- **安全性**：参数验证，防止null值
- **简洁性**：单一职责，专注于数据承载

### 0.5. SystemOutputManager 管理器类（已实现）

**设计目标**：集中管理系统输出消息，提供添加和维护功能

```csharp
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
    private const int DefaultMaxMessages = 100;  // 默认最大消息数量

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
```

**核心特性**：
- **单例模式**：提供全局唯一实例
- **线程安全**：使用lock保护并发访问
- **容量管理**：自动维护最大消息数量限制
- **数据保护**：GetAllMessages返回副本，防止外部修改
- **轻量级**：专注于消息管理和存储

**使用示例**：
```csharp
// 添加消息
SystemOutputManager.Instance.AddMessage("建筑建造完成", "ConstructionModule");
SystemOutputManager.Instance.AddMessage("资源不足警告", "ResourceManager");

// 获取消息
var messages = SystemOutputManager.Instance.GetAllMessages();
var count = SystemOutputManager.Instance.GetMessageCount();
```

## 存储和访问机制详解

### 消息存储策略

#### 内部存储结构
```csharp
// 内部使用List<SystemMessage>存储消息
private readonly List<SystemMessage> _messages = new List<SystemMessage>();
```

**存储特性**：
- **内存存储**：所有消息存储在内存中，游戏重启时清空
- **先进先出**：新消息添加到列表末尾，超出容量时移除最旧消息（索引0）
- **线程安全**：所有操作使用`lock(_messages)`保护
- **容量限制**：默认最多存储100条消息

#### 容量管理机制
```csharp
// 添加消息时的容量检查
if (_messages.Count > DefaultMaxMessages)
{
    _messages.RemoveAt(0); // 移除最旧的消息
}
```

### UI层只读访问机制

#### 数据保护策略
**核心原则**：UI层只能读取数据，不能修改内部存储

```csharp
/// <summary>
/// 获取所有消息（只读访问）
/// 返回副本确保数据安全
/// </summary>
public List<SystemMessage> GetAllMessages()
{
    lock (_messages)
    {
        // 创建副本，防止外部修改
        return new List<SystemMessage>(_messages);
    }
}
```

#### UI层访问模式

**推荐的访问方式**：
```csharp
// 在UI绘制时获取消息
void DrawSystemOutput()
{
    // 1. 获取消息副本（线程安全，只读）
    var messages = SystemOutputManager.Instance.GetAllMessages();

    // 2. 在UI线程中安全使用（无需担心并发修改）
    foreach (var message in messages)
    {
        // 渲染消息内容
        DrawMessage(message.Content, message.Source, message.Timestamp);
    }
}
```

#### 性能考虑

**当前实现的优势**：
- ✅ **绝对安全**：副本机制确保内部数据不会被意外修改
- ✅ **线程安全**：UI线程和添加线程可以并发访问
- ✅ **简单可靠**：实现简单，bug风险低

**潜在性能影响**：
- ⚠️ **内存开销**：每次调用`GetAllMessages()`都创建完整列表副本
- ⚠️ **GC压力**：频繁UI刷新时可能产生较多临时对象

#### 优化建议

**对于高频UI刷新场景**，可以考虑以下优化：

```csharp
// 方案1：缓存机制（UI层维护缓存）
private List<SystemMessage> _cachedMessages;
private float _lastCacheTime;

void UpdateCacheIfNeeded()
{
    if (Time.time - _lastCacheTime > 0.5f) // 每500ms更新一次
    {
        _cachedMessages = SystemOutputManager.Instance.GetAllMessages();
        _lastCacheTime = Time.time;
    }
}

// 方案2：增量更新接口（未来扩展）
public IEnumerable<SystemMessage> GetMessagesSince(DateTime since)
{
    // 只返回指定时间之后的消息
}
```

#### 安全保证

**数据一致性**：
- SystemMessage是不可变对象（所有属性只读）
- UI层无法修改消息内容或时间戳
- 消息顺序由时间戳保证，不依赖列表顺序

**并发安全**：
- 读写操作都使用相同的锁对象
- UI读取时不会阻塞消息添加
- 消息添加时不会影响正在进行的UI渲染

## UI层集成指南

### 在ModularOperationWindow中集成SystemOutput

#### 1. 修改DrawCommandWindow方法

在 `DrawCommandWindow` 方法中添加消息显示逻辑：

```csharp
/// <summary>
/// 绘制命令窗口（SystemOutput显示区域）
/// </summary>
private void DrawCommandWindow(Rect rect)
{
    // ... 现有的背景和标题绘制代码 ...

    // 添加消息显示区域
    Rect contentRect = new Rect(
        rect.x + 5f,
        titleRect.yMax + 5f,
        rect.width - 10f,
        rect.height - titleHeight - 10f
    );

    // 绘制系统输出消息
    DrawSystemMessages(contentRect);
}
```

#### 2. 实现DrawSystemMessages方法

```csharp
/// <summary>
/// 绘制系统消息列表
/// UI层只读访问消息数据，不拥有或修改任何消息
/// </summary>
private void DrawSystemMessages(Rect rect)
{
    // 📖 只读访问：获取消息副本，不修改任何数据
    var messages = SystemOutputManager.Instance.GetAllMessages();

    if (messages.Count == 0)
    {
        // 无消息时的占位显示
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        DrawCustomLabel(rect, "暂无系统消息", TextAnchor.MiddleCenter);
        Text.Anchor = TextAnchor.UpperLeft;
        return;
    }

    // 消息显示参数
    float lineHeight = 20f;
    float scrollBarWidth = 16f;

    // 滚动视图设置
    Rect viewRect = new Rect(0f, 0f, rect.width - scrollBarWidth, messages.Count * lineHeight);
    Rect scrollRect = new Rect(rect.x, rect.y, rect.width, rect.height);

    // 初始化滚动位置（类级别字段）
    if (_systemOutputScrollPosition == Vector2.zero)
    {
        _systemOutputScrollPosition = Vector2.zero;
    }

    Widgets.BeginScrollView(scrollRect, ref _systemOutputScrollPosition, viewRect);

    // 绘制消息列表
    float currentY = 0f;
    foreach (var message in messages)
    {
        Rect messageRect = new Rect(0f, currentY, viewRect.width, lineHeight);

        // 📖 只读使用：传递消息引用，但不修改
        DrawSingleSystemMessage(messageRect, message);

        currentY += lineHeight;
    }

    Widgets.EndScrollView();
}
```

#### 3. 实现DrawSingleSystemMessage方法

```csharp
/// <summary>
/// 绘制单条系统消息
/// 严格只读访问消息属性，不修改任何数据
/// </summary>
private void DrawSingleSystemMessage(Rect rect, SystemMessage message)
{
    // 📖 只读访问时间戳
    string timeStr = message.Timestamp.ToString("HH:mm:ss");
    Rect timeRect = new Rect(rect.x, rect.y, 60f, rect.height);
    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.MiddleLeft;
    DrawCustomLabel(timeRect, timeStr, TextAnchor.MiddleLeft);

    // 📖 只读访问来源
    Rect sourceRect = new Rect(timeRect.xMax + 5f, rect.y, 80f, rect.height);
    DrawCustomLabel(sourceRect, $"[{message.Source}]", TextAnchor.MiddleLeft);

    // 📖 只读访问内容
    Rect contentRect = new Rect(sourceRect.xMax + 5f, rect.y, rect.width - sourceRect.xMax - 5f, rect.height);
    Text.Font = GameFont.Small;
    DrawCustomLabel(contentRect, message.Content, TextAnchor.MiddleLeft);

    Text.Anchor = TextAnchor.UpperLeft;
}
```

#### 4. 添加必要的类级别字段

在ModularOperationWindow类中添加：

```csharp
// ================================
// SystemOutput UI 相关字段
// ================================

// SystemOutput滚动位置
private Vector2 _systemOutputScrollPosition = Vector2.zero;

// SystemOutput缓存（可选，用于性能优化）
private List<SystemMessage> _cachedMessages;
private float _lastCacheUpdateTime;
```

#### 5. 在DoWindowContents中初始化缓存

```csharp
public override void DoWindowContents(Rect inRect)
{
    // ... 现有代码 ...

    // 初始化SystemOutput缓存
    InitializeSystemOutputCache();

    // ... 其余代码 ...
}

/// <summary>
/// 初始化SystemOutput缓存
/// </summary>
private void InitializeSystemOutputCache()
{
    // 每秒更新一次缓存，避免频繁调用GetAllMessages()
    if (Time.time - _lastCacheUpdateTime > 1.0f)
    {
        _cachedMessages = SystemOutputManager.Instance.GetAllMessages();
        _lastCacheUpdateTime = Time.time;
    }
}
```

#### 6. 修改DrawSystemMessages使用缓存

```csharp
private void DrawSystemMessages(Rect rect)
{
    // 使用缓存的消息列表（已通过InitializeSystemOutputCache初始化）
    var messages = _cachedMessages ?? new List<SystemMessage>();

    // ... 其余代码不变 ...
}
```

### UI层职责边界

#### ✅ 允许的操作
- 📖 **读取消息数据**：通过`SystemOutputManager.Instance.GetAllMessages()`获取副本
- 🎨 **渲染显示**：将消息内容绘制到UI上
- 📜 **滚动控制**：管理消息列表的滚动位置
- 🎛️ **布局控制**：控制消息的显示格式和布局

#### ❌ 禁止的操作
- ✏️ **修改消息**：不能修改SystemMessage的任何属性
- 🗑️ **删除消息**：不能从SystemOutputManager中移除消息
- ➕ **添加消息**：不能直接添加消息（应通过其他系统调用SystemOutputManager）
- 🏗️ **重建消息**：不能创建新的SystemMessage实例

### 数据流向

```
其他系统 → SystemOutputManager.AddMessage() → 存储消息
                              ↓
UI层 ← SystemOutputManager.GetAllMessages() ← 只读副本
                              ↓
渲染显示（时间戳 + 来源 + 内容）
```

### 性能优化建议

#### 缓存策略
```csharp
// 在类级别添加缓存
private List<SystemMessage> _cachedMessages;
private float _lastCacheUpdateTime;

// 在DoWindowContents开始处更新缓存
if (Time.time - _lastCacheUpdateTime > 1.0f) // 每秒更新一次
{
    _cachedMessages = SystemOutputManager.Instance.GetAllMessages();
    _lastCacheUpdateTime = Time.time;
}
```

#### 增量更新（未来扩展）
```csharp
// 只获取新消息（需要SystemOutputManager提供新接口）
var newMessages = SystemOutputManager.Instance.GetMessagesSince(_lastMessageTime);
```

### 错误处理

#### 优雅降级
```csharp
try
{
    var messages = SystemOutputManager.Instance.GetAllMessages();
    // 正常渲染
}
catch (Exception e)
{
    // 降级显示：显示错误提示
    DrawCustomLabel(rect, "系统输出暂时不可用", TextAnchor.MiddleCenter);
    Log.Error($"SystemOutput渲染错误: {e.Message}");
}
```

### 1. SystemMessage（消息实体）

```csharp
public class SystemMessage
{
    public string Content { get; }          // 消息内容
    public DateTime Timestamp { get; }     // 创建时间
    public MessageType Type { get; }       // 消息类型
    public string Source { get; }          // 来源模块标识
    public Dictionary<string, object> Metadata { get; }  // 扩展数据
}
```

**MessageType 枚举**：
- `Info`：普通信息
- `Warning`：警告
- `Error`：错误
- `Success`：成功操作反馈
- `System`：系统状态变更

### 2. MessageQueue（消息队列）

**核心功能**：
- `AddMessage(SystemMessage message)`：添加消息
- `GetMessages()`：获取所有消息
- `GetMessagesSince(DateTime time)`：获取指定时间后的消息
- `ClearOldMessages(TimeSpan maxAge)`：清理过期消息
- `TrimToCapacity(int maxMessages)`：限制队列大小

**存储策略**：
- 内存队列，游戏重启时清空
- 容量限制（默认1000条）
- 时间限制（默认24小时）
- 线程安全保护

### 3. SystemOutputManager（核心管理器）

**单例模式**，提供全局访问点

**消息接收接口**：
- `LogInfo(string content, string source = null)`
- `LogWarning(string content, string source = null)`
- `LogError(string content, string source = null)`
- `LogSuccess(string content, string source = null)`
- `LogSystem(string content, string source = null)`

### 4. IMessageReader（读取接口）

**基本查询**：
- `GetAllMessages()`：获取所有消息
- `GetMessagesByType(MessageType type)`：按类型获取
- `GetRecentMessages(int count)`：获取最新消息

**高级查询**：
- `GetMessagesInRange(DateTime start, DateTime end)`：时间范围查询
- `GetMessagesBySource(string source)`：按来源查询

**统计信息**：
- `GetTotalCount()`：总消息数
- `GetCountByType(MessageType type)`：按类型统计
- `GetLastMessageTime()`：最后消息时间

## 扩展性设计

### 插件架构

**IMessageProcessor 接口**：
```csharp
public interface IMessageProcessor
{
    void ProcessMessage(SystemMessage message);
}
```

**IMessageFilter 接口**：
```csharp
public interface IMessageFilter
{
    bool ShouldInclude(SystemMessage message);
}
```

### 配置系统

```csharp
public class SystemOutputConfig
{
    public int MaxMessages { get; set; } = 1000;
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(24);
    public bool EnableDebugLogging { get; set; } = false;
}
```

## 架构优势

### 模块化优势
- **独立部署**：可单独启用/禁用
- **职责分离**：不与UI或游戏逻辑耦合
- **易于测试**：组件独立测试

### 可扩展性优势
- **新消息类型**：扩展MessageType枚举
- **自定义处理器**：插件化处理逻辑
- **过滤机制**：UI层灵活过滤

### 性能优势
- **轻量级操作**：O(1)或O(log n)复杂度
- **内存控制**：自动清理机制
- **非阻断式**：不影响游戏帧率

## 使用场景

### 典型集成流程
1. **其他系统调用**：
   ```csharp
   SystemOutputManager.Instance.LogSuccess("建筑建造完成", "ConstructionModule");
   ```

2. **UI层读取**：
   ```csharp
   var reader = SystemOutputManager.Instance.GetReader();
   var recentMessages = reader.GetRecentMessages(50);
   ```

## 解耦的调用结构设计

### 设计理念

为了避免主UI文件与SystemOutputManager产生强耦合，并避免为每一句文本单独创建接口方法，我们采用**事件驱动架构**：

1. **UI层发布事件**：UI只负责发布语义明确的事件，不关心谁在监听
2. **监听器处理记录**：SystemOutput作为事件监听器，自动将事件转换为消息
3. **松耦合设计**：UI层不直接依赖SystemOutputManager
4. **可扩展性**：可轻松添加其他监听器（如分析系统、调试工具等）

### 核心组件

#### 1. 事件总线（EventBus）

```csharp
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
                    Log.Error($"UIEventBus: 事件处理失败 {eventType.Name}: {e.Message}");
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

/// <summary>
/// UI事件接口
/// </summary>
public interface IUIEvent { }
```

#### 2. 语义化事件定义

```csharp
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
        public string AdditionalInfo { get; }

        public ButtonClicked(string buttonText, string windowContext = null, string additionalInfo = null)
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
        public Dictionary<string, object> Parameters { get; }

        public OperationStarted(string operationName, string moduleName, Dictionary<string, object> parameters = null)
        {
            OperationName = operationName;
            ModuleName = moduleName;
            Parameters = parameters ?? new Dictionary<string, object>();
        }
    }

    // 操作完成事件
    public class OperationCompleted : IUIEvent
    {
        public string OperationName { get; }
        public string ModuleName { get; }
        public TimeSpan Duration { get; }
        public object Result { get; }

        public OperationCompleted(string operationName, string moduleName, TimeSpan duration, object result = null)
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
        public Exception Exception { get; }

        public OperationFailed(string operationName, string moduleName, string errorMessage, Exception exception = null)
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

        public CustomMessage(string message, string category = null, string moduleName = null, LogLevel level = LogLevel.Info)
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
```

#### 3. SystemOutput事件监听器

```csharp
/// <summary>
/// SystemOutput事件监听器
/// 将UI事件转换为SystemOutput消息
/// </summary>
public class SystemOutputEventListener
{
    private readonly SystemOutputManager _outputManager;

    public SystemOutputEventListener(SystemOutputManager outputManager)
    {
        _outputManager = outputManager;
        RegisterEventHandlers();
    }

    /// <summary>
    /// 注册所有事件处理器
    /// </summary>
    private void RegisterEventHandlers()
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

    private void HandleButtonClicked(UIEvents.ButtonClicked e)
    {
        string message = e.AdditionalInfo != null
            ? $"点击了 '{e.ButtonText}' 按钮 ({e.AdditionalInfo})"
            : $"点击了 '{e.ButtonText}' 按钮";

        _outputManager.AddMessage(message, e.WindowContext);
    }

    private void HandleOperationStarted(UIEvents.OperationStarted e)
    {
        string message = $"开始执行: {e.OperationName}";
        _outputManager.AddMessage(message, e.ModuleName);
    }

    private void HandleOperationCompleted(UIEvents.OperationCompleted e)
    {
        string durationText = e.Duration.TotalSeconds > 1
            ? $"{e.Duration.TotalSeconds:F1}s"
            : $"{e.Duration.TotalMilliseconds:F0}ms";

        string message = $"完成: {e.OperationName} ({durationText})";
        _outputManager.AddMessage(message, e.ModuleName);
    }

    private void HandleOperationFailed(UIEvents.OperationFailed e)
    {
        string message = $"{e.OperationName} 失败: {e.ErrorMessage}";
        _outputManager.AddMessage(message, e.ModuleName);
    }

    private void HandleStatusChanged(UIEvents.StatusChanged e)
    {
        string message = $"{e.Component} 状态变更: {e.OldStatus} → {e.NewStatus}";
        _outputManager.AddMessage(message, e.ModuleName);
    }

    private void HandleResourceChanged(UIEvents.ResourceChanged e)
    {
        string direction = e.ChangeAmount > 0 ? "获得" : "消耗";
        string amountText = Mathf.Abs(e.ChangeAmount).ToString();
        string message = $"{direction} {e.ResourceName}: {amountText} (总计: {e.NewTotal})";
        _outputManager.AddMessage(message, e.ModuleName);
    }

    private void HandleCustomMessage(UIEvents.CustomMessage e)
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
```

#### 4. 系统初始化

```csharp
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
        Log.Message("[SystemOutput] 初始化完成");
    }
}
```

### 在ModularOperationWindow中的使用

#### 修改后的UI代码（完全解耦）

```csharp
public class ModularOperationWindow : Window
{
    // ================================
    // 不再需要 SystemOutput 相关字段
    // ================================

    /// <summary>
    /// 建筑按钮点击处理（完全解耦）
    /// </summary>
    private bool DrawBuildingButton(Rect rect, BuildingSlot slot, bool isSelected)
    {
        // ... 现有绘制代码 ...

        if (Widgets.ButtonInvisible(rect))
        {
            if (slot.isEmpty)
            {
                // 🎯 只发布事件，不关心谁在监听
                UIEventBus.Publish(new UIEvents.ButtonClicked(
                    $"空建筑槽位 {slot.slotIndex + 1}",
                    "ConstructionUI"
                ));

                selectedSlotIndex = slot.slotIndex;
            }
            else
            {
                // 🎯 发布选中事件
                UIEventBus.Publish(new UIEvents.ButtonClicked(
                    $"建筑槽位 {slot.slotIndex + 1}",
                    "ConstructionUI",
                    "选中操作"
                ));

                selectedBuildingIndex = slotIndex;
            }
        }

        return clicked;
    }

    /// <summary>
    /// 功能按钮点击处理（完全解耦）
    /// </summary>
    private bool DrawCustomButton(Rect rect, string label, string buttonType = "default", bool showIcon = false, bool fillMode = false)
    {
        // ... 现有绘制代码 ...

        if (clicked)
        {
            // 🎯 发布按钮点击事件
            UIEventBus.Publish(new UIEvents.ButtonClicked(label, "ConstructionUI"));

            // 🎯 根据按钮类型发布对应的操作事件
            switch (buttonType)
            {
                case "production":
                    UIEventBus.Publish(new UIEvents.OperationStarted(
                        "生产规划",
                        "ConstructionUI"
                    ));
                    // TODO: 实现生产规划功能
                    break;

                case "tactical":
                    UIEventBus.Publish(new UIEvents.OperationStarted(
                        "战术支援",
                        "TacticalModule"
                    ));
                    // TODO: 实现战术支援功能
                    break;

                case "mechanoid":
                    UIEventBus.Publish(new UIEvents.OperationStarted(
                        "机械体管理",
                        "MechanoidModule"
                    ));
                    // TODO: 实现机械体管理功能
                    break;

                case "strategic":
                    UIEventBus.Publish(new UIEvents.OperationStarted(
                        "战略部署",
                        "StrategicModule"
                    ));
                    // TODO: 实现战略部署功能
                    break;
            }
        }

        return clicked;
    }

    /// <summary>
    /// 决策按钮处理（完全解耦）
    /// </summary>
    private void DrawDecisionButtonsTopRight(Rect rect)
    {
        // ... 按钮创建代码 ...

        if (DrawCustomButton(decisionButtonRect, "决策", "decision", true))
        {
            // 🎯 只发布操作开始事件
            UIEventBus.Publish(new UIEvents.OperationStarted(
                "决策分析",
                "DecisionModule"
            ));
        }

        if (DrawCustomButton(specializationButtonRect, "集群特化", "specialization", true))
        {
            // 🎯 只发布操作开始事件
            UIEventBus.Publish(new UIEvents.OperationStarted(
                "集群特化",
                "SpecializationModule"
            ));
        }
    }
}
```

### 在其他系统中的使用

#### 资源管理系统（完全解耦）

```csharp
public class ResourceManager
{
    public void ConsumeResource(string resourceName, int amount)
    {
        int oldAmount = GetResourceAmount(resourceName);

        // 执行消耗逻辑...

        int newAmount = GetResourceAmount(resourceName);

        // 🎯 只发布资源变化事件，不直接调用SystemOutput
        UIEventBus.Publish(new UIEvents.ResourceChanged(
            resourceName,
            -amount, // 负数表示消耗
            newAmount,
            "ResourceManager"
        ));

        // 检查是否需要警告
        if (newAmount < GetLowThreshold(resourceName))
        {
            UIEventBus.Publish(new UIEvents.CustomMessage(
                $"{resourceName} 资源不足: {newAmount}/{GetLowThreshold(resourceName)}",
                "ResourceWarning",
                "ResourceManager",
                LogLevel.Warning
            ));
        }
    }

    public void ProduceResource(string resourceName, int amount)
    {
        int oldAmount = GetResourceAmount(resourceName);

        // 执行生产逻辑...

        int newAmount = GetResourceAmount(resourceName);

        // 🎯 只发布资源变化事件
        UIEventBus.Publish(new UIEvents.ResourceChanged(
            resourceName,
            amount, // 正数表示获得
            newAmount,
            "ResourceManager"
        ));
    }
}
```

#### 建筑系统（完全解耦）

```csharp
public class ConstructionManager
{
    public bool BuildStructure(string structureName, Vector3 position)
    {
        var startTime = DateTime.Now;

        // 🎯 发布操作开始事件
        UIEventBus.Publish(new UIEvents.OperationStarted(
            $"建造 {structureName}",
            "ConstructionManager"
        ));

        try
        {
            // 执行建造逻辑...

            var duration = DateTime.Now - startTime;

            // 🎯 发布操作完成事件
            UIEventBus.Publish(new UIEvents.OperationCompleted(
                $"建造 {structureName}",
                "ConstructionManager",
                duration
            ));

            return true;
        }
        catch (Exception e)
        {
            // 🎯 发布操作失败事件
            UIEventBus.Publish(new UIEvents.OperationFailed(
                $"建造 {structureName}",
                "ConstructionManager",
                e.Message,
                e
            ));

            return false;
        }
    }
}
```

### 架构优势

#### 1. **完全解耦**
- UI层**不引用**SystemOutputManager
- UI层**不依赖**SystemOutput的具体实现
- 可以通过接口替换整个日志系统

#### 2. **语义化事件**
- 每个事件都有明确的业务含义
- 支持参数传递，提供丰富上下文
- 便于后续扩展和分析

#### 3. **可扩展性**
- 可轻松添加新的事件监听器
- 支持同时记录到多个系统（SystemOutput + 分析系统）
- 事件类型可以自由扩展

#### 4. **测试友好**
- UI层可以独立测试（不依赖日志系统）
- 事件监听器可以独立测试
- 可以mock事件总线进行单元测试

#### 5. **性能友好**
- 事件发布是轻量级操作
- 监听器处理是异步的
- 支持条件订阅（只监听感兴趣的事件）

### 推荐的调用方式

| 场景 | 事件类型 | 示例代码 |
|-----|---------|---------|
| 按钮点击 | `ButtonClicked` | `UIEventBus.Publish(new ButtonClicked("保存", "Dialog"))` |
| 操作开始 | `OperationStarted` | `UIEventBus.Publish(new OperationStarted("分析", "Analytics"))` |
| 操作完成 | `OperationCompleted` | `UIEventBus.Publish(new OperationCompleted("分析", "Analytics", duration))` |
| 资源变化 | `ResourceChanged` | `UIEventBus.Publish(new ResourceChanged("钢材", -10, 50, "Resource"))` |
| 自定义消息 | `CustomMessage` | `UIEventBus.Publish(new CustomMessage("系统就绪", "System", "Core", LogLevel.Info))` |

### 实现步骤

1. **实现事件总线** (`UIEventBus`)
2. **定义事件类** (`UIEvents.*`)
3. **实现监听器** (`SystemOutputEventListener`)
4. **在系统启动时注册监听器** (`SystemOutputInitializer.Initialize()`)
5. **将UI代码中的直接调用替换为事件发布**

这样的架构让UI层专注于UI逻辑，日志记录完全由事件监听器处理，实现了完美的解耦。

### 1. 静态便捷API（推荐首选）

在SystemOutputManager中添加静态方法，提供全局访问：

```csharp
/// <summary>
/// SystemOutput 静态便捷API
/// 提供全局访问的便捷方法
/// </summary>
public static class SystemOutput
{
    // 基础消息类型
    public static void Info(string message, string source = null)
        => SystemOutputManager.Instance.AddMessage(message, source ?? "System");

    public static void Warning(string message, string source = null)
        => SystemOutputManager.Instance.AddMessage($"⚠️ {message}", source ?? "System");

    public static void Error(string message, string source = null)
        => SystemOutputManager.Instance.AddMessage($"❌ {message}", source ?? "System");

    public static void Success(string message, string source = null)
        => SystemOutputManager.Instance.AddMessage($"✅ {message}", source ?? "System");

    // 专用场景方法
    public static void ButtonClick(string buttonName, string windowName = null)
        => Info($"点击了 {buttonName} 按钮", windowName ?? "UI");

    public static void OperationStart(string operationName, string moduleName)
        => Info($"开始执行: {operationName}", moduleName);

    public static void OperationComplete(string operationName, string moduleName)
        => Success($"完成: {operationName}", moduleName);

    public static void OperationFailed(string operationName, string reason, string moduleName)
        => Error($"{operationName} 失败: {reason}", moduleName);

    // 状态变化
    public static void StatusChange(string component, string oldStatus, string newStatus, string moduleName)
        => Info($"{component} 状态变更: {oldStatus} → {newStatus}", moduleName);

    // 资源相关
    public static void ResourceConsumed(string resourceName, int amount, string moduleName)
        => Info($"消耗 {resourceName}: {amount}", moduleName);

    public static void ResourceProduced(string resourceName, int amount, string moduleName)
        => Success($"生产 {resourceName}: {amount}", moduleName);

    public static void ResourceLow(string resourceName, int currentAmount, int threshold, string moduleName)
        => Warning($"{resourceName} 不足: {currentAmount}/{threshold}", moduleName);
}
```

### 2. 扩展SystemOutputManager的便捷方法

在SystemOutputManager中添加更多便捷方法：

```csharp
public class SystemOutputManager
{
    // ... 现有代码 ...

    /// <summary>
    /// 记录按钮点击事件
    /// </summary>
    public void LogButtonClick(string buttonText, string windowContext = null)
    {
        AddMessage($"点击了 '{buttonText}' 按钮", windowContext ?? "UI");
    }

    /// <summary>
    /// 记录操作状态变化
    /// </summary>
    public void LogOperation(string operation, OperationStatus status, string module = null)
    {
        string statusText = status switch
        {
            OperationStatus.Started => "开始",
            OperationStatus.InProgress => "进行中",
            OperationStatus.Completed => "完成",
            OperationStatus.Failed => "失败",
            OperationStatus.Cancelled => "取消",
            _ => "未知"
        };

        string icon = status switch
        {
            OperationStatus.Started => "▶️",
            OperationStatus.Completed => "✅",
            OperationStatus.Failed => "❌",
            OperationStatus.Cancelled => "⏹️",
            _ => "ℹ️"
        };

        AddMessage($"{icon} {operation}: {statusText}", module ?? "System");
    }

    /// <summary>
    /// 记录资源变化
    /// </summary>
    public void LogResourceChange(string resourceName, int changeAmount, string module = null)
    {
        string direction = changeAmount > 0 ? "获得" : "消耗";
        string icon = changeAmount > 0 ? "📈" : "📉";
        string amountText = Mathf.Abs(changeAmount).ToString();

        AddMessage($"{icon} {direction} {resourceName}: {amountText}", module ?? "Resource");
    }

    /// <summary>
    /// 记录系统事件
    /// </summary>
    public void LogSystemEvent(string eventName, string details = null, string module = null)
    {
        string message = details != null ? $"{eventName}: {details}" : eventName;
        AddMessage($"🔧 {message}", module ?? "System");
    }
}

/// <summary>
/// 操作状态枚举
/// </summary>
public enum OperationStatus
{
    Started,
    InProgress,
    Completed,
    Failed,
    Cancelled
}
```

### 3. 在ModularOperationWindow中的使用示例

```csharp
public class ModularOperationWindow : Window
{
    // ... 现有代码 ...

    /// <summary>
    /// 建筑按钮点击处理
    /// </summary>
    private bool DrawBuildingButton(Rect rect, BuildingSlot slot, bool isSelected)
    {
        // ... 现有绘制代码 ...

        if (Widgets.ButtonInvisible(rect))
        {
            if (slot.isEmpty)
            {
                // 🎯 使用便捷API记录按钮点击
                SystemOutput.Info($"点击了空建筑槽位 {slot.slotIndex + 1}", "ConstructionUI");

                selectedSlotIndex = slot.slotIndex;
            }
            else
            {
                // 🎯 记录选中操作
                SystemOutput.Info($"选中了建筑槽位 {slot.slotIndex + 1}", "ConstructionUI");

                selectedBuildingIndex = slotIndex;
            }
        }

        return clicked;
    }

    /// <summary>
    /// 功能按钮点击处理
    /// </summary>
    private bool DrawCustomButton(Rect rect, string label, string buttonType = "default", bool showIcon = false, bool fillMode = false)
    {
        // ... 现有绘制代码 ...

        if (clicked)
        {
            // 🎯 使用专用方法记录按钮点击
            SystemOutputManager.Instance.LogButtonClick(label, "ConstructionUI");

            // 根据按钮类型执行不同逻辑和记录不同消息
            switch (buttonType)
            {
                case "production":
                    SystemOutput.OperationStart("生产规划", "ConstructionUI");
                    // TODO: 实现生产规划功能
                    break;

                case "tactical":
                    SystemOutput.Info("启动战术支援系统", "TacticalModule");
                    // TODO: 实现战术支援功能
                    break;

                case "mechanoid":
                    SystemOutput.Info("打开机械体管理界面", "MechanoidModule");
                    // TODO: 实现机械体管理功能
                    break;

                case "strategic":
                    SystemOutput.Info("初始化战略部署系统", "StrategicModule");
                    // TODO: 实现战略部署功能
                    break;
            }
        }

        return clicked;
    }

    /// <summary>
    /// 决策按钮处理
    /// </summary>
    private void DrawDecisionButtonsTopRight(Rect rect)
    {
        // ... 现有代码 ...

        if (DrawCustomButton(decisionButtonRect, "决策", "decision", true))
        {
            // 🎯 记录决策操作开始
            SystemOutput.OperationStart("决策分析", "DecisionModule");
        }

        if (DrawCustomButton(specializationButtonRect, "集群特化", "specialization", true))
        {
            // 🎯 记录特化操作开始
            SystemOutput.OperationStart("集群特化", "SpecializationModule");
        }
    }
}
```

### 4. 在其他系统中的使用示例

```csharp
// 在资源管理系统中
public class ResourceManager
{
    public void ConsumeResource(string resourceName, int amount)
    {
        // 执行消耗逻辑
        // ...

        // 🎯 记录资源消耗
        SystemOutput.ResourceConsumed(resourceName, amount, "ResourceManager");

        // 检查是否低资源
        if (GetResourceAmount(resourceName) < GetLowThreshold(resourceName))
        {
            SystemOutput.ResourceLow(resourceName, GetResourceAmount(resourceName),
                                   GetLowThreshold(resourceName), "ResourceManager");
        }
    }

    public void ProduceResource(string resourceName, int amount)
    {
        // 执行生产逻辑
        // ...

        // 🎯 记录资源生产
        SystemOutput.ResourceProduced(resourceName, amount, "ResourceManager");
    }
}

// 在建筑系统中
public class ConstructionManager
{
    public bool BuildStructure(string structureName, Vector3 position)
    {
        try
        {
            // 🎯 记录操作开始
            SystemOutput.OperationStart($"建造 {structureName}", "ConstructionManager");

            // 执行建造逻辑
            // ...

            // 🎯 记录操作完成
            SystemOutput.OperationComplete($"建造 {structureName}", "ConstructionManager");

            return true;
        }
        catch (Exception e)
        {
            // 🎯 记录操作失败
            SystemOutput.OperationFailed($"建造 {structureName}", e.Message, "ConstructionManager");

            return false;
        }
    }
}

// 在游戏状态监听器中
public class GameStateMonitor
{
    public void OnGameSpeedChanged(GameSpeed oldSpeed, GameSpeed newSpeed)
    {
        // 🎯 记录状态变化
        SystemOutput.StatusChange("游戏速度",
                                oldSpeed.ToString(),
                                newSpeed.ToString(),
                                "GameState");
    }

    public void OnColonyFounded()
    {
        // 🎯 记录系统事件
        SystemOutput.Info("殖民地建立完成！", "GameState");
    }
}
```

### 5. 批量操作的便捷方式

```csharp
/// <summary>
/// 批量添加消息的扩展方法
/// </summary>
public static class SystemOutputExtensions
{
    public static void LogMultiple(this SystemOutputManager manager,
                                   IEnumerable<string> messages,
                                   string source)
    {
        foreach (var message in messages)
        {
            manager.AddMessage(message, source);
        }
    }

    public static void LogBatch(this SystemOutputManager manager,
                                IEnumerable<(string message, string source)> messageBatch)
    {
        foreach (var (message, source) in messageBatch)
        {
            manager.AddMessage(message, source);
        }
    }
}

// 使用示例
var statusMessages = new[]
{
    "系统初始化完成",
    "模块加载完毕",
    "配置验证通过"
};

SystemOutputManager.Instance.LogMultiple(statusMessages, "SystemInit");
```

### 调用方式对比

| 调用方式 | 复杂程度 | 适用场景 | 示例 |
|---------|---------|---------|------|
| `SystemOutput.Info(msg, source)` | ⭐⭐⭐ | 简单信息记录 | 按钮点击、状态提示 |
| `SystemOutputManager.Instance.AddMessage(msg, source)` | ⭐⭐ | 基础调用 | 通用消息添加 |
| `manager.LogButtonClick(button, window)` | ⭐⭐⭐ | 专用场景 | UI交互记录 |
| `SystemOutput.OperationStart(name, module)` | ⭐⭐⭐⭐ | 操作流程 | 完整的操作生命周期 |

### 最佳实践

1. **在UI交互处**：使用`SystemOutput.Info()`记录用户操作
2. **在业务逻辑中**：使用专用方法如`OperationStart()`、`ResourceConsumed()`
3. **在异常处理中**：使用`Error()`记录错误信息
4. **在状态变化时**：使用`StatusChange()`记录重要变更
5. **在系统事件中**：使用`SystemEvent()`记录系统级事件

这样的设计让各个系统可以**以最少的代码量、最直观的方式**向SystemOutput输出信息，同时保持了消息的规范性和一致性。

## 后续改进计划

### Phase 1: 基础实现
- [x] **设计SystemMessage最小数据结构**
- [x] **实现SystemOutputManager管理器类**
- [ ] 实现核心组件（MessageQueue, 读取接口）
- [ ] 集成到ModularOperationWindow的命令窗口
- [ ] 添加基础配置系统

### Phase 2: 功能扩展
- [ ] 实现插件架构（IMessageProcessor, IMessageFilter）
- [ ] 添加消息过滤和搜索功能
- [ ] 支持消息优先级排序
- [ ] 添加消息模板系统

### Phase 3: 高级特性
- [ ] 实现消息分组和标签系统
- [ ] 添加消息统计和分析功能
- [ ] 支持消息导出功能
- [ ] 添加实时消息推送机制

### Phase 4: 性能优化
- [ ] 实现消息压缩存储
- [ ] 添加消息池复用机制
- [ ] 优化大数据量下的查询性能
- [ ] 添加性能监控和报警

### Phase 5: 生态扩展
- [ ] 开发标准插件库
- [ ] 添加与其他MOD的兼容性
- [ ] 提供开发者SDK
- [ ] 建立社区贡献机制

## 技术债务和风险

### 潜在风险
1. **内存泄漏**：大量消息积累导致内存溢出
2. **性能影响**：UI频繁读取影响渲染性能
3. **线程安全**：多线程访问导致的数据竞争

### 缓解策略
1. **容量限制**：严格的消息数量和时间限制
2. **缓存机制**：UI层缓存查询结果
3. **锁优化**：使用细粒度锁和读写锁分离

## 验收标准

### 功能验收
- [ ] 支持5种消息类型的记录和读取
- [ ] 消息队列容量控制在合理范围内
- [ ] UI层能够实时显示最新消息
- [ ] 不影响游戏基础性能（帧率>30FPS）

### 扩展性验收
- [ ] 能够通过插件扩展新消息处理器
- [ ] 支持自定义消息过滤器
- [ ] 配置系统灵活可调整

### 稳定性验收
- [ ] 无内存泄漏
- [ ] 多线程访问安全
- [ ] 异常情况下的优雅降级

## 文本资源集中管理系统

### 设计理念

为了避免主文件代码膨胀，支持后续修改与扩展，我们设计了**分层文本管理系统**：

1. **文本资源文件**：集中存储所有输出文本
2. **键值映射系统**：使用语义化键值替代硬编码文本
3. **参数化模板**：支持动态内容插入
4. **模块化组织**：按功能模块分类管理
5. **本地化就绪**：为多语言支持做准备

### 核心组件

#### 1. 文本资源定义文件

```csharp
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

    // ================================
    // 资源系统相关
    // ================================

    public static class Resources
    {
        // 资源变化
        public const string RESOURCE_GAINED = "Resources.Gained";
        public const string RESOURCE_CONSUMED = "Resources.Consumed";
        public const string RESOURCE_PRODUCED = "Resources.Produced";
        public const string RESOURCE_WASTED = "Resources.Wasted";

        // 资源警告
        public const string RESOURCE_LOW = "Resources.Warning.Low";
        public const string RESOURCE_CRITICAL = "Resources.Warning.Critical";
        public const string RESOURCE_EMPTY = "Resources.Warning.Empty";
        public const string RESOURCE_FULL = "Resources.Warning.Full";

        // 存储状态
        public const string STORAGE_UPGRADED = "Resources.Storage.Upgraded";
        public const string STORAGE_OVERLOADED = "Resources.Storage.Overloaded";
        public const string STORAGE_MAINTENANCE = "Resources.Storage.Maintenance";
    }
}
```

#### 2. 文本内容映射文件

```csharp
/// <summary>
/// SystemOutput文本内容映射
/// 将键值映射到实际的显示文本
/// </summary>
public static class SystemOutputTextContent
{
    // 文本映射字典
    private static readonly Dictionary<string, string> _textMap = new Dictionary<string, string>
    {
        // UI交互相关
        [SystemOutputText.UI.BUTTON_CLICKED] = "点击了 '{0}' 按钮",
        [SystemOutputText.UI.SLOT_SELECTED] = "选中了 {0}",
        [SystemOutputText.UI.OPERATION_STARTED] = "开始执行: {0}",
        [SystemOutputText.UI.OPERATION_COMPLETED] = "完成: {0} ({1})",
        [SystemOutputText.UI.OPERATION_FAILED] = "{0} 失败: {1}",

        // 建筑系统相关
        [SystemOutputText.Construction.BUILDING_PLACED] = "建造完成: {0} (位置: {1})",
        [SystemOutputText.Construction.SLOT_EMPTY_CLICKED] = "点击了空建筑槽位 {0}",
        [SystemOutputText.Construction.SLOT_OCCUPIED_CLICKED] = "点击了建筑槽位 {0} ({1})",
        [SystemOutputText.Construction.CONSTRUCTION_STARTED] = "开始建造: {0}",
        [SystemOutputText.Construction.CONSTRUCTION_COMPLETED] = "建造完成: {0} ({1})",

        // 资源系统相关
        [SystemOutputText.Resources.RESOURCE_GAINED] = "获得 {0}: +{1} (总计: {2})",
        [SystemOutputText.Resources.RESOURCE_CONSUMED] = "消耗 {0}: -{1} (剩余: {2})",
        [SystemOutputText.Resources.RESOURCE_PRODUCED] = "生产 {0}: +{1}",
        [SystemOutputText.Resources.RESOURCE_LOW] = "⚠️ {0} 不足: {1}/{2}",
        [SystemOutputText.Resources.RESOURCE_CRITICAL] = "❌ {0} 严重不足: {1}/{2}",
        [SystemOutputText.Resources.RESOURCE_EMPTY] = "🚫 {0} 已耗尽",
        [SystemOutputText.Resources.RESOURCE_FULL] = "📦 {0} 存储已满: {1}/{1}",
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
            catch (FormatException)
            {
                Log.Warning($"SystemOutput文本格式化失败: {key}");
                return template;
            }
        }

        Log.Warning($"SystemOutput文本键不存在: {key}");
        return $"[{key}]";
    }
}
```

#### 3. 文本管理器

```csharp
/// <summary>
/// SystemOutput文本管理器
/// 提供文本获取和管理的统一接口
/// </summary>
public static class SystemOutputTextManager
{
    /// <summary>
    /// 获取建筑相关的文本
    /// </summary>
    public static class Construction
    {
        public static string SlotEmptyClicked(int slotIndex)
            => SystemOutputTextContent.Get(SystemOutputText.Construction.SLOT_EMPTY_CLICKED, slotIndex);

        public static string ConstructionStarted(string buildingName)
            => SystemOutputTextContent.Get(SystemOutputText.Construction.CONSTRUCTION_STARTED, buildingName);

        public static string ConstructionCompleted(string buildingName, string duration)
            => SystemOutputTextContent.Get(SystemOutputText.Construction.CONSTRUCTION_COMPLETED, buildingName, duration);
    }

    /// <summary>
    /// 获取资源相关的文本
    /// </summary>
    public static class Resources
    {
        public static string ResourceConsumed(string resourceName, int amount, int remaining)
            => SystemOutputTextContent.Get(SystemOutputText.Resources.RESOURCE_CONSUMED, resourceName, amount, remaining);

        public static string ResourceLow(string resourceName, int current, int threshold)
            => SystemOutputTextContent.Get(SystemOutputText.Resources.RESOURCE_LOW, resourceName, current, threshold);
    }
}
```

### 在SystemOutputEventListener中的集成

```csharp
public class SystemOutputEventListener
{
    private void HandleButtonClicked(UIEvents.ButtonClicked e)
    {
        // 🎯 使用集中文本管理，避免硬编码
        string message;
        if (e.ButtonText.StartsWith("空建筑槽位"))
        {
            // 解析参数并使用文本管理器
            var parts = e.ButtonText.Split(' ');
            if (parts.Length >= 1 && int.TryParse(parts[^1], out int slotIndex))
            {
                message = SystemOutputTextManager.Construction.SlotEmptyClicked(slotIndex);
            }
            else
            {
                message = e.ButtonText; // 降级处理
            }
        }
        else
        {
            // 对于其他按钮，使用通用格式
            message = SystemOutputTextContent.Get(SystemOutputText.UI.BUTTON_CLICKED, e.ButtonText);
        }

        _outputManager.AddMessage(message, e.WindowContext);
    }

    private void HandleOperationStarted(UIEvents.OperationStarted e)
    {
        // 🎯 使用文本管理器
        string message = SystemOutputTextContent.Get(SystemOutputText.UI.OPERATION_STARTED, e.OperationName);
        _outputManager.AddMessage(message, e.ModuleName);
    }

    private void HandleResourceChanged(UIEvents.ResourceChanged e)
    {
        // 🎯 根据变化类型选择合适的文本
        string message;
        if (e.ChangeAmount > 0)
        {
            message = SystemOutputTextContent.Get(SystemOutputText.Resources.RESOURCE_PRODUCED, e.ResourceName, e.ChangeAmount);
        }
        else
        {
            message = SystemOutputTextManager.Resources.ResourceConsumed(e.ResourceName, -e.ChangeAmount, e.NewTotal);
        }
        _outputManager.AddMessage(message, e.ModuleName);
    }
}
```

### 优势总结

#### ✅ **代码组织**
- 所有文本集中管理，避免分散在各处
- 按模块分类，结构清晰
- 支持参数化，避免字符串拼接

#### ✅ **维护性**
- 修改文本只需改一处
- 键值系统支持重构
- 类型安全，避免拼写错误

#### ✅ **扩展性**
- 轻松添加新文本和新模块
- 支持本地化预留接口
- 向后兼容旧代码

#### ✅ **开发效率**
- IDE自动补全键值
- 编译时检查参数匹配
- 统一的文本获取接口

#### ✅ **运行时优势**
- 内存共享，减少重复字符串
- 懒加载，按需初始化
- 错误处理，提供友好的降级

### 扩展和维护指南

#### 添加新文本
1. 在`SystemOutputText`中定义键值常量
2. 在`SystemOutputTextContent`中添加文本映射
3. 在`SystemOutputTextManager`中添加便捷方法（可选）

#### 修改现有文本
只需要修改`SystemOutputTextContent`中的映射值，所有使用该键值的地方都会自动更新。

#### 本地化支持
可以通过扩展`SystemOutputTextContent.LoadFromFile()`方法来支持从外部文件加载不同语言的文本映射。

这个文本管理系统让SystemOutput变得高度可维护和可扩展，同时保持了代码的简洁性和性能。

## 未来扩展方案（架构兼容性）

### 设计原则

在**不修改现有架构**的前提下，通过**扩展和组合**现有组件，实现新功能的无缝集成：

1. **向后兼容**：现有代码无需修改
2. **渐进增强**：新功能可选启用
3. **组合扩展**：通过组合现有组件实现新功能
4. **接口扩展**：通过新接口支持高级功能

### 1. 消息等级扩展

#### 当前架构分析
现有的`SystemMessage`类只包含基础字段，可以通过扩展属性支持等级系统。

#### 扩展实现方案

**方案A：扩展SystemMessage类（添加可选属性）**
```csharp
/// <summary>
/// 扩展的SystemMessage类（向后兼容）
/// </summary>
public class ExtendedSystemMessage : SystemMessage
{
    /// <summary>
    /// 消息等级（可选扩展）
    /// </summary>
    public MessageLevel Level { get; set; } = MessageLevel.Info;

    /// <summary>
    /// 构造函数（保持兼容性）
    /// </summary>
    public ExtendedSystemMessage(string content, string source)
        : base(content, source)
    {
        // 默认等级为Info，保持向后兼容
    }

    /// <summary>
    /// 构造函数（支持等级）
    /// </summary>
    public ExtendedSystemMessage(string content, string source, MessageLevel level)
        : base(content, source)
    {
        Level = level;
    }
}

/// <summary>
/// 消息等级枚举
/// </summary>
public enum MessageLevel
{
    Debug,      // 调试信息
    Info,       // 普通信息
    Warning,    // 警告
    Error,      // 错误
    Critical    // 严重错误
}
```

**方案B：通过事件扩展等级信息**
```csharp
/// <summary>
/// 扩展的事件定义（保持原有事件不变）
/// </summary>
public static class ExtendedUIEvents
{
    /// <summary>
    /// 带等级的自定义消息事件
    /// </summary>
    public class LeveledMessage : IUIEvent
    {
        public string Message { get; }
        public string Category { get; }
        public string ModuleName { get; }
        public MessageLevel Level { get; }

        public LeveledMessage(string message, MessageLevel level,
                            string category = null, string moduleName = null)
        {
            Message = message;
            Level = level;
            Category = category ?? "General";
            ModuleName = moduleName ?? "Unknown";
        }
    }
}
```

**方案C：等级过滤监听器**
```csharp
/// <summary>
/// 等级感知的事件监听器
/// 可以根据等级过滤消息
/// </summary>
public class LeveledSystemOutputEventListener : SystemOutputEventListener
{
    private readonly MessageLevel _minLevel;

    public LeveledSystemOutputEventListener(SystemOutputManager outputManager,
                                          MessageLevel minLevel = MessageLevel.Info)
        : base(outputManager)
    {
        _minLevel = minLevel;
    }

    // 重写事件处理方法，添加等级过滤
    protected override void HandleCustomMessage(UIEvents.CustomMessage e)
    {
        // 将LogLevel转换为MessageLevel
        var messageLevel = e.Level switch
        {
            LogLevel.Debug => MessageLevel.Debug,
            LogLevel.Info => MessageLevel.Info,
            LogLevel.Warning => MessageLevel.Warning,
            LogLevel.Error => MessageLevel.Error,
            LogLevel.Success => MessageLevel.Info, // Success当作Info处理
            _ => MessageLevel.Info
        };

        // 等级过滤
        if (messageLevel >= _minLevel)
        {
            base.HandleCustomMessage(e);
        }
        // 低于最低等级的消息被静默忽略
    }

    // 处理新的LeveledMessage事件
    private void HandleLeveledMessage(ExtendedUIEvents.LeveledMessage e)
    {
        if (e.Level >= _minLevel)
        {
            // 创建扩展消息并添加到管理器
            var extendedMessage = new ExtendedSystemMessage(e.Message, e.ModuleName, e.Level);
            _outputManager.AddMessage(extendedMessage.Content, extendedMessage.Source);
        }
    }
}
```

### 2. 消息分类扩展

#### 扩展实现方案

**方案A：分类枚举定义**
```csharp
/// <summary>
/// 消息分类枚举
/// </summary>
public enum MessageCategory
{
    // 系统分类
    System,
    UI,
    GameLogic,

    // 功能分类
    Construction,
    Resources,
    Production,
    Combat,
    Research,

    // 状态分类
    Status,
    Warning,
    Error,

    // 自定义分类
    Custom
}
```

**方案B：分类增强的事件**
```csharp
/// <summary>
/// 带分类的事件定义
/// </summary>
public static class CategorizedUIEvents
{
    /// <summary>
    /// 分类消息事件
    /// </summary>
    public class CategorizedMessage : IUIEvent
    {
        public string Message { get; }
        public MessageCategory Category { get; }
        public string SubCategory { get; }
        public string ModuleName { get; }

        public CategorizedMessage(string message, MessageCategory category,
                                string subCategory = null, string moduleName = null)
        {
            Message = message;
            Category = category;
            SubCategory = subCategory ?? string.Empty;
            ModuleName = moduleName ?? "Unknown";
        }
    }
}
```

**方案C：分类过滤监听器**
```csharp
/// <summary>
/// 分类感知的事件监听器
/// 支持按分类过滤消息
/// </summary>
public class CategorizedSystemOutputEventListener : SystemOutputEventListener
{
    private readonly HashSet<MessageCategory> _enabledCategories;

    public CategorizedSystemOutputEventListener(SystemOutputManager outputManager,
                                               IEnumerable<MessageCategory> enabledCategories = null)
        : base(outputManager)
    {
        // 默认启用所有分类
        _enabledCategories = enabledCategories != null
            ? new HashSet<MessageCategory>(enabledCategories)
            : new HashSet<MessageCategory>(Enum.GetValues(typeof(MessageCategory)).Cast<MessageCategory>());
    }

    // 处理分类消息事件
    private void HandleCategorizedMessage(CategorizedUIEvents.CategorizedMessage e)
    {
        if (_enabledCategories.Contains(e.Category))
        {
            // 为不同分类添加前缀
            string prefix = GetCategoryPrefix(e.Category);
            string fullMessage = string.IsNullOrEmpty(prefix) ? e.Message : $"[{prefix}] {e.Message}";

            _outputManager.AddMessage(fullMessage, e.ModuleName);
        }
    }

    private string GetCategoryPrefix(MessageCategory category)
    {
        return category switch
        {
            MessageCategory.Construction => "🏗️",
            MessageCategory.Resources => "📦",
            MessageCategory.Production => "⚙️",
            MessageCategory.Combat => "⚔️",
            MessageCategory.Research => "🔬",
            MessageCategory.Warning => "⚠️",
            MessageCategory.Error => "❌",
            MessageCategory.System => "🔧",
            _ => string.Empty
        };
    }
}
```

### 3. DevMode专用输出扩展

#### 扩展实现方案

**方案A：DevMode监听器**
```csharp
/// <summary>
/// 开发者模式专用监听器
/// 只在DevMode下记录调试信息
/// </summary>
public class DevModeSystemOutputEventListener : SystemOutputEventListener
{
    public DevModeSystemOutputEventListener(SystemOutputManager outputManager)
        : base(outputManager)
    {
        // 只在DevMode下订阅调试事件
        if (Prefs.DevMode)
        {
            SubscribeToDevEvents();
        }
    }

    private void SubscribeToDevEvents()
    {
        // 订阅调试专用事件
        UIEventBus.Subscribe<DevUIEvents.DebugInfo>(HandleDebugInfo);
        UIEventBus.Subscribe<DevUIEvents.PerformanceMetric>(HandlePerformanceMetric);
        UIEventBus.Subscribe<DevUIEvents.SystemState>(HandleSystemState);
    }

    private void HandleDebugInfo(DevUIEvents.DebugInfo e)
    {
        if (!Prefs.DevMode) return;

        string debugMessage = $"[DEBUG] {e.Component}: {e.Info}";
        _outputManager.AddMessage(debugMessage, "DevMode");
    }

    private void HandlePerformanceMetric(DevUIEvents.PerformanceMetric e)
    {
        if (!Prefs.DevMode) return;

        string perfMessage = $"[PERF] {e.MetricName}: {e.Value:F2} {e.Unit} ({e.Timestamp})";
        _outputManager.AddMessage(perfMessage, "DevMode");
    }

    private void HandleSystemState(DevUIEvents.SystemState e)
    {
        if (!Prefs.DevMode) return;

        string stateMessage = $"[STATE] {e.SystemName}: {e.State} - {e.Details}";
        _outputManager.AddMessage(stateMessage, "DevMode");
    }
}

/// <summary>
/// 开发者模式专用事件
/// </summary>
public static class DevUIEvents
{
    public class DebugInfo : IUIEvent
    {
        public string Component { get; }
        public string Info { get; }

        public DebugInfo(string component, string info)
        {
            Component = component;
            Info = info;
        }
    }

    public class PerformanceMetric : IUIEvent
    {
        public string MetricName { get; }
        public float Value { get; }
        public string Unit { get; }
        public DateTime Timestamp { get; }

        public PerformanceMetric(string metricName, float value, string unit)
        {
            MetricName = metricName;
            Value = value;
            Unit = unit;
            Timestamp = DateTime.Now;
        }
    }

    public class SystemState : IUIEvent
    {
        public string SystemName { get; }
        public string State { get; }
        public string Details { get; }

        public SystemState(string systemName, string state, string details = null)
        {
            SystemName = systemName;
            State = state;
            Details = details ?? string.Empty;
        }
    }
}
```

**方案B：条件渲染扩展**
```csharp
/// <summary>
/// DevMode感知的UI渲染器
/// 在UI层根据DevMode状态决定是否显示调试信息
/// </summary>
public class DevModeAwareSystemOutputRenderer
{
    private readonly SystemOutputManager _outputManager;
    private bool _showDebugMessages;

    public DevModeAwareSystemOutputRenderer(SystemOutputManager outputManager)
    {
        _outputManager = outputManager;
        _showDebugMessages = Prefs.DevMode;

        // 监听DevMode变化
        RegisterDevModeChangeListener();
    }

    private void RegisterDevModeChangeListener()
    {
        // 当DevMode状态改变时更新显示设置
        // 这里需要hook到RimWorld的DevMode切换事件
    }

    public void DrawSystemMessages(Rect rect)
    {
        var messages = _outputManager.GetAllMessages();

        // 根据DevMode过滤消息
        var filteredMessages = _showDebugMessages
            ? messages
            : messages.Where(m => !m.Content.Contains("[DEBUG]") &&
                                !m.Content.Contains("[PERF]") &&
                                !m.Content.Contains("[STATE]")).ToList();

        // 渲染过滤后的消息
        // ... 渲染逻辑
    }
}
```

### 4. 高级查询和过滤扩展

#### 扩展实现方案

**方案A：查询接口扩展**
```csharp
/// <summary>
/// 高级查询接口
/// </summary>
public interface IAdvancedMessageQuery
{
    IEnumerable<SystemMessage> GetMessagesByLevel(MessageLevel level);
    IEnumerable<SystemMessage> GetMessagesByCategory(MessageCategory category);
    IEnumerable<SystemMessage> GetMessagesInTimeRange(DateTime start, DateTime end);
    IEnumerable<SystemMessage> GetMessagesBySource(string source);
    IEnumerable<SystemMessage> GetMessagesContaining(string keyword);
}

/// <summary>
/// 高级查询实现
/// </summary>
public class AdvancedMessageQuery : IAdvancedMessageQuery
{
    private readonly List<SystemMessage> _messages;

    public AdvancedMessageQuery(List<SystemMessage> messages)
    {
        _messages = messages;
    }

    public IEnumerable<SystemMessage> GetMessagesByLevel(MessageLevel level)
    {
        return _messages.OfType<ExtendedSystemMessage>()
                       .Where(m => m.Level == level);
    }

    public IEnumerable<SystemMessage> GetMessagesByCategory(MessageCategory category)
    {
        // 通过解析消息内容或扩展属性来过滤
        return _messages.Where(m => ExtractCategoryFromMessage(m) == category);
    }

    // 其他查询方法实现...
    private MessageCategory ExtractCategoryFromMessage(SystemMessage message)
    {
        // 从消息内容或来源解析分类的逻辑
        if (message.Content.Contains("🏗️")) return MessageCategory.Construction;
        if (message.Content.Contains("📦")) return MessageCategory.Resources;
        // ... 其他分类识别逻辑
        return MessageCategory.System;
    }
}
```

### 5. 消息持久化和导出扩展

#### 扩展实现方案

**方案A：消息序列化器**
```csharp
/// <summary>
/// 消息序列化器
/// 支持消息的持久化和导出
/// </summary>
public class MessageSerializer
{
    /// <summary>
    /// 将消息序列化为JSON
    /// </summary>
    public static string SerializeToJson(IEnumerable<SystemMessage> messages)
    {
        var serializableMessages = messages.Select(m => new
        {
            m.Content,
            m.Source,
            m.Timestamp,
            // 扩展属性（如果有）
            Level = (m as ExtendedSystemMessage)?.Level.ToString() ?? "Info"
        });

        return JsonConvert.SerializeObject(serializableMessages, Formatting.Indented);
    }

    /// <summary>
    /// 从JSON反序列化消息
    /// </summary>
    public static List<SystemMessage> DeserializeFromJson(string json)
    {
        var data = JsonConvert.DeserializeObject<List<SerializableMessage>>(json);
        return data.Select(d => new SystemMessage(d.Content, d.Source)).ToList();
    }

    private class SerializableMessage
    {
        public string Content { get; set; }
        public string Source { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
    }
}
```

### 6. 多监听器组合扩展

#### 扩展实现方案

**方案A：监听器管理器**
```csharp
/// <summary>
/// 监听器管理器
/// 支持同时运行多个监听器，实现组合功能
/// </summary>
public class SystemOutputListenerManager
{
    private readonly List<SystemOutputEventListener> _listeners = new List<SystemOutputEventListener>();
    private readonly SystemOutputManager _outputManager;

    public SystemOutputListenerManager(SystemOutputManager outputManager)
    {
        _outputManager = outputManager;
    }

    /// <summary>
    /// 添加监听器
    /// </summary>
    public void AddListener(SystemOutputEventListener listener)
    {
        _listeners.Add(listener);
    }

    /// <summary>
    /// 创建标准监听器组合
    /// </summary>
    public void CreateStandardListeners()
    {
        // 基础监听器
        AddListener(new SystemOutputEventListener(_outputManager));

        // DevMode监听器（只在DevMode下添加）
        if (Prefs.DevMode)
        {
            AddListener(new DevModeSystemOutputEventListener(_outputManager));
        }

        // 等级过滤监听器
        AddListener(new LeveledSystemOutputEventListener(_outputManager, MessageLevel.Info));

        // 分类监听器
        AddListener(new CategorizedSystemOutputEventListener(_outputManager,
            new[] { MessageCategory.System, MessageCategory.UI, MessageCategory.Construction }));
    }

    /// <summary>
    /// 移除所有监听器
    /// </summary>
    public void ClearListeners()
    {
        // 清理监听器订阅
        _listeners.Clear();
    }
}
```

### 扩展实施路线图

#### Phase 2A：基础扩展（推荐优先实施）
1. ✅ 实现ExtendedSystemMessage类
2. ✅ 添加MessageLevel枚举
3. ✅ 实现LeveledSystemOutputEventListener
4. ✅ 扩展UIEvents支持等级参数

#### Phase 2B：分类系统
1. ✅ 定义MessageCategory枚举
2. ✅ 实现CategorizedSystemOutputEventListener
3. ✅ 添加分类识别逻辑
4. ✅ UI支持分类过滤显示

#### Phase 2C：开发者工具
1. ✅ 实现DevModeSystemOutputEventListener
2. ✅ 定义DevUIEvents事件
3. ✅ 添加性能监控事件
4. ✅ DevMode条件渲染

#### Phase 3：高级功能
1. 实现AdvancedMessageQuery
2. 添加MessageSerializer
3. 实现SystemOutputListenerManager
4. 支持消息导出和导入

### 向后兼容性保证

所有扩展都遵循以下原则：
- **现有代码无需修改**：所有新功能都是 additive（添加性的）
- **渐进采用**：可以逐步启用新功能
- **降级兼容**：在没有扩展功能的环境下仍能正常工作
- **接口稳定**：现有接口保持不变

通过这种扩展策略，SystemOutput系统可以在保持架构稳定的同时，灵活地支持各种高级功能需求。

---

# SystemOutput 系统完整架构设计

## 系统概述

SystemOutput 是为 RimWorld MOD "The Dead Man's Switch - Legion" 设计的模块化系统文本输出通道，采用**事件驱动架构**实现UI层与日志系统的完全解耦。

## 核心架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           外部系统调用方                                   │
│  (UI系统、游戏逻辑、资源管理、建造系统等)                                 │
└─────────────────────┬───────────────────────────────────────────────────┘
                      │ 发布事件
                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                           UIEventBus (事件总线)                           │
│  • 事件发布：Publish<T>(event)                                        │
│  • 事件订阅：Subscribe<T>(handler)                                    │
│  • 事件分发：自动调用所有订阅者                                        │
└─────────────────────┼───────────────────────────────────────────────────┘
                      │ 事件流
                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     SystemOutputEventListener (监听器)                   │
│  • 事件处理：将UI事件转换为SystemOutput消息                            │
│  • 消息格式化：调用文本管理系统生成最终消息                             │
│  • 消息存储：调用SystemOutputManager存储消息                            │
└─────────────────────┼───────────────────────────────────────────────────┘
                      │ 存储消息
                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        SystemOutputManager (管理器)                      │
│  • 消息存储：维护消息列表和容量控制                                   │
│  • 线程安全：加锁保护并发访问                                         │
│  • 只读访问：提供GetAllMessages()等读取接口                            │
└─────────────────────┼───────────────────────────────────────────────────┘
                      │ 只读访问
                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                   ModularOperationWindow (UI显示)                        │
│  • 消息获取：调用SystemOutputManager.GetAllMessages()                 │
│  • 消息渲染：将消息显示在System窗口中                                 │
│  • 滚动控制：提供消息列表的滚动浏览                                    │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                        文本资源管理系统                                  │
│  SystemOutputText ───→ SystemOutputTextContent ───→ SystemOutputTextManager │
│  (键值定义)         (文本映射)                 (便捷接口)                 │
│                                                                         │
│  被SystemOutputEventListener调用，用于生成格式化的消息文本             │
└─────────────────────────────────────────────────────────────────────────┘
```

## 模块职责与接口

### 1. 数据层 (Data Layer)

#### SystemMessage
**职责**：消息数据载体
**接口**：
- 构造函数：`SystemMessage(string content, string source)`
- 只读属性：`Content`, `Source`, `Timestamp`

#### SystemOutputManager
**职责**：消息存储和管理
**接口**：
- `AddMessage(string content, string source)` - 添加消息
- `GetAllMessages()` - 获取所有消息副本
- `GetMessageCount()` - 获取消息数量
- `ClearAllMessages()` - 清空消息

### 2. 事件层 (Event Layer)

#### UIEventBus
**职责**：事件发布订阅管理
**接口**：
- `Publish<T>(T eventData)` - 发布事件
- `Subscribe<T>(Action<T> handler)` - 订阅事件
- `Unsubscribe<T>(Action<T> handler)` - 取消订阅

#### UIEvents.*
**职责**：事件数据定义
**接口**：各种事件类的构造函数和属性

### 3. 处理层 (Processing Layer)

#### SystemOutputEventListener
**职责**：事件到消息的转换
**接口**：
- 构造函数：`SystemOutputEventListener(SystemOutputManager)`
- 事件处理方法：各种`HandleXxx`方法

#### 扩展监听器
- `LeveledSystemOutputEventListener` - 等级过滤
- `CategorizedSystemOutputEventListener` - 分类过滤
- `DevModeSystemOutputEventListener` - 开发者模式

### 4. 资源层 (Resource Layer)

#### SystemOutputText
**职责**：文本键值常量定义
**接口**：各种静态常量字符串

#### SystemOutputTextContent
**职责**：键值到文本的映射
**接口**：
- `Get(string key, params object[] args)` - 获取格式化文本

#### SystemOutputTextManager
**职责**：便捷的文本获取接口
**接口**：各种`GetXxx`静态方法

### 5. 显示层 (Presentation Layer)

#### ModularOperationWindow
**职责**：消息UI显示
**接口**：
- `DrawSystemMessages(Rect rect)` - 绘制消息列表

## 调用方向与数据流

### 主要数据流

```
1. 触发 → 2. 事件发布 → 3. 事件处理 → 4. 消息存储 → 5. UI显示

外部系统触发操作
        ↓
    UIEventBus.Publish(event)
        ↓
SystemOutputEventListener.HandleXxx(event)
        ↓
SystemOutputTextManager.GetXxx() + SystemOutputManager.AddMessage()
        ↓
SystemOutputManager存储消息
        ↓
ModularOperationWindow.GetAllMessages() → 渲染显示
```

### 详细调用关系

#### 正向调用 (Forward Calls)
```
外部系统 → UIEventBus.Publish() → 监听器 → 文本管理器 → 管理器
                                     → 管理器.AddMessage()
```

#### 反向调用 (Reverse Calls)
```
UI显示 ← 管理器.GetAllMessages() ← ModularOperationWindow
```

#### 配置调用 (Configuration Calls)
```
系统初始化 → SystemOutputInitializer.Initialize()
               ↓
           创建监听器
               ↓
           注册事件处理器
```

## 依赖关系图

```
高层模块 (依赖少)
├── ModularOperationWindow (只依赖SystemOutputManager)
├── SystemOutputEventListener (依赖管理器和文本管理器)
├── 扩展监听器 (依赖基础监听器)
└── UIEventBus (无依赖，核心基础设施)

中层模块
├── SystemOutputManager (核心，管理消息存储)
├── SystemOutputTextManager (依赖文本内容)
└── UIEvents (事件定义，无逻辑依赖)

底层模块 (被依赖多)
├── SystemMessage (数据结构，被广泛使用)
├── SystemOutputText (常量定义)
└── SystemOutputTextContent (文本映射)
```

## 模块间通信方式

### 1. 事件驱动通信
- **适用**：UI交互、状态变化、操作反馈
- **方向**：外部系统 → UIEventBus → 监听器
- **特点**：解耦、异步、可扩展

### 2. 直接方法调用
- **适用**：消息存储读取、文本格式化
- **方向**：
  - 监听器 → 管理器 (存储消息)
  - UI → 管理器 (读取消息)
  - 监听器 → 文本管理器 (格式化文本)

### 3. 配置初始化
- **适用**：系统启动时的组件组装
- **方向**：初始化器 → 各组件
- **特点**：一次性设置，运行时静态

## 扩展机制

### 1. 监听器扩展
```csharp
// 新功能通过继承实现
public class CustomEventListener : SystemOutputEventListener
{
    public CustomEventListener(SystemOutputManager manager) : base(manager)
    {
        // 注册新的处理器
        UIEventBus.Subscribe<CustomEvent>(HandleCustomEvent);
    }

    private void HandleCustomEvent(CustomEvent e)
    {
        // 自定义处理逻辑
        string message = SystemOutputTextManager.Get("Custom.Key", e.Data);
        _outputManager.AddMessage(message, "CustomModule");
    }
}
```

### 2. 事件扩展
```csharp
// 定义新事件
public class CustomEvent : IUIEvent
{
    public string Data { get; }
    public CustomEvent(string data) { Data = data; }
}

// 在SystemOutputText中添加键值
public const string CUSTOM_MESSAGE = "Custom.Message";
```

### 3. 文本扩展
```csharp
// 在SystemOutputTextContent中添加映射
[SystemOutputText.Custom.CUSTOM_MESSAGE] = "自定义消息: {0}";

// 在SystemOutputTextManager中添加方法
public static string CustomMessage(string data)
    => Get(SystemOutputText.Custom.CUSTOM_MESSAGE, data);
```

## 性能优化策略

### 1. 缓存机制
- **文本缓存**：SystemOutputTextContent在静态初始化时加载
- **消息缓存**：UI层缓存消息列表，0.25秒更新间隔以平衡性能和实时性
- **监听器缓存**：事件处理器预编译，避免运行时反射

### 2. 异步处理
- **事件分发**：UIEventBus采用同步分发，但可扩展为异步
- **消息格式化**：可在后台线程预处理文本格式化

### 3. 内存管理
- **容量限制**：SystemOutputManager自动清理旧消息
- **对象池**：可为频繁创建的对象实现对象池
- **弱引用**：监听器使用弱引用避免内存泄漏

## 错误处理策略

### 1. 降级处理
- **文本缺失**：返回键值本身加警告前缀 `[KeyName]`
- **格式化失败**：返回未格式化的模板字符串
- **监听器异常**：记录错误但不中断事件处理

### 2. 监控机制
- **性能监控**：记录事件处理时间
- **错误统计**：统计各类错误的发生频率
- **健康检查**：定期验证各组件状态

## 部署配置

### 基本配置
```csharp
// 核心组件（必须）
var manager = SystemOutputManager.Instance;
var baseListener = new SystemOutputEventListener(manager);

// 文本系统初始化
SystemOutputTextManager.Initialize();
```

### 扩展配置
```csharp
// 等级过滤
if (useLevelFiltering)
{
    var levelListener = new LeveledSystemOutputEventListener(manager, minLevel);
}

// 分类过滤
if (useCategoryFiltering)
{
    var categoryListener = new CategorizedSystemOutputEventListener(manager, enabledCategories);
}

// 开发者模式
if (Prefs.DevMode)
{
    var devListener = new DevModeSystemOutputEventListener(manager);
}
```

---

## 🎭 **打字机效果功能**

### 功能描述

SystemOutput 支持打字机效果，让消息像 Windows 命令行一样逐字显示，增强视觉体验和沉浸感。

### 核心特性

#### 1. **逐字显示动画**
- **显示速度**: 每50毫秒显示一个字符
- **平滑动画**: 基于 Unity 时间系统的流畅动画
- **可配置**: 支持启用/禁用和速度调整

#### 2. **智能状态管理**
- **消息标识**: 为每个消息生成唯一ID进行跟踪
- **状态同步**: UI 和逻辑层状态保持同步
- **内存管理**: 自动清理已完成的打字机效果

#### 3. **用户体验优化**
- **即时开始**: 消息添加后立即开始打字机效果
- **连续显示**: 多条消息依次显示，不会相互干扰
- **性能友好**: 只在需要时更新，减少性能开销

### 技术实现

#### 打字机效果管理器

```csharp
/// <summary>
/// 打字机效果管理器
/// 负责管理所有活跃的打字机效果
/// </summary>
public static class TypewriterManager
{
    // 配置参数
    public const float CharacterInterval = 0.05f; // 50ms per character
    public static bool Enabled = true;

    // 核心方法
    public static void StartTypewriter(string messageId, string fullText);
    public static string GetCurrentText(string messageId);
    public static bool IsComplete(string messageId);
    public static void CompleteTypewriter(string messageId);
    public static void CleanupCompleted();
}
```

#### UI 集成

```csharp
private void DrawSystemMessages(Rect rect)
{
    // 获取消息副本（只读访问）
    var messages = _cachedMessages ?? new List<SystemMessage>();

    // 固定显示6行，无滚动条
    float lineHeight = 32f;  // 增大行间距
    int maxVisibleLines = 6; // 固定显示6行

    // 绘制最近的6条消息
    float currentY = 0f;
    int messagesToShow = Mathf.Min(messages.Count, maxVisibleLines);

    for (int i = 0; i < messagesToShow; i++)
    {
        var message = messages[messages.Count - messagesToShow + i];
        Rect messageRect = new Rect(0f, currentY, rect.width, lineHeight);

        DrawSingleSystemMessage(messageRect, message, i);
        currentY += lineHeight;
    }
}

private void DrawSingleSystemMessage(Rect rect, SystemMessage message, int messageIndex)
{
    // 生成消息唯一标识符
    string messageId = $"{message.Content.GetHashCode()}_{message.Timestamp.Ticks}";

    // 获取当前显示的文字（支持打字机效果）
    string displayText = SystemOutputText.TypewriterManager.GetCurrentText(messageId);

    // 启动打字机效果（如果还没开始，且启用了自定义UI）
    if (string.IsNullOrEmpty(displayText) && !SystemOutputText.TypewriterManager.IsComplete(messageId))
    {
        SystemOutputText.TypewriterManager.StartTypewriter(messageId, message.Content);
    }

    // 绘制当前显示的文字
    DrawSystemOutputLabel(rect, displayText, TextAnchor.MiddleLeft);
}
```

### 使用效果

**打字机效果演示**:
```
点击了生[延时] → 点击了生产[延时] → 点击了生产规[延时] → ... → 点击了生产规划按钮
```

### 配置选项

#### 启用/禁用
```csharp
// 自动受自定义UI设置控制，无需手动设置
bool isEnabled = DMSL_ModSettings.settings?.useCustomUI ?? false;
SystemOutputText.TypewriterConfig.Enabled; // 返回上述设置的值
```

#### 速度调整
```csharp
// 修改字符显示间隔（单位：秒）
SystemOutputText.TypewriterConfig.CharacterInterval = 0.03f; // 更快 (30ms)
SystemOutputText.TypewriterConfig.CharacterInterval = 0.08f; // 更慢 (80ms)
```

### 性能考虑

#### 内存管理
- **自动清理**: 已完成的打字机效果会自动清理
- **轻量级**: 每个效果只存储必要的状态信息
- **无泄漏**: UI 层定期清理过期效果

#### CPU 开销
- **按需更新**: 只在有活跃效果时进行计算
- **高效算法**: 使用简单的时间差分计算
- **可配置**: 可以完全禁用以提升性能

### 扩展可能性

#### 自定义效果
```csharp
// 可以扩展支持不同的打字机效果
public enum TypewriterEffect
{
    Normal,     // 普通逐字
    Wave,       // 波浪效果
    FadeIn,     // 淡入效果
    Typewriter  // 经典打字机音效
}
```

#### 音效集成
```csharp
// 可以添加打字音效
public static AudioClip TypewriterSound;
private void PlayTypewriterSound() { /* 播放音效 */ }
```

#### 高级控制
```csharp
// 消息优先级 - 重要消息快速显示
public static void StartPriorityTypewriter(string messageId, string text, bool isPriority)
{
    float interval = isPriority ? 0.01f : 0.05f; // 优先消息显示更快
}
```

### 用户体验提升

#### 沉浸感
- **命令行体验**: 模拟真实的命令行输出
- **视觉反馈**: 让用户感受到系统的"工作"状态
- **节奏控制**: 避免消息瞬间全部显示带来的信息过载

#### 无障碍性
- **可配置**: 用户可以根据需要启用或禁用
- **性能友好**: 对性能影响可控
- **降级友好**: 禁用时无任何功能损失

---

**文档版本**: v2.2
**最后更新**: 2025-01-02
**设计者**: AI Assistant
**最新更新**: 优化打字机效果和UI显示（6行固定显示，移除滚动条，增大行间距）
