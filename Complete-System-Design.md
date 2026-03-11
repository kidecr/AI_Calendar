# Desktop AI Calendar 完整系统设计文档
# Desktop AI Calendar (DAC)

| 文档版本 | **V1.2** (同步冲突解决方案) |
|:---|:---|
| **项目名称** | Desktop AI Calendar |
| **作者** | AI Assistant |
| **创建日期** | 2026-03-11 |
| **最后更新** | 2026-03-11 |
| **整合文档** | Module-Interface-Design.md V1.0 + System-Module-Design.md V1.0 |
| **依赖文档** | System-Architecture.md V1.5, PRD.md V1.3, Database-Design.md V1.2, API-Interface-Design.md V1.3 |

---

## 目录 (Table of Contents)

1. [文档对比分析](#1-文档对比分析)
2. [系统架构概览](#2-系统架构概览)
3. [核心模块设计](#3-核心模块设计)
4. [完整接口定义](#4-完整接口定义)
5. [数据模型设计](#5-数据模型设计)
6. [业务服务设计](#6-业务服务设计)
7. [基础设施设计](#7-基础设施设计)
8. [表示层设计](#8-表示层设计)
9. [外部服务集成](#9-外部服务集成)
10. [配置与依赖注入](#10-配置与依赖注入)
11. [实现优先级](#11-实现优先级)
12. [测试策略](#12-测试策略)

---

## 1. 文档对比分析

### 1.1 两文档功能对比表

| 对比维度 | Module-Interface-Design.md | System-Module-Design.md |
|:---|:---|:---|
| **核心焦点** | 接口规范定义 | 系统架构实现 |
| **目标受众** | 接口设计者、API 使用者 | 开发实现者、架构师 |
| **详细程度** | 接口级（方法签名、参数、返回值） | 模块级（类实现、依赖关系） |
| **代码示例** | 完整的接口定义代码 | 实现类代码示例 |
| **覆盖范围** | 5 个 Phase + 外部服务 | 领域层、应用层、基础设施层 |
| **设计原则** | 命名规范、参数设计、返回值设计、异常设计 | 分层架构、依赖注入、目录结构 |
| **扩展内容** | 接口设计原则、优先级建议 | 测试策略、实现清单 |

### 1.2 内容互补性分析

| 内容类型 | Module-Interface-Design.md 贡献 | System-Module-Design.md 贡献 | 融合价值 |
|:---|:---|:---|:---|
| **实体定义** | 完整的领域逻辑方法 | 基础属性定义 | 完整的实体模型 |
| **仓储接口** | 25+ 个详细方法定义 | 基础接口定义 | 全面的数据访问抽象 |
| **业务服务** | 完整的 IEventService 接口 | EventService 实现示例 | 接口与实现对应 |
| **MCP 集成** | MCP 工具基类和接口规范 | 具体工具实现示例 | 规范与实例结合 |
| **视图模型** | IWidgetViewModel、ISettingsViewModel | 缺少 | 补充表示层设计 |
| **原生服务** | IToastNotificationService、ISystemHotKeyService | ToastNotificationService 实现 | 接口与实现对应 |
| **后台服务** | IReminderBackgroundService、IHolidayUpdateBackgroundService、IHealthCheckBackgroundService | 缺少 | Infrastructure.BackgroundServices 子模块 |
| **外部服务** | IHolidayService、ILunarCalendarService | 缺少完整接口 | 补充外部集成设计 |

### 1.3 接口覆盖度对比

| 模块层 | Module-Interface-Design.md | System-Module-Design.md | 融合后覆盖度 |
|:---|:---|:---|:---|
| **Domain Layer** | ✅ 完整实体接口 | ✅ 基础实体定义 | **100%** |
| **Application Layer** | ✅ 7 个服务接口 | ✅ 4 个服务实现 | **100%** |
| **Infrastructure - Data** | ✅ 完整仓储接口 | ✅ 仓储实现示例 | **100%** |
| **Infrastructure - MCP** | ✅ MCP 工具基类 | ✅ 具体工具实现 | **100%** |
| **Infrastructure - Native** | ✅ Toast、HotKey 接口 | ✅ Toast 实现 | **100%** |
| **Infrastructure - External** | ✅ Holiday、Lunar 接口 | ❌ 缺少 | **新增** |
| **Infrastructure - Background** | ✅ 3 个后台服务接口 | ❌ 缺少 | **新增** |
| **Presentation Layer** | ✅ 2 个 ViewModel 接口 | ❌ 缺少 | **新增** |

---

## 2. 系统架构概览

### 2.1 整体分层架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Presentation Layer                             │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐     │
│  │  Widget Module   │  │ Settings Module  │  │ TrayIcon Module  │     │
│  │  (桌面挂件)       │  │  (设置窗口)      │  │  (系统托盘)      │     │
│  │                  │  │                  │  │                  │     │
│  │ - IWidgetViewModel│  │-ISettingsViewModel│ │ - TrayIcon      │     │
│  │ - DesktopWidget  │  │ - SettingsWindow │  │   Management    │     │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘     │
└─────────────────────────────────────────────────────────────────────────┘
                                    ↕ IMessenger
┌─────────────────────────────────────────────────────────────────────────┐
│                          Application Layer                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐     │
│  │  Event Service   │  │Reminder Service  │  │Config Service    │     │
│  │  (事件服务)       │  │  (提醒服务)      │  │  (配置服务)      │     │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘     │
│  ┌──────────────────┐  ┌──────────────────┐                           │
│  │  HotKey Service  │  │  Messenger Hub   │  (事件总线)             │
│  │  (热键服务)       │  │                  │                           │
│  └──────────────────┘  └──────────────────┘                           │
└─────────────────────────────────────────────────────────────────────────┘
                                    ↕
┌─────────────────────────────────────────────────────────────────────────┐
│                        Domain Layer (领域层)                             │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐     │
│  │  Event Entity    │  │ Repository       │  │ Domain Events    │     │
│  │  (事件实体)       │  │  Interfaces      │  │  (领域事件)      │     │
│  │                  │  │                  │  │                  │     │
│  │ - Priority       │  │ - IEventRepository│ │ - AppEvent       │     │
│  │ - Conflict Logic │  │ - IOperationLog  │  │   (Enum)        │     │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘     │
└─────────────────────────────────────────────────────────────────────────┘
                                    ↕
┌─────────────────────────────────────────────────────────────────────────┐
│                        Infrastructure Layer                             │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐     │
│  │   Data Access    │  │   MCP Server     │  │  External APIs   │     │
│  │   (SQLite)       │  │   (AI交互)       │  │  (节假日/农历)   │     │
│  │                  │  │                  │  │                  │     │
│  │ - AppDbContext   │  │ - 7 MCP Tools    │  │ - HolidayService │     │
│  │ - EventRepository│  │ - 2 Resources    │  │ - LunarCalendar  │     │
│  ├──────────────────┤  ├──────────────────┤  └──────────────────┘     │
│  │ Background       │  │ Native Services  │                           │
│  │ Services         │  │ (热键/通知)      │                           │
│  │                  │  └──────────────────┘                           │
│  │ - Reminder BgSvc │                                                  │
│  │ - Holiday BgSvc  │  ┌──────────────────┐                           │
│  │ - Health BgSvc   │  │   Logging        │                           │
│  └──────────────────┘  │  (审计日志)      │                           │
│                        └──────────────────┘                           │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 模块依赖关系图

```
Presentation:
  Widget ──┐
  Settings ├─> EventService ──> EventRepository ──> AppDbContext (SQLite)
  TrayIcon ─┤      │                  │                    │
              ├─> ReminderService ──┤                    ├─> OperationLogRepository
              │      │                                  │
              │      └─> ToastNotificationService       └─> AppSettingRepository
              │
              ├─> ConfigurationService ──> AppSettingRepository
              │
              ├─> HotKeyService ──> SystemHotKeyService
              │
              └─> MessengerHub (Event Bus)

Infrastructure:
  MCP Server ──> EventService
             ──> OperationLogRepository

  External Services:
    HolidayService ──> HolidayFileCache
    LunarCalendarService ──> LunarCalendarLibrary

  Background Services (Infrastructure 子模块):
    ReminderBackgroundService ──> ReminderService ──> EventService
    HolidayUpdateBackgroundService ──> HolidayService
    HealthCheckBackgroundService ──> (Database, MCP Server, Cache)
```

### 2.3 数据流图

```
用户操作 (UI/MCP)
    ↓
Presentation Layer (ViewModels)
    ↓
Application Layer (Services) ←→ Messenger (Event Bus)
    ↓                              ↓
Domain Layer (Entities)       Domain Events
    ↓
Infrastructure Layer (Repositories, Background Services, MCP Server)
    ↓
Data Store (SQLite)
```

**说明**：
- Background Services 作为 Infrastructure Layer 的子模块，定期调用 Application Layer 的服务
- 数据流向仍然是单向的：Infrastructure → Application → Domain → Infrastructure (Repositories)

---

## 3. 核心模块设计

### 3.1 领域层模块 (Domain Layer)

#### 3.1.1 实体模块 (Entities)

**命名空间**: `AI_Calendar.Core.Entities`

**主要实体**:
1. **Event** - 日历事件实体
2. **OperationLog** - 操作审计日志
3. **AppSetting** - 应用配置
4. **Reminder** - 提醒记录

#### 3.1.2 仓储接口模块 (Repository Interfaces)

**命名空间**: `AI_Calendar.Core.Interfaces`

**主要接口**:
1. **IEventRepository** - 事件仓储
2. **IOperationLogRepository** - 操作日志仓储
3. **IAppSettingRepository** - 配置仓储

#### 3.1.3 领域异常模块 (Domain Exceptions)

**命名空间**: `AI_Calendar.Core.Exceptions`

**主要异常**:
1. **EventConflictException** - 事件时间冲突异常
2. **ValidationException** - 验证异常
3. **NotFoundException** - 资源未找到异常

---

## 4. 完整接口定义

### 4.1 领域层接口

#### 4.1.1 IEventRepository - 事件仓储接口

**命名空间**: `AI_Calendar.Core.Interfaces`
**文件路径**: `Core/Interfaces/IEventRepository.cs`

```csharp
using AI_Calendar.Core.Entities;

namespace AI_Calendar.Core.Interfaces;

/// <summary>
/// 事件仓储接口
/// 负责事件的持久化和查询操作
/// </summary>
public interface IEventRepository
{
    #region 基本CRUD操作

    /// <summary>
    /// 根据 ID 获取事件
    /// </summary>
    /// <param name="id">事件 ID</param>
    /// <returns>事件实体，未找到则返回 null</returns>
    Task<Event?> GetByIdAsync(int id);

    /// <summary>
    /// 添加新事件
    /// </summary>
    /// <param name="evt">事件实体</param>
    /// <returns>添加后的实体（包含自增 ID）</returns>
    /// <exception cref="InvalidOperationException">实体验证失败时抛出</exception>
    Task<Event> AddAsync(Event evt);

    /// <summary>
    /// 更新事件
    /// </summary>
    /// <param name="evt">事件实体</param>
    /// <returns>更新后的实体</returns>
    /// <exception cref="InvalidOperationException">实体不存在时抛出</exception>
    Task<Event> UpdateAsync(Event evt);

    /// <summary>
    /// 软删除事件
    /// </summary>
    /// <param name="id">事件 ID</param>
    /// <returns>删除的记录数</returns>
    Task SoftDeleteAsync(int id);

    /// <summary>
    /// 永久删除事件 (物理删除)
    /// </summary>
    /// <param name="id">事件 ID</param>
    /// <returns>删除的记录数</returns>
    Task HardDeleteAsync(int id);

    #endregion

    #region 查询操作

    /// <summary>
    /// 搜索事件 (支持关键词和时间范围)
    /// </summary>
    /// <param name="query">搜索关键词 (标题或地点)</param>
    /// <param name="start">开始时间 (可选)</param>
    /// <param name="end">结束时间 (可选)</param>
    /// <param name="includeDeleted">是否包含已删除的事件</param>
    /// <returns>事件列表</returns>
    Task<List<Event>> SearchAsync(
        string? query,
        DateTime? start,
        DateTime? end,
        bool includeDeleted = false);

    /// <summary>
    /// 获取即将到来的事件 (按时间排序)
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <param name="count">获取数量</param>
    /// <returns>事件列表</returns>
    Task<List<Event>> GetUpcomingEventsAsync(DateTime now, int count);

    /// <summary>
    /// 获取需要提醒的事件
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <param name="lookAhead">前瞻时间范围</param>
    /// <returns>需要提醒的事件列表</returns>
    Task<List<Event>> GetDueEventsAsync(DateTime now, TimeSpan lookAhead);

    /// <summary>
    /// 获取指定日期范围内的所有事件
    /// </summary>
    /// <param name="start">开始日期</param>
    /// <param name="end">结束日期</param>
    /// <returns>事件列表</returns>
    Task<List<Event>> GetEventsInRangeAsync(DateTime start, DateTime end);

    /// <summary>
    /// 获取指定日期的所有事件
    /// </summary>
    /// <param name="date">目标日期</param>
    /// <returns>事件列表</returns>
    Task<List<Event>> GetEventsByDateAsync(DateTime date);

    /// <summary>
    /// 获取回收站中的事件 (已删除但未超过7天)
    /// </summary>
    /// <param name="days">保留天数 (默认7天)</param>
    /// <returns>已删除事件列表</returns>
    Task<List<Event>> GetDeletedEventsAsync(int days = 7);

    #endregion

    #region 冲突检测

    /// <summary>
    /// 检查时间范围内是否存在冲突事件
    /// </summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间 (可选)</param>
    /// <param name="excludeEventId">排除的事件 ID (用于更新时排除自身)</param>
    /// <returns>如果存在冲突返回 true</returns>
    Task<bool> HasConflictAsync(DateTime start, DateTime? end, int? excludeEventId = null);

    /// <summary>
    /// 获取与指定时间段冲突的事件列表
    /// </summary>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间 (可选)</param>
    /// <param name="excludeEventId">排除的事件 ID</param>
    /// <returns>冲突事件列表</returns>
    Task<List<Event>> GetConflictingEventsAsync(DateTime start, DateTime? end, int? excludeEventId = null);

    #endregion

    #region 统计操作

    /// <summary>
    /// 统计指定日期范围内的事件数量
    /// </summary>
    /// <param name="start">开始日期</param>
    /// <param name="end">结束日期</param>
    /// <returns>事件数量</returns>
    Task<int> CountEventsAsync(DateTime start, DateTime end);

    /// <summary>
    /// 获取下一个事件的开始时间
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <returns>下一个事件的开始时间，如果无事件则返回 null</returns>
    Task<DateTime?> GetNextEventTimeAsync(DateTime now);

    #endregion
}
```

#### 4.1.2 IOperationLogRepository - 操作日志仓储接口

**命名空间**: `AI_Calendar.Core.Interfaces`
**文件路径**: `Core/Interfaces/IOperationLogRepository.cs`

```csharp
using AI_Calendar.Core.Entities;

namespace AI_Calendar.Core.Interfaces;

/// <summary>
/// 操作审计日志仓储接口
/// </summary>
public interface IOperationLogRepository
{
    /// <summary>
    /// 添加操作日志
    /// </summary>
    /// <param name="log">日志实体</param>
    /// <returns>添加后的日志</returns>
    Task<OperationLog> AddAsync(OperationLog log);

    /// <summary>
    /// 获取最近的日志记录
    /// </summary>
    /// <param name="count">获取数量</param>
    /// <returns>日志列表</returns>
    Task<List<OperationLog>> GetRecentLogsAsync(int count);

    /// <summary>
    /// 获取最近的删除操作日志
    /// </summary>
    /// <param name="timeWindow">时间窗口</param>
    /// <returns>删除操作日志列表</returns>
    Task<List<OperationLog>> GetRecentDeletesAsync(TimeSpan timeWindow);

    /// <summary>
    /// 根据工具名称获取日志
    /// </summary>
    /// <param name="toolName">工具名称 (如 "delete_event")</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <returns>日志列表</returns>
    Task<List<OperationLog>> GetLogsByToolAsync(string toolName, DateTime? start, DateTime? end);

    /// <summary>
    /// 清理过期日志 (保留指定天数)
    /// </summary>
    /// <param name="days">保留天数</param>
    /// <returns>删除的记录数</returns>
    Task<int> CleanupOldLogsAsync(int days);
}
```

---

### 4.2 应用层接口

#### 4.2.1 IEventService - 事件业务服务接口

**命名空间**: `AI_Calendar.Application.Services`
**文件路径**: `Application/Services/IEventService.cs`

```csharp
using AI_Calendar.Core.Entities;

namespace AI_Calendar.Application.Services;

/// <summary>
/// 事件业务服务接口
/// </summary>
public interface IEventService
{
    #region MCP 工具方法

    /// <summary>
    /// 搜索事件 (MCP-02)
    /// </summary>
    Task<List<Event>> SearchAsync(string? query, DateTime? start, DateTime? end);

    /// <summary>
    /// 创建事件 (MCP-03)
    /// </summary>
    /// <exception cref="EventConflictException">时间冲突时抛出</exception>
    Task<Event> CreateAsync(EventDto dto);

    /// <summary>
    /// 更新事件 (MCP-04)
    /// </summary>
    Task<Event> UpdateAsync(int id, EventDto changes);

    /// <summary>
    /// 删除事件 (MCP-05)
    /// </summary>
    Task SoftDeleteAsync(int id, bool confirm);

    /// <summary>
    /// 获取空闲时间段 (MCP-06)
    /// </summary>
    Task<List<TimeSlot>> GetFreeTimeAsync(TimeSpan duration, DateTime date);

    #endregion

    #region UI 业务方法

    /// <summary>
    /// 获取即将到来的事件 (用于 Widget 显示)
    /// </summary>
    Task<List<EventDisplayModel>> GetUpcomingEvents(int count);

    /// <summary>
    /// 获取今日事件列表
    /// </summary>
    Task<List<Event>> GetTodayEventsAsync();

    /// <summary>
    /// 获取指定日期范围内的事件
    /// </summary>
    Task<List<Event>> GetEventsAsync(DateTime start, DateTime end);

    /// <summary>
    /// 获取需要提醒的事件
    /// </summary>
    Task<List<Event>> GetDueEventsAsync(DateTime now, TimeSpan lookAhead);

    /// <summary>
    /// 恢复已删除的事件
    /// </summary>
    Task RestoreEventAsync(int id);

    /// <summary>
    /// 获取回收站中的事件
    /// </summary>
    Task<List<Event>> GetRecycleBinEventsAsync();

    /// <summary>
    /// 清空回收站 (永久删除过期事件)
    /// </summary>
    Task<int> EmptyRecycleBinAsync();

    #endregion

    #region 批量操作

    /// <summary>
    /// 批量创建事件
    /// </summary>
    Task<List<Event>> CreateBatchAsync(List<EventDto> events);

    /// <summary>
    /// 批量删除事件
    /// </summary>
    Task SoftDeleteBatchAsync(List<int> ids, bool confirm);

    #endregion

    #region 统计方法

    /// <summary>
    /// 获取今日事件统计
    /// </summary>
    Task<EventStatistics> GetTodayStatisticsAsync();

    /// <summary>
    /// 获取本周事件统计
    /// </summary>
    Task<EventStatistics> GetWeekStatisticsAsync();

    #endregion
}

#region 数据传输对象

/// <summary>
/// 事件数据传输对象
/// </summary>
public class EventDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Location { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public int ReminderOffset { get; set; } = 15;
    public bool IsLunar { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 时间段
/// </summary>
public class TimeSlot
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public TimeSpan Duration => End - Start;
}

/// <summary>
/// 事件显示模型
/// </summary>
public class EventDisplayModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Location { get; set; }
    public Priority Priority { get; set; }
    public bool IsUrgent { get; set; }
    public string TimeRangeText => EndTime.HasValue
        ? $"{StartTime:HH:mm} - {EndTime:HH:mm}"
        : $"{StartTime:HH:mm}";
}

/// <summary>
/// 事件统计
/// </summary>
public class EventStatistics
{
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int UpcomingCount { get; set; }
    public int OverdueCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

#endregion
```

#### 4.2.2 IReminderService - 提醒服务接口

**命名空间**: `AI_Calendar.Application.Services`
**文件路径**: `Application/Services/IReminderService.cs`

```csharp
namespace AI_Calendar.Application.Services;

/// <summary>
/// 提醒服务接口
/// </summary>
public interface IReminderService
{
    #region 提醒管理

    /// <summary>
    /// 延后提醒 (RM-04)
    /// </summary>
    /// <param name="eventId">事件 ID</param>
    /// <param name="delay">延后时长</param>
    Task SnoozeAsync(int eventId, TimeSpan delay);

    /// <summary>
    /// 确认提醒 (标记为已读)
    /// </summary>
    Task AcknowledgeReminderAsync(int reminderRecordId);

    /// <summary>
    /// 取消提醒
    /// </summary>
    Task CancelReminderAsync(int eventId);

    #endregion

    #region 提醒检查

    /// <summary>
    /// 检查并发送到期提醒
    /// </summary>
    Task CheckAndSendRemindersAsync();

    /// <summary>
    /// 获取待发送的提醒列表
    /// </summary>
    Task<List<Reminder>> GetPendingRemindersAsync();

    #endregion

    #region 健康提醒 (RM-05)

    /// <summary>
    /// 设置久坐提醒
    /// </summary>
    Task SetupBreakReminderAsync(TimeSpan interval);

    /// <summary>
    /// 设置喝水提醒
    /// </summary>
    Task SetupDrinkWaterReminderAsync(TimeSpan interval);

    /// <summary>
    /// 设置护眼提醒
    /// </summary>
    Task SetupEyeCareReminderAsync(TimeSpan interval);

    #endregion

    #region 提醒历史

    /// <summary>
    /// 获取事件的提醒历史
    /// </summary>
    Task<List<Reminder>> GetReminderHistoryAsync(int eventId);

    #endregion
}
```

#### 4.2.3 IHotKeyService - 热键服务接口

**命名空间**: `AI_Calendar.Application.Services`
**文件路径**: `Application/Services/IHotKeyService.cs`

```csharp
namespace AI_Calendar.Application.Services;

/// <summary>
/// 热键服务接口
/// </summary>
public interface IHotKeyService
{
    #region 热键注册

    /// <summary>
    /// 注册隐私模式热键 (DW-07)
    /// </summary>
    void RegisterPrivacyModeHotKey(Action callback);

    /// <summary>
    /// 注册设置窗口热键 (MM-01)
    /// </summary>
    void RegisterSettingsHotKey(Action callback);

    /// <summary>
    /// 注册自定义热键
    /// </summary>
    void RegisterHotKey(string modifiers, string key, Action callback);

    #endregion

    #region 热键管理

    /// <summary>
    /// 注销所有热键
    /// </summary>
    void UnregisterAllHotKeys();

    /// <summary>
    /// 注销指定热键
    /// </summary>
    void UnregisterHotKey(int hotKeyId);

    /// <summary>
    /// 检查热键是否可用
    /// </summary>
    bool IsHotKeyAvailable(string modifiers, string key);

    #endregion
}
```

#### 4.2.4 IConfigurationService - 配置服务接口

**命名空间**: `AI_Calendar.Application.Services`
**文件路径**: `Application/Services/IConfigurationService.cs`

```csharp
namespace AI_Calendar.Application.Services;

/// <summary>
/// 配置服务接口
/// </summary>
public interface IConfigurationService
{
    #region 通用配置

    /// <summary>
    /// 获取配置值
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// 设置配置值
    /// </summary>
    Task SetAsync<T>(string key, T value);

    /// <summary>
    /// 删除配置值
    /// </summary>
    Task RemoveAsync(string key);

    #endregion

    #region Widget 配置

    /// <summary>
    /// 获取 Widget 外观配置
    /// </summary>
    Task<WidgetOptions> GetWidgetOptionsAsync();

    /// <summary>
    /// 保存 Widget 外观配置
    /// </summary>
    Task SaveWidgetOptionsAsync(WidgetOptions options);

    #endregion

    #region 热键配置

    /// <summary>
    /// 获取热键配置
    /// </summary>
    Task<HotKeyOptions> GetHotKeyOptionsAsync();

    /// <summary>
    /// 保存热键配置
    /// </summary>
    Task SaveHotKeyOptionsAsync(HotKeyOptions options);

    #endregion

    #region 提醒配置

    /// <summary>
    /// 获取提醒配置
    /// </summary>
    Task<ReminderOptions> GetReminderOptionsAsync();

    /// <summary>
    /// 保存提醒配置
    /// </summary>
    Task SaveReminderOptionsAsync(ReminderOptions options);

    #endregion

    #region 配置事件

    /// <summary>
    /// 配置更改事件
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    #endregion
}

#region 配置对象

/// <summary>
/// Widget 外观配置
/// </summary>
public class WidgetOptions
{
    public double PositionX { get; set; } = 100;
    public double PositionY { get; set; } = 100;
    public int FontSize { get; set; } = 14;
    public string FontFamily { get; set; } = "Microsoft YaHei";
    public string FontColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "#000000";
    public double Transparency { get; set; } = 0.9;
    public bool ShowLunarDate { get; set; } = true;
    public bool ShowHolidayInfo { get; set; } = true;
    public bool ShowWeekNumber { get; set; } = false;
}

/// <summary>
/// 热键配置
/// </summary>
public class HotKeyOptions
{
    public bool PrivacyModeEnabled { get; set; } = true;
    public string PrivacyModeModifiers { get; set; } = "Ctrl|Alt";
    public string PrivacyModeKey { get; set; } = "P";

    public bool SettingsEnabled { get; set; } = true;
    public string SettingsModifiers { get; set; } = "Ctrl|Alt";
    public string SettingsKey { get; set; } = "C";
}

/// <summary>
/// 提醒配置
/// </summary>
public class ReminderOptions
{
    public int DefaultOffsetMinutes { get; set; } = 15;
    public bool EnableFullScreenDetection { get; set; } = true;
    public int DefaultSnoozeMinutes { get; set; } = 10;

    // 健康提醒
    public bool EnableBreakReminder { get; set; } = true;
    public int BreakIntervalMinutes { get; set; } = 60;

    public bool EnableDrinkWaterReminder { get; set; } = true;
    public int DrinkWaterIntervalMinutes { get; set; } = 120;

    public bool EnableEyeCareReminder { get; set; } = true;
    public int EyeCareIntervalMinutes { get; set; } = 45;
}

/// <summary>
/// 配置更改事件参数
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    public string Key { get; set; } = string.Empty;
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
}

#endregion
```

#### 4.2.5 IMessenger - 消息总线接口

**命名空间**: `AI_Calendar.Application.Messenger`
**文件路径**: `Application/Messenger/IMessenger.cs`

```csharp
namespace AI_Calendar.Application.Messenger;

/// <summary>
/// 应用事件总线接口
/// </summary>
public interface IMessenger
{
    /// <summary>
    /// 订阅事件
    /// </summary>
    void Subscribe<T>(AppEvent @event, Action<T> handler);

    /// <summary>
    /// 发布事件
    /// </summary>
    void Publish<T>(AppEvent @event, T payload);

    /// <summary>
    /// 取消订阅
    /// </summary>
    void Unsubscribe<T>(AppEvent @event, Action<T> handler);
}

/// <summary>
/// 应用事件枚举
/// </summary>
public enum AppEvent
{
    EventCreated,
    EventUpdated,
    EventDeleted,
    SettingsChanged,
    RefreshRequested,
    PrivacyModeToggled,
    HolidayDataUpdated
}
```

---

## 5. 数据模型设计

### 5.1 Event - 事件实体

**命名空间**: `AI_Calendar.Core.Entities`
**文件路径**: `Core/Entities/Event.cs`

```csharp
namespace AI_Calendar.Core.Entities;

/// <summary>
/// 日历事件实体
/// 符合 PRD 要求，无 ORM 特性注解
/// </summary>
public class Event
{
    /// <summary>
    /// 主键 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 事件标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间 (本地时间)
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间 (可选)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 地点或会议链接
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 优先级
    /// </summary>
    public Priority Priority { get; set; } = Priority.Medium;

    /// <summary>
    /// 提前提醒分钟数 (0 表示不提醒)
    /// </summary>
    public int ReminderOffset { get; set; } = 15;

    /// <summary>
    /// 是否为农历循环事件
    /// </summary>
    public bool IsLunar { get; set; }

    /// <summary>
    /// 事件描述 (可选)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // ==================== 领域逻辑方法 ====================

    /// <summary>
    /// 判断是否为紧急事件 (1小时内开始)
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <returns>如果事件未删除且在1小时内开始，返回 true</returns>
    public bool IsUrgent(DateTime now)
    {
        if (IsDeleted) return false;
        var timeUntilStart = StartTime - now;
        return timeUntilStart.TotalHours > 0 && timeUntilStart.TotalHours <= 1;
    }

    /// <summary>
    /// 判断是否在指定时间需要提醒
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <returns>如果已到达提醒时间但还未到开始时间，返回 true</returns>
    public bool ShouldRemind(DateTime now)
    {
        if (IsDeleted) return false;
        if (ReminderOffset <= 0) return false;

        var reminderTime = StartTime.AddMinutes(-ReminderOffset);
        return now >= reminderTime && now < StartTime;
    }

    /// <summary>
    /// 判断是否与另一事件时间冲突
    /// </summary>
    /// <param name="other">另一事件</param>
    /// <returns>如果时间重叠，返回 true</returns>
    public bool ConflictsWith(Event other)
    {
        if (IsDeleted || other.IsDeleted) return false;

        var myEnd = EndTime ?? StartTime.AddHours(1);
        var otherEnd = other.EndTime ?? other.StartTime.AddHours(1);

        return StartTime < otherEnd && myEnd > other.StartTime;
    }

    /// <summary>
    /// 计算持续时间
    /// </summary>
    /// <returns>持续时间，如果未设置结束时间则返回 null</returns>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime - StartTime : null;

    /// <summary>
    /// 获取事件的显示文本
    /// </summary>
    /// <returns>格式化的时间范围和标题</returns>
    public string GetDisplayText()
    {
        var timeRange = EndTime.HasValue
            ? $"{StartTime:HH:mm} - {EndTime:HH:mm}"
            : $"{StartTime:HH:mm}";

        return $"{timeRange} {Title}";
    }

    /// <summary>
    /// 标记为已删除
    /// </summary>
    public void MarkAsDeleted()
    {
        IsDeleted = true;
        DeletedAt = DateTime.Now;
    }

    /// <summary>
    /// 恢复已删除的事件
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
    }
}

/// <summary>
/// 优先级枚举（通过EF Core值转换器映射到数据库INTEGER）
/// </summary>
public enum Priority
{
    /// <summary>
    /// 低优先级（普通）
    /// </summary>
    Low = 0,

    /// <summary>
    /// 中等优先级（默认）
    /// </summary>
    Medium = 1,

    /// <summary>
    /// 高优先级（重要）
    /// </summary>
    High = 2
}
```

### 5.2 OperationLog - 操作日志实体

**命名空间**: `AI_Calendar.Core.Entities`
**文件路径**: `Core/Entities/OperationLog.cs`

```csharp
namespace AI_Calendar.Core.Entities;

/// <summary>
/// 操作审计日志实体
/// </summary>
public class OperationLog
{
    /// <summary>
    /// 主键 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 工具名称 (如 "delete_event")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// 参数 (JSON 格式)
    /// </summary>
    [Required]
    public string Params { get; set; } = string.Empty;

    /// <summary>
    /// 执行结果
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// MCP 客户端标识 (可选)
    /// </summary>
    [MaxLength(200)]
    public string? ClientId { get; set; }
}
```

### 5.3 AppSetting - 应用配置实体

**命名空间**: `AI_Calendar.Core.Entities`
**文件路径**: `Core/Entities/AppSetting.cs`

```csharp
namespace AI_Calendar.Core.Entities;

/// <summary>
/// 应用配置实体
/// </summary>
public class AppSetting
{
    /// <summary>
    /// 配置键 (主键)
    /// </summary>
    [Key]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 配置值 (JSON 格式)
    /// </summary>
    [Required]
    [MaxLength(4000)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 配置描述
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
```

### 5.4 Reminder - 提醒记录实体

**命名空间**: `AI_Calendar.Core.Entities`
**文件路径**: `Core/Entities/Reminder.cs`

```csharp
namespace AI_Calendar.Core.Entities;

/// <summary>
/// 提醒记录实体
/// </summary>
public class Reminder
{
    /// <summary>
    /// 主键 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 关联的事件 ID
    /// </summary>
    public int EventId { get; set; }

    /// <summary>
    /// 提醒时间
    /// </summary>
    public DateTime RemindTime { get; set; }

    /// <summary>
    /// 是否已确认 (用户已查看)
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// 确认时间
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// 提醒方式 (Toast/Email/其他)
    /// </summary>
    [MaxLength(50)]
    public string ReminderMethod { get; set; } = "Toast";

    /// <summary>
    /// 是否已发送
    /// </summary>
    public bool IsSent { get; set; }

    /// <summary>
    /// 发送时间
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ==================== 导航属性 ====================

    /// <summary>
    /// 关联的事件
    /// </summary>
    public virtual Event? Event { get; set; }
}
```

---

## 6. 业务服务设计

### 6.1 EventService - 事件服务实现

**命名空间**: `AI_Calendar.Application.Services`
**文件路径**: `Application/Services/EventService.cs`

```csharp
using AI_Calendar.Core.Entities;
using AI_Calendar.Core.Exceptions;
using AI_Calendar.Core.Interfaces;
using AI_Calendar.Application.Messenger;
using System.Text.Json;

namespace AI_Calendar.Application.Services;

/// <summary>
/// 事件业务服务实现
/// </summary>
public class EventService : IEventService
{
    private readonly IEventRepository _repository;
    private readonly IOperationLogRepository _auditLog;
    private readonly IMessenger _messenger;

    public EventService(
        IEventRepository repository,
        IOperationLogRepository auditLog,
        IMessenger messenger)
    {
        _repository = repository;
        _auditLog = auditLog;
        _messenger = messenger;
    }

    // ========== MCP 工具方法 ==========

    /// <summary>
    /// 搜索事件 (MCP-02)
    /// </summary>
    public async Task<List<Event>> SearchAsync(string? query, DateTime? start, DateTime? end)
    {
        var events = await _repository.SearchAsync(query, start, end, false);

        // 记录审计日志
        await _auditLog.AddAsync(new OperationLog
        {
            ToolName = "search_events",
            Params = JsonSerializer.Serialize(new { query, start, end }),
            Result = $"Found {events.Count} events"
        });

        return events;
    }

    /// <summary>
    /// 创建事件 (MCP-03)
    /// </summary>
    public async Task<Event> CreateAsync(EventDto dto)
    {
        // 1. 参数验证
        ValidateEventDto(dto);

        // 2. 时间冲突检测
        var conflicts = await _repository.GetConflictingEventsAsync(dto.StartTime, dto.EndTime);
        if (conflicts.Any())
        {
            var conflictInfo = conflicts.Select(e =>
                $"{e.Id}: {e.Title} @ {e.StartTime:yyyy-MM-dd HH:mm}").ToList();

            throw new EventConflictException(
                $"检测到时间冲突，与以下 {conflicts.Count} 个事件重叠",
                conflictInfo);
        }

        // 3. 创建实体
        var newEvent = new Event
        {
            Title = dto.Title,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Location = dto.Location,
            Priority = dto.Priority,
            ReminderOffset = dto.ReminderOffset,
            IsLunar = dto.IsLunar,
            Description = dto.Description
        };

        // 4. 保存到数据库
        var created = await _repository.AddAsync(newEvent);

        // 5. 记录审计日志
        await _auditLog.AddAsync(new OperationLog
        {
            ToolName = "create_event",
            Params = JsonSerializer.Serialize(dto),
            Result = $"Success (ID: {created.Id})"
        });

        // 6. 发布事件创建消息
        _messenger.Publish(AppEvent.EventCreated, created);

        return created;
    }

    /// <summary>
    /// 更新事件 (MCP-04)
    /// </summary>
    public async Task<Event> UpdateAsync(int id, EventDto changes)
    {
        // 1. 获取原事件
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.IsDeleted)
            throw new InvalidOperationException($"事件 {id} 不存在或已删除");

        // 2. 应用变更
        ApplyChanges(existing, changes);

        // 3. 冲突检测 (排除自身)
        var conflicts = await _repository.GetConflictingEventsAsync(
            existing.StartTime, existing.EndTime, id);
        if (conflicts.Any())
        {
            throw new EventConflictException(
                "更新后时间与现有事件冲突",
                conflicts.Select(e => e.Title).ToList());
        }

        // 4. 保存
        existing.UpdatedAt = DateTime.Now;
        var updated = await _repository.UpdateAsync(existing);

        // 5. 记录日志
        await _auditLog.AddAsync(new OperationLog
        {
            ToolName = "update_event",
            Params = JsonSerializer.Serialize(new { id, changes }),
            Result = "Success"
        });

        // 6. 发布更新消息
        _messenger.Publish(AppEvent.EventUpdated, updated);

        return updated;
    }

    /// <summary>
    /// 删除事件 (MCP-05)
    /// </summary>
    public async Task SoftDeleteAsync(int id, bool confirm)
    {
        if (!confirm)
            throw new InvalidOperationException("必须设置 confirm=true 才能删除事件");

        var evt = await _repository.GetByIdAsync(id);
        if (evt == null)
            throw new KeyNotFoundException($"事件 {id} 不存在");

        await _repository.SoftDeleteAsync(id);

        // 记录日志
        await _auditLog.AddAsync(new OperationLog
        {
            ToolName = "delete_event",
            Params = JsonSerializer.Serialize(new { id, confirm }),
            Result = "Success"
        });

        // 发布删除消息
        _messenger.Publish(AppEvent.EventDeleted, id);
    }

    /// <summary>
    /// 获取空闲时间 (MCP-06)
    /// </summary>
    public async Task<List<TimeSlot>> GetFreeTimeAsync(TimeSpan duration, DateTime date)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentException("时长必须大于0");

        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        // 获取当天所有事件
        var events = await _repository.GetEventsInRangeAsync(startOfDay, endOfDay);

        // 计算空闲时间段
        return CalculateFreeSlots(events, duration, startOfDay, endOfDay);
    }

    // ========== UI 业务方法 ==========

    public async Task<List<EventDisplayModel>> GetUpcomingEvents(int count)
    {
        var events = await _repository.GetUpcomingEventsAsync(DateTime.Now, count);

        return events.Select(e => new EventDisplayModel
        {
            Id = e.Id,
            Title = e.Title,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Location = e.Location,
            Priority = e.Priority,
            IsUrgent = e.IsUrgent(DateTime.Now)
        }).ToList();
    }

    public async Task<List<Event>> GetTodayEventsAsync()
    {
        var today = DateTime.Now;
        return await _repository.GetEventsByDateAsync(today);
    }

    public async Task<List<Event>> GetEventsAsync(DateTime start, DateTime end)
    {
        return await _repository.GetEventsInRangeAsync(start, end);
    }

    public async Task<List<Event>> GetDueEventsAsync(DateTime now, TimeSpan lookAhead)
    {
        return await _repository.GetDueEventsAsync(now, lookAhead);
    }

    public async Task RestoreEventAsync(int id)
    {
        var evt = await _repository.GetByIdAsync(id);
        if (evt == null || !evt.IsDeleted)
            throw new InvalidOperationException("事件不存在或未删除");

        evt.Restore();
        await _repository.UpdateAsync(evt);

        _messenger.Publish(AppEvent.EventUpdated, evt);
    }

    public async Task<List<Event>> GetRecycleBinEventsAsync()
    {
        return await _repository.GetDeletedEventsAsync(7);
    }

    public async Task<int> EmptyRecycleBinAsync()
    {
        // 获取7天前的删除事件
        var oldEvents = await _repository.GetDeletedEventsAsync(7);
        var count = 0;

        foreach (var evt in oldEvents)
        {
            if (evt.DeletedAt.HasValue &&
                DateTime.Now - evt.DeletedAt.Value > TimeSpan.FromDays(7))
            {
                await _repository.HardDeleteAsync(evt.Id);
                count++;
            }
        }

        return count;
    }

    // ========== 批量操作 ==========

    public async Task<List<Event>> CreateBatchAsync(List<EventDto> events)
    {
        var results = new List<Event>();

        foreach (var dto in events)
        {
            try
            {
                var created = await CreateAsync(dto);
                results.Add(created);
            }
            catch
            {
                // 批量操作中单个失败不影响其他
            }
        }

        return results;
    }

    public async Task SoftDeleteBatchAsync(List<int> ids, bool confirm)
    {
        if (!confirm)
            throw new InvalidOperationException("必须设置 confirm=true");

        foreach (var id in ids)
        {
            await SoftDeleteAsync(id, true);
        }
    }

    // ========== 统计方法 ==========

    public async Task<EventStatistics> GetTodayStatisticsAsync()
    {
        var today = DateTime.Now;
        var startOfDay = today.Date;
        var endOfDay = startOfDay.AddDays(1);

        var events = await _repository.GetEventsInRangeAsync(startOfDay, endOfDay);

        return new EventStatistics
        {
            TotalCount = events.Count,
            CompletedCount = events.Count(e => e.EndTime.HasValue && e.EndTime < DateTime.Now),
            UpcomingCount = events.Count(e => e.StartTime > DateTime.Now),
            OverdueCount = events.Count(e => e.EndTime.HasValue && e.EndTime < DateTime.Now && !e.IsDeleted),
            TotalDuration = TimeSpan.FromMinutes(events.Sum(e => e.Duration?.TotalMinutes ?? 60))
        };
    }

    public async Task<EventStatistics> GetWeekStatisticsAsync()
    {
        var today = DateTime.Now;
        var dayOfWeek = (int)today.DayOfWeek;
        var startOfWeek = today.Date.AddDays(-dayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);

        var events = await _repository.GetEventsInRangeAsync(startOfWeek, endOfWeek);

        return new EventStatistics
        {
            TotalCount = events.Count,
            CompletedCount = events.Count(e => e.EndTime.HasValue && e.EndTime < DateTime.Now),
            UpcomingCount = events.Count(e => e.StartTime > DateTime.Now),
            OverdueCount = 0,
            TotalDuration = TimeSpan.FromMinutes(events.Sum(e => e.Duration?.TotalMinutes ?? 60))
        };
    }

    // ========== 私有辅助方法 ==========

    private void ValidateEventDto(EventDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("事件标题不能为空");

        if (dto.StartTime == default)
            throw new ArgumentException("开始时间无效");

        if (dto.EndTime.HasValue && dto.EndTime <= dto.StartTime)
            throw new ArgumentException("结束时间必须晚于开始时间");
    }

    private void ApplyChanges(Event target, EventDto changes)
    {
        if (!string.IsNullOrEmpty(changes.Title))
            target.Title = changes.Title;

        if (changes.StartTime != default)
            target.StartTime = changes.StartTime;

        if (changes.EndTime.HasValue)
            target.EndTime = changes.EndTime;

        if (changes.Location != null)
            target.Location = changes.Location;

        target.Priority = changes.Priority;
        target.ReminderOffset = changes.ReminderOffset;
        target.IsLunar = changes.IsLunar;
        target.Description = changes.Description;
    }

    private List<TimeSlot> CalculateFreeSlots(
        List<Event> events,
        TimeSpan duration,
        DateTime startOfDay,
        DateTime endOfDay)
    {
        var slots = new List<TimeSlot>();
        var currentTime = startOfDay;

        foreach (var evt in events.OrderBy(e => e.StartTime))
        {
            var freeTime = evt.StartTime - currentTime;
            if (freeTime >= duration)
            {
                slots.Add(new TimeSlot
                {
                    Start = currentTime,
                    End = evt.StartTime
                });
            }

            currentTime = evt.EndTime ?? evt.StartTime.AddHours(1);
        }

        // 检查最后一段
        if (endOfDay - currentTime >= duration)
        {
            slots.Add(new TimeSlot
            {
                Start = currentTime,
                End = endOfDay
            });
        }

        return slots;
    }
}
```

---

## 7. 基础设施设计

### 7.1 数据访问层

#### 7.1.1 AppDbContext - 数据库上下文

**命名空间**: `AI_Calendar.Infrastructure.Data`
**文件路径**: `Infrastructure/Data/AppDbContext.cs`

```csharp
using AI_Calendar.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AI_Calendar.Infrastructure.Data;

/// <summary>
/// 应用数据库上下文
/// </summary>
public class AppDbContext : DbContext
{
    #region DbSet

    /// <summary>
    /// 事件表
    /// </summary>
    public DbSet<Event> Events { get; set; } = null!;

    /// <summary>
    /// 操作日志表
    /// </summary>
    public DbSet<OperationLog> OperationLogs { get; set; } = null!;

    /// <summary>
    /// 应用配置表
    /// </summary>
    public DbSet<AppSetting> Settings { get; set; } = null!;

    /// <summary>
    /// 提醒记录表
    /// </summary>
    public DbSet<Reminder> Reminders { get; set; } = null!;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化数据库上下文
    /// </summary>
    public AppDbContext()
    {
    }

    /// <summary>
    /// 初始化数据库上下文 (依赖注入)
    /// </summary>
    /// <param name="options">上下文选项</param>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    #endregion

    #region 配置

    /// <summary>
    /// 配置数据库连接
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            // 获取程序所在目录（与exe同级）
            var exePath = Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath);

            // 数据库文件路径：程序目录/data.db
            var dbPath = Path.Combine(exeDir!, "data.db");

            options.UseSqlite($"Data Source={dbPath}");
        }
    }

    /// <summary>
    /// 配置实体模型
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Event 实体配置
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.StartTime).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(2000);

            // 索引
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => e.Priority);

            // 全局查询过滤器 (自动过滤已删除事件)
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // OperationLog 实体配置
        modelBuilder.Entity<OperationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ToolName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Params).IsRequired();
            entity.Property(e => e.Result).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Timestamp).IsRequired();

            // 索引
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.ToolName);
        });

        // AppSetting 实体配置
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Reminder 实体配置
        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventId).IsRequired();
            entity.Property(e => e.RemindTime).IsRequired();
            entity.Property(e => e.IsAcknowledged).HasDefaultValue(false);
            entity.Property(e => e.ReminderMethod).HasMaxLength(50).HasDefaultValue("Toast");

            // 索引
            entity.HasIndex(e => e.RemindTime);
            entity.HasIndex(e => e.IsAcknowledged);
            entity.HasIndex(e => e.IsSent);

            // 关系配置
            entity.HasOne(e => e.Event)
                  .WithMany(e => e.Reminders)
                  .HasForeignKey(e => e.EventId);
        });

        base.OnModelCreating(modelBuilder);
    }

    #endregion

    #region 数据库管理

    /// <summary>
    /// 确保数据库已创建
    /// </summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        await Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// 执行数据库迁移
    /// </summary>
    public async Task MigrateDatabaseAsync()
    {
        await Database.MigrateAsync();
    }

    /// <summary>
    /// 备份数据库
    /// </summary>
    /// <param name="backupPath">备份文件路径</param>
    public async Task BackupDatabaseAsync(string backupPath)
    {
        var dbPath = Database.GetConnectionString();
        // 实现备份逻辑
        File.Copy(dbPath!, backupPath, true);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 开始事务
    /// </summary>
    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await Database.BeginTransactionAsync();
    }

    #endregion
}
```

#### 7.1.2 EventRepository - 事件仓储实现

**命名空间**: `AI_Calendar.Infrastructure.Data`
**文件路径**: `Infrastructure/Data/EventRepository.cs`

```csharp
using AI_Calendar.Core.Entities;
using AI_Calendar.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AI_Calendar.Infrastructure.Data;

/// <summary>
/// 事件仓储实现
/// </summary>
public class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    #region 基本CRUD操作

    public async Task<Event?> GetByIdAsync(int id)
    {
        return await _context.Events
            .IgnoreQueryFilters()  // 忽略全局过滤器以获取已删除事件
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Event> AddAsync(Event evt)
    {
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        return evt;
    }

    public async Task<Event> UpdateAsync(Event evt)
    {
        _context.Events.Update(evt);
        await _context.SaveChangesAsync();
        return evt;
    }

    public async Task SoftDeleteAsync(int id)
    {
        var evt = await _context.Events.FindAsync(id);
        if (evt != null)
        {
            evt.MarkAsDeleted();
            await _context.SaveChangesAsync();
        }
    }

    public async Task HardDeleteAsync(int id)
    {
        var evt = await _context.Events.FindAsync(id);
        if (evt != null)
        {
            _context.Events.Remove(evt);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region 查询操作

    public async Task<List<Event>> SearchAsync(
        string? query,
        DateTime? start,
        DateTime? end,
        bool includeDeleted = false)
    {
        var q = _context.Events;

        if (!includeDeleted)
        {
            q = q.Where(e => !e.IsDeleted);
        }

        // 关键词搜索
        if (!string.IsNullOrEmpty(query))
        {
            q = q.Where(e =>
                EF.Functions.Like(e.Title, $"%{query}%") ||
                (e.Location != null && EF.Functions.Like(e.Location, $"%{query}%")));
        }

        // 时间范围过滤
        if (start.HasValue)
            q = q.Where(e => e.StartTime >= start.Value);

        if (end.HasValue)
            q = q.Where(e => e.StartTime <= end.Value);

        return await q.OrderBy(e => e.StartTime).ToListAsync();
    }

    public async Task<List<Event>> GetUpcomingEventsAsync(DateTime now, int count)
    {
        return await _context.Events
            .Where(e => !e.IsDeleted && e.StartTime > now)
            .OrderBy(e => e.StartTime)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Event>> GetDueEventsAsync(DateTime now, TimeSpan lookAhead)
    {
        var endTime = now.Add(lookAhead);

        return await _context.Events
            .Where(e => !e.IsDeleted && e.StartTime >= now && e.StartTime <= endTime)
            .OrderBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<List<Event>> GetEventsInRangeAsync(DateTime start, DateTime end)
    {
        return await _context.Events
            .Where(e => !e.IsDeleted && e.StartTime >= start && e.StartTime <= end)
            .OrderBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<List<Event>> GetEventsByDateAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await GetEventsInRangeAsync(startOfDay, endOfDay);
    }

    public async Task<List<Event>> GetDeletedEventsAsync(int days = 7)
    {
        var cutoffDate = DateTime.Now.AddDays(-days);

        return await _context.Events
            .IgnoreQueryFilters()
            .Where(e => e.IsDeleted && e.DeletedAt >= cutoffDate)
            .OrderByDescending(e => e.DeletedAt)
            .ToListAsync();
    }

    #endregion

    #region 冲突检测

    public async Task<bool> HasConflictAsync(DateTime start, DateTime? end, int? excludeEventId = null)
    {
        var q = _context.Events
            .Where(e => !e.IsDeleted && e.Id != excludeEventId);

        var endToCheck = end ?? start.AddHours(1);

        return await q.AnyAsync(e =>
            e.StartTime < endToCheck &&
            (e.EndTime ?? e.StartTime.AddHours(1)) > start);
    }

    public async Task<List<Event>> GetConflictingEventsAsync(DateTime start, DateTime? end, int? excludeEventId = null)
    {
        var q = _context.Events
            .Where(e => !e.IsDeleted && e.Id != excludeEventId);

        var endToCheck = end ?? start.AddHours(1);

        return await q
            .Where(e =>
                e.StartTime < endToCheck &&
                (e.EndTime ?? e.StartTime.AddHours(1)) > start)
            .ToListAsync();
    }

    #endregion

    #region 统计操作

    public async Task<int> CountEventsAsync(DateTime start, DateTime end)
    {
        return await _context.Events
            .Where(e => !e.IsDeleted && e.StartTime >= start && e.StartTime <= end)
            .CountAsync();
    }

    public async Task<DateTime?> GetNextEventTimeAsync(DateTime now)
    {
        var nextEvent = await _context.Events
            .Where(e => !e.IsDeleted && e.StartTime > now)
            .OrderBy(e => e.StartTime)
            .FirstOrDefaultAsync();

        return nextEvent?.StartTime;
    }

    #endregion
}
```

---

## 8. 表示层设计

### 8.1 IWidgetViewModel - 桌面挂件视图模型

**命名空间**: `AI_Calendar.Presentation.ViewModels`
**文件路径**: `Presentation/ViewModels/IWidgetViewModel.cs`

```csharp
using AI_Calendar.Core.Entities;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AI_Calendar.Presentation.ViewModels;

/// <summary>
/// 桌面挂件视图模型接口
/// </summary>
public interface IWidgetViewModel : INotifyPropertyChanged
{
    #region 显示属性

    /// <summary>
    /// 当前日期 (公历)
    /// </summary>
    string CurrentDate { get; }

    /// <summary>
    /// 当前时间
    /// </summary>
    string CurrentTime { get; }

    /// <summary>
    /// 星期
    /// </summary>
    string CurrentWeekday { get; }

    /// <summary>
    /// 农历日期
    /// </summary>
    string LunarDate { get; }

    /// <summary>
    /// 节假日信息
    /// </summary>
    string? HolidayInfo { get; }

    /// <summary>
    /// 即将到来的事件列表
    /// </summary>
    ObservableCollection<EventDisplayModel> UpcomingEvents { get; }

    /// <summary>
    /// 剩余事件数量
    /// </summary>
    int RemainingEventCount { get; }

    /// <summary>
    /// 隐私模式是否激活
    /// </summary>
    bool IsPrivacyModeActive { get; set; }

    #endregion

    #region 命令

    /// <summary>
    /// 刷新命令
    /// </summary>
    ICommand RefreshCommand { get; }

    /// <summary>
    /// 打开设置窗口命令
    /// </summary>
    ICommand OpenSettingsCommand { get; }

    /// <summary>
    /// 切换隐私模式命令
    /// </summary>
    ICommand TogglePrivacyModeCommand { get; }

    #endregion

    #region 方法

    /// <summary>
    /// 刷新显示内容
    /// </summary>
    Task RefreshAsync();

    /// <summary>
    /// 更新事件列表
    /// </summary>
    Task UpdateEventsAsync();

    /// <summary>
    /// 切换隐私模式
    /// </summary>
    void TogglePrivacyMode();

    #endregion

    #region 事件

    /// <summary>
    /// 请求打开设置窗口事件
    /// </summary>
    event EventHandler? OpenSettingsRequested;

    #endregion
}
```

### 8.2 ISettingsViewModel - 设置窗口视图模型

**命名空间**: `AI_Calendar.Presentation.ViewModels`
**文件路径**: `Presentation/ViewModels/ISettingsViewModel.cs`

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AI_Calendar.Presentation.ViewModels;

/// <summary>
/// 设置窗口视图模型接口
/// </summary>
public interface ISettingsViewModel : INotifyPropertyChanged
{
    #region 页面状态

    /// <summary>
    /// 当前选中的标签页
    /// </summary>
    int SelectedTabIndex { get; set; }

    #endregion

    #region 事件管理

    /// <summary>
    /// 事件列表
    /// </summary>
    ObservableCollection<EventItemModel> Events { get; }

    /// <summary>
    /// 选中的事件
    /// </summary>
    EventItemModel? SelectedEvent { get; set; }

    /// <summary>
    /// 事件过滤条件
    /// </summary>
    string EventFilter { get; set; }

    /// <summary>
    /// 日期范围过滤
    /// </summary>
    DateTimeRangeFilter DateRangeFilter { get; set; }

    #endregion

    #region 回收站

    /// <summary>
    /// 已删除事件列表
    /// </summary>
    ObservableCollection<EventItemModel> DeletedEvents { get; }

    /// <summary>
    /// 恢复命令
    /// </summary>
    ICommand RestoreCommand { get; }

    /// <summary>
    /// 永久删除命令
    /// </summary>
    ICommand PermanentDeleteCommand { get; }

    #endregion

    #region 外观配置

    /// <summary>
    /// Widget 配置
    /// </summary>
    WidgetOptions WidgetOptions { get; set; }

    /// <summary>
    /// 保存配置命令
    /// </summary>
    ICommand SaveOptionsCommand { get; }

    /// <summary>
    /// 重置配置命令
    /// </summary>
    ICommand ResetOptionsCommand { get; }

    #endregion

    #region 方法

    /// <summary>
    /// 加载事件列表
    /// </summary>
    Task LoadEventsAsync();

    /// <summary>
    /// 创建新事件
    /// </summary>
    Task CreateEventAsync();

    /// <summary>
    /// 编辑事件
    /// </summary>
    Task EditEventAsync(int eventId);

    /// <summary>
    /// 删除事件
    /// </summary>
    Task DeleteEventAsync(int eventId);

    /// <summary>
    /// 恢复事件
    /// </summary>
    Task RestoreEventAsync(int eventId);

    #endregion
}

#region 辅助类

/// <summary>
/// 事件项模型
/// </summary>
public class EventItemModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Location { get; set; }
    public Priority Priority { get; set; }
    public string TimeDisplay { get; set; } = string.Empty;
    public bool IsPast { get; set; }
}

/// <summary>
/// 日期范围过滤
/// </summary>
public enum DateTimeRangeFilter
{
    All,
    Today,
    Tomorrow,
    ThisWeek,
    NextWeek,
    ThisMonth,
    Custom
}

#endregion
```

---

## 9. 外部服务集成

### 9.1 IHolidayService - 节假日服务

**命名空间**: `AI_Calendar.Infrastructure.External.Interfaces`
**文件路径**: `Infrastructure/External/Interfaces/IHolidayService.cs`

```csharp
namespace AI_Calendar.Infrastructure.External.Interfaces;

/// <summary>
/// 节假日服务接口
/// </summary>
public interface IHolidayService
{
    #region 查询操作

    /// <summary>
    /// 获取指定日期的节假日信息
    /// </summary>
    Task<HolidayInfo?> GetHolidayAsync(DateTime date);

    /// <summary>
    /// 判断指定日期是否为工作日
    /// </summary>
    Task<bool> IsWorkdayAsync(DateTime date);

    /// <summary>
    /// 判断指定日期是否为节假日
    /// </summary>
    Task<bool> IsHolidayAsync(DateTime date);

    /// <summary>
    /// 获取指定年份的所有节假日
    /// </summary>
    Task<YearHolidays?> GetYearHolidaysAsync(int year);

    /// <summary>
    /// 获取下一个节假日
    /// </summary>
    Task<HolidayInfo?> GetNextHolidayAsync(DateTime fromDate);

    #endregion

    #region 数据管理

    /// <summary>
    /// 更新指定年份的节假日数据
    /// </summary>
    Task<bool> UpdateYearDataAsync(int year);

    /// <summary>
    /// 清除缓存
    /// </summary>
    Task ClearCacheAsync();

    #endregion
}

#region 数据模型

/// <summary>
/// 节假日信息
/// </summary>
public class HolidayInfo
{
    public string Date { get; set; } = string.Empty;  // "2026-01-01"
    public string Name { get; set; } = string.Empty;  // "元旦"
    public bool IsWorkday { get; set; }              // 是否为调休
    public bool IsOffday { get; set; }               // 是否为放假
}

/// <summary>
/// 年度节假日数据
/// </summary>
public class YearHolidays
{
    public int Year { get; set; }
    public List<HolidayInfo> Holidays { get; set; } = new();
}

#endregion
```

### 9.2 ILunarCalendarService - 农历日历服务

**命名空间**: `AI_Calendar.Infrastructure.External.Interfaces`
**文件路径**: `Infrastructure/External/Interfaces/ILunarCalendarService.cs`

```csharp
namespace AI_Calendar.Infrastructure.External.Interfaces;

/// <summary>
/// 农历日历服务接口
/// </summary>
public interface ILunarCalendarService
{
    #region 日期转换

    /// <summary>
    /// 公历转农历
    /// </summary>
    LunarDate GetLunarDate(DateTime solarDate);

    /// <summary>
    /// 农历转公历
    /// </summary>
    DateTime GetSolarDate(int lunarYear, int lunarMonth, int lunarDay, bool isLeap = false);

    #endregion

    #region 节日查询

    /// <summary>
    /// 获取指定日期的农历节日
    /// </summary>
    List<LunarFestival> GetLunarFestivals(DateTime solarDate);

    /// <summary>
    /// 获取指定日期的节气
    /// </summary>
    string? GetSolarTerm(DateTime solarDate);

    #endregion

    #region 信息查询

    /// <summary>
    /// 获取生肖
    /// </summary>
    string GetChineseZodiac(int year);

    /// <summary>
    /// 获取天干地支
    /// </summary>
    string GetGanZhi(int year);

    #endregion
}

#region 数据模型

/// <summary>
/// 农历日期
/// </summary>
public class LunarDate
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public bool IsLeapMonth { get; set; }
    public string ChineseYear { get; set; } = string.Empty;
    public string ChineseMonth { get; set; } = string.Empty;
    public string ChineseDay { get; set; } = string.Empty;
    public string FullString { get; set; } = string.Empty;
}

/// <summary>
/// 农历节日
/// </summary>
public class LunarFestival
{
    public string Name { get; set; } = string.Empty;
    public FestivalType Type { get; set; }
}

/// <summary>
/// 节日类型
/// </summary>
public enum FestivalType
{
    Traditional,  // 传统节日 (春节、中秋等)
    Solar,        // 公历节日 (国庆、元旦等)
    Special       // 特殊节日 (植树节、护士节等)
}

#endregion
```

---

## 10. 系统基础服务接口 (补充 PRD 缺失功能)

### 10.1 IAutoStartService - 开机自启服务

**命名空间**: `AI_Calendar.Infrastructure.System.Interfaces`
**文件路径**: `Infrastructure/System/Interfaces/IAutoStartService.cs`
**对应 PRD**: SY-01

```csharp
namespace AI_Calendar.Infrastructure.System.Interfaces;

/// <summary>
/// 开机自启服务接口
/// 对应 PRD SY-01：支持注册表或任务计划程序自启
/// </summary>
public interface IAutoStartService
{
    /// <summary>
    /// 检查是否已设置开机自启
    /// </summary>
    Task<bool> IsEnabledAsync();

    /// <summary>
    /// 启用开机自启
    /// </summary>
    Task EnableAsync();

    /// <summary>
    /// 禁用开机自启
    /// </summary>
    Task DisableAsync();

    /// <summary>
    /// 获取自启状态（注册表/任务计划程序）
    /// </summary>
    Task<AutoStartStatus> GetStatusAsync();
}

#region 数据模型

/// <summary>
/// 自启状态
/// </summary>
public class AutoStartStatus
{
    public bool IsEnabled { get; set; }
    public string Method { get; set; } = "Registry";  // "Registry" or "TaskScheduler"
    public string? ExecutablePath { get; set; }
}

#endregion
```

---

### 10.2 IUpdateService - 自动更新服务

**命名空间**: `AI_Calendar.Infrastructure.System.Interfaces`
**文件路径**: `Infrastructure/System/Interfaces/IUpdateService.cs`
**对应 PRD**: SY-03

```csharp
namespace AI_Calendar.Infrastructure.System.Interfaces;

/// <summary>
/// 自动更新服务接口
/// 对应 PRD SY-03：检查新版本并提示下载
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 检查是否有新版本
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync();

    /// <summary>
    /// 下载更新
    /// </summary>
    Task DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<double> progress);

    /// <summary>
    /// 安装更新
    /// </summary>
    Task InstallUpdateAsync(string installerPath);

    /// <summary>
    /// 获取当前版本
    /// </summary>
    Version GetCurrentVersion();

    /// <summary>
    /// 跳过当前版本（不提示更新）
    /// </summary>
    Task SkipVersionAsync(Version version);
}

#region 数据模型

/// <summary>
/// 更新信息
/// </summary>
public class UpdateInfo
{
    public Version Version { get; set; } = null!;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ReleaseDate { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
}

#endregion
```

---

### 10.3 IScreenHelper - 多屏适配辅助

**命名空间**: `AI_Calendar.Common.Helpers`
**文件路径**: `Common/Helpers/IScreenHelper.cs`
**对应 PRD**: DW-06

```csharp
namespace AI_Calendar.Common.Helpers;

/// <summary>
/// 多屏适配辅助接口
/// 对应 PRD DW-06：自动识别主显示器，支持多显示器 DPI 缩放
/// </summary>
public interface IScreenHelper
{
    /// <summary>
    /// 获取主显示器 DPI 缩放比例
    /// </summary>
    double GetDpiScale();

    /// <summary>
    /// 获取主显示器工作区域
    /// </summary>
    System.Drawing.Rectangle GetPrimaryScreenWorkArea();

    /// <summary>
    /// 监听显示器设置变化
    /// </summary>
    event EventHandler? DisplaySettingsChanged;

    /// <summary>
    /// 将坐标从逻辑像素转换为物理像素
    /// </summary>
    void TransformLogicalToDevicePixels(ref double x, ref double y);

    /// <summary>
    /// 获取所有显示器信息
    /// </summary>
    List<ScreenInfo> GetAllScreens();
}

#region 数据模型

/// <summary>
/// 屏幕信息
/// </summary>
public class ScreenInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double DpiScale { get; set; }
    public bool IsPrimary { get; set; }
    public System.Drawing.Rectangle Bounds { get; set; }
}

#endregion
```

---

## 11. 配置与依赖注入

### 11.1 Program.cs - 应用入口与配置（混合架构）

**文件路径**: `Program.cs` (混合架构入口点)

```csharp
using AI_Calendar.Application.Services;
using AI_Calendar.Application.Messenger;
using AI_Calendar.Infrastructure.Data;
using AI_Calendar.Infrastructure.MCP;
using AI_Calendar.Infrastructure.Native;
using AI_Calendar.Infrastructure.External;
using AI_Calendar.Presentation.Views;  // WPF Views
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// ========================================
// 双Host架构：WPF + MCP Server
// 符合 System-Architecture.md V1.5 设计
// ========================================

// 步骤 1: 创建 ASP.NET Core Web Application (用于 MCP Server)
var webBuilder = WebApplication.CreateBuilder(args);

// 步骤 2: 添加配置
webBuilder.Configuration.AddJsonFile("appsettings.json", optional: false);
webBuilder.Configuration.AddJsonFile($"appsettings.{webBuilder.Environment.EnvironmentName}.json", optional: true);

// 步骤 3: 注册数据库 (SQLite)
webBuilder.Services.AddDbContext<AppDbContext>(options =>
{
    // 获取程序所在目录（与exe同级）
    var exePath = Assembly.GetExecutingAssembly().Location;
    var exeDir = Path.GetDirectoryName(exePath);

    // 数据库文件路径：程序目录/data.db
    var dbPath = Path.Combine(exeDir!, "data.db");

    options.UseSqlite($"Data Source={dbPath}");
});

// 步骤 4: 注册仓储
webBuilder.Services.AddScoped<IEventRepository, EventRepository>();
webBuilder.Services.AddScoped<IOperationLogRepository, OperationLogRepository>();
webBuilder.Services.AddScoped<IAppSettingRepository, AppSettingRepository>();

// 步骤 5: 注册业务服务
webBuilder.Services.AddScoped<IEventService, EventService>();
webBuilder.Services.AddScoped<IReminderService, ReminderService>();
webBuilder.Services.AddScoped<IConfigurationService, ConfigurationService>();
webBuilder.Services.AddScoped<IHotKeyService, HotKeyService>();

// 步骤 6: 注册消息总线
webBuilder.Services.AddSingleton<IMessenger, MessengerHub>();

// 步骤 7: 注册原生服务
webBuilder.Services.AddSingleton<IToastNotificationService, ToastNotificationService>();
webBuilder.Services.AddSingleton<ISystemHotKeyService, SystemHotKeyService>();

// 步骤 8: 注册外部服务
webBuilder.Services.AddScoped<IHolidayService, HolidayService>();
webBuilder.Services.AddScoped<ILunarCalendarService, LunarCalendarService>();

// 步骤 9: 添加 MCP 服务器 (使用 ModelContextProtocol.AspNetCore)
webBuilder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<AI_Calendar.Infrastructure.MCP.Tools.SearchEventsTool>()
    .WithTools<AI_Calendar.Infrastructure.MCP.Tools.CreateEventTool>()
    .WithTools<AI_Calendar.Infrastructure.MCP.Tools.UpdateEventTool>()
    .WithTools<AI_Calendar.Infrastructure.MCP.Tools.DeleteEventTool>()
    .WithTools<AI_Calendar.Infrastructure.MCP.Tools.GetFreeTimeTool>()
    .WithResources<AI_Calendar.Infrastructure.MCP.Resources.CalendarResources>();

// 步骤 10: 注册后台服务（Infrastructure.BackgroundServices）
webBuilder.Services.AddHostedService<Infrastructure.BackgroundServices.ReminderBackgroundService>();
webBuilder.Services.AddHostedService<Infrastructure.BackgroundServices.HealthCheckBackgroundService>();
webBuilder.Services.AddHostedService<Infrastructure.BackgroundServices.HolidayUpdateBackgroundService>();

// 步骤 11: 添加日志
webBuilder.Services.AddLogging(configure =>
{
    configure.AddConsole();
    configure.AddDebug();
    configure.SetMinimumLevel(LogLevel.Information);
});

// 步骤 12: 构建 Web Application
var webApp = webBuilder.Build();

// 步骤 13: 初始化数据库
using (var scope = webApp.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.EnsureDatabaseCreatedAsync();
}

// 步骤 14: 启动 MCP 服务器 (后台线程)
// MCP Server 监听 http://localhost:5000
_ = Task.Run(() =>
{
    try
    {
        webApp.Run("http://localhost:5000");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"MCP Server 启动失败: {ex.Message}");
    }
});

// 步骤 15: 启动 WPF 应用 (主线程)
// WPF 窗口可以通过 webApp.Services 获取依赖注入的服务
var wpfApp = new App();
wpfApp.InitializeServiceProvider(webApp.Services);  // 注入服务容器
wpfApp.Run();
```

**关键说明**：
1. **双Host架构**：
   - WPF应用使用 `Host.CreateDefaultBuilder()`（微软官方模式，支持 `IHostedService` 后台任务）
   - MCP Server使用 `WebApplication.CreateBuilder()`（官方示例模式，支持HTTP端点）
   - 两者**不是替代关系**，而是**互补的双Host架构**
2. **MCP Server**：监听 `http://localhost:5000`（端口可根据配置调整）
3. **WPF 集成**：主线程运行 WPF 应用，通过 `InitializeServiceProvider()` 注入服务
4. **服务共享**：WPF 和 MCP Server 共享同一套服务容器（通过依赖注入）
5. **官方参考**：[AspNetCoreMcpServer 示例](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples/AspNetCoreMcpServer)

### 10.2 appsettings.json - 配置文件

**文件路径**: `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "MCP": {
    "ServerUrl": "http://localhost:5000",
    "Enabled": true
  },
  "Reminder": {
    "CheckIntervalMinutes": 1,
    "LookAheadMinutes": 5,
    "EnableFullScreenDetection": true
  },
  "BackgroundServices": {
    "HealthCheckIntervalMinutes": 30,
    "HolidayUpdateCheckTime": "02:00"
  },
  "Widget": {
    "DefaultFontSize": 14,
    "DefaultTransparency": 0.9,
    "ShowLunarDate": true,
    "ShowHolidayInfo": true
  }
}
```

---

## 11. 实现优先级

> **重要说明**：本优先级已根据 **PRD.md** 和 **System-Architecture.md V1.4** 重新对齐，确保符合产品需求文档的 Phase 划分。

### 11.1 Phase 1: 桌面挂件原型 (P0 - 必须实现)

**目标**: 实现 UI 原型，验证核心用户价值

**对应 PRD**: Phase 1 "原型：透明窗口、穿透、显示时间"

| 序号 | 模块 | 文件路径 | 对应 PRD 需求 | 预估工时 |
|:---|:---|:---|:---|:---|
| 1.1 | IWidgetViewModel | `Presentation/ViewModels/IWidgetViewModel.cs` | - | 2h |
| 1.2 | WidgetViewModel | `Presentation/ViewModels/WidgetViewModel.cs` | - | 4h |
| 1.3 | DesktopWidget.xaml | `Presentation/Views/DesktopWidget.xaml` | **DW-01~07** | 6h |
| 1.4 | 窗口穿透 | `Common/Helpers/WindowHelper.cs` | DW-01 | 2h |
| 1.5 | 透明背景 | DesktopWidget.xaml.cs | DW-02 | 1h |
| 1.6 | 层级固定 | DesktopWidget.xaml.cs | DW-03 | 1h |
| 1.7 | 时间显示 | WidgetViewModel.cs | DW-04 | 2h |
| 1.8 | 日程预览 | WidgetViewModel.cs | DW-05 | 3h |
| 1.9 | 隐私模式 | WidgetViewModel.cs | DW-07 | 2h |

**小计**: 23 小时

**验收标准**:
- [ ] 窗口鼠标穿透正常工作（DW-01）
- [ ] 背景完全透明（DW-02）
- [ ] 固定在桌面图标层（DW-03）
- [ ] 显示公历、农历、星期、时间（DW-04）
- [ ] 显示最近 3 条事件 + 剩余数量（DW-05）
- [ ] 快捷键切换隐私模式（DW-07）
- [ ] **可运行的 Demo.exe**（仅显示，无数据持久化）

---

### 11.2 Phase 2: 数据层与业务层 (P0 - 必须实现)

**目标**: 建立数据持久化和核心业务逻辑

**对应 PRD**: Phase 2 "核心：SQLite 集成、手动 CRUD、Toast 提醒"

| 序号 | 模块 | 文件路径 | 对应 PRD 需求 | 预估工时 |
|:---|:---|:---|:---|:---|
| 2.1 | Event 实体 | `Core/Entities/Event.cs` | - | 2h |
| 2.2 | OperationLog 实体 | `Core/Entities/OperationLog.cs` | SY-04 | 1h |
| 2.3 | AppSetting 实体 | `Core/Entities/AppSetting.cs` | - | 1h |
| 2.4 | IEventRepository 接口 | `Core/Interfaces/IEventRepository.cs` | - | 2h |
| 2.5 | IOperationLogRepository 接口 | `Core/Interfaces/IOperationLogRepository.cs` | SY-04 | 1h |
| 2.6 | AppDbContext | `Infrastructure/Data/AppDbContext.cs` | 3.1 | 3h |
| 2.7 | EventRepository 实现 | `Infrastructure/Data/EventRepository.cs` | - | 4h |
| 2.8 | OperationLogRepository 实现 | `Infrastructure/Data/OperationLogRepository.cs` | SY-04 | 2h |
| 2.9 | IEventService 接口 | `Application/Services/IEventService.cs` | - | 1h |
| 2.10 | EventService 实现 | `Application/Services/EventService.cs` | MCP-02~06 | 6h |
| 2.11 | IMessenger 接口 | `Application/Messenger/IMessenger.cs` | - | 1h |
| 2.12 | MessengerHub 实现 | `Application/Messenger/MessengerHub.cs` | - | 2h |
| 2.13 | ISettingsViewModel | `Presentation/ViewModels/ISettingsViewModel.cs` | MM-01~02 | 2h |
| 2.14 | SettingsViewModel | `Presentation/ViewModels/SettingsViewModel.cs` | MM-01~02 | 5h |
| 2.15 | SettingsWindow.xaml | `Presentation/Views/SettingsWindow.xaml` | MM-01 | 6h |

**小计**: 39 小时

**验收标准**:
- [ ] SQLite 数据库成功创建（3.1）
- [ ] 可以进行基本的 CRUD 操作（MM-02）
- [ ] 软删除功能正常工作（MCP-05）
- [ ] 审计日志正确记录（SY-04）
- [ ] 设置窗口可以管理事件（MM-01~02）
- [ ] **可运行的 Alpha.exe**（可手动管理）

---

### 11.3 Phase 3: MCP 集成 (P1 - 高优先级)

**目标**: 实现 AI 交互能力

**对应 PRD**: Phase 3 "AI 集成：MCP Server、安全机制、日志审计"

| 序号 | 模块 | 文件路径 | 对应 PRD 需求 | 预估工时 |
|:---|:---|:---|:---|:---|
| 3.1 | MCP Server 配置 | `Infrastructure/MCP/MCPServerExtensions.cs` | MCP-01 | 2h |
| 3.2 | SearchEventsTool | `Infrastructure/MCP/Tools/SearchEventsTool.cs` | MCP-02 | 2h |
| 3.3 | CreateEventTool | `Infrastructure/MCP/Tools/CreateEventTool.cs` | MCP-03 | 2h |
| 3.4 | UpdateEventTool | `Infrastructure/MCP/Tools/UpdateEventTool.cs` | MCP-04 | 2h |
| 3.5 | DeleteEventTool | `Infrastructure/MCP/Tools/DeleteEventTool.cs` | MCP-05 | 2h |
| 3.6 | GetFreeTimeTool | `Infrastructure/MCP/Tools/GetFreeTimeTool.cs` | MCP-06 | 2h |
| 3.7 | CalendarResources | `Infrastructure/MCP/Resources/CalendarResources.cs` | MCP-07 | 2h |
| 3.8 | SafetyMiddleware | `Infrastructure/MCP/SafetyMiddleware.cs` | 安全机制 | 3h |
| 3.9 | Program.cs 混合架构 | `Program.cs` | 架构集成 | 3h |

**小计**: 20 小时

**验收标准**:
- [ ] MCP 服务器成功启动监听 localhost:5000（MCP-01）
- [ ] 所有工具可以被 AI 调用（MCP-02~06）
- [ ] 资源可以被 AI 访问（MCP-07）
- [ ] 强制 ID 操作机制正常（安全）
- [ ] 操作审计日志正确记录（SY-04）
- [ ] **可运行的 Beta.exe**（支持 AI 控制）

---

### 11.4 Phase 4: 原生服务与提醒 (P1 - 高优先级)

**目标**: 实现系统级集成和提醒功能

**对应 PRD**: Phase 2 + Phase 4 "Toast 提醒、热键、后台服务"

| 序号 | 模块 | 文件路径 | 对应 PRD 需求 | 预估工时 |
|:---|:---|:---|:---|:---|
| 4.1 | IToastNotificationService | `Infrastructure/Native/Interfaces/IToastNotificationService.cs` | RM-02 | 1h |
| 4.2 | ToastNotificationService | `Infrastructure/Native/ToastNotificationService.cs` | RM-02~04 | 4h |
| 4.3 | ISystemHotKeyService | `Infrastructure/Native/Interfaces/ISystemHotKeyService.cs` | DW-07, MM-01 | 1h |
| 4.4 | SystemHotKeyService | `Infrastructure/Native/SystemHotKeyService.cs` | DW-07, MM-01 | 3h |
| 4.5 | IReminderBackgroundService | `Services/Interfaces/IReminderBackgroundService.cs` | RM-01 | 1h |
| 4.6 | ReminderBackgroundService | `Services/ReminderBackgroundService.cs` | RM-01~05 | 3h |
| 4.7 | 全屏应用检测 | ToastNotificationService.cs | RM-03 | 2h |
| 4.8 | 健康提醒 | ReminderBackgroundService.cs | RM-05 | 2h |

**小计**: 17 小时

**验收标准**:
- [ ] Toast 通知正常显示（RM-02）
- [ ] 热键注册成功（DW-07, MM-01）
- [ ] 后台服务正常运行（RM-01）
- [ ] 全屏应用检测正常（RM-03）
- [ ] 健康提醒功能正常（RM-05）

---

### 11.5 Phase 5: 外部服务集成 (P2 - 中优先级)

**目标**: 集成节假日和农历数据

**对应 PRD**: Phase 4 "节假日 API、开机自启、安装包" 中的节假日部分

| 序号 | 模块 | 文件路径 | 对应 PRD 需求 | 预估工时 |
|:---|:---|:---|:---|:---|
| 5.1 | IHolidayService | `Infrastructure/External/Interfaces/IHolidayService.cs` | MM-03 | 1h |
| 5.2 | HolidayService | `Infrastructure/External/HolidayService.cs` | MM-03 | 4h |
| 5.3 | ILunarCalendarService | `Infrastructure/External/Interfaces/ILunarCalendarService.cs` | DW-04 | 1h |
| 5.4 | LunarCalendarService | `Infrastructure/External/LunarCalendarService.cs` | DW-04 | 4h |
| 5.5 | IHolidayUpdateBackgroundService | `Services/Interfaces/IHolidayUpdateBackgroundService.cs` | MM-03 | 1h |
| 5.6 | HolidayUpdateBackgroundService | `Services/HolidayUpdateBackgroundService.cs` | MM-03 | 2h |
| 5.7 | 节假日缓存 | `Infrastructure/External/Cache/HolidayFileCache.cs` | MM-03 | 2h |

**小计**: 15 小时

**验收标准**:
- [ ] 节假日数据正确显示（MM-03）
- [ ] 农历日期正确显示（DW-04）
- [ ] 数据自动更新功能正常（MM-03）
- [ ] API 失败时使用本地缓存

---

### 11.6 Phase 6: 完善功能 (P2 - 中优先级)

**目标**: 实现开机自启、自动更新、系统托盘等完善功能

**对应 PRD**: Phase 4 "节假日 API、开机自启、安装包" + SY 模块

| 序号 | 模块 | 文件路径 | 对应 PRD 需求 | 预估工时 |
|:---|:---|:---|:---|:---|
| 6.1 | IAutoStartService | `Infrastructure/System/Interfaces/IAutoStartService.cs` | SY-01 | 1h |
| 6.2 | AutoStartService | `Infrastructure/System/AutoStartService.cs` | SY-01 | 3h |
| 6.3 | IUpdateService | `Infrastructure/System/Interfaces/IUpdateService.cs` | SY-03 | 1h |
| 6.4 | UpdateService | `Infrastructure/System/UpdateService.cs` | SY-03 | 4h |
| 6.5 | 系统托盘 | `Presentation/Views/TrayIcon.xaml` | SY-02 | 3h |
| 6.6 | IHealthCheckBackgroundService | `Services/Interfaces/IHealthCheckBackgroundService.cs` | - | 1h |
| 6.7 | HealthCheckBackgroundService | `Services/HealthCheckBackgroundService.cs` | - | 3h |
| 6.8 | 回收站功能 | SettingsViewModel.cs | MM-05 | 3h |

**小计**: 19 小时

**验收标准**:
- [ ] 开机自启功能正常（SY-01）
- [ ] 系统托盘图标正常（SY-02）
- [ ] 自动更新检查正常（SY-03）
- [ ] 回收站可以恢复事件（MM-05）

---

### 11.7 Phase 7: 高级功能 (P3 - 低优先级)

**目标**: 实现增强功能

| 序号 | 模块 | 文件路径 | 预估工时 |
|:---|:---|:---|:---|
| 7.1 | IHealthCheckBackgroundService | `Services/Interfaces/IHealthCheckBackgroundService.cs` | 1h |
| 7.2 | HealthCheckBackgroundService | `Services/HealthCheckBackgroundService.cs` | 3h |
| 7.3 | 批量操作 | 扩展现有服务 | 4h |
| 7.4 | 统计功能 | 扩展现有服务 | 3h |
| 7.5 | 导入导出 | 新建模块 | 6h |

**小计**: 17 小时

---

### 11.8 总工时估算（修复后）

| Phase | 描述 | 工时 | 优先级 | 对应 PRD Phase |
|:---|:---|:---|:---|:---|
| **Phase 1** | **桌面挂件原型** | **23h** | **P0** | **PRD Phase 1** |
| **Phase 2** | **数据层与业务层** | **39h** | **P0** | **PRD Phase 2** |
| **Phase 3** | **MCP 集成** | **20h** | **P1** | **PRD Phase 3** |
| **Phase 4** | **原生服务与提醒** | **17h** | **P1** | **PRD Phase 2** |
| **Phase 5** | **外部服务集成** | **15h** | **P2** | **PRD Phase 4** |
| **Phase 6** | **完善功能** | **19h** | **P2** | **PRD Phase 4** |
| **Phase 7** | **高级功能** | **17h** | **P3** | **PRD Phase 5** |
| **总计** | | **150h** | | |

**约 19 个工作日** (按每天 8 小时计算)

**对比 PRD Phase**：
- ✅ **Phase 1** (原型) → 对应新 Phase 1（23h vs PRD 的 1 周 ≈ 40h）
- ✅ **Phase 2** (核心) → 对应新 Phase 2+4（56h vs PRD 的 2 周 ≈ 80h）
- ✅ **Phase 3** (AI) → 对应新 Phase 3（20h vs PRD 的 2 周 ≈ 80h）
- ✅ **Phase 4** (完善) → 对应新 Phase 5+6（34h vs PRD 的 1 周 ≈ 40h）

**说明**：重新对齐后，优先级顺序符合 PRD 要求（先可见原型，后数据层）

---

## 12. 测试策略

### 12.1 单元测试

#### 12.1.1 领域层测试

**测试框架**: xUnit

| 测试类 | 测试内容 | 预估用例数 |
|:---|:---|:---|
| EventTests | 实体领域逻辑 | 8 |
| - Event_IsUrgent_ReturnsTrue_WhenWithinOneHour | 紧急事件判断 | 1 |
| - Event_ShouldRemind_ReturnsTrue_WhenTimeReached | 提醒时间判断 | 1 |
| - Event_ConflictsWith_ReturnsTrue_WhenOverlapping | 时间冲突检测 | 1 |
| - Event_MarkAsDeleted_SetsIsDeletedToTrue | 软删除 | 1 |
| - Event_Restore_SetsIsDeletedToFalse | 恢复删除 | 1 |
| - Event_Duration_ReturnsTimeSpan_WhenEndTimeSet | 持续时间计算 | 1 |
| - Event_GetDisplayText_ReturnsFormattedString | 显示文本 | 1 |
| - Event_ConflictsWith_IgnoresDeletedEvents | 忽略已删除 | 1 |

#### 12.1.2 应用层测试

**测试框架**: xUnit + Moq

| 测试类 | 测试内容 | 预估用例数 |
|:---|:---|:---|
| EventServiceTests | 事件服务业务逻辑 | 12 |
| - SearchAsync_ReturnsFilteredResults | 搜索功能 | 1 |
| - CreateAsync_ThrowsException_WhenConflictExists | 冲突检测 | 1 |
| - CreateAsync_CreatesEvent_WhenValid | 创建事件 | 1 |
| - UpdateAsync_ThrowsException_WhenNotFound | 更新不存在 | 1 |
| - UpdateAsync_AppliesChanges_WhenFound | 更新事件 | 1 |
| - SoftDeleteAsync_RequiresConfirmation | 删除确认 | 1 |
| - GetFreeTimeAsync_ReturnsSlots_WhenAvailable | 空闲时间 | 1 |
| - RestoreEventAsync_RestoresDeletedEvent | 恢复事件 | 1 |
| - EmptyRecycleBinAsync_DeletesOldEvents | 清空回收站 | 1 |
| - CreateBatchAsync_CreatesMultiple | 批量创建 | 1 |
| - GetTodayStatisticsAsync_ReturnsCorrectStats | 今日统计 | 1 |
| - GetWeekStatisticsAsync_ReturnsCorrectStats | 本周统计 | 1 |

#### 12.1.3 基础设施层测试

**测试框架**: xUnit + SQLite InMemory

> **说明**：使用 `Microsoft.Data.Sqlite` 的内存模式，符合 System-Architecture.md 技术选型

| 测试类 | 测试内容 | 预估用例数 |
|:---|:---|:---|
| EventRepositoryTests | 仓储数据访问 | 10 |
| - AddAsync_ReturnsEventWithId | 添加事件 | 1 |
| - GetByIdAsync_ReturnsEvent_WhenExists | 查询事件 | 1 |
| - SearchAsync_FiltersByKeyword | 关键词搜索 | 1 |
| - SearchAsync_FiltersByTimeRange | 时间范围搜索 | 1 |
| - SoftDeleteAsync_SetsIsDeletedToTrue | 软删除 | 1 |
| - GetUpcomingEventsAsync_ReturnsFutureEvents | 即将到来 | 1 |
| - HasConflictAsync_ReturnsTrue_WhenOverlapping | 冲突检测 | 1 |
| - GetConflictingEventsAsync_ReturnsConflictingList | 冲突列表 | 1 |
| - GetDeletedEventsAsync_ReturnsRecentlyDeleted | 已删除事件 | 1 |
| - CountEventsAsync_ReturnsCorrectCount | 事件计数 | 1 |

**单元测试总计**: 30 个用例

---

### 12.2 集成测试

#### 12.2.1 MCP 工具集成测试

| 测试场景 | 描述 |
|:---|:---|
| MCP 工具端到端测试 | 通过 MCP 客户端调用所有工具，验证返回结果 |
| MCP 资源访问测试 | 验证资源可以被正确读取 |
| MCP 错误处理测试 | 验证异常情况下工具的正确响应 |

#### 12.2.2 数据库集成测试

| 测试场景 | 描述 |
|:---|:---|
| 数据库事务测试 | 验证事务的提交和回滚 |
| 并发操作测试 | 验证多线程场景下的数据一致性 |
| 数据库迁移测试 | 验证 EF Core 迁移的正确性 |

#### 12.2.3 消息总线集成测试

| 测试场景 | 描述 |
|:---|:---|
| 发布订阅测试 | 验证消息的正确发布和订阅 |
| 跨模块通信测试 | 验证不同模块间的事件传递 |

**集成测试总计**: 9 个场景

---

### 12.3 UI 测试

#### 12.3.1 桌面挂件测试

| 测试场景 | 描述 |
|:---|:---|
| 挂件显示测试 | 验证挂件正确显示时间和事件 |
| 隐私模式测试 | 验证隐私模式的切换 |
| 拖拽定位测试 | 验证挂件的拖拽功能 |

#### 12.3.2 设置窗口测试

| 测试场景 | 描述 |
|:---|:---|
| 事件管理测试 | 创建、编辑、删除事件的 UI 流程 |
| 配置保存测试 | 验证配置的正确保存和加载 |
| 回收站功能测试 | 恢复和永久删除功能 |

**UI 测试总计**: 6 个场景

---

### 12.4 性能测试

| 测试类型 | 指标 | 目标值 |
|:---|:---|:---|
| 数据库查询性能 | 查询响应时间 | < 100ms |
| MCP 工具响应性能 | 工具执行时间 | < 500ms |
| 内存占用 | 应用内存使用 | < 100MB |
| 启动时间 | 应用启动耗时 | < 3s |

---

### 12.5 测试覆盖率目标

| 层级 | 覆盖率目标 |
|:---|:---|
| Domain Layer | 90%+ |
| Application Layer | 80%+ |
| Infrastructure Layer | 70%+ |
| Presentation Layer | 50%+ |

---

## 附录

### A. 命名规范速查表

| 类型 | 命名规范 | 示例 |
|:---|:---|:---|
| **命名空间** | `AI_Calendar.{Layer}.{Module}` | `AI_Calendar.Application.Services` |
| **接口** | `I` + 功能名 + 类型 | `IEventService`, `IEventRepository` |
| **类** | 功能名 + 类型 | `EventService`, `EventRepository` |
| **方法** | 动词 + 名词 | `GetEventAsync`, `CreateEventAsync` |
| **异步方法** | 必须以 `Async` 结尾 | `SearchAsync`, `UpdateAsync` |
| **布尔方法** | `Is`, `Has`, `Can` 开头 | `IsUrgent`, `HasConflict` |
| **DTO** | 实体名 + `Dto` | `EventDto` |
| **显示模型** | 实体名 + `DisplayModel` | `EventDisplayModel` |
| **异常** | 异常名 + `Exception` | `EventConflictException` |
| **枚举** | 描述性名称 | `Priority`, `AppEvent` |

### B. 接口设计原则速查表

| 原则 | 说明 |
|:---|:---|
| **单一职责** | 每个接口只负责一个明确的功能领域 |
| **接口隔离** | 客户端不应依赖它不需要的接口方法 |
| **依赖倒置** | 高层模块不依赖低层模块，都依赖抽象 |
| **开放封闭** | 对扩展开放，对修改封闭 |
| **里氏替换** | 子类可以替换父类而不影响程序正确性 |

### C. 返回值规范速查表

| 场景 | 返回值类型 | 说明 |
|:---|:---|:---|
| **查询单个对象** | `Task<T?>` | 未找到时返回 null |
| **查询列表** | `Task<List<T>>` | 即使为空也返回空列表 |
| **创建操作** | `Task<T>` | 返回创建后的实体（含 ID） |
| **更新操作** | `Task<T>` | 返回更新后的实体 |
| **删除操作** | `Task` | 仅表示操作完成，通过异常表示失败 |
| **布尔判断** | `Task<bool>` | 返回判断结果 |

### D. 异常处理规范速查表

| 异常类型 | 使用场景 | HTTP 状态码 |
|:---|:---|:---|
| `InvalidOperationException` | 业务逻辑错误 | 400 |
| `EventConflictException` | 时间冲突 | 409 |
| `KeyNotFoundException` | 资源不存在 | 404 |
| `UnauthorizedAccessException` | 权限不足 | 403 |
| `ArgumentException` | 参数验证失败 | 400 |
| `ValidationException` | 实体验证失败 | 400 |

---

**文档结束**

---

## 版本历史

| 版本 | 日期 | 作者 | 变更说明 |
|:---|:---|:---|:---|
| V1.2 | 2026-03-11 | AI Assistant | **文档冲突修复**：澄清双Host架构、移除"混合架构"误导描述 |
| V1.1 | 2026-03-11 | AI Assistant | **修复版**：统一架构、简化实体、对齐 PRD 优先级 |
| V1.0 | 2026-03-11 | AI Assistant | 初始版本，整合 Module-Interface-Design.md 和 System-Module-Design.md |

---

## V1.1 修复详情

### 修复内容

#### 1. 架构模式澄清 ⚠️ 重大修复
- **问题**：原 V1.0 描述"混合架构：使用 WebApplication.CreateBuilder() 而非 Host.CreateApplicationBuilder()"存在误导
- **修复**：澄清双Host架构设计（WPF Host + MCP Web Host，两者互补而非替代）
- **影响**：10.1 节 Program.cs 说明更新
- **符合**：System-Architecture.md V1.6 的双Host架构设计

#### 2. Event 实体简化 ⚠️ 重大修复
- **问题**：原 V1.0 使用 EF Core 特性（`[Key]`、`[Required]`、`[MaxLength]`）和导航属性
- **修复**：移除所有验证注解和导航属性 `ICollection<Reminder>`
- **影响**：5.1 节 Event 实体定义
- **符合**：System-Architecture.md L502-L506 的简化实体模型

#### 3. Phase 优先级重新对齐 ⚠️ 重大修复
- **问题**：原 V1.0 的 Phase 顺序（数据层 → 业务层 → UI）与 PRD 不一致
- **修复**：调整为 Phase 1 (UI 原型) → Phase 2 (数据层)
- **影响**：11.1-11.7 节所有 Phase 重新排序
- **符合**：PRD.md L199-L205 的开发里程碑

#### 4. 补充缺失的 PRD 功能 ✅ 新增
- **新增**：IAutoStartService（开机自启 SY-01）
- **新增**：IUpdateService（自动更新 SY-03）
- **新增**：IScreenHelper（多屏适配 DW-06）
- **影响**：新增 10.1-10.3 节系统基础服务接口
- **符合**：PRD.md 的系统基础模块要求

#### 5. 测试策略调整 ✅ 优化
- **问题**：原 V1.0 使用 EF Core InMemory
- **修复**：改为 SQLite InMemory（符合技术选型）
- **影响**：12.1.3 节测试框架说明

### 修复验证

| 验证项 | System-Architecture.md | PRD.md | Complete-System-Design.md V1.1 |
|:---|:---|:---|:---:|
| **架构模式** | Web SDK + MCP AspNetCore | - | ✅ 符合 |
| **实体模型** | 简化模型（无注解） | - | ✅ 符合 |
| **Phase 顺序** | - | 原型 → 核心 → AI | ✅ 符合 |
| **技术选型** | SQLite (无 EF Core 注解) | - | ✅ 符合 |
| **功能覆盖** | - | SY-01, SY-03, DW-06 | ✅ 符合 |

### 工时调整

| 阶段 | V1.0 工时 | V1.1 工时 | 变化 |
|:---|:---|:---|:---|
| Phase 1 | 17h (数据层) | **23h (UI 原型)** | +6h |
| Phase 2 | 18h (业务层) | **39h (数据+业务)** | +21h |
| Phase 3 | 16h (MCP) | **20h (MCP)** | +4h |
| Phase 4 | 13h (原生服务) | **17h (原生服务)** | +4h |
| Phase 5 | 25h (表示层) | **15h (外部服务)** | -10h |
| **总计** | **119h** | **150h** | **+31h** |

**说明**：V1.1 工时更贴近 PRD 估算（150h ≈ PRD 的 5 周 ≈ 200h）

### 兼容性说明

- ✅ **接口定义**：保持 V1.0 的所有接口定义不变
- ✅ **数据模型**：属性名称不变，仅移除注解
- ✅ **功能范围**：完全覆盖 PRD 所有 P0-P1 功能
- ⚠️ **实现细节**：Program.cs 架构变更，需重新适配

---

## 13. 版本历史 (Version History)

### V1.2 (2026-03-11)

**架构澄清**：
- ✅ 澄清双Host架构设计（WPF Host + MCP Web Host）
- ✅ 移除"混合架构"误导描述
- ✅ 添加官方AspNetCoreMcpServer示例引用
- ✅ 明确：WPF使用`Host.CreateDefaultBuilder()`，MCP使用`WebApplication.CreateBuilder()`

**冲突解决同步**：
- ✅ 项目类型：WPF应用（`Microsoft.NET.Sdk` + `UseWPF=true`）
- ✅ MCP工具：统一为7个工具（移除MCP-01服务宿主）
- ✅ Priority类型：使用enum + EF Core值转换器
- ✅ 数据库路径：程序目录/data.db
- ✅ 节假日：ChineseCalendar 1.0.4库
- ✅ AppEvent枚举：统一为9个枚举值
- ✅ Description字段：确认包含在Event实体中

**依赖文档更新**：
- System-Architecture.md: V1.5 → V1.6
- PRD.md: V1.3 → V1.4
- API-Interface-Design.md: V1.3 → V1.4
- Database-Design.md: V1.2（保持）
- API-Interface-Design.md: V1.2 → V1.3
- Detailed-Design.md: V1.0 → V1.1

**架构影响**：
- 项目SDK从Sdk.Web改为Sdk（WPF）
- TargetFramework从net9.0改为net8.0
- 数据库路径从用户目录改为程序目录
- 节假日从API改为ChineseCalendar库

### V1.1 (2026-03-11)

**修复内容**：
- 统一架构模式、简化实体模型、对齐 PRD 优先级
- 修复验证：架构模式、实体模型、Phase顺序、技术选型、功能覆盖
- 工时调整：从119h调整到150h

---**V1.1 文档结束**
