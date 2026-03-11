# 详细设计说明书 (Detailed Design Document)

| 文档版本 | V1.1 (更新数据库路径、AppEvent枚举确认) |
|:---|:---|
| **项目名称** | Desktop AI Calendar (DAC) |
| **编写日期** | 2026-03-11 |
| **编写人** | AI Assistant |
| **依赖文档** | System-Architecture.md V1.4, Database-Design.md V1.1, API-Interface-Design.md V1.2 |

---

## 目录 (Table of Contents)

1. [文档概述](#1-文档概述)
2. [模块详细设计](#2-模块详细设计)
3. [核心类设计](#3-核心类设计)
4. [时序图设计](#4-时序图设计)
5. [关键算法设计](#5-关键算法设计)
6. [异常处理机制](#6-异常处理机制)
7. [性能优化方案](#7-性能优化方案)
8. [UI组件详细设计](#8-ui组件详细设计)
9. [配置管理设计](#9-配置管理设计)
10. [日志与监控设计](#10-日志与监控设计)
11. [部署与运维设计](#11-部署与运维设计)

---

## 1. 文档概述

### 1.1 文档目标

本文档是对 Desktop AI Calendar 项目的详细设计说明，在系统架构设计、数据库设计、接口设计文档的基础上，进一步细化各模块的内部实现细节，包括：

- 模块的详细类结构和职责
- 关键业务流程的时序图
- 核心算法的实现逻辑
- 异常处理和错误恢复机制
- 性能优化策略
- UI组件的交互细节
- 配置管理和部署方案

### 1.2 适用范围

本文档适用于：
- **开发人员**：理解模块内部实现细节，指导编码工作
- **测试人员**：理解系统行为，设计测试用例
- **运维人员**：理解部署架构，制定运维方案
- **项目管理者**：评估开发进度和风险

### 1.3 参考文档

| 文档名称 | 版本 | 说明 |
|:---|:---|:---|
| [PRD.md](PRD.md) | V1.2 | 产品需求文档 |
| [System-Architecture.md](System-Architecture.md) | V1.4 | 系统架构设计文档 |
| [Database-Design.md](Database-Design.md) | V1.1 | 数据库设计说明书 |
| [API-Interface-Design.md](API-Interface-Design.md) | V1.2 | 接口设计说明书 |
| [Complete-System-Design.md](Complete-System-Design.md) | V1.1 | 完整系统设计文档 |

---

## 2. 模块详细设计

### 2.1 领域层模块 (Domain Layer)

#### 2.1.1 Event 实体类

**命名空间**: `AI_Calendar.Core.Entities`

**职责**: 封装日历事件的核心业务逻辑和数据

**类图**:

```
┌─────────────────────────────────────────────┐
│                  Event                       │
├─────────────────────────────────────────────┤
│ + Id : int                                  │
│ + Title : string                            │
│ + Description : string?                     │
│ + StartTime : DateTime                      │
│ + EndTime : DateTime?                       │
│ + Location : string?                        │
│ + Priority : Priority (enum)                │
│ + ReminderOffset : int                      │
│ + IsAllDay : bool                           │
│ + IsLunar : bool                            │
│ + RecurrenceRule : string?                  │
│ + IsDeleted : bool                          │
│ + DeletedAt : DateTime?                     │
│ + CreatedAt : DateTime                      │
│ + UpdatedAt : DateTime                      │
├─────────────────────────────────────────────┤
│ + Event(title, startTime)                   │
│ + Validate() : void                         │
│ + OverlapsWith(other: Event) : bool         │
│ + GetDuration() : TimeSpan?                 │
│ + IsUrgent(threshold: int) : bool           │
│ + CanRestore() : bool                       │
│ + CreateReminder() : Reminder?              │
├─────────────────────────────────────────────┤
│ - ValidateTitle() : void                    │
│ - ValidateTimeRange() : void                │
│ - CalculateUrgency() : UrgencyLevel         │
└─────────────────────────────────────────────┘
```

**Priority 枚举定义**：

```csharp
namespace AI_Calendar.Core.Entities;

/// <summary>
/// 事件优先级
/// </summary>
public enum Priority
{
    /// <summary>低优先级（普通，默认值）</summary>
    Low = 0,

    /// <summary>中等优先级</summary>
    Medium = 1,

    /// <summary>高优先级（重要）</summary>
    High = 2
}

/// <summary>
/// Priority 扩展方法
/// </summary>
public static class PriorityExtensions
{
    /// <summary>
    /// 获取优先级对应的显示颜色
    /// </summary>
    public static string GetColor(this Priority priority)
    {
        return priority switch
        {
            Priority.Low => "#FFFFFF",      // 白色
            Priority.Medium => "#FFD700",   // 金色
            Priority.High => "#FF4500",      // 橙红色
            _ => "#FFFFFF"
        };
    }

    /// <summary>
    /// 获取优先级对应的中文标签
    /// </summary>
    public static string GetLabel(this Priority priority)
    {
        return priority switch
        {
            Priority.Low => "普通",
            Priority.Medium => "中等",
            Priority.High => "重要",
            _ => "普通"
        };
    }

    /// <summary>
    /// 判断是否为高优先级（重要或紧急）
    /// </summary>
    public static bool IsHighPriority(this Priority priority)
    {
        return priority == Priority.High;
    }
}
```

**关键方法设计**:

```csharp
/// <summary>
/// 检查与另一个事件是否时间重叠
/// </summary>
public bool OverlapsWith(Event other)
{
    if (other == null) return false;

    // 全天事件总是被认为重叠（如果同一天）
    if (this.IsAllDay && other.IsAllDay)
    {
        return this.StartTime.Date == other.StartTime.Date;
    }

    // 计算实际结束时间
    var thisEnd = this.EndTime ?? this.StartTime.AddHours(1);
    var otherEnd = other.EndTime ?? other.StartTime.AddHours(1);

    // 时间重叠判断逻辑
    return this.StartTime < otherEnd && thisEnd > other.StartTime;
}

/// <summary>
/// 检查事件是否紧急（在指定分钟数内开始）
/// </summary>
public bool IsUrgent(int thresholdMinutes = 60)
{
    var timeUntilStart = StartTime - DateTime.Now;
    return timeUntilStart.TotalMinutes > 0 &&
           timeUntilStart.TotalMinutes <= thresholdMinutes;
}

/// <summary>
/// 创建提醒记录
/// </summary>
public Reminder? CreateReminder()
{
    if (ReminderOffset <= 0) return null;

    var remindTime = StartTime.AddMinutes(-ReminderOffset);

    // 如果提醒时间已过，不创建提醒
    if (remindTime < DateTime.Now) return null;

    return new Reminder
    {
        EventId = this.Id,
        RemindTime = remindTime,
        IsNotified = false,
        RetryCount = 0
    };
}
```

#### 2.1.2 OperationLog 实体类

**命名空间**: `AI_Calendar.Core.Entities`

**职责**: 记录所有 MCP 操作的审计日志

**类图**:

```
┌─────────────────────────────────────────────┐
│              OperationLog                    │
├─────────────────────────────────────────────┤
│ + Id : int                                  │
│ + ToolName : string                         │
│ + Params : string (JSON)                    │
│ + Result : string                           │
│ + ErrorCode : string?                       │
│ + ErrorMessage : string?                    │
│ + ExecutionTime : long? (ms)                │
│ + Timestamp : DateTime                      │
│ + UserId : string?                          │
├─────────────────────────────────────────────┤
│ + RecordSuccess(toolName, params) : Log     │
│ + RecordError(toolName, params, error) : Log│
│ + GetExecutionTime() : TimeSpan?            │
│ - SanitizeParams() : string                 │
└─────────────────────────────────────────────┘
```

#### 2.1.3 领域异常类

**命名空间**: `AI_Calendar.Core.Exceptions`

```
┌──────────────────────┐       ┌──────────────────────┐
│ DomainException      │       │ EventConflictException│
│ <<abstract>>         │◄──────┤ (时间冲突异常)        │
├──────────────────────┤       ├──────────────────────┤
│ + Message : string   │       │ + ConflictingEvents  │
│ + ErrorCode : string │       └──────────────────────┘
└──────────────────────┘
         △                                △
         │                                │
         └──────────────┬─────────────────┘
                        │
    ┌───────────────────┴─────────────────┐
    │                                     │
┌───┴────────┐                    ┌───────┴────────┐
│Validation  │                    │ NotFoundException│
│Exception   │                    │ (资源未找到)      │
├────────────┤                    ├─────────────────┤
│+Validation │                    │+ResourceId     │
│Errors      │                    └─────────────────┘
└────────────┘
```

**警告类设计**：

```csharp
namespace AI_Calendar.Core.Models;

/// <summary>
/// 警告类型枚举
/// </summary>
public enum WarningType
{
    TimeConflict,      // 时间冲突
    NearDuplicate,     // 近似重复
    PastTime          // 过去时间
}

/// <summary>
/// 时间冲突警告
/// </summary>
public class TimeConflictWarning
{
    public WarningType Type { get; set; } = WarningType.TimeConflict;
    public string Message { get; set; }
    public int? ConflictingEventId { get; set; }
    public DateTime? ConflictingTime { get; set; }

    public TimeConflictWarning(Event conflictingEvent)
    {
        ConflictingEventId = conflictingEvent.Id;
        ConflictingTime = conflictingEvent.StartTime;
        Message = $"与现有事件「{conflictingEvent.Title}」时间冲突";
    }
}
```

**异常类设计**:

```csharp
/// <summary>
/// 领域异常基类
/// </summary>
public abstract class DomainException : Exception
{
    public string ErrorCode { get; }

    protected DomainException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected DomainException(string message, string errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// 事件时间冲突异常
/// </summary>
public class EventConflictException : DomainException
{
    public List<Event> ConflictingEvents { get; }

    public EventConflictException(List<Event> conflictingEvents)
        : base(
            $"检测到 {conflictingEvents.Count} 个时间冲突的事件",
            "EVENT_CONFLICT"
        )
    {
        ConflictingEvents = conflictingEvents;
    }
}

/// <summary>
/// 验证异常
/// </summary>
public class ValidationException : DomainException
{
    public Dictionary<string, string> ValidationErrors { get; }

    public ValidationException(Dictionary<string, string> errors)
        : base(
            $"数据验证失败，{errors.Count} 个字段无效",
            "VALIDATION_FAILED"
        )
    {
        ValidationErrors = errors;
    }
}

/// <summary>
/// 资源未找到异常
/// </summary>
public class NotFoundException : DomainException
{
    public object ResourceId { get; }
    public string ResourceType { get; }

    public NotFoundException(string resourceType, object id)
        : base(
            $"{resourceType} (ID: {id}) 未找到",
            "NOT_FOUND"
        )
    {
        ResourceType = resourceType;
        ResourceId = id;
    }
}
```

---

### 2.2 应用层模块 (Application Layer)

#### 2.2.0 ViewModel 接口定义

**命名空间**: `AI_Calendar.Application.ViewModels`

**IWidgetViewModel 接口**：

```csharp
namespace AI_Calendar.Application.ViewModels;

/// <summary>
/// 桌面挂件 ViewModel 接口
/// </summary>
public interface IWidgetViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// 即将到来的事件列表（显示在Widget上）
    /// </summary>
    ObservableCollection<EventModel> UpcomingEvents { get; }

    /// <summary>
    /// 当前日期显示
    /// </summary>
    string CurrentDateDisplay { get; }

    /// <summary>
    /// 农历日期显示
    /// </summary>
    string LunarDateDisplay { get; }

    /// <summary>
    /// 当前时间显示
    /// </summary>
    string CurrentTimeDisplay { get; }

    /// <summary>
    /// 是否处于隐私模式
    /// </summary>
    bool IsPrivacyModeEnabled { get; set; }

    /// <summary>
    /// 刷新事件列表
    /// </summary>
    Task RefreshAsync();

    /// <summary>
    /// 切换隐私模式
    /// </summary>
    void TogglePrivacyMode();
}
```

**ISettingsViewModel 接口**：

```csharp
namespace AI_Calendar.Application.ViewModels;

/// <summary>
/// 设置窗口 ViewModel 接口
/// </summary>
public interface ISettingsViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// 所有事件列表
    /// </summary>
    ObservableCollection<EventModel> AllEvents { get; }

    /// <summary>
    /// 回收站事件列表
    /// </summary>
    ObservableCollection<EventModel> DeletedEvents { get; }

    /// <summary>
    /// 应用设置
    /// </summary>
    AppSettingModel Settings { get; }

    /// <summary>
    /// 创建新事件
    /// </summary>
    Task<EventModel> CreateEventAsync(EventDto dto);

    /// <summary>
    /// 更新事件
    /// </summary>
    Task<EventModel> UpdateEventAsync(int id, EventDto changes);

    /// <summary>
    /// 删除事件
    /// </summary>
    Task DeleteEventAsync(int id, bool confirm);

    /// <summary>
    /// 恢复已删除事件
    /// </summary>
    Task RestoreEventAsync(int id);

    /// <summary>
    /// 保存设置
    /// </summary>
    Task SaveSettingsAsync();

    /// <summary>
    /// 同步节假日数据
    /// </summary>
    Task SyncHolidaysAsync();

    /// <summary>
    /// 清空回收站
    /// </summary>
    Task EmptyRecycleBinAsync();
}
```

#### 2.2.1 EventService 业务服务

**命名空间**: `AI_Calendar.Application.Services`

**职责**: 处理事件相关的所有业务逻辑

**类图**:

```
┌─────────────────────────────────────────────┐
│              EventService                   │
│         <<implements IEventService>>       │
├─────────────────────────────────────────────┤
│ - _eventRepository : IEventRepository       │
│ - _logRepository : IOperationLogRepository │
│ - _messenger : IMessenger                  │
│ - _logger : ILogger<EventService>          │
├─────────────────────────────────────────────┤
│ + SearchAsync(query, start, end) : Task<List<Event>> │
│ + CreateAsync(dto) : Task<Event>           │
│ + UpdateAsync(id, changes) : Task<Event>   │
│ + SoftDeleteAsync(id, confirm) : Task      │
│ + GetFreeTimeAsync(duration, date) : Task<List<TimeSlot>> │
│ + GetUpcomingEvents(count) : Task<List<EventDisplayModel>> │
├─────────────────────────────────────────────┤
│ - ValidateEventDto(dto) : void             │
│ - DetectConflicts(evt) : List<Event>       │
│ - LogOperation(toolName, params, result) : Task │
│ - PublishEventCreated(evt) : void          │
│ - PublishEventUpdated(evt) : void          │
│ - PublishEventDeleted(id) : void           │
└─────────────────────────────────────────────┘
```

**关键方法实现**:

```csharp
/// <summary>
/// 创建事件（带冲突检测和审计日志）
/// </summary>
/// <returns>创建的事件和警告列表</returns>
public async Task<(Event createdEvent, List<TimeConflictWarning> warnings)> CreateAsync(EventDto dto)
{
    // 1. 验证输入
    ValidateEventDto(dto);

    // 2. 创建实体
    var evt = new Event(dto.Title, dto.StartTime)
    {
        Description = dto.Description,
        EndTime = dto.EndTime,
        Location = dto.Location,
        Priority = dto.Priority,
        ReminderOffset = dto.ReminderOffset,
        IsAllDay = dto.IsAllDay,
        IsLunar = dto.IsLunar,
        RecurrenceRule = dto.RecurrenceRule
    };

    // 3. 冲突检测（在保存前检测）
    var conflicts = await DetectConflicts(evt);
    var warnings = conflicts.Select(c => new TimeConflictWarning(c)).ToList();

    // 4. 保存到数据库
    var created = await _eventRepository.AddAsync(evt);

    // 5. 创建提醒记录
    if (created.ReminderOffset > 0)
    {
        var reminder = created.CreateReminder();
        if (reminder != null)
        {
            await _reminderRepository.AddAsync(reminder);
        }
    }

    // 6. 记录审计日志
    await LogOperation("create_event",
        JsonSerializer.Serialize(dto),
        $"Success (ID: {created.Id})");

    // 7. 发布事件（通知UI更新）
    _messenger.Publish(AppEvent.EventCreated, created);

    // 8. 返回结果（包含事件和警告）
    return (created, warnings);
}

/// <summary>
/// 检测事件时间冲突
/// </summary>
private async Task<List<Event>> DetectConflicts(Event evt)
{
    var conflicts = await _eventRepository.GetConflictingEventsAsync(
        evt.StartTime,
        evt.EndTime,
        excludeEventId: null
    );

    return conflicts;
}

/// <summary>
/// 软删除事件（带二次确认）
/// </summary>
public async Task SoftDeleteAsync(int id, bool confirm)
{
    // 1. 验证确认
    if (!confirm)
    {
        throw new ValidationException(
            new Dictionary<string, string>
            {
                ["confirm"] = "删除操作需要确认参数 confirm=true"
            }
        );
    }

    // 2. 检查事件是否存在
    var evt = await _eventRepository.GetByIdAsync(id);
    if (evt == null)
    {
        throw new NotFoundException("Event", id);
    }

    // 3. 执行软删除
    await _eventRepository.SoftDeleteAsync(id);

    // 4. 删除相关提醒记录
    await _reminderRepository.DeleteByEventIdAsync(id);

    // 5. 记录审计日志
    await LogOperation("delete_event",
        JsonSerializer.Serialize(new { id, confirm }),
        $"Success (deleted {evt.Title})");

    // 6. 发布事件（通知UI更新）
    _messenger.Publish(AppEvent.EventDeleted, id);
}
```

#### 2.2.2 MessengerHub 消息总线

**命名空间**: `AI_Calendar.Application.Messenger`

**职责**: 实现发布-订阅模式，解耦模块间通信

**设计原理**：

当 MCP Server 接收 LLM 指令修改数据库后，需要通知 WPF UI 更新显示。本项目采用**应用层消息总线（IMessenger）**实现发布-订阅模式。

**核心优势**：
- **解耦性**：MCP Server 不需要知道 UI 的存在，只发布消息
- **可测试性**：可以单元测试消息发布和订阅
- **可扩展性**：新增订阅者（如日志记录、统计）无需修改发布者
- **类型安全**：使用泛型保证消息类型安全

**IMessenger 接口定义**：

```csharp
namespace AI_Calendar.Application.Messenger;

/// <summary>
/// 应用内消息总线接口
/// 实现发布-订阅模式，用于模块间解耦通信
/// </summary>
public interface IMessenger
{
    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="T">消息载荷类型</typeparam>
    /// <param name="event">要订阅的事件类型</param>
    /// <param name="handler">事件处理器</param>
    void Subscribe<T>(AppEvent @event, Action<T> handler);

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="T">消息载荷类型</typeparam>
    /// <param name="event">事件类型</param>
    /// <param name="payload">消息载荷</param>
    void Publish<T>(AppEvent @event, T payload);

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <typeparam name="T">消息载荷类型</typeparam>
    /// <param name="event">事件类型</param>
    /// <param name="handler">事件处理器</param>
    void Unsubscribe<T>(AppEvent @event, Action<T> handler);
}
```

**AppEvent 枚举定义**：

```csharp
namespace AI_Calendar.Application.Messenger;

/// <summary>
/// 应用事件枚举
/// </summary>
public enum AppEvent
{
    /// <summary>事件创建时触发</summary>
    /// <para>用途: 通知Widget更新显示，提醒服务创建提醒任务</para>
    EventCreated,

    /// <summary>事件更新时触发</summary>
    /// <para>用途: 通知Widget更新显示，提醒服务更新提醒时间</para>
    EventUpdated,

    /// <summary>事件删除时触发</summary>
    /// <para>用途: 通知Widget移除显示，提醒服务删除提醒任务</para>
    EventDeleted,

    /// <summary>设置变更时触发</summary>
    /// <para>用途: 通知所有设置相关的ViewModel重新加载配置</para>
    SettingsChanged,

    /// <summary>刷新请求时触发</summary>
    /// <para>用途: 用户手动刷新时触发，重新加载数据</para>
    RefreshRequested,

    /// <summary>隐私模式切换时触发</summary>
    /// <para>用途: 通知Widget更新显示模式（显示/隐藏事件标题）</para>
    PrivacyModeToggled,

    /// <summary>节假日数据更新时触发</summary>
    /// <para>用途: 通知Widget重新计算工作日、显示节假日信息</para>
    HolidayDataUpdated,

    /// <summary>提醒触发时触发</summary>
    /// <para>用途: 用于提醒服务通知UI</para>
    ReminderTriggered,

    /// <summary>数据库恢复时触发</summary>
    /// <para>用途: 用于备份恢复后通知UI刷新</para>
    DatabaseRestored
}
```

**数据流向图**：

```
用户操作 (UI/MCP)
    ↓
Presentation Layer (ViewModels)
    ↓
Application Layer (Services)
    ↓        ↓
Domain Layer (Entities)   IMessenger.Publish()
    ↓                          ↓
Infrastructure Layer    AppEvent 消息
    ↓                          ↓
Data Store (SQLite)      ViewModels 订阅者
                              ↓
                        WPF UI 更新
```

**MessengerHub 类图**:

```
┌─────────────────────────────────────────────┐
│              MessengerHub                   │
│         <<implements IMessenger>>          │
├─────────────────────────────────────────────┤
│ - _subscribers : Dictionary<AppEvent, List<Delegate>> │
│ - _lock : ReaderWriterLockSlim             │
├─────────────────────────────────────────────┤
│ + Subscribe<T>(event, handler) : void      │
│ + Publish<T>(event, payload) : void        │
│ + Unsubscribe<T>(event, handler) : void    │
│ + Clear() : void                           │
│ - GetSubscribers(event) : List<Delegate>   │
└─────────────────────────────────────────────┘
```

**实现**:

```csharp
/// <summary>
/// 内存消息总线（单进程模式）
/// </summary>
public class MessengerHub : IMessenger
{
    private readonly Dictionary<AppEvent, List<Delegate>> _subscribers = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ILogger<MessengerHub> _logger;

    public MessengerHub(ILogger<MessengerHub> logger)
    {
        _logger = logger;
    }

    public void Subscribe<T>(AppEvent @event, Action<T> handler)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_subscribers.ContainsKey(@event))
            {
                _subscribers[@event] = new List<Delegate>();
            }

            _subscribers[@event].Add(handler);

            _logger.LogDebug("订阅事件: {Event}, 处理器: {Handler}",
                @event, handler.Method.Name);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Publish<T>(AppEvent @event, T payload)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_subscribers.ContainsKey(@event))
            {
                _logger.LogTrace("发布事件但无订阅者: {Event}", @event);
                return;
            }

            // 复制订阅者列表，避免在持有锁时调用
            var handlers = _subscribers[@event].ToList();

            foreach (var handler in handlers)
            {
                try
                {
                    var action = (Action<T>)handler;
                    action(payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "事件处理器执行失败: {Event}, Handler: {Handler}",
                        @event, handler.Method.Name);
                }
            }

            _logger.LogDebug("发布事件: {Event}, 订阅者数: {Count}",
                @event, handlers.Count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Unsubscribe<T>(AppEvent @event, Action<T> handler)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_subscribers.ContainsKey(@event))
            {
                _subscribers[@event].Remove(handler);

                _logger.LogDebug("取消订阅: {Event}, Handler: {Handler}",
                    @event, handler.Method.Name);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _subscribers.Clear();
            _logger.LogInformation("清除所有事件订阅");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}

/// <summary>
/// 应用事件枚举
/// </summary>
public enum AppEvent
{
    EventCreated,        // 事件创建
    EventUpdated,        // 事件更新
    EventDeleted,        // 事件删除
    SettingsChanged,     // 设置变更
    RefreshRequested,    // 刷新请求
    PrivacyModeToggled,  // 隐私模式切换
    HolidayDataUpdated,  // 节假日数据更新
    ReminderTriggered,   // 提醒触发
    DatabaseRestored     // 数据库恢复
}
```

---

### 2.3 基础设施层模块 (Infrastructure Layer)

#### 2.3.1 MCP Server 实现

**命名空间**: `AI_Calendar.Infrastructure.MCP`

**职责**: 实现 MCP 协议，处理 LLM 请求

**类图**:

```
┌─────────────────────────────────────────────┐
│               McpServer                     │
├─────────────────────────────────────────────┤
│ - _eventService : IEventService             │
│ - _tools : Dictionary<string, IMcpTool>     │
│ - _resources : Dictionary<string, IMcpResource> │
│ - _prompts : Dictionary<string, IMcpPrompt> │
│ - _logger : ILogger<McpServer>              │
├─────────────────────────────────────────────┤
│ + StartAsync() : Task                       │
│ + StopAsync() : Task                        │
│ - HandleRequestAsync(request) : Task<Response> │
│ - RegisterTools() : void                    │
│ - RegisterResources() : void                │
│ - RegisterPrompts() : void                  │
└─────────────────────────────────────────────┘
```

**MCP 工具基类**:

```csharp
/// <summary>
/// MCP 工具基类
/// </summary>
public abstract class McpToolBase : IMcpTool
{
    protected readonly ILogger _logger;
    protected readonly IEventService _eventService;
    protected readonly IOperationLogRepository _logRepository;

    protected McpToolBase(
        IEventService eventService,
        IOperationLogRepository logRepository,
        ILogger logger)
    {
        _eventService = eventService;
        _logRepository = logRepository;
        _logger = logger;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonObject InputSchema { get; }

    public abstract Task<JsonNode> ExecuteAsync(JsonObject arguments);

    /// <summary>
    /// 记录操作日志
    /// </summary>
    protected async Task LogOperationAsync(
        string paramsJson,
        string result,
        string? errorCode = null,
        string? errorMessage = null)
    {
        var log = new OperationLog
        {
            ToolName = Name,
            Params = paramsJson,
            Result = result,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.Now
        };

        await _logRepository.AddAsync(log);
    }

    /// <summary>
    /// 构建成功响应
    /// </summary>
    protected JsonNode SuccessResponse(object data)
    {
        return new JsonObject
        {
            ["success"] = true,
            ["data"] = JsonSerializer.SerializeToNode(data)
        };
    }

    /// <summary>
    /// 构建错误响应
    /// </summary>
    protected JsonNode ErrorResponse(string code, string message)
    {
        return new JsonObject
        {
            ["success"] = false,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }
}
```

**SearchEventsTool 实现**:

```csharp
/// <summary>
/// 搜索事件工具 (MCP-02)
/// </summary>
public class SearchEventsTool : McpToolBase
{
    public override string Name => "search_events";
    public override string Description => "搜索指定时间范围内的事件（支持关键词匹配）";

    public override JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "搜索关键词（匹配标题、地点、描述）"
            },
            ["start"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date-time",
                ["description"] = "开始时间（ISO 8601格式）"
            },
            ["end"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date-time",
                ["description"] = "结束时间（ISO 8601格式）"
            },
            ["limit"] = new JsonObject
            {
                ["type"] = "integer",
                ["default"] = 20,
                ["minimum"] = 1,
                ["maximum"] = 100
            }
        },
        ["required"] = new JsonArray { "start", "end" }
    };

    public SearchEventsTool(
        IEventService eventService,
        IOperationLogRepository logRepository,
        ILogger<SearchEventsTool> logger)
        : base(eventService, logRepository, logger)
    {
    }

    public override async Task<JsonNode> ExecuteAsync(JsonObject arguments)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // 1. 解析参数
            var query = arguments.TryGetValue("query", out var q) ? q.GetValue<string>() : null;
            var startStr = arguments["start"].GetValue<string>();
            var endStr = arguments["end"].GetValue<string>();
            var limit = arguments.TryGetValue("limit", out var l) ? l.GetValue<int>() : 20;

            // 2. 验证时间格式
            if (!DateTime.TryParse(startStr, out var start))
            {
                return ErrorResponse("INVALID_DATE_FORMAT", "开始时间格式错误");
            }
            if (!DateTime.TryParse(endStr, out var end))
            {
                return ErrorResponse("INVALID_DATE_FORMAT", "结束时间格式错误");
            }

            // 3. 执行搜索
            var events = await _eventService.SearchAsync(query, start, end);

            // 4. 限制返回数量
            var result = events.Take(limit).Select(e => new
            {
                id = e.Id,
                title = e.Title,
                description = e.Description,
                startTime = e.StartTime.ToString("yyyy-MM-ddTHH:mm:ssK"),
                endTime = e.EndTime?.ToString("yyyy-MM-ddTHH:mm:ssK"),
                location = e.Location,
                priority = e.Priority.ToString(),
                isAllDay = e.IsAllDay
            }).ToList();

            // 5. 记录日志
            sw.Stop();
            await LogOperationAsync(
                JsonSerializer.Serialize(arguments),
                $"Success (found {result.Count} events)",
                executionTime: sw.ElapsedMilliseconds
            );

            // 6. 返回结果
            return SuccessResponse(new
            {
                events = result,
                total = events.Count,
                returned = result.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索事件失败");

            await LogOperationAsync(
                JsonSerializer.Serialize(arguments),
                "Error",
                errorCode: "INTERNAL_ERROR",
                errorMessage: ex.Message
            );

            return ErrorResponse("INTERNAL_ERROR", ex.Message);
        }
    }
}
```

#### 2.3.2 SQLite 数据访问实现

**命名空间**: `AI_Calendar.Infrastructure.Data`

**职责**: 实现 EF Core 数据访问

**AppDbContext 配置**:

```csharp
using System.Reflection;

/// <summary>
/// 应用数据库上下文
/// </summary>
public class AppDbContext : DbContext
{
    private readonly string _dbPath;

    public AppDbContext()
    {
        // 数据库路径：程序目录/data.db
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeDir = Path.GetDirectoryName(exePath);
        var dbDir = Path.Combine(exeDir!);

        // 确保目录存在
        Directory.CreateDirectory(dbDir);

        _dbPath = Path.Combine(dbDir, "data.db");
    }

    public DbSet<Event> Events { get; set; }
    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<OperationLog> OperationLogs { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }
    public DbSet<HolidayData> HolidayData { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 配置 SQLite 连接
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");

        // 启用敏感数据日志（仅开发环境）
        #if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
        #endif

        // 配置性能优化
        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.EnableSensitiveDataLogging(false);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. 配置软删除全局过滤器
        modelBuilder.Entity<Event>().HasQueryFilter(e => !e.IsDeleted);

        // 2. 配置索引
        modelBuilder.Entity<Event>()
            .HasIndex(e => new { e.IsDeleted, e.StartTime })
            .HasDatabaseName("IX_Events_Upcoming");

        modelBuilder.Entity<Event>()
            .HasIndex(e => e.StartTime)
            .HasDatabaseName("IX_Events_StartTime");

        modelBuilder.Entity<Reminder>()
            .HasIndex(r => new { r.IsNotified, r.RemindTime })
            .HasDatabaseName("IX_Reminders_Pending");

        modelBuilder.Entity<OperationLog>()
            .HasIndex(l => l.Timestamp)
            .IsDescending()
            .HasDatabaseName("IX_OperationLogs_Timestamp");

        modelBuilder.Entity<HolidayData>()
            .HasIndex(h => h.Date)
            .IsUnique()
            .HasDatabaseName("UX_HolidayData_Date");

        // 3. 配置关系
        modelBuilder.Entity<Reminder>()
            .HasOne(r => r.Event)
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // 4. 配置值转换
        modelBuilder.Entity<Event>()
            .Property(e => e.Priority)
            .HasConversion(
                p => p.ToString(),
                p => (Priority)Enum.Parse(typeof(Priority), p)
            );
    }
}
```

**EventRepository 实现**:

```csharp
/// <summary>
/// 事件仓储实现
/// </summary>
public class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<EventRepository> _logger;

    public EventRepository(AppDbContext context, ILogger<EventRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Event?> GetByIdAsync(int id)
    {
        return await _context.Events
            .IgnoreQueryFilters()  // 包含已删除的事件
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Event> AddAsync(Event evt)
    {
        // 验证实体
        evt.Validate();

        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        _logger.LogDebug("添加事件: ID={Id}, Title={Title}", evt.Id, evt.Title);

        return evt;
    }

    public async Task<Event> UpdateAsync(Event evt)
    {
        // 验证实体
        evt.Validate();

        var existing = await _context.Events
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == evt.Id);

        if (existing == null)
        {
            throw new NotFoundException("Event", evt.Id);
        }

        // 更新字段
        _context.Entry(existing).CurrentValues.SetValues(evt);
        existing.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        _logger.LogDebug("更新事件: ID={Id}, Title={Title}", evt.Id, evt.Title);

        return existing;
    }

    public async Task SoftDeleteAsync(int id)
    {
        var evt = await _context.Events
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (evt == null)
        {
            throw new NotFoundException("Event", id);
        }

        evt.IsDeleted = true;
        evt.DeletedAt = DateTime.Now;
        evt.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        _logger.LogDebug("软删除事件: ID={Id}, Title={Title}", evt.Id, evt.Title);
    }

    public async Task<List<Event>> SearchAsync(
        string? query,
        DateTime? start,
        DateTime? end,
        bool includeDeleted = false)
    {
        var queryable = _context.Events;

        if (!includeDeleted)
        {
            queryable = queryable.Where(e => !e.IsDeleted);
        }
        else
        {
            queryable = queryable.IgnoreQueryFilters();
        }

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(query))
        {
            queryable = queryable.Where(e =>
                e.Title.Contains(query) ||
                (e.Location != null && e.Location.Contains(query)) ||
                (e.Description != null && e.Description.Contains(query))
            );
        }

        // 时间范围过滤
        if (start.HasValue)
        {
            queryable = queryable.Where(e => e.StartTime >= start.Value);
        }

        if (end.HasValue)
        {
            queryable = queryable.Where(e => e.StartTime <= end.Value);
        }

        return await queryable
            .OrderBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<List<Event>> GetUpcomingEventsAsync(DateTime now, int count)
    {
        return await _context.Events
            .Where(e => e.StartTime > now)
            .OrderBy(e => e.StartTime)
            .Take(count)
            .ToListAsync();
    }

    public async Task<bool> HasConflictAsync(DateTime start, DateTime? end, int? excludeEventId = null)
    {
        var query = _context.Events
            .Where(e => e.Id != excludeEventId);

        var actualEnd = end ?? start.AddHours(1);

        var conflicts = await query
            .Where(e => e.StartTime < actualEnd &&
                       (e.EndTime ?? e.StartTime.AddHours(1)) > start)
            .CountAsync();

        return conflicts > 0;
    }

    public async Task<List<Event>> GetConflictingEventsAsync(DateTime start, DateTime? end, int? excludeEventId = null)
    {
        var query = _context.Events
            .Where(e => e.Id != excludeEventId);

        var actualEnd = end ?? start.AddHours(1);

        return await query
            .Where(e => e.StartTime < actualEnd &&
                       (e.EndTime ?? e.StartTime.AddHours(1)) > start)
            .ToListAsync();
    }
}
```

---

### 2.4 表示层模块 (Presentation Layer)

#### 2.4.1 DesktopWidgetViewModel

**命名空间**: `AI_Calendar.Presentation.ViewModels`

**职责**: 桌面挂件的视图模型

**类图**:

```
┌─────────────────────────────────────────────┐
│         DesktopWidgetViewModel              │
│          <<ObservableObject>>              │
├─────────────────────────────────────────────┤
│ - _eventService : IEventService             │
│ - _messenger : IMessenger                   │
│ - _configService : IConfigurationService    │
│ - _timer : DispatcherTimer                  │
├─────────────────────────────────────────────┤
│ + CurrentDate : string                      │
│ + CurrentTime : string                      │
│ + LunarDate : string                        │
│ + Weekday : string                          │
│ + UpcomingEvents : ObservableCollection<EventDisplayModel> │
│ + RemainingEventsCount : int                │
│ + IsPrivacyMode : bool                      │
│ + Opacity : double                          │
├─────────────────────────────────────────────┤
│ + InitializeAsync() : Task                  │
│ + TogglePrivacyMode() : void                │
│ + RefreshEvents() : Task                    │
│ - UpdateDateTime() : void                   │
│ - OnEventCreated(evt) : void                │
│ - OnEventUpdated(evt) : void                │
│ - OnEventDeleted(id) : void                 │
└─────────────────────────────────────────────┘
```

**实现**:

```csharp
/// <summary>
/// 桌面挂件视图模型
/// </summary>
public partial class DesktopWidgetViewModel : ObservableObject, IDisposable
{
    private readonly IEventService _eventService;
    private readonly IMessenger _messenger;
    private readonly IConfigurationService _configService;
    private readonly DispatcherTimer _dateTimeTimer;
    private readonly ILunarCalendarService _lunarService;

    [ObservableProperty]
    private string _currentDate = string.Empty;

    [ObservableProperty]
    private string _currentTime = string.Empty;

    [ObservableProperty]
    private string _lunarDate = string.Empty;

    [ObservableProperty]
    private string _weekday = string.Empty;

    [ObservableProperty]
    private ObservableCollection<EventDisplayModel> _upcomingEvents = new();

    [ObservableProperty]
    private int _remainingEventsCount;

    [ObservableProperty]
    private bool _isPrivacyMode;

    [ObservableProperty]
    private double _opacity = 0.9;

    public DesktopWidgetViewModel(
        IEventService eventService,
        IMessenger messenger,
        IConfigurationService configService,
        ILunarCalendarService lunarService)
    {
        _eventService = eventService;
        _messenger = messenger;
        _configService = configService;
        _lunarService = lunarService;

        // 初始化时间更新定时器（每秒更新）
        _dateTimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _dateTimeTimer.Tick += (s, e) => UpdateDateTime();
        _dateTimeTimer.Start();

        // 订阅消息
        _messenger.Subscribe<Event>(AppEvent.EventCreated, OnEventCreated);
        _messenger.Subscribe<Event>(AppEvent.EventUpdated, OnEventUpdated);
        _messenger.Subscribe<int>(AppEvent.EventDeleted, OnEventDeleted);
        _messenger.Subscribe<bool>(AppEvent.PrivacyModeToggled, OnPrivacyModeToggled);

        // 初始化
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // 加载配置
        var config = await _configService.LoadAsync();
        Opacity = config.Widget.Opacity;
        IsPrivacyMode = config.Widget.PrivacyMode;

        // 初始更新时间
        UpdateDateTime();

        // 加载事件列表
        await RefreshEvents();
    }

    private void UpdateDateTime()
    {
        var now = DateTime.Now;

        CurrentDate = now.ToString("MM月dd日");
        CurrentTime = now.ToString("HH:mm:ss");
        Weekday = now.ToString("dddd", new CultureInfo("zh-CN"));

        // 获取农历
        var lunar = _lunarService.GetLunarDate(now);
        LunarDate = $"农历{lunar.MonthName}{lunar.DayName}";
    }

    private async Task RefreshEvents()
    {
        try
        {
            var events = await _eventService.GetUpcomingEvents(3);
            var allEvents = await _eventService.GetTodayEventsAsync();

            // 更新显示的事件
            UpcomingEvents.Clear();

            foreach (var evt in events)
            {
                var model = new EventDisplayModel(evt)
                {
                    IsPrivacyMode = IsPrivacyMode
                };
                UpcomingEvents.Add(model);
            }

            RemainingEventsCount = Math.Max(0, allEvents.Count - 3);
        }
        catch (Exception ex)
        {
            // 静默处理错误，避免影响UI显示
            Console.WriteLine($"刷新事件失败: {ex.Message}");
        }
    }

    private void OnEventCreated(Event evt)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            await RefreshEvents();
        });
    }

    private void OnEventUpdated(Event evt)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            // 更新现有事件或重新加载
            await RefreshEvents();
        });
    }

    private void OnEventDeleted(int eventId)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            // 移除已删除的事件
            var toRemove = UpcomingEvents.FirstOrDefault(e => e.Id == eventId);
            if (toRemove != null)
            {
                UpcomingEvents.Remove(toRemove);
                RemainingEventsCount = Math.Max(0, RemainingEventsCount - 1);
            }
        });
    }

    private void OnPrivacyModeToggled(bool enabled)
    {
        IsPrivacyMode = enabled;

        // 更新所有事件显示
        foreach (var evt in UpcomingEvents)
        {
            evt.IsPrivacyMode = enabled;
        }
    }

    public void TogglePrivacyMode()
    {
        IsPrivacyMode = !IsPrivacyMode;
        _messenger.Publish(AppEvent.PrivacyModeToggled, IsPrivacyMode);

        // 保存配置
        _ = _configService.UpdatePrivacyModeAsync(IsPrivacyMode);
    }

    public void Dispose()
    {
        _dateTimeTimer.Stop();
        _dateTimeTimer.Tick -= (s, e) => UpdateDateTime();

        _messenger.Unsubscribe<Event>(AppEvent.EventCreated, OnEventCreated);
        _messenger.Unsubscribe<Event>(AppEvent.EventUpdated, OnEventUpdated);
        _messenger.Unsubscribe<int>(AppEvent.EventDeleted, OnEventDeleted);
        _messenger.Unsubscribe<bool>(AppEvent.PrivacyModeToggled, OnPrivacyModeToggled);
    }
}
```

---

## 3. 核心类设计

### 3.1 实体类完整定义

```csharp
namespace AI_Calendar.Core.Entities;

/// <summary>
/// 日历事件实体
/// </summary>
public class Event
{
    public int Id { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public string? Location { get; private set; }
    public Priority Priority { get; private set; }
    public int ReminderOffset { get; private set; }
    public bool IsAllDay { get; private set; }
    public bool IsLunar { get; private set; }
    public string? RecurrenceRule { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // 导航属性
    public List<Warning> Warnings { get; set; } = new();

    protected Event() { }

    public Event(string title, DateTime startTime)
    {
        Title = title;
        StartTime = startTime;
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
        Priority = Priority.Medium;
        ReminderOffset = 0;
        IsAllDay = false;
        IsLunar = false;
        IsDeleted = false;
    }

    public void Validate()
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(Title))
        {
            errors["title"] = "标题不能为空";
        }
        else if (Title.Length > 200)
        {
            errors["title"] = "标题不能超过200个字符";
        }

        if (EndTime.HasValue && EndTime.Value <= StartTime)
        {
            errors["endTime"] = "结束时间必须晚于开始时间";
        }

        if (errors.Any())
        {
            throw new ValidationException(errors);
        }
    }

    public bool OverlapsWith(Event other)
    {
        if (other == null) return false;

        if (this.IsAllDay && other.IsAllDay)
        {
            return this.StartTime.Date == other.StartTime.Date;
        }

        var thisEnd = this.EndTime ?? this.StartTime.AddHours(1);
        var otherEnd = other.EndTime ?? other.StartTime.AddHours(1);

        return this.StartTime < otherEnd && thisEnd > other.StartTime;
    }

    public TimeSpan? GetDuration()
    {
        if (!EndTime.HasValue) return null;
        return EndTime.Value - StartTime;
    }

    public bool IsUrgent(int thresholdMinutes = 60)
    {
        var timeUntilStart = StartTime - DateTime.Now;
        return timeUntilStart.TotalMinutes > 0 &&
               timeUntilStart.TotalMinutes <= thresholdMinutes;
    }

    public bool CanRestore()
    {
        if (!IsDeleted || !DeletedAt.HasValue) return false;

        var daysSinceDeletion = (DateTime.Now - DeletedAt.Value).Days;
        return daysSinceDeletion <= 7;
    }

    public Reminder? CreateReminder()
    {
        if (ReminderOffset <= 0) return null;

        var remindTime = StartTime.AddMinutes(-ReminderOffset);

        if (remindTime < DateTime.Now) return null;

        return new Reminder
        {
            EventId = this.Id,
            RemindTime = remindTime,
            IsNotified = false,
            RetryCount = 0
        };
    }
}

/// <summary>
/// 优先级枚举
/// </summary>
public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>
/// 紧急程度枚举
/// </summary>
public enum UrgencyLevel
{
    Immediate = 1,  // 15分钟内
    Soon = 2,       // 1小时内
    Normal = 3      // 正常
}
```

---

## 4. 时序图设计

### 4.1 创建事件时序图

```
用户/LLM         MCP Server      EventService      EventRepository      AppDbContext      Messenger        ViewModel
    |                 |                |                  |                  |                 |               |
    |-- create_event->|                |                  |                  |                 |               |
    |                 |-- ValidateDto->|                  |                  |                 |               |
    |                 |                |-- DetectConflicts()             |                  |                 |               |
    |                 |                |<-- conflicts ---|                  |                 |               |
    |                 |                |-- AddAsync----->|                  |                 |               |
    |                 |                |                  |-- Events.Add--->|                 |               |
    |                 |                |                  |<-- SaveChanges--|                 |               |
    |                 |                |<-- created -----|                  |                 |               |
    |                 |                |-- CreateReminder()               |                 |               |
    |                 |                |-- LogOperation()                 |                 |               |
    |                 |                |-- Publish(EventCreated)--------->|---------------->|               |
    |                 |                |                  |                  |                 |-- OnEventCreated->|
    |                 |<-- success ----|                  |                  |                 |               |
    |<-- response ----|                |                  |                  |                 |               |
    |                 |                |                  |                  |                 |<- RefreshEvents|
```

### 4.2 搜索事件时序图

```
Claude/LLM       MCP Server      SearchEventsTool    EventService      EventRepository      AppDbContext
    |                 |                |                  |                  |                  |
    |-- search_events>|                |                  |                  |                  |
    |                 |-- ExecuteAsync>|                  |                  |                  |
    |                 |                |-- ParseArguments|                  |                  |
    |                 |                |-- SearchAsync-->|                  |                  |
    |                 |                |                  |-- SearchAsync-->|                  |
    |                 |                |                  |                  |-- SQL Query----->|
    |                 |                |                  |                  |<-- Events-------|
    |                 |                |                  |<-- events-------|                  |
    |                 |                |<-- events -------|                  |                  |
    |                 |                |-- LogOperation()                 |                  |
    |                 |<-- result -----|                  |                  |                  |
    |<-- events-------|                |                  |                  |                  |
```

### 4.3 UI 同步更新时序图

```
MCP Tool      EventService      EventRepository      AppDbContext      Messenger      ViewModel          UI
    |               |                  |                  |                 |                |            |
    |-- CreateAsync>|                  |                  |                 |                |            |
    |               |-- AddAsync----->|                  |                 |                |            |
    |               |                  |-- Events.Add--->|                 |                |            |
    |               |                  |<-- SaveChanges--|                 |                |            |
    |               |<-- created -----|                  |                 |                |            |
    |               |-- Publish(EventCreated)----------->|                 |                |            |
    |               |                  |                  |                 |-- OnEventCreated->|
    |               |                  |                  |                 |                |-- UI Update->|
    |<-- return -----|                  |                  |                 |                |            |
```

---

## 5. 关键算法设计

### 5.1 时间冲突检测算法

```csharp
/// <summary>
/// 时间冲突检测算法
/// </summary>
public class TimeConflictDetector
{
    /// <summary>
    /// 检测新事件与现有事件的冲突
    /// </summary>
    public static List<ConflictResult> DetectConflicts(
        Event newEvent,
        List<Event> existingEvents)
    {
        var conflicts = new List<ConflictResult>();

        foreach (var existing in existingEvents)
        {
            if (newEvent.OverlapsWith(existing))
            {
                var overlap = CalculateOverlap(newEvent, existing);

                conflicts.Add(new ConflictResult
                {
                    ConflictingEvent = existing,
                    OverlapDuration = overlap,
                    OverlapPercentage = CalculateOverlapPercentage(newEvent, overlap),
                    Severity = DetermineSeverity(overlap)
                });
            }
        }

        return conflicts.OrderBy(c => c.OverlapDuration).ToList();
    }

    /// <summary>
    /// 计算重叠时长
    /// </summary>
    private static TimeSpan CalculateOverlap(Event evt1, Event evt2)
    {
        var start = Max(evt1.StartTime, evt2.StartTime);
        var end1 = evt1.EndTime ?? evt1.StartTime.AddHours(1);
        var end2 = evt2.EndTime ?? evt2.StartTime.AddHours(1);
        var end = Min(end1, end2);

        return end - start;
    }

    /// <summary>
    /// 计算重叠百分比
    /// </summary>
    private static double CalculateOverlapPercentage(Event evt, TimeSpan overlap)
    {
        var duration = evt.GetDuration() ?? TimeSpan.FromHours(1);
        return (overlap.TotalMinutes / duration.TotalMinutes) * 100;
    }

    /// <summary>
    /// 确定冲突严重程度
    /// </summary>
    private static ConflictSeverity DetermineSeverity(TimeSpan overlap)
    {
        if (overlap.TotalMinutes >= 30) return ConflictSeverity.Severe;
        if (overlap.TotalMinutes >= 15) return ConflictSeverity.Moderate;
        return ConflictSeverity.Minor;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}

/// <summary>
/// 冲突结果
/// </summary>
public class ConflictResult
{
    public Event ConflictingEvent { get; set; }
    public TimeSpan OverlapDuration { get; set; }
    public double OverlapPercentage { get; set; }
    public ConflictSeverity Severity { get; set; }
}

public enum ConflictSeverity
{
    Minor,      // < 15分钟重叠
    Moderate,   // 15-30分钟重叠
    Severe      // >= 30分钟重叠
}
```

### 5.2 空闲时间查找算法

```csharp
/// <summary>
/// 空闲时间查找算法
/// </summary>
public class FreeTimeFinder
{
    private readonly TimeSpan _workDayStart = TimeSpan.FromHours(9);
    private readonly TimeSpan _workDayEnd = TimeSpan.FromHours(18);

    /// <summary>
    /// 查找指定日期的空闲时间段
    /// </summary>
    public List<TimeSlot> FindFreeSlots(
        DateTime date,
        TimeSpan requiredDuration,
        List<Event> existingEvents,
        bool workHoursOnly = true)
    {
        var slots = new List<TimeSlot>();

        // 1. 确定搜索范围
        var searchStart = workHoursOnly
            ? date.Date + _workDayStart
            : date.Date;

        var searchEnd = workHoursOnly
            ? date.Date + _workDayEnd
            : date.Date.AddDays(1).AddTicks(-1);

        // 2. 过滤并排序当天的事件
        var dayEvents = existingEvents
            .Where(e => e.StartTime.Date == date.Date && !e.IsDeleted)
            .OrderBy(e => e.StartTime)
            .ToList();

        // 3. 查找事件之间的空隙
        var currentStart = searchStart;

        foreach (var evt in dayEvents)
        {
            var eventEnd = evt.EndTime ?? evt.StartTime.AddHours(1);

            // 检查当前事件前的空隙
            if (evt.StartTime > currentStart)
            {
                var gapDuration = evt.StartTime - currentStart;

                if (gapDuration >= requiredDuration)
                {
                    slots.Add(new TimeSlot
                    {
                        Start = currentStart,
                        End = evt.StartTime,
                        Duration = gapDuration
                    });
                }
            }

            // 更新当前时间指针
            if (eventEnd > currentStart)
            {
                currentStart = eventEnd;
            }
        }

        // 4. 检查最后一个事件后的空隙
        if (currentStart < searchEnd)
        {
            var gapDuration = searchEnd - currentStart;

            if (gapDuration >= requiredDuration)
            {
                slots.Add(new TimeSlot
                {
                    Start = currentStart,
                    End = searchEnd,
                    Duration = gapDuration
                });
            }
        }

        // 5. 按持续时间排序（优先推荐较长的时段）
        return slots.OrderByDescending(s => s.Duration).ToList();
    }
}

/// <summary>
/// 时间段
/// </summary>
public class TimeSlot
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public TimeSpan Duration { get; set; }

    public override string ToString()
    {
        return $"{Start:HH:mm} - {End:HH:mm} ({Duration.TotalMinutes:F0}分钟)";
    }
}
```

### 5.3 提醒调度算法

```csharp
/// <summary>
/// 提醒调度器
/// </summary>
public class ReminderScheduler : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Timer _timer;
    private readonly ILogger<ReminderScheduler> _logger;

    public ReminderScheduler(IServiceProvider serviceProvider, ILogger<ReminderScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // 每分钟检查一次
        _timer = new Timer(CheckReminders, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    private async void CheckReminders(object? state)
    {
        using var scope = _serviceProvider.CreateScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IToastNotificationService>();

        try
        {
            // 1. 获取需要提醒的事件
            var dueReminders = await reminderService.GetDueRemindersAsync(DateTime.Now);

            foreach (var reminder in dueReminders)
            {
                try
                {
                    // 2. 检查全屏应用（避免打扰）
                    if (IsFullScreenAppRunning())
                    {
                        _logger.LogInformation("检测到全屏应用，跳过提醒: {EventId}", reminder.EventId);
                        continue;
                    }

                    // 3. 发送通知
                    await notificationService.ShowAsync(reminder.Event);

                    // 4. 标记为已通知
                    await reminderService.MarkAsNotifiedAsync(reminder.Id);

                    _logger.LogInformation("已发送提醒: EventId={EventId}, Title={Title}",
                        reminder.EventId, reminder.Event.Title);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送提醒失败: EventId={EventId}", reminder.EventId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查提醒失败");
        }
    }

    /// <summary>
    /// 检测是否有全屏应用运行
    /// </summary>
    private bool IsFullScreenAppRunning()
    {
        try
        {
            // Windows API 调用检测全屏应用
            var hwnd = GetForegroundWindow();
            var rect = new RECT();
            GetWindowRect(hwnd, ref rect);

            var screenWidth = GetSystemMetrics(0);
            var screenHeight = GetSystemMetrics(1);

            return rect.right - rect.left == screenWidth &&
                   rect.bottom - rect.top == screenHeight;
        }
        catch
        {
            return false;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("提醒调度器已启动");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
        _logger.LogInformation("提醒调度器已停止");
        return Task.CompletedTask;
    }

    // Windows API 导入
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, ref RECT rect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
}
```

---

## 6. 异常处理机制

### 6.1 全局异常处理

```csharp
/// <summary>
/// 全局异常处理器
/// </summary>
public class GlobalExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;

        // 订阅未处理异常事件
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        Dispatcher.CurrentDispatcher.UnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            _logger.LogCritical(ex, "未处理的异常: IsTerminating={IsTerminating}", e.IsTerminating);

            // 记录到文件
            LogToFile(ex, "UnhandledException");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Dispatcher 未处理异常");

        // 记录到文件
        LogToFile(e.Exception, "DispatcherException");

        // 防止应用程序崩溃
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "未观察到的任务异常");

        // 记录到文件
        LogToFile(e.Exception, "TaskException");

        // 防止应用程序崩溃
        e.SetObserved();
    }

    private void LogToFile(Exception ex, string category)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopAICalendar",
                "Logs"
            );

            Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            var content = $"[{category}] {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                         $"Message: {ex.Message}\n" +
                         $"StackTrace: {ex.StackTrace}\n";

            if (ex.InnerException != null)
            {
                content += $"Inner: {ex.InnerException.Message}\n";
            }

            File.WriteAllText(logFile, content);
        }
        catch
        {
            // 忽略日志记录错误
        }
    }
}
```

### 6.2 异常处理策略

| 异常类型 | 处理策略 | 用户提示 |
|:---|:---|:---|
| **DomainException** | 记录警告，返回友好错误信息 | 显示业务错误提示 |
| **ValidationException** | 返回字段级别的错误信息 | 在UI字段下显示错误 |
| **NotFoundException** | 返回404或空结果 | "未找到相关数据" |
| **EventConflictException** | 返回冲突详情 | "检测到时间冲突，是否继续？" |
| **SqlException** | 记录错误，尝试重试 | "数据库操作失败，请重试" |
| **Exception** | 记录错误，返回通用信息 | "操作失败，请联系管理员" |

---

## 7. 性能优化方案

### 7.1 数据库优化

**1. 索引策略**:

```sql
-- 组合索引（用于即将到来的事件查询）
CREATE INDEX IX_Events_Upcoming ON Events(IsDeleted, StartTime ASC)
WHERE IsDeleted = 0;

-- 覆盖索引（减少回表查询）
CREATE INDEX IX_Events_Covering ON Events(StartTime, Title, Priority, IsAllDay)
WHERE IsDeleted = 0;

-- 全文搜索索引
CREATE VIRTUAL TABLE EventsFTS USING fts5(
    Title, Description,
    content=Events,
    content_rowid=Id
);
```

**2. 查询优化**:

```csharp
// ❌ 低效：返回所有字段
var events = await _context.Events.ToListAsync();

// ✅ 高效：只返回需要的字段
var events = await _context.Events
    .Where(e => e.StartTime > now)
    .Select(e => new EventDisplayModel
    {
        Id = e.Id,
        Title = e.Title,
        StartTime = e.StartTime,
        Priority = e.Priority
    })
    .Take(10)
    .ToListAsync();
```

**3. 连接池配置**:

```csharp
services.AddDbContextPool<AppDbContext>(options =>
{
    options.UseSqlite("Data Source=data.db");
}, poolSize: 5);
```

### 7.2 UI 渲染优化

**1. 虚拟化长列表**:

```xaml
<!-- 使用 VirtualizingStackPanel 优化长列表 -->
<ListBox ItemsSource="{Binding UpcomingEvents}">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
    <ListBox.VirtualizingStackPanel.IsVirtualizing="True">
        <VirtualizationMode="Recycling" />
    </ListBox.VirtualizingStackPanel.IsVirtualizing>
</ListBox>
```

**2. 冻结可冻结对象**:

```csharp
// 冻结画刷和笔刷，提升渲染性能
var whiteBrush = new SolidColorBrush(Colors.White);
whiteBrush.Freeze();

var textPen = new Pen(Brushes.Black, 1);
textPen.Freeze();
```

**3. 延迟加载**:

```csharp
// 延迟加载非关键资源
protected override async void OnActivated(EventArgs e)
{
    base.OnActivated(e);

    // 立即加载关键数据
    await LoadCriticalDataAsync();

    // 延迟加载次要数据
    _ = Task.Run(async () => await LoadSecondaryDataAsync());
}
```

### 7.3 内存优化

**1. 对象池**:

```csharp
/// <summary>
/// 事件显示模型对象池
/// </summary>
public class EventModelPool
{
    private readonly ConcurrentBag<EventDisplayModel> _pool = new();

    public EventDisplayModel Rent(Event evt)
    {
        if (_pool.TryTake(out var model))
        {
            model.UpdateFrom(evt);
            return model;
        }

        return new EventDisplayModel(evt);
    }

    public void Return(EventDisplayModel model)
    {
        model.Reset();
        _pool.Add(model);
    }
}
```

**2. 弱引用事件**:

```csharp
// 使用弱引用避免内存泄漏
private readonly List<WeakReference<Action<Event>>> _subscribers = new();

public void Subscribe(Action<Event> handler)
{
    _subscribers.Add(new WeakReference<Action<Event>>(handler));
}
```

---

## 8. UI组件详细设计

### 8.1 桌面挂件窗口

```xaml
<!-- DesktopWidget.xaml -->
<Window x:Class="AI_Calendar.Presentation.Views.DesktopWidget"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:AI_Calendar.Presentation.ViewModels"
        AllowsTransparency="True"
        WindowStyle="None"
        ShowInTaskbar="False"
        Topmost="False"
        Background="Transparent"
        Window.X="100"
        Window.Y="100"
        Width="300"
        Height="400">

    <Window.Resources>
        <!-- 阴影效果 -->
        <DropShadowEffect x:Key="TextShadow"
                         Color="Black"
                         BlurRadius="2"
                         ShadowDepth="1"
                         Direction="315"
                         Opacity="0.7"/>
    </Window.Resources>

    <Grid>
        <!-- 背景层（可选，用于调试） -->
        <Border Background="Transparent"
                BorderBrush="Transparent"
                BorderThickness="1"/>

        <!-- 内容层 -->
        <StackPanel Margin="20" Opacity="{Binding Opacity}">
            <!-- 日期时间 -->
            <TextBlock Text="{Binding CurrentDate}"
                       FontSize="24"
                       FontWeight="Bold"
                       Foreground="White"
                       Effect="{StaticResource TextShadow}"/>

            <TextBlock Text="{Binding Weekday}"
                       FontSize="14"
                       Foreground="LightGray"
                       Effect="{StaticResource TextShadow}"/>

            <TextBlock Text="{Binding LunarDate}"
                       FontSize="12"
                       Foreground="Gray"
                       Effect="{StaticResource TextShadow}"/>

            <Separator Margin="0,10"/>

            <!-- 事件列表 -->
            <ItemsControl ItemsSource="{Binding UpcomingEvents}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Margin="0,5">
                            <TextBlock FontSize="14"
                                       Foreground="White"
                                       Effect="{StaticResource TextShadow}">
                                <Run Text="●"
                                     Foreground="{Binding PriorityColor}"/>
                                <Run Text="{Binding StartTime, StringFormat='{}{0:HH:mm} '}"/>
                                <Run Text="{Binding DisplayTitle}"/>
                            </TextBlock>
                        </StackPanel>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <!-- 剩余事件 -->
            <TextBlock Margin="0,10,0,0"
                       FontSize="12"
                       Foreground="LightBlue"
                       Text="{Binding RemainingEventsCount, StringFormat='+ 还有 {0} 个事件'}"
                       Effect="{StaticResource TextShadow}"/>
        </StackPanel>
    </Grid>
</Window>
```

### 8.2 设置窗口

```xaml
<!-- SettingsWindow.xaml -->
<Window x:Class="AI_Calendar.Presentation.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Desktop AI Calendar - 设置"
        Width="800"
        Height="600"
        WindowStartupLocation="CenterScreen">

    <Grid>
        <TabControl>
            <!-- 事件管理 -->
            <TabItem Header="事件管理">
                <ListView ItemsSource="{Binding AllEvents}">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn Header="标题" DisplayMemberBinding="{Binding Title}" Width="200"/>
                            <GridViewColumn Header="开始时间" DisplayMemberBinding="{Binding StartTime, StringFormat='{}{0:yyyy-MM-dd HH:mm}'}" Width="150"/>
                            <GridViewColumn Header="优先级" DisplayMemberBinding="{Binding Priority}" Width="80"/>
                            <GridViewColumn Header="操作" Width="100">
                                <GridViewColumn.CellTemplate>
                                    <DataTemplate>
                                        <StackPanel Orientation="Horizontal">
                                            <Button Content="编辑" Command="{Binding EditCommand}"/>
                                            <Button Content="删除" Command="{Binding DeleteCommand}"/>
                                        </StackPanel>
                                    </DataTemplate>
                                </GridViewColumn.CellTemplate>
                            </GridViewColumn>
                        </GridView>
                    </ListView.View>
                </ListView>
            </TabItem>

            <!-- 外观设置 -->
            <TabItem Header="外观">
                <StackPanel Margin="20">
                    <TextBlock Text="透明度:"/>
                    <Slider Minimum="0.5" Maximum="1.0"
                            Value="{Binding Opacity}"
                            TickFrequency="0.1"
                            IsSnapToTickEnabled="True"/>

                    <TextBlock Text="字体大小:" Margin="0,10,0,0"/>
                    <Slider Minimum="10" Maximum="20"
                            Value="{Binding FontSize}"
                            TickFrequency="1"
                            IsSnapToTickEnabled="True"/>

                    <CheckBox Content="隐私模式（隐藏事件详情）"
                              IsChecked="{Binding IsPrivacyMode}"
                              Margin="0,10,0,0"/>
                </StackPanel>
            </TabItem>

            <!-- 回收站 -->
            <TabItem Header="回收站">
                <ListView ItemsSource="{Binding DeletedEvents}">
                    <ListView.View>
                        <GridView>
                            <GridViewColumn Header="标题" DisplayMemberBinding="{Binding Title}" Width="200"/>
                            <GridViewColumn Header="删除时间" DisplayMemberBinding="{Binding DeletedAt, StringFormat='{}{0:yyyy-MM-dd HH:mm}'}" Width="150"/>
                            <GridViewColumn Header="剩余天数" DisplayMemberBinding="{Binding DaysUntilExpiry}" Width="80"/>
                            <GridViewColumn Header="操作" Width="100">
                                <GridViewColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="恢复" Command="{Binding RestoreCommand}"/>
                                    </DataTemplate>
                                </GridViewColumn.CellTemplate>
                            </GridViewColumn>
                        </GridView>
                    </ListView.View>
                </ListView>
            </TabItem>
        </TabControl>
    </Grid>
</Window>
```

---

## 9. 配置管理设计

### 9.1 配置文件结构

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data.db"
  },
  "Widget": {
    "Opacity": 0.9,
    "FontSize": 14,
    "PositionX": 100,
    "PositionY": 100,
    "PrivacyMode": false
  },
  "Reminder": {
    "Enabled": true,
    "DefaultOffset": 15,
    "SoundEnabled": true
  },
  "System": {
    "AutoStart": false,
    "Language": "zh-CN",
    "Theme": "light",
    "CheckUpdates": true
  },
  "MCP": {
    "Enabled": true,
    "Port": 37281,
    "Transport": "stdio"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### 9.2 配置服务实现

```csharp
/// <summary>
/// 配置服务
/// </summary>
public interface IConfigurationService
{
    Task<AppConfig> LoadAsync();
    Task SaveAsync(AppConfig config);
    Task UpdateWidgetPositionAsync(int x, int y);
    Task UpdatePrivacyModeAsync(bool enabled);
}

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IMessenger _messenger;

    public ConfigurationService(ILogger<ConfigurationService> logger, IMessenger messenger)
    {
        _logger = logger;
        _messenger = messenger;

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopAICalendar"
        );

        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "appsettings.json");
    }

    public async Task<AppConfig> LoadAsync()
    {
        if (!File.Exists(_configPath))
        {
            return GetDefaultConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return config ?? GetDefaultConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载配置失败，使用默认配置");
            return GetDefaultConfig();
        }
    }

    public async Task SaveAsync(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configPath, json);

            _logger.LogInformation("配置已保存");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置失败");
        }
    }

    public async Task UpdateWidgetPositionAsync(int x, int y)
    {
        var config = await LoadAsync();
        config.Widget.PositionX = x;
        config.Widget.PositionY = y;
        await SaveAsync(config);
    }

    public async Task UpdatePrivacyModeAsync(bool enabled)
    {
        var config = await LoadAsync();
        config.Widget.PrivacyMode = enabled;
        await SaveAsync(config);

        // 发布配置变更消息
        _messenger.Publish(AppEvent.PrivacyModeToggled, enabled);
    }

    private AppConfig GetDefaultConfig()
    {
        return new AppConfig
        {
            Widget = new WidgetConfig
            {
                Opacity = 0.9,
                FontSize = 14,
                PositionX = 100,
                PositionY = 100,
                PrivacyMode = false
            },
            Reminder = new ReminderConfig
            {
                Enabled = true,
                DefaultOffset = 15,
                SoundEnabled = true
            },
            System = new SystemConfig
            {
                AutoStart = false,
                Language = "zh-CN",
                Theme = "light",
                CheckUpdates = true
            },
            MCP = new McpConfig
            {
                Enabled = true,
                Port = 37281,
                Transport = "stdio"
            }
        };
    }
}
```

---

## 10. 日志与监控设计

### 10.1 日志配置

```csharp
/// <summary>
/// 日志配置
/// </summary>
public static class LoggingConfiguration
{
    public static IHostBuilder ConfigureLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopAICalendar",
                "Logs",
                "log-.txt"
            );

            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "DesktopAICalendar")
                .WriteTo.Debug()
                .WriteTo.Console()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                );
        });
    }
}
```

### 10.2 审计日志记录

```csharp
/// <summary>
/// 审计日志记录器
/// </summary>
public class AuditLogger
{
    private readonly IOperationLogRepository _repository;

    public AuditLogger(IOperationLogRepository repository)
    {
        _repository = repository;
    }

    public async Task LogAsync(
        string toolName,
        object? parameters,
        string result,
        string? errorCode = null,
        string? errorMessage = null,
        long? executionTime = null)
    {
        var log = new OperationLog
        {
            ToolName = toolName,
            Params = parameters != null ? JsonSerializer.Serialize(parameters) : "{}",
            Result = result,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ExecutionTime = executionTime,
            Timestamp = DateTime.Now
        };

        await _repository.AddAsync(log);
    }
}
```

---

## 11. 部署与运维设计

### 11.1 安装程序设计

```xml
<!-- Installer.wxs (WiX) -->
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*"
           Name="Desktop AI Calendar"
           Language="1033"
           Version="1.0.0.0"
           Manufacturer="AI Calendar Team">

    <Package InstallerVersion="200" Compressed="yes" InstallScope="perUser" />

    <Media Id="1" Cabinet="app.cab" EmbedCab="yes" />

    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="LocalAppDataFolder">
        <Directory Id="INSTALLFOLDER" Name="DesktopAICalendar" />
      </Directory>

      <Directory Id="ProgramMenuFolder">
        <Directory Id="ApplicationProgramsFolder" Name="DesktopAICalendar"/>
      </Directory>
    </Directory>

    <ComponentGroup Id="ProductComponents" Directory="INSTALLFOLDER">
      <Component Id="Executable">
        <File Source="AI_Calendar.exe" />
      </Component>
      <Component Id="Configuration">
        <File Source="appsettings.json" />
      </Component>
    </ComponentGroup>

    <DirectoryRef Id="ApplicationProgramsFolder">
      <Component Id="ApplicationShortcut">
        <Shortcut Id="ApplicationStartMenuShortcut"
                  Name="Desktop AI Calendar"
                  Description="AI-powered desktop calendar"
                  Target="[INSTALLFOLDER]AI_Calendar.exe"
                  WorkingDirectory="INSTALLFOLDER"/>
        <RemoveFolder Id="ApplicationProgramsFolder" On="uninstall"/>
        <RegistryValue Root="HKCU" Key="Software\DesktopAICalendar" Name="installed" Type="integer" Value="1" KeyPath="yes"/>
      </Component>
    </DirectoryRef>

    <Feature Id="ProductFeature" Title="Desktop AI Calendar" Level="1">
      <ComponentGroupRef Id="ProductComponents" />
      <ComponentRef Id="ApplicationShortcut" />
    </Feature>
  </Product>
</Wix>
```

### 11.2 自动更新机制

```csharp
/// <summary>
/// 自动更新服务
/// </summary>
public class UpdateService : IHostedService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly Timer _timer;

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;

        // 每天检查一次更新
        _timer = new Timer(CheckForUpdates, null,
            TimeSpan.Zero,
            TimeSpan.FromDays(1));
    }

    private async void CheckForUpdates(object? state)
    {
        try
        {
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var latestVersion = await GetLatestVersionAsync();

            if (latestVersion > currentVersion)
            {
                _logger.LogInformation("发现新版本: {Version}", latestVersion);

                // 通知用户
                await NotifyUpdateAvailableAsync(latestVersion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查更新失败");
        }
    }

    private async Task<Version?> GetLatestVersionAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "DesktopAICalendar");

        var response = await client.GetStringAsync(
            "https://api.github.com/repos/your-org/AI_Calendar/releases/latest");

        using var json = JsonDocument.Parse(response);
        var tagName = json.RootElement.GetProperty("tag_name").GetString();

        return Version.Parse(tagName.TrimStart('v'));
    }

    private async Task NotifyUpdateAvailableAsync(Version version)
    {
        // 使用 Windows Toast 通知
        await Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("更新服务已启动");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
        return Task.CompletedTask;
    }
}
```

---

## 附录

### A. 命名规范

| 类型 | 规范 | 示例 |
|:---|:---|:---|
| **命名空间** | PascalCase | `AI_Calendar.Application.Services` |
| **类名** | PascalCase | `EventService` |
| **接口** | I + PascalCase | `IEventService` |
| **方法** | PascalCase | `CreateAsync` |
| **属性** | PascalCase | `StartTime` |
| **私有字段** | _camelCase | `_eventService` |
| **常量** | PascalCase | `MaxRetryCount` |
| **局部变量** | camelCase | `eventCount` |

### B. 文件组织规范

```
AI_Calendar/
├── Core/                          # 领域层
│   ├── Entities/                  # 实体
│   ├── Interfaces/                # 仓储接口
│   └── Exceptions/                # 领域异常
├── Application/                   # 应用层
│   ├── Services/                  # 业务服务
│   ├── DTOs/                      # 数据传输对象
│   └── Messenger/                 # 消息总线
├── Infrastructure/                # 基础设施层
│   ├── Data/                      # 数据访问
│   ├── MCP/                       # MCP 服务
│   ├── Native/                    # 原生服务
│   └── External/                  # 外部服务
├── Presentation/                  # 表示层
│   ├── Views/                     # 视图
│   └── ViewModels/                # 视图模型
└── Services/                      # 后台服务
    ├── ReminderBackgroundService/
    └── HolidayUpdateBackgroundService/
```

### C. 性能指标

| 指标 | 目标值 | 监控方法 |
|:---|:---|:---|
| **内存占用** | < 50MB | 性能计数器 |
| **CPU 占用** | < 1% | 性能计数器 |
| **启动时间** | < 3s | 日志记录 |
| **查询响应** | < 100ms | 日志记录 |
| **数据库大小** | < 100MB | 定期检查 |

---

## 12. 版本历史 (Version History)

### V1.1 (2026-03-11)

**数据库路径更新**：
- 数据库路径从 `%APPDATA%/DesktopAICalendar/data.db` 改为 `程序目录/data.db`
- 使用 `Assembly.GetExecutingAssembly().Location` 获取程序目录
- 更新AppDbContext构造函数实现

**AppEvent枚举更新**：
- 统一为9个枚举值
- 新增 `ReminderTriggered`：提醒触发时触发
- 新增 `DatabaseRestored`：数据库恢复时触发
- 统一所有AppEvent定义为9个值

**CreateAsync方法确认**：
- 返回类型保持为 `(Event createdEvent, List<TimeConflictWarning> warnings)`
- 冲突检测返回warnings数组，不抛出异常（符合API设计）
- 与API-Interface-Design.md V1.3保持一致

**决策依据**：
- 与Database-Design.md V1.2、API-Interface-Design.md V1.3保持一致

### V1.0 (2026-03-11)

**初始版本**：
- 模块详细设计
- 核心类设计
- 时序图设计
- 关键算法设计
- 异常处理机制
- 性能优化方案
- UI组件详细设计
- 配置管理设计
- 日志与监控设计
- 部署与运维设计

---

**文档结束**

> 本详细设计说明书提供了 Desktop AI Calendar 项目的完整实现细节，包括模块设计、类结构、时序图、算法、异常处理、性能优化等方面，为开发团队提供全面的实现指导。
