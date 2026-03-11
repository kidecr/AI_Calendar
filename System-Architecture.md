# 系统架构设计文档 (System Architecture Design)
# Desktop AI Calendar (DAC)

| 文档版本 | V1.5 (项目类型改为WPF、更新MCP工具、数据库路径) |
| :--- | :--- |
| **项目名称** | Desktop AI Calendar |
| **架构师** | AI Assistant |
| **最后更新** | 2026-03-11 |

---

## 1. 架构概览 (Architecture Overview)

### 1.1 总体架构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Presentation Layer                          │
├─────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ Desktop      │  │ Settings     │  │ System Tray  │              │
│  │ Widget View  │  │ Window View  │  │ Icon         │              │
│  │ (穿透/透明)  │  │ (可交互)     │  │ (H.Notify)   │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
└─────────────────────────────────────────────────────────────────────┘
                                 ↕
┌─────────────────────────────────────────────────────────────────────┐
│                    Application Layer (WPF App)                      │
├─────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ ViewModels   │  │ Messenger    │  │ Services     │              │
│  │ (MVVM)       │  │ (Event Bus)  │  │ (Business)   │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
└─────────────────────────────────────────────────────────────────────┘
                                 ↕
┌─────────────────────────────────────────────────────────────────────┐
│                      Domain Layer (Core)                            │
├─────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ Entities     │  │ Repositories │  │ Domain       │              │
│  │ (Event, etc) │  │ (IData)      │  │ Services     │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
└─────────────────────────────────────────────────────────────────────┘
                                 ↕
┌─────────────────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                              │
├─────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ Data         │  │ MCP          │  │ External     │              │
│  │ (SQLite)     │  │ Server       │  │ Services     │              │
│  ├──────────────┤  ├──────────────┤  ├──────────────┤              │
│  │ Background   │  │ Native       │  │ Logging      │              │
│  │ Services     │  │ Services     │  │ (Audit)      │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
└─────────────────────────────────────────────────────────────────────┘
```

### 1.2 架构风格

本项目采用 **分层架构 (Layered Architecture)** + **MVVM 模式** + **插件化设计**：

- **分层架构**：Presentation → Application → Domain → Infrastructure（4 层架构）
- **MVVM 模式**：UI 层采用 Model-View-ViewModel 模式
- **依赖注入**：使用 Microsoft.Extensions.DependencyInjection
- **事件驱动**：使用 Messenger 模式实现模块间通信
- **后台服务**：作为 Infrastructure Layer 的独立子模块，使用 IHostedService 实现定时任务

---

## 2. 技术栈映射 (Technology Stack Mapping)

### 2.1 已安装库的功能映射

| NuGet 包 | 版本 | 在项目中的应用 | 负责模块 |
| :--- | :--- | :--- | :--- |
| **ChineseCalendar** | 1.0.4 | 中国农历日期、传统节假日、特殊节假日 | DW-04 农历显示、MM-03 节假日 |
| **H.NotifyIcon.Wpf** | 2.4.1 | 系统托盘图标、右键菜单 | SY-02 系统托盘 |
| **Microsoft.Data.Sqlite** | 10.0.3 | SQLite 数据库访问 | 3.1 数据库层 |
| **Microsoft.Extensions.Configuration** | 10.0.3 | 配置管理（字体、位置等） | MM-04 外观配置 |
| **Microsoft.Extensions.Configuration.Json** | 10.0.3 | JSON 配置文件读取 | MM-04 外观配置 |
| **Microsoft.Extensions.DependencyInjection** | 10.0.3 | 依赖注入容器 | 全局 |
| **Microsoft.Extensions.Hosting** | 10.0.3 | 应用生命周期管理、后台服务 | RM-01 后台轮询 |
| **ModelContextProtocol.AspNetCore** | 1.1.0 | MCP Server 实现 | MCP 服务模块 |
| **Serilog** | 4.3.1 | 结构化日志记录 | SY-04 日志审计 |

### 2.2 外部 API 资源

| API 端点 | 用途 | 更新策略 | 负责模块 |
| :--- | :--- | :--- | :--- |
| **https://cdn.jsdelivr.net/npm/chinese-days/dist/years/{年份}.json** | 中国公历节假日及调休数据 | 每年 12 月自动更新下一年度 | MM-03 节假日同步 |

### 2.3 需要额外引入的技术

| 功能 | 推荐技术/库 | 版本建议 | 用途 |
| :--- | :--- | :--- | :--- |
| **Toast 通知** | `Microsoft.Toolkit.Uwp.Notifications` | **7.1.3** | RM-02 原生通知 |
| **热键支持** | `P/Invoke` (user32.dll) | - | DW-07, MM-01 快捷键 |
| **多屏 DPI** | `System.Windows.Forms` | - | DW-06 多屏适配 |

**技术实现说明：**

#### Toast 通知

**NuGet 包安装：**
```bash
Install-Package Microsoft.Toolkit.Uwp.Notifications
```

**必要的 using 语句：**
```csharp
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
```

**系统要求：**
- Windows 10 或更高版本

**核心功能：**

1. **基本文本通知**
```csharp
public static void SendBasicNotification(string title, string content)
{
    var builder = new ToastContentBuilder()
        .AddText(title)
        .AddText(content);

    builder.Show();
}
```

2. **带图片的通知**
```csharp
public static void SendNotificationWithImage(string title, string content, string imageUrl)
{
    var builder = new ToastContentBuilder()
        .AddText(title)
        .AddText(content)
        .AddInlineImage(new Uri(imageUrl));

    builder.Show();
}
```

3. **带按钮的交互式通知**
```csharp
public static void SendNotificationWithActions(string title, string content)
{
    var builder = new ToastContentBuilder()
        .AddText(title)
        .AddText(content)
        .AddButton(new ToastButton()
            .SetContent("确定")
            .AddArgument("action", "confirm"))
        .AddButton(new ToastButton()
            .SetContent("取消")
            .AddArgument("action", "cancel"));

    builder.Show();
}
```

4. **带进度条的通知**
```csharp
public static void SendProgressNotificationWithStatus(string title, string content,
    double progressValue, string status)
{
    progressValue = Math.Max(0, Math.Min(1, progressValue));

    var builder = new ToastContentBuilder()
        .AddText(title)
        .AddText(content)
        .AddProgressBar("progressBar1", progressValue,
            valueStringOverride: $"{(progressValue * 100):F0}%",
            status: status);

    builder.Show();
}
```

5. **计划通知**
```csharp
public static void SendScheduledNotification(string title, string content, DateTimeOffset scheduleTime)
{
    if (scheduleTime <= DateTimeOffset.Now)
    {
        throw new ArgumentException("计划时间必须是将来的时间");
    }

    var builder = new ToastContentBuilder()
        .AddText(title)
        .AddText(content);

    var scheduledToast = new ScheduledToastNotification(
        builder.GetXml(),
        scheduleTime);

    ToastNotificationManagerCompat.CreateToastNotifier();
    ToastNotificationManagerCompat.CreateToastNotifier().AddToSchedule(scheduledToast);
}
```

6. **事件处理**
```csharp
// 在程序启动时注册事件
ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;

private static void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
{
    var args = ToastArguments.Parse(e.Argument);
    string action = args["action"];

    switch (action)
    {
        case "confirm":
            // 处理确定按钮点击
            break;
        case "cancel":
            // 处理取消按钮点击
            break;
    }
}
```

**特性支持：**
- ✅ Windows 10/11 原生通知中心
- ✅ 按钮交互操作（延后提醒、关闭等）
- ✅ 进度条、图片等富媒体内容
- ✅ 计划通知（定时提醒）
- ✅ 事件回调处理

#### 热键支持
- 使用 P/Invoke 调用 `user32.dll` 的 `RegisterHotKey` 和 `UnregisterHotKey` 函数
- 支持组合键：Alt、Ctrl、Shift、Windows
- 需要获取 WPF 窗口句柄：`new WindowInteropHelper(window).Handle`

---

## 3. 模块设计 (Module Design)

### 3.0 功能ID追溯矩阵 (Feature ID Traceability)

| 功能模块 | 功能ID | 功能名称 | 设计文档章节 |
|:---|:---|:---|:---|
| **桌面挂件** | DW-01 | 窗口穿透 | §4.1 Presentation Layer |
| | DW-02 | 透明背景 | §4.1 Presentation Layer |
| | DW-03 | 层级固定 | §4.1 Presentation Layer |
| | DW-04 | 信息展示 | §4.1 Presentation Layer |
| | DW-05 | 日程预览 | §4.1 Presentation Layer |
| | DW-06 | 多屏适配 | §9 性能优化 |
| | DW-07 | 隐私模式 | §4.2 Application Layer |
| **MCP服务** | - | 服务宿主（非工具） | §4.4 Infrastructure Layer |
| | MCP-02 | 搜索事件 | §4.2 Application Layer |
| | MCP-03 | 创建事件 | §4.2 Application Layer |
| | MCP-04 | 更新事件 | §4.2 Application Layer |
| | MCP-05 | 删除事件 | §4.2 Application Layer |
| | MCP-06 | 空闲分析 | §4.2 Application Layer |
| | MCP-07 | 恢复事件 | §4.2 Application Layer |
| **手动管理** | MM-01 | 设置窗口 | §4.1 Presentation Layer |
| | MM-02 | 事件CRUD | §4.2 Application Layer |
| | MM-03 | 节假日同步 | §4.4 Infrastructure Layer |
| | MM-04 | 外观配置 | §6 配置管理 |
| | MM-05 | 回收站 | §4.2 Application Layer |
| **提醒通知** | RM-01 | 后台轮询 | §4.4 Infrastructure Layer |
| | RM-02 | Toast通知 | §4.4 Infrastructure Layer |
| | RM-03 | 智能免打扰 | §4.4 Infrastructure Layer |
| | RM-04 | 延后提醒 | §4.2 Application Layer |
| | RM-05 | 健康提醒 | §4.4 Infrastructure Layer |
| **系统基础** | SY-01 | 开机自启 | §9 部署架构 |
| | SY-02 | 系统托盘 | §4.1 Presentation Layer |
| | SY-03 | 自动更新 | §4.4 Infrastructure Layer |
| | SY-04 | 日志审计 | §4.4 Infrastructure Layer |

### 3.1 核心模块划分

```
AI_Calendar/
├── Core/                          # 领域层
│   ├── Entities/                  # 实体
│   │   ├── Event.cs
│   │   ├── OperationLog.cs
│   │   └── AppSetting.cs
│   ├── Interfaces/                # 接口
│   │   ├── IEventRepository.cs
│   │   ├── IEventService.cs
│   │   └── IMessenger.cs
│   └── Exceptions/                # 异常定义
│       └── EventConflictException.cs
│
├── Infrastructure/                # 基础设施层
│   ├── Data/                      # 数据访问
│   │   ├── AppDbContext.cs
│   │   ├── EventRepository.cs
│   │   └── OperationLogRepository.cs
│   ├── MCP/                       # MCP 服务
│   │   ├── MCPServer.cs
│   │   ├── Tools/
│   │   │   ├── ListEventsTool.cs           # 获取事件列表（MCP-02.5）
│   │   │   ├── SearchEventsTool.cs         # 搜索事件（MCP-02）
│   │   │   ├── CreateEventTool.cs          # 创建事件（MCP-03）
│   │   │   ├── UpdateEventTool.cs          # 更新事件（MCP-04）
│   │   │   ├── DeleteEventTool.cs          # 删除事件（MCP-05）
│   │   │   ├── GetFreeTimeTool.cs          # 查询空闲时间（MCP-06）
│   │   │   └── RestoreEventTool.cs         # 恢复已删除事件（MCP-07）
│   │   └── SafetyMiddleware.cs
│   ├── External/                  # 外部服务
│   │   ├── HolidayService.cs      # 节假日服务（ChineseCalendar 1.0.4库）
│   │   ├── LunarCalendarService.cs # 农历服务（ChineseCalendar 库）
│   │   └── Cache/                 # 缓存管理
│   │       ├── IHolidayCache.cs
│   │       └── HolidayFileCache.cs
│   └── Logging/                   # 日志
│       └── AuditLogger.cs
│
├── Application/                   # 应用层
│   ├── Services/                  # 业务服务
│   │   ├── EventService.cs
│   │   ├── ReminderService.cs
│   │   └── ConfigurationService.cs
│   ├── ViewModels/                # MVVM
│   │   ├── WidgetViewModel.cs
│   │   └── SettingsViewModel.cs
│   └── Messenger/                 # 消息总线
│       └── MessengerHub.cs
│
├── Presentation/                  # 表示层
│   ├── Views/
│   │   ├── DesktopWidget.xaml     # 透明挂件
│   │   └── SettingsWindow.xaml    # 设置窗口
│   ├── Controls/                  # 自定义控件
│   │   └── EventListItem.xaml
│   └── Converters/                # 值转换器
│       └── DateTimeConverter.cs
│
├── Infrastructure/                # 基础设施层
│   ├── BackgroundServices/        # 后台服务（独立子模块）
│   │   ├── ReminderBackgroundService.cs
│   │   ├── HealthCheckBackgroundService.cs
│   │   └── HolidayUpdateBackgroundService.cs
│   ├── Native/                    # 原生 API 封装
│   │   ├── SystemHotKey.cs        # 热键 P/Invoke 封装
│   │   └── ToastNotificationService.cs
│   ├── Data/                      # 数据访问
│   │   ├── AppDbContext.cs
│   │   ├── EventRepository.cs
│   │   └── OperationLogRepository.cs
│   ├── MCP/                       # MCP 服务
│   │   ├── MCPServer.cs
│   │   ├── Tools/
│   │   │   ├── ListEventsTool.cs           # 获取事件列表（MCP-02.5）
│   │   │   ├── SearchEventsTool.cs         # 搜索事件（MCP-02）
│   │   │   ├── CreateEventTool.cs          # 创建事件（MCP-03）
│   │   │   ├── UpdateEventTool.cs          # 更新事件（MCP-04）
│   │   │   ├── DeleteEventTool.cs          # 删除事件（MCP-05）
│   │   │   ├── GetFreeTimeTool.cs          # 查询空闲时间（MCP-06）
│   │   │   └── RestoreEventTool.cs         # 恢复已删除事件（MCP-07）
│   │   └── SafetyMiddleware.cs
│   ├── External/                  # 外部服务
│   │   ├── HolidayService.cs      # 节假日服务（ChineseCalendar 1.0.4库）
│   │   ├── LunarCalendarService.cs # 农历服务（ChineseCalendar 库）
│   │   └── Cache/                 # 缓存管理
│   │       ├── IHolidayCache.cs
│   │       └── HolidayFileCache.cs
│   └── Logging/                   # 日志
│       └── AuditLogger.cs
│
├── Application/                   # 应用层
│   ├── Services/                  # 业务服务
│   │   ├── EventService.cs
│   │   ├── ReminderService.cs
│   │   ├── HotKeyService.cs       # 热键管理服务
│   │   └── ConfigurationService.cs
│   ├── ViewModels/                # MVVM
│   │   ├── WidgetViewModel.cs
│   │   └── SettingsViewModel.cs
│   └── Messenger/                 # 消息总线
│       └── MessengerHub.cs
│
├── Presentation/                  # 表示层
│   ├── Views/
│   │   ├── DesktopWidget.xaml     # 透明挂件
│   │   └── SettingsWindow.xaml    # 设置窗口
│   ├── Controls/                  # 自定义控件
│   │   └── EventListItem.xaml
│   └── Converters/                # 值转换器
│       └── DateTimeConverter.cs
│
└── Common/                        # 公共组件
    ├── Helpers/
    │   ├── WindowHelper.cs        # 穿透、层级设置
    │   └── ScreenHelper.cs        # 多屏适配
    └── Extensions/
        └── DateTimeExtensions.cs
```

---

## 4. 分层详细设计 (Layer Details)

### 4.1 Presentation Layer (表示层)

#### 职责
- UI 渲染与用户交互
- 数据绑定与命令处理
- 窗口行为控制（穿透、透明、层级）

#### 关键类

```csharp
// DesktopWidget.xaml.cs - 桌面挂件窗口
public partial class DesktopWidget : Window
{
    // DW-01: 窗口穿透
    private void SetTransparentMouseThrough()
    {
        // P/Invoke: SetWindowLong with WS_EX_TRANSPARENT
    }

    // DW-02: 透明背景
    public DesktopWidget()
    {
        AllowsTransparency = true;
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
    }

    // DW-03: 层级固定
    private void SetDesktopLayer()
    {
        ShowInTaskbar = false;
        Topmost = false;
        // Owner = IntPtr.Zero (通过 P/Invoke)
    }
}

// WidgetViewModel.cs - 挂件视图模型
public class WidgetViewModel : INotifyPropertyChanged
{
    public ObservableCollection<EventDisplayModel> UpcomingEvents { get; }
    public string CurrentDate { get; }
    public string LunarDate { get; }

    // DW-05: 仅显示最近 3 条事件
    public void RefreshEvents()
    {
        var events = _eventService.GetUpcomingEvents(3);
        UpcomingEvents.Clear();
        foreach (var evt in events)
            UpcomingEvents.Add(evt);
    }
}
```

---

### 4.2 Application Layer (应用层)

#### 职责
- 业务逻辑编排
- ViewModel 与 Service 的协调
- 事件聚合与消息分发

#### Messenger 设计

```csharp
// MessengerHub.cs - 事件总线
public enum AppEvent
{
    EventCreated,
    EventUpdated,
    EventDeleted,
    SettingsChanged,
    RefreshRequested,
    PrivacyModeToggled,    // DW-07: 隐私模式切换
    HolidayDataUpdated     // 节假日数据更新
}

public class MessengerHub
{
    public void Subscribe<T>(AppEvent @event, Action<T> handler);
    public void Publish<T>(AppEvent @event, T payload);
}

// 使用场景：MCP 修改事件后通知 UI 刷新
public class CreateEventTool
{
    private readonly MessengerHub _messenger;

    public async Task<ToolResult> Execute(CreateEventParams args)
    {
        var newEvent = await _eventService.CreateAsync(args);
        _messenger.Publish(AppEvent.EventCreated, newEvent);
        return ToolResult.Success(newEvent);
    }
}
```

#### 服务设计

```csharp
// EventService.cs - 事件业务服务
public class EventService : IEventService
{
    // MCP-02: 搜索事件（修改操作的前置）
    public async Task<List<Event>> SearchAsync(string query, DateTime? start, DateTime? end);

    // MCP-03: 创建事件（冲突检测）
    public async Task<Event> CreateAsync(EventDto dto)
    {
        if (await CheckConflictAsync(dto.StartTime, dto.EndTime))
            throw new EventConflictException("时间冲突");
        // ...
    }

    // MCP-04: 更新事件（必须基于 ID）
    public async Task<Event> UpdateAsync(int id, EventDto changes);

    // MCP-05: 删除事件（软删除）
    public async Task SoftDeleteAsync(int id, bool confirm);

    // MCP-06: 空闲时间分析
    public async Task<List<TimeSlot>> GetFreeTimeAsync(TimeSpan duration, DateTime date);
}

// ReminderService.cs - 提醒服务
public class ReminderService : IReminderService
{
    // RM-04: 延后提醒
    public async Task SnoozeAsync(int eventId, TimeSpan delay);
}
```

#### 4.2.1 UI 同步机制 (UI Synchronization)

**设计原理**：

当 MCP Server 接收 LLM 指令修改数据库后，需要通知 WPF UI 更新显示。本项目采用**应用层消息总线（IMessenger）**实现发布-订阅模式，而非数据库触发器或文件监听。

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
    EventCreated,

    /// <summary>事件更新时触发</summary>
    EventUpdated,

    /// <summary>事件删除时触发</summary>
    EventDeleted,

    /// <summary>设置变更时触发</summary>
    SettingsChanged,

    /// <summary>刷新请求时触发</summary>
    RefreshRequested,

    /// <summary>隐私模式切换时触发</summary>
    PrivacyModeToggled,

    /// <summary>节假日数据更新时触发</summary>
    HolidayDataUpdated
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

**完整调用流程示例**：

```
1. 用户在 Claude 中输入："明天下午3点创建会议"

2. Claude 调用 MCP 工具：
   MCP Server → CreateEventTool.ExecuteAsync()
     ↓
   EventService.CreateAsync(dto)
     ↓ (1) 写入数据库
   AppDbContext.Events.Add(created)
     ↓ (2) 发布消息
   IMessenger.Publish(AppEvent.EventCreated, created)
     ↓ (3) 通知订阅者
   WidgetViewModel.OnEventCreated(created)
     ↓ (4) 更新 UI
   UpcomingEvents.Add(new EventModel(created))
     ↓
   WPF 界面实时显示新事件 ✅
```

**MessengerHub 实现示例**：

```csharp
namespace AI_Calendar.Application.Messenger;

/// <summary>
/// 内存消息总线实现（单进程模式）
/// </summary>
public class MessengerHub : IMessenger, IDisposable
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
            if (_subscribers.TryGetValue(@event, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        ((Action<T>)handler)(payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "处理事件失败: {Event}", @event);
                    }
                }
            }
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
            if (_subscribers.TryGetValue(@event, out var handlers))
            {
                handlers.Remove(handler);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
}
```

**ViewModel 订阅示例**：

```csharp
namespace AI_Calendar.Application.ViewModels;

public class WidgetViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly IEventService _eventService;

    public ObservableCollection<EventModel> UpcomingEvents { get; }

    public WidgetViewModel(
        IMessenger messenger,
        IEventService eventService)
    {
        _messenger = messenger;
        _eventService = eventService;

        // 订阅消息
        _messenger.Subscribe<Event>(AppEvent.EventCreated, OnEventCreated);
        _messenger.Subscribe<Event>(AppEvent.EventUpdated, OnEventUpdated);
        _messenger.Subscribe<int>(AppEvent.EventDeleted, OnEventDeleted);

        // 初始加载数据
        _ = LoadUpcomingEventsAsync();
    }

    private void OnEventCreated(Event newEvent)
    {
        // 在 UI 线程更新
        Application.Current.Dispatcher.Invoke(() =>
        {
            UpcomingEvents.Add(new EventModel(newEvent));
        });
    }

    private void OnEventUpdated(Event updated)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = UpcomingEvents.FirstOrDefault(e => e.Id == updated.Id);
            if (existing != null)
            {
                existing.UpdateFrom(updated);
            }
        });
    }

    private void OnEventDeleted(int eventId)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var toRemove = UpcomingEvents.FirstOrDefault(e => e.Id == eventId);
            if (toRemove != null)
            {
                UpcomingEvents.Remove(toRemove);
            }
        });
    }

    // 防止内存泄漏：取消订阅
    public void Dispose()
    {
        _messenger.Unsubscribe<Event>(AppEvent.EventCreated, OnEventCreated);
        _messenger.Unsubscribe<Event>(AppEvent.EventUpdated, OnEventUpdated);
        _messenger.Unsubscribe<int>(AppEvent.EventDeleted, OnEventDeleted);
    }
}
```

**依赖注入配置**：

```csharp
// Program.cs 或 App.xaml.cs
public void ConfigureServices(IServiceCollection services)
{
    // 注册消息总线（单例）
    services.AddSingleton<IMessenger, MessengerHub>();

    // 注册业务服务
    services.AddScoped<IEventService, EventService>();
    services.AddScoped<IReminderService, ReminderService>();

    // 注册 ViewModels
    services.AddTransient<WidgetViewModel>();
    services.AddTransient<SettingsViewModel>();
}
```

---

### 4.3 Domain Layer (领域层)

#### 实体设计

```csharp
// Event.cs - 事件实体
public class Event
{
    public int Id { get; set; }
    public string Title { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Location { get; set; }
    public Priority Priority { get; set; }
    public int ReminderOffset { get; set; }  // 提前提醒分钟数
    public bool IsLunar { get; set; }
    public bool IsDeleted { get; set; }      // 软删除标记
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // 领域逻辑
    public bool IsUrgent(DateTime now) => !IsDeleted &&
        StartTime.Subtract(now).TotalHours <= 1;
}

// OperationLog.cs - 审计日志
public class OperationLog
{
    public int Id { get; set; }
    public string ToolName { get; set; }      // 如 "delete_event"
    public string Params { get; set; }        // JSON
    public string Result { get; set; }
    public DateTime Timestamp { get; set; }
}
```

#### 仓储接口

```csharp
// IEventRepository.cs
public interface IEventRepository
{
    Task<Event?> GetByIdAsync(int id);
    Task<List<Event>> SearchAsync(string query, DateTime? start, DateTime? end);
    Task<Event> AddAsync(Event evt);
    Task<Event> UpdateAsync(Event evt);
    Task SoftDeleteAsync(int id);
    Task<List<Event>> GetUpcomingEventsAsync(DateTime now, int count);
}
```

---

### 4.4 Infrastructure Layer (基础设施层)

#### 数据访问层

```csharp
// AppDbContext.cs
public class AppDbContext : DbContext
{
    public DbSet<Event> Events { get; set; }
    public DbSet<OperationLog> OperationLogs { get; set; }
    public DbSet<AppSetting> Settings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // 获取程序所在目录（与exe同级）
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeDir = Path.GetDirectoryName(exePath);

        // 数据库文件路径：程序目录/data.db
        var dbPath = Path.Combine(exeDir!, "data.db");

        options.UseSqlite($"Data Source={dbPath}");
    }
}
```

#### MCP 服务

**架构说明：** 本项目使用 `ModelContextProtocol.AspNetCore` (v1.1.0) 构建 MCP 服务器，支持 AI 助手（如 Claude Desktop）通过标准 MCP 协议调用日历管理工具。

**参考实现：** [官方 AspNetCoreMcpServer 示例](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples/AspNetCoreMcpServer)

**双Host架构设计：**
- **WPF Host** - 使用 `Host.CreateDefaultBuilder()` 启动WPF应用和后台服务（`IHostedService`）
- **MCP Web Host** - 使用 `WebApplication.CreateBuilder()` 启动MCP Server（HTTP端点）
- **服务共享** - 两个Host共享同一套 `IServiceProvider`（通过依赖注入容器）
- **架构优势** - 符合微软官方模式，支持后台服务轮询，支持HTTP跨进程调用

##### MCP 传输方式配置

本项目支持 **两种 MCP 传输方式**，根据部署环境自动选择：

| 环境模式 | 传输方式 | 配置方式 | 端口 | 适用场景 |
|:---|:---|:---|:---|:---|
| **开发环境** | **Stdio** | 通过标准输入/输出通信 | N/A | 本地调试，与 Claude Desktop 集成 |
| **生产环境** | **HTTP (SSE)** | HTTP Server + Server-Sent Events | 37281 | 独立部署，跨进程调用 |

**开发配置（Stdio 模式）：**

```json
// Claude Desktop 配置文件 (claude_desktop_config.json)
{
  "mcpServers": {
    "desktop-calendar": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\AI_Calendar.csproj"
      ],
      "env": {
        "MCP_TRANSPORT": "stdio",
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**生产配置（HTTP 模式）：**

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 添加 MCP 服务器服务
builder.Services.AddMcpServer()
    .WithHttpTransport()                    // 启用 HTTP 传输
    .WithTools<SearchEventsTool>()
    .WithTools<CreateEventTool>()
    // ... 其他工具
    .WithResources<CalendarResources>();

var app = builder.Build();

// HTTP 模式：监听 localhost:37281
if (args.Contains("--http-transport"))
{
    app.Urls.Add("http://localhost:37281");
}

app.MapMcp();  // 映射 MCP 端点
app.Run();
```

**传输方式选择理由：**
- **Stdio（开发）**：零配置，启动快速，适合本地调试
- **HTTP（生产）**：支持跨进程调用，可远程访问（仅限 localhost），性能更稳定

**安全说明：**
- 无论哪种模式，MCP Server **仅绑定 localhost**
- HTTP 模式默认监听 `127.0.0.1:37281`，拒绝外网访问
- 生产环境建议配置防火墙规则，仅允许本地进程访问

##### 项目配置

**AI_Calendar.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- MCP Server (HTTP传输) -->
    <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.1.0" />
    <!-- 后台服务支持 -->
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.3" />
    <!-- WPF Toast通知 -->
    <PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.1.3" />
    <!-- 系统托盘 -->
    <PackageReference Include="H.NotifyIcon.Wpf" Version="2.4.1" />
    <!-- 农历和节假日 -->
    <PackageReference Include="ChineseCalendar" Version="1.0.4" />
    <!-- SQLite数据库 -->
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.3" />
    <!-- 日志 -->
    <PackageReference Include="Serilog" Version="4.3.1" />
  </ItemGroup>
</Project>
```

**说明**：
- 主项目为WPF应用（`Microsoft.NET.Sdk` + `UseWPF=true`）
- 通过`ModelContextProtocol.AspNetCore`提供MCP Server能力
- 在WPF应用内部嵌入Kestrel HTTP服务器监听localhost:37281
- 不使用`Microsoft.NET.Sdk.Web`

##### 服务器配置

**Program.cs - MCP 服务器启动配置**
```csharp
using AI_Calendar.Infrastructure.MCP.Tools;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 1. 添加 MCP 服务器服务
builder.Services.AddMcpServer()
    .WithHttpTransport()                           // 启用 HTTP 传输
    .WithTools<SearchEventsTool>()                 // 注册工具
    .WithTools<CreateEventTool>()
    .WithTools<UpdateEventTool>()
    .WithTools<DeleteEventTool>()
    .WithTools<GetFreeTimeTool>()
    .WithResources<CalendarResources>();           // 注册资源

// 2. 注册业务服务（供 MCP 工具使用）
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

var app = builder.Build();

// 3. 映射 MCP 端点（默认路径: /）
app.MapMcp();

app.Run();
```

##### HTTP 传输协议

MCP 服务器支持两种 HTTP 模式（由 `MapMcp()` 自动处理）：

| 模式 | 规范版本 | 端点 | 说明 |
|:---|:---|:---|:---|
| **Streamable HTTP** | 2025-06-18 | POST `/` | 推荐使用，支持请求/响应流 |
| **HTTP with SSE** | 2024-11-05 | GET `/sse` + POST `/message` | 遗留模式，向后兼容 |

**安全配置：**
```csharp
// MCP-01: 仅监听 localhost（默认行为）
builder.WebHost.UseUrls("http://localhost:5000");
```

##### MCP 工具定义

**工具特性标记：**
- `[McpServerToolType]` - 标记类包含 MCP 工具
- `[McpServerTool]` - 标记方法为可调用的工具
- `[Description]` - 添加 AI 可见的描述
- `[McpMeta]` - 添加自定义元数据

**1. 简单工具示例**
```csharp
// Infrastructure/MCP/Tools/SearchEventsTool.cs
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AI_Calendar.Infrastructure.MCP.Tools;

[McpServerToolType]
public sealed class SearchEventsTool
{
    private readonly IEventService _eventService;

    public SearchEventsTool(IEventService eventService)
    {
        _eventService = eventService;
    }

    [McpServerTool, Description("搜索日历事件")]
    public async Task<string> Search(
        [Description("搜索关键词（标题、地点）")] string? query,
        [Description("开始时间（可选）")] DateTime? start,
        [Description("结束时间（可选）")] DateTime? end)
    {
        var events = await _eventService.SearchAsync(query, start, end);

        if (events.Count == 0)
            return "未找到匹配的事件";

        return string.Join("\n", events.Select(e =>
            $"ID: {e.Id} | {e.StartTime:yyyy-MM-dd HH:mm} | {e.Title}"));
    }
}
```

**2. 带依赖注入的工具**
```csharp
// Infrastructure/MCP/Tools/CreateEventTool.cs
[McpServerToolType]
public sealed class CreateEventTool
{
    private readonly IEventService _eventService;
    private readonly IAuditLogger _auditLogger;

    public CreateEventTool(IEventService eventService, IAuditLogger auditLogger)
    {
        _eventService = eventService;
        _auditLogger = auditLogger;
    }

    [McpServerTool]
    [Description("创建新的日历事件")]
    [McpMeta("category", "calendar")]
    public async Task<string> Create(
        [Description("事件标题")] string title,
        [Description("开始时间")] DateTime startTime,
        [Description("结束时间（可选）")] DateTime? endTime,
        [Description("地点（可选）")] string? location,
        [Description("优先级：0=低，1=中，2=高")] int priority = 1)
    {
        try
        {
            var newEvent = await _eventService.CreateAsync(new EventDto
            {
                Title = title,
                StartTime = startTime,
                EndTime = endTime,
                Location = location,
                Priority = priority
            });

            // SY-04: 审计日志
            await _auditLogger.LogAsync("create_event", newEvent);

            return $"✓ 事件已创建（ID: {newEvent.Id}）: {title} @ {startTime:yyyy-MM-dd HH:mm}";
        }
        catch (EventConflictException ex)
        {
            return $"✗ 时间冲突：{ex.Message}";
        }
    }
}
```

**3. 删除工具（带安全机制）**
```csharp
// Infrastructure/MCP/Tools/DeleteEventTool.cs
[McpServerToolType]
public sealed class DeleteEventTool
{
    private readonly IEventService _eventService;
    private readonly IAuditLogger _auditLogger;

    [McpServerTool]
    [Description("删除事件（软删除，7天内可恢复）")]
    public async Task<string> Delete(
        [Description("事件 ID")] int id,
        [Description("确认删除（必须设置为 true）")] bool confirm = false)
    {
        // 安全检查
        if (!confirm)
            return "⚠️ 必须设置 confirm=true 才能删除事件";

        await _eventService.SoftDeleteAsync(id, confirm);

        // SY-04: 审计日志
        await _auditLogger.LogAsync("delete_event", new { id, confirm });

        return $"✓ 事件 {id} 已移至回收站（7天后永久删除）";
    }
}
```

**4. 更新工具（必须基于 ID）**
```csharp
// Infrastructure/MCP/Tools/UpdateEventTool.cs
[McpServerToolType]
public sealed class UpdateEventTool
{
    private readonly IEventService _eventService;

    [McpServerTool]
    [Description("更新现有事件（必须提供事件 ID）")]
    public async Task<string> Update(
        [Description("事件 ID")] int id,
        [Description("新标题（可选）")] string? title = null,
        [Description("新开始时间（可选）")] DateTime? startTime = null,
        [Description("新结束时间（可选）")] DateTime? endTime = null,
        [Description("新地点（可选）")] string? location = null)
    {
        var changes = new EventDto();
        if (!string.IsNullOrEmpty(title)) changes.Title = title;
        if (startTime.HasValue) changes.StartTime = startTime.Value;
        if (endTime.HasValue) changes.EndTime = endTime.Value;
        if (!string.IsNullOrEmpty(location)) changes.Location = location;

        var updated = await _eventService.UpdateAsync(id, changes);
        return $"✓ 事件已更新（ID: {updated.Id}）";
    }
}
```

**5. 空闲时间分析工具**
```csharp
// Infrastructure/MCP/Tools/GetFreeTimeTool.cs
[McpServerToolType]
public sealed class GetFreeTimeTool
{
    private readonly IEventService _eventService;

    [McpServerTool]
    [Description("查找指定日期的空闲时间段")]
    public async Task<string> GetFreeTime(
        [Description("所需时长（分钟）")] int durationMinutes,
        [Description("查询日期（默认今天）")] DateTime? date = null)
    {
        var targetDate = date ?? DateTime.Today;
        var slots = await _eventService.GetFreeTimeAsync(
            TimeSpan.FromMinutes(durationMinutes),
            targetDate);

        if (slots.Count == 0)
            return $"⚠️ {targetDate:yyyy-MM-dd} 没有足够的连续空闲时间";

        return string.Join("\n", slots.Select(s =>
            $"• {s.Start:HH:mm} - {s.End:HH:mm} ({(s.End - s.Start).TotalMinutes:F0}分钟)"));
    }
}
```

##### MCP 资源定义

**Infrastructure/MCP/Resources/CalendarResources.cs**
```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AI_Calendar.Infrastructure.MCP.Resources;

[McpServerResourceType]
public class CalendarResources
{
    private readonly IEventService _eventService;

    public CalendarResources(IEventService eventService)
    {
        _eventService = eventService;
    }

    [McpServerResource]
    [Description("今天的日程概览")]
    public async Task<string> TodaySchedule()
    {
        var events = await _eventService.GetEventsAsync(
            DateTime.Today,
            DateTime.Today.AddDays(1));

        if (events.Count == 0)
            return "今天没有安排任何事件";

        var sb = new StringBuilder();
        sb.AppendLine($"📅 今天（{DateTime.Today:yyyy年MM月dd日}）的日程：\n");

        foreach (var evt in events)
        {
            sb.AppendLine($"  • {evt.StartTime:HH:mm} - {evt.EndTime:HH:mm} | {evt.Title}");
            if (!string.IsNullOrEmpty(evt.Location))
                sb.AppendLine($"    📍 {evt.Location}");
        }

        return sb.ToString();
    }

    [McpServerResource]
    [Description("本周日程汇总")]
    public async Task<string> WeeklySummary()
    {
        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var weekEnd = weekStart.AddDays(7);

        var events = await _eventService.GetEventsAsync(weekStart, weekEnd);

        // 按日期分组
        var grouped = events.GroupBy(e => e.StartTime.Date);

        var sb = new StringBuilder();
        sb.AppendLine($"📆 本周（{weekStart:MM月dd日} - {weekEnd:MM月dd日}）日程：\n");

        foreach (var group in grouped)
        {
            sb.AppendLine($"🗓️ {group.Key:MM月dd日（{GetWeekday(group.Key.DayOfWeek)}）}");
            foreach (var evt in group)
            {
                sb.AppendLine($"  • {evt.StartTime:HH:mm} {evt.Title}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GetWeekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        DayOfWeek.Sunday => "周日",
        _ => ""
    };
}
```

##### AI 安全机制（System Prompt 注入）

**Program.cs - 配置安全提示**
```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<SearchEventsTool>()
    .WithTools<CreateEventTool>()
    .WithTools<UpdateEventTool>()
    .WithTools<DeleteEventTool>()
    .WithTools<GetFreeTimeTool>()
    .WithResources<CalendarResources>()
    // MCP-07: 注入安全规则到 System Prompt
    .WithServerConfig(async (sp, server) =>
    {
        var eventService = sp.GetRequiredService<IEventService>();

        // 获取今日事件作为上下文
        var todayEvents = await eventService.GetEventsAsync(
            DateTime.Today,
            DateTime.Today.AddDays(1));

        var summary = todayEvents.Any()
            ? string.Join("\n", todayEvents.Select(e => $"  - {e.StartTime:HH:mm} {e.Title}"))
            : "  （今天没有安排）";

        // 设置 System Prompt
        await server.SetPromptAsync($@"
You are a Calendar Assistant for a Desktop AI Calendar application.

## Today's Schedule
{summary}

## Safety Rules (CRITICAL)
1. **NEVER delete or update events without knowing the specific `id`**
2. **ALWAYS call `search_events` first before any modify/delete operation**
3. If search returns multiple results, ask user to clarify which one
4. All deletions are **soft deletes** (restorable within 7 days)
5. Use `confirm=true` parameter for delete operations
6. Check for time conflicts before creating new events

## Tool Usage Guidelines
- Use `search_events` to find events by keywords or time range
- Use `create_event` to add new events (returns event ID)
- Use `update_event` to modify existing events (requires ID)
- Use `delete_event` to remove events (requires ID + confirm=true)
- Use `get_free_time` to find available time slots

## Response Format
- Be concise and helpful
- Use clear date/time formats (e.g., "2026-03-15 14:00")
- Include event IDs when referring to specific events
- Warn about conflicts or potential issues
");
    });
```

##### MCP 通信流程示例

**Claude Desktop 调用示例：**

```json
// 用户："删除明天的会议"
// Claude 调用 search_events
{
  "method": "tools/call",
  "params": {
    "name": "search_events",
    "arguments": {
      "start": "2026-03-11T00:00:00",
      "end": "2026-03-12T00:00:00"
    }
  }
}

// 服务器返回
{
  "result": {
    "content": [
      {
        "type": "text",
        "text": "ID: 101 | 2026-03-11 10:00 | 产品评审会\nID: 102 | 2026-03-11 14:00 | 客户会议"
      }
    ]
  }
}

// Claude："找到两个会议，删除哪个？"
// 用户："删除 102"
// Claude 调用 delete_event
{
  "method": "tools/call",
  "params": {
    "name": "delete_event",
    "arguments": {
      "id": 102,
      "confirm": true
    }
  }
}

// 服务器返回
{
  "result": {
    "content": [
      {
        "type": "text",
        "text": "✓ 事件 102 已移至回收站（7天后永久删除）"
      }
    ]
  }
}
```

##### MCP 工具清单

| 工具名称 | 功能 | 安全机制 |
|:---|:---|:---|
| `list_events` | 获取所有有效事件列表（默认排除已删除） | 默认过滤软删除数据 |
| `search_events` | 按关键词/时间搜索事件（默认排除已删除） | 默认过滤软删除数据 |
| `create_event` | 创建新事件 | 时间冲突检测 |
| `update_event` | 更新现有事件 | 必须提供 ID |
| `delete_event` | 删除事件 | 软删除 + confirm=true |
| `get_free_time` | 查找空闲时间段 | 无 |
| `restore_event` | 恢复已删除事件 | 7天保留期限制 |

##### MCP 资源清单

| 资源名称 | 功能 | 更新频率 |
|:---|:---|:---|
| `today_schedule` | 今日日程概览 | 实时 |
| `weekly_summary` | 本周日程汇总 | 实时 |

#### 后台服务

```csharp
// ReminderBackgroundService.cs - RM-01: 后台轮询
public class ReminderBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var dueEvents = await _eventService
                .GetDueEventsAsync(now, TimeSpan.FromMinutes(5));

            foreach (var evt in dueEvents)
            {
                // RM-03: 检测全屏应用
                if (!IsFullScreenAppRunning())
                    await _notificationService.ShowAsync(evt);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private bool IsFullScreenAppRunning()
    {
        // P/Invoke: GetForegroundWindow + GetWindowRect
        // 判断是否全屏
    }
}
```

#### 节假日服务架构

```csharp
// HolidayService.cs - 公历节假日服务（chinese-days API）
public class HolidayService : IHolidayService
{
    private readonly IHolidayCache _cache;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HolidayService> _logger;

    // API 端点模板
    private const string ApiUrlTemplate =
        "https://cdn.jsdelivr.net/npm/chinese-days/dist/years/{0}.json";

    public async Task<HolidayInfo?> GetHolidayAsync(DateTime date)
    {
        var year = date.Year;
        var holidays = await GetYearHolidaysAsync(year);

        return holidays?.FirstOrDefault(h =>
            h.Date == date.ToString("yyyy-MM-dd"));
    }

    public async Task<bool> IsWorkdayAsync(DateTime date)
    {
        var holiday = await GetHolidayAsync(date);

        // 如果是节假日且不是调休，则非工作日
        if (holiday != null && !holiday.IsWorkday)
            return false;

        // 如果是调休（周末上班），则是工作日
        if (holiday != null && holiday.IsWorkday)
            return true;

        // 周末判断
        return date.DayOfWeek != DayOfWeek.Saturday &&
               date.DayOfWeek != DayOfWeek.Sunday;
    }

    public async Task<YearHolidays?> GetYearHolidaysAsync(int year)
    {
        // 1. 尝试从缓存读取
        var cached = await _cache.GetAsync(year);
        if (cached != null)
            return cached;

        // 2. 从 API 下载
        try
        {
            var url = string.Format(ApiUrlTemplate, year);
            var json = await _httpClient.GetStringAsync(url);

            var holidays = JsonSerializer.Deserialize<YearHolidays>(json);

            // 3. 写入缓存
            await _cache.SetAsync(year, holidays);

            _logger.LogInformation("成功下载 {Year} 年节假日数据", year);
            return holidays;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "下载 {Year} 年节假日数据失败", year);

            // 4. 如果下载失败且缓存不存在，尝试从内置资源加载
            return await LoadBuiltinHolidaysAsync(year);
        }
    }

    private async Task<YearHolidays?> LoadBuiltinHolidaysAsync(int year)
    {
        // 从嵌入资源加载当年内置的节假日数据
        // 路径: AI_Calendar.Resources.Holidays.{year}.json
        var resourceName = $"AI_Calendar.Resources.Holidays.{year}.json";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        return JsonSerializer.Deserialize<YearHolidays>(json);
    }
}

// HolidayFileCache.cs - 节假日文件缓存
public class HolidayFileCache : IHolidayCache
{
    private readonly string _cacheDirectory;

    public HolidayFileCache()
    {
        // 获取程序所在目录（与exe同级）
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeDir = Path.GetDirectoryName(exePath);

        // 缓存目录：程序目录/cache/holidays
        _cacheDirectory = Path.Combine(exeDir!, "cache", "holidays");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<YearHolidays?> GetAsync(int year)
    {
        var filePath = GetFilePath(year);

        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<YearHolidays>(json);
    }

    public async Task SetAsync(int year, YearHolidays holidays)
    {
        var filePath = GetFilePath(year);
        var json = JsonSerializer.Serialize(holidays, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(filePath, json);
    }

    private string GetFilePath(int year) =>
        Path.Combine(_cacheDirectory, $"{year}.json");
}

// YearHolidays.cs - 节假日数据模型（匹配 chinese-days API 格式）
public class YearHolidays
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("holidays")]
    public List<HolidayInfo> Holidays { get; set; } = new();
}

public class HolidayInfo
{
    [JsonPropertyName("date")]
    public string Date { get; set; }  // 格式: "2026-01-01"

    [JsonPropertyName("name")]
    public string Name { get; set; }  // 如: "元旦"

    [JsonPropertyName("isWorkday")]
    public bool IsWorkday { get; set; }  // 是否为调休

    [JsonPropertyName("isOffday")]
    public bool IsOffday { get; set; }  // 是否为放假
}
```

#### 农历服务架构

```csharp
// LunarCalendarService.cs - 农历服务（ChineseCalendar 库）
public class LunarCalendarService : ILunarCalendarService
{
    // 使用 ChineseCalendar 库
    private readonly ChineseCalendar _chineseCalendar = new ChineseCalendar();

    public LunarDate GetLunarDate(DateTime solarDate)
    {
        _chineseCalendar.Date = solarDate;

        return new LunarDate
        {
            Year = _chineseCalendar.LunarYear,
            Month = _chineseCalendar.LunarMonth,
            Day = _chineseCalendar.LunarDay,
            IsLeapMonth = _chineseCalendar.IsLunarLeapMonth,
            ChineseYear = _chineseCalendar.AnimalString,  // 如: "龙年"
            ChineseMonth = _chineseCalendar.MonthString,  // 如: "三月"
            ChineseDay = _chineseCalendar.DayString,      // 如: "初一"
            FullString = _chineseCalendar.LongDate        // 如: "甲辰年三月初一"
        };
    }

    public List<LunarFestival> GetLunarFestivals(DateTime solarDate)
    {
        var festivals = new List<LunarFestival>();
        var lunar = GetLunarDate(solarDate);

        // 传统农历节日（通过 ChineseCalendar 库判断）
        // 春节
        if (lunar.Month == 1 && lunar.Day == 1)
            festivals.Add(new LunarFestival
            {
                Name = "春节",
                Type = FestivalType.Traditional
            });

        // 元宵节
        if (lunar.Month == 1 && lunar.Day == 15)
            festivals.Add(new LunarFestival
            {
                Name = "元宵节",
                Type = FestivalType.Traditional
            });

        // 端午节
        if (lunar.Month == 5 && lunar.Day == 5)
            festivals.Add(new LunarFestival
            {
                Name = "端午节",
                Type = FestivalType.Traditional
            });

        // 中秋节
        if (lunar.Month == 8 && lunar.Day == 15)
            festivals.Add(new LunarFestival
            {
                Name = "中秋节",
                Type = FestivalType.Traditional
            });

        // 除夕
        if (lunar.Month == 12 && lunar.Day == 29 ||
            lunar.Month == 12 && lunar.Day == 30)
        {
            festivals.Add(new LunarFestival
            {
                Name = "除夕",
                Type = FestivalType.Traditional
            });
        }

        return festivals;
    }

    public SolarDate GetSolarDate(int lunarYear, int lunarMonth, int lunarDay, bool isLeap = false)
    {
        _chineseCalendar.LunarYear = lunarYear;
        _chineseCalendar.LunarMonth = lunarMonth;
        _chineseCalendar.LunarDay = lunarDay;
        _chineseCalendar.IsLunarLeapMonth = isLeap;

        return new SolarDate
        {
            Date = _chineseCalendar.Date,
            Year = _chineseCalendar.Date.Year,
            Month = _chineseCalendar.Date.Month,
            Day = _chineseCalendar.Date.Day
        };
    }
}

// LunarDate.cs - 农历日期模型
public class LunarDate
{
    public int Year { get; set; }          // 农历年
    public int Month { get; set; }         // 农历月
    public int Day { get; set; }           // 农历日
    public bool IsLeapMonth { get; set; }  // 是否闰月
    public string ChineseYear { get; set; } = "";   // 如: "龙年"
    public string ChineseMonth { get; set; } = "";  // 如: "三月"
    public string ChineseDay { get; set; } = "";    // 如: "初一"
    public string FullString { get; set; } = "";    // 如: "甲辰年三月初一"

    public override string ToString() =>
        IsLeapMonth
            ? $"闰{ChineseMonth}{ChineseDay}"
            : $"{ChineseMonth}{ChineseDay}";
}

// LunarFestival.cs - 农历节日模型
public class LunarFestival
{
    public string Name { get; set; } = "";
    public FestivalType Type { get; set; }
}

public enum FestivalType
{
    Traditional,  // 传统节日（春节、中秋等）
    Solar,        // 公历节日（国庆、元旦等）
    Special       // 特殊节日（植树节、护士节等）
}
```

#### 节假日自动更新服务

```csharp
// HolidayUpdateBackgroundService.cs - 节假日自动更新后台服务
public class HolidayUpdateBackgroundService : BackgroundService
{
    private readonly IHolidayService _holidayService;
    private readonly ILogger<HolidayUpdateBackgroundService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动时立即检查一次
        await UpdateHolidaysIfNeededAsync();

        // 每天凌晨 2 点检查更新
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0)
                .AddDays(1);

            var delay = nextRun - now;
            await Task.Delay(delay, stoppingToken);

            await UpdateHolidaysIfNeededAsync();
        }
    }

    private async Task UpdateHolidaysIfNeededAsync()
    {
        var currentYear = DateTime.Now.Year;

        // 检查今年数据是否存在
        var thisYear = await _holidayService.GetYearHolidaysAsync(currentYear);
        if (thisYear == null)
        {
            _logger.LogWarning("缺少 {Year} 年节假日数据，正在下载...", currentYear);
            await _holidayService.GetYearHolidaysAsync(currentYear);
        }

        // 每年 12 月检查下一年数据
        if (DateTime.Now.Month == 12)
        {
            var nextYear = currentYear + 1;
            var nextYearData = await _holidayService.GetYearHolidaysAsync(nextYear);

            if (nextYearData == null)
            {
                _logger.LogInformation("正在下载 {Year} 年节假日数据...", nextYear);
                await _holidayService.GetYearHolidaysAsync(nextYear);
            }
        }
    }
}
```

#### Toast 通知服务架构

```csharp
// ToastNotificationService.cs - Toast 通知服务
// 使用 Microsoft.Toolkit.Uwp.Notifications 库
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

public class ToastNotificationService : IToastNotificationService
{
    // 初始化通知服务
    public ToastNotificationService()
    {
        // 注册 COM 组件事件处理
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }

    // RM-02: 调用 Windows 原生通知中心
    public void ShowEventReminder(Event evt)
    {
        var builder = new ToastContentBuilder()
            .AddText("日程提醒")
            .AddText($"{evt.StartTime:HH:mm} - {evt.Title}")
            .AddButton(new ToastButton()
                .SetContent("查看详情")
                .AddArgument("action", "view")
                .AddArgument("eventId", evt.Id))
            .AddButton(new ToastButton()
                .SetContent("延后 5 分钟")
                .AddArgument("action", "snooze")
                .AddArgument("eventId", evt.Id))
            .AddButton(new ToastButton()
                .SetContent("关闭")
                .AddArgument("action", "dismiss")
                .AddArgument("eventId", evt.Id));

        builder.Show();
    }

    // 发送带进度条的通知
    public void ShowProgressNotification(string title, string content, double progressValue, string status)
    {
        progressValue = Math.Max(0, Math.Min(1, progressValue));

        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(content)
            .AddProgressBar("progressBar1", progressValue,
                valueStringOverride: $"{(progressValue * 100):F0}%",
                status: status);

        builder.Show();
    }

    // 发送计划通知
    public void ScheduleNotification(string title, string content, DateTimeOffset scheduleTime)
    {
        if (scheduleTime <= DateTimeOffset.Now)
        {
            throw new ArgumentException("计划时间必须是将来的时间");
        }

        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(content);

        var scheduledToast = new ScheduledToastNotification(
            builder.GetXml(),
            scheduleTime);

        ToastNotificationManagerCompat.CreateToastNotifier();
        ToastNotificationManagerCompat.CreateToastNotifier().AddToSchedule(scheduledToast);
    }

    // 处理通知激活事件
    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        string action = args["action"];
        string eventId = args["eventId"];

        switch (action)
        {
            case "view":
                // 打开事件详情窗口
                Messenger.Default.Send(new OpenEventDetailMessage(eventId));
                break;
            case "snooze":
                // 延后 5 分钟再次提醒
                var evt = _eventRepository.GetById(eventId);
                var newTime = DateTime.Now.AddMinutes(5);
                ScheduleNotification("日程提醒", $"{evt.Title}", new DateTimeOffset(newTime));
                break;
            case "dismiss":
                // 关闭通知，记录到日志
                _logger.Information($"用户关闭了事件 {eventId} 的提醒通知");
                break;
        }
    }

    // 初始化：在 App.xaml.cs 中调用
    public static void Initialize()
    {
        ToastNotificationManagerCompat.CreateToastNotifier();
    }
}
            {
                BindingGeneric = new ToastBindingGeneric()
                {
                    Children =
                    {
                        new AdaptiveText()
                        {
                            Text = evt.Title
                        },
                        new AdaptiveText()
                        {
                            Text = $"开始时间: {evt.StartTime:yyyy-MM-dd HH:mm}"
                        },
                        new AdaptiveText()
                        {
                            Text = evt.Location ?? ""
                        }
                    }
                }
            },
            Actions = new ToastActionsCustom()
            {
                Buttons =
                {
                    // RM-04: 延后提醒按钮
                    new ToastButton("延后 10 分钟", "snooze")
                    {
                        ActivationType = ToastActivationType.Background
                    },
                    new ToastButton("查看详情", "view")
                    {
                        ActivationType = ToastActivationType.Foreground
                    },
                    new ToastButton("关闭", "dismiss")
                    {
                        ActivationType = ToastActivationType.Background
                    }
                }
            },
            // RM-03: 如果检测到全屏应用，设置 SuppressPopup = true
            Scenario = IsFullScreenAppRunning()
                ? ToastScenario.Alarm
                : ToastScenario.Default,

            ActivationType = ToastActivationType.Background
        };

        // 显示通知
        ToastNotificationManagerCompat.CreateToastNotifier()
            .Show(new ToastNotification(toastContent.GetXml()));
    }

    private bool IsFullScreenAppRunning()
    {
        // P/Invoke: GetForegroundWindow + GetWindowRect
        // 判断前台窗口是否全屏
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        GetWindowRect(hwnd, out var rect);
        int screenWidth = GetSystemMetrics(0);  // SM_CXSCREEN
        int screenHeight = GetSystemMetrics(1); // SM_CYSCREEN

        return rect.Width >= screenWidth && rect.Height >= screenHeight;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Width;
        public int Height;
    }
}

// 在 App.xaml.cs 中初始化 Toast 通知
public partial class App : Application
{
    private IHost _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 初始化 Toast 通知服务
        ToastNotificationService.Initialize();

        // 构建依赖注入容器
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 注册服务
                services.AddSingleton<IToastNotificationService, ToastNotificationService>();
                services.AddSingleton<IEventRepository, EventRepository>();
                services.AddSingleton<IDateTimeService, DateTimeService>();

                // 注册 ViewModels
                services.AddTransient<DesktopWidgetViewModel>();
                services.AddTransient<SettingsViewModel>();

                // 注册后台服务
                services.AddHostedService<ReminderBackgroundService>();
            })
            .Build();

        // 启动后台服务
        _host.Start();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 清理资源
        _host?.Dispose();
        base.OnExit(e);
    }
}

        // 必须先调用此方法以兼容 Desktop 应用
        ToastNotificationManagerCompat.Register();
    }
}
```

#### 热键服务架构

```csharp
// SystemHotKey.cs - 热键 P/Invoke 封装
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class SystemHotKey
{
    /// <summary>
    /// 注册热键
    /// </summary>
    /// <param name="hWnd">要定义热键的窗口的句柄</param>
    /// <param name="id">定义热键ID（不能与其它ID重复）</param>
    /// <param name="fsModifiers">标识热键是否在按Alt、Ctrl、Shift、Windows等键时才会生效</param>
    /// <param name="vk">定义热键的内容</param>
    /// <returns>如果函数执行成功，返回值不为0。如果函数执行失败，返回值为0。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, KeyModifiers fsModifiers, Keys vk);

    /// <summary>
    /// 注销热键
    /// </summary>
    /// <param name="hWnd">要取消热键的窗口句柄</param>
    /// <param name="id">要取消热键的ID</param>
    /// <returns></returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// 辅助键名称。Alt, Ctrl, Shift, WindowsKey
    /// </summary>
    [Flags]
    public enum KeyModifiers
    {
        None = 0,
        Alt = 1,
        Ctrl = 2,
        Shift = 4,
        WindowsKey = 8
    }
}

// HotKeyService.cs - 热键服务
public class HotKeyService : IHotKeyService
{
    private readonly Window _mainWindow;
    private IntPtr _windowHandle;
    private readonly Dictionary<int, Action> _hotKeyActions;
    private int _nextHotKeyId = 1000;

    public HotKeyService(Window mainWindow)
    {
        _mainWindow = mainWindow;
        _hotKeyActions = new Dictionary<int, Action>();

        // 获取 WPF 窗口句柄
        var helper = new WindowInteropHelper(_mainWindow);
        _windowHandle = helper.Handle;

        // 订阅窗口消息
        HwndSource source = HwndSource.FromHwnd(_windowHandle);
        source.AddHook(WndProc);
    }

    // DW-07: 注册隐私模式快捷键 (Ctrl + Alt + P)
    public void RegisterPrivacyModeHotKey()
    {
        RegisterHotKey(
            SystemHotKey.KeyModifiers.Ctrl | SystemHotKey.KeyModifiers.Alt,
            Keys.P,
            () => TogglePrivacyMode()
        );
    }

    // MM-01: 注册设置窗口快捷键 (Ctrl + Alt + C)
    public void RegisterSettingsHotKey()
    {
        RegisterHotKey(
            SystemHotKey.KeyModifiers.Ctrl | SystemHotKey.KeyModifiers.Alt,
            Keys.C,
            () => OpenSettingsWindow()
        );
    }

    private void RegisterHotKey(SystemHotKey.KeyModifiers modifiers, Keys key, Action action)
    {
        int hotKeyId = _nextHotKeyId++;

        bool success = SystemHotKey.RegisterHotKey(_windowHandle, hotKeyId, modifiers, key);
        if (!success)
        {
            int errorCode = Marshal.GetLastWin32Error();
            if (errorCode == 1409)
            {
                throw new InvalidOperationException("热键被占用");
            }
            else
            {
                throw new InvalidOperationException($"注册热键失败，错误代码：{errorCode}");
            }
        }

        _hotKeyActions[hotKeyId] = action;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY)
        {
            int hotKeyId = wParam.ToInt32();

            if (_hotKeyActions.TryGetValue(hotKeyId, out var action))
            {
                // 在 UI 线程上执行
                _mainWindow.Dispatcher.Invoke(action);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private void TogglePrivacyMode()
    {
        // DW-07: 隐藏事件标题，仅显示时间块或"忙碌"
        var messenger = App.Current.Services.GetService<MessengerHub>();
        messenger.Publish(AppEvent.PrivacyModeToggled, null);
    }

    private void OpenSettingsWindow()
    {
        // MM-01: 打开设置窗口
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show();
    }

    public void UnregisterAllHotKeys()
    {
        foreach (var hotKeyId in _hotKeyActions.Keys)
        {
            SystemHotKey.UnregisterHotKey(_windowHandle, hotKeyId);
        }
        _hotKeyActions.Clear();
    }
}

// 在 MainWindow 或 DesktopWidget 中使用
public partial class DesktopWidget : Window
{
    private HotKeyService _hotKeyService;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 初始化热键服务
        _hotKeyService = new HotKeyService(this);
        _hotKeyService.RegisterPrivacyModeHotKey();
        _hotKeyService.RegisterSettingsHotKey();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 注销所有热键
        _hotKeyService?.UnregisterAllHotKeys();

        base.OnClosed(e);
    }
}
```

---

## 5. 数据流设计 (Data Flow)

### 5.1 创建事件流程

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│ User/LLM    │───>│ MCP Server  │───>│ Event       │
│             │    │             │    │ Service     │
└─────────────┘    └─────────────┘    └─────────────┘
                          │                   │
                          v                   v
                    ┌─────────────┐    ┌─────────────┐
                    │ Audit       │    │ Repository  │
                    │ Logger      │───>│ (SQLite)    │
                    └─────────────┘    └─────────────┘
                                              │
                                              v
                                    ┌─────────────────────┐
                                    │ Publish EventCreated│
                                    │ via Messenger       │
                                    └─────────────────────┘
                                              │
                                              v
                                    ┌─────────────┐
                                    │ Widget      │
                                    │ ViewModel   │
                                    │ Refresh UI  │
                                    └─────────────┘
```

### 5.2 AI 删除事件流程（安全机制）

```
┌─────────────┐
│ User: "Delete│
│ tomorrow's  │
│ meeting"    │
└──────┬──────┘
       v
┌─────────────────────┐
│ LLM 调用            │
│ search_events(      │
│   start=tomorrow)   │
└──────┬──────────────┘
       v
┌─────────────────────┐
│ 返回 3 个结果:      │
│ [101, 102, 103]     │
└──────┬──────────────┘
       v
┌─────────────────────┐
│ LLM: "Found 3       │
│ meetings. Which     │
│ one?"               │
└──────┬──────────────┘
       v
┌─────────────────────┐
│ User: "Delete 102"  │
└──────┬──────────────┘
       v
┌─────────────────────┐
│ LLM 调用            │
│ delete_event(       │
│   id=102,           │
│   confirm=true)     │
└──────┬──────────────┘
       v
┌─────────────────────┐
│ Service 执行软删除  │
│ 写入审计日志        │
│ 发布 EventDeleted   │
└─────────────────────┘
```

### 5.3 节假日数据获取流程

```
┌─────────────────────────────────────────────────────────────┐
│                     应用启动/每日凌晨2点                      │
└──────────────────────────────┬──────────────────────────────┘
                               v
┌─────────────────────────────────────────────────────────────┐
│              HolidayUpdateBackgroundService                  │
│              检查是否需要更新节假日数据                        │
└──────────────────────────────┬──────────────────────────────┘
                               v
                    ┌──────────────────────┐
                    │ 检查本地缓存是否存在   │
                    └──────────┬───────────┘
                               │
              ┌────────────────┴────────────────┐
              v                                 v
       ┌─────────────┐                  ┌─────────────┐
       │  缓存存在    │                  │  缓存不存在  │
       └──────┬──────┘                  └──────┬──────┘
              │                                │
              v                                v
       ┌─────────────┐                  ┌──────────────────────┐
       │ 返回缓存数据  │                  │ 请求 chinese-days   │
       └─────────────┘                  │ API: {年份}.json     │
                                         └──────────┬───────────┘
                                                    │
                                        ┌───────────┴───────────┐
                                        v                       v
                                 ┌─────────────┐        ┌─────────────┐
                                 │  下载成功    │        │  下载失败    │
                                 └──────┬──────┘        └──────┬──────┘
                                        │                      │
                                        v                      v
                                 ┌─────────────┐        ┌─────────────┐
                                 │ 写入本地缓存  │        │ 加载内置资源 │
                                 └──────┬──────┘        └──────┬──────┘
                                        │                      │
                                        └──────────┬───────────┘
                                                   v
                                        ┌──────────────────────┐
                                        │ 返回 YearHolidays     │
                                        └──────────────────────┘
```

### 5.4 农历日期转换流程

```
┌─────────────────────────────────────────────────────────────┐
│                   WidgetViewModel.RefreshUI()                │
│                   需要显示当前日期的农历                       │
└──────────────────────────────┬──────────────────────────────┘
                               v
┌─────────────────────────────────────────────────────────────┐
│              LunarCalendarService.GetLunarDate()             │
│              调用 ChineseCalendar 库                         │
└──────────────────────────────┬──────────────────────────────┘
                               v
┌─────────────────────────────────────────────────────────────┐
│              ChineseCalendar 库内部处理                       │
│              - 公历转农历算法                                  │
│              - 生肖天干地支计算                                │
│              - 闰月判断                                       │
└──────────────────────────────┬──────────────────────────────┘
                               v
┌─────────────────────────────────────────────────────────────┐
│              返回 LunarDate 对象                              │
│              {                                               │
│                Year: 2026 (甲辰年)                           │
│                Month: 3 (三月)                               │
│                Day: 13 (十三)                                │
│                FullString: "甲辰年三月十三"                   │
│              }                                               │
└──────────────────────────────┬──────────────────────────────┘
                               v
┌─────────────────────────────────────────────────────────────┐
│              检查农历节日                                       │
│              GetLunarFestivals()                             │
└──────────────────────────────┬──────────────────────────────┘
                               v
                    ┌──────────────────────┐
                    │ 是否有传统节日？       │
                    └──────────┬───────────┘
                               │
              ┌────────────────┴────────────────┐
              v (是)                            v (否)
       ┌─────────────┐                  ┌─────────────┐
       │ 显示节日标记  │                  │ 仅显示农历  │
       │ 如: "清明节"  │                  │ 如: "三月初一"│
       └─────────────┘                  └─────────────┘
```

### 5.5 节假日提醒流程（AI 交互增强）

```
┌─────────────────────────────────────────────────────────────┐
│              User: "帮我安排下周三开会"                       │
└──────────────────────────────┬──────────────────────────────┘
                               v
┌─────────────────────────────────────────────────────────────┐
│              LLM 调用 create_event 工具                       │
└──────────────────────────────┬──────────────────────────────┘
                               v
┌─────────────────────────────────────────────────────────────┐
│              EventService.CreateAsync()                       │
│              检测时间冲突时调用节假日服务                      │
└──────────────────────────────┬──────────────────────────────┘
                               v
┌─────────────────────────────────────────────────────────────┐
│              HolidayService.IsWorkdayAsync()                 │
│              检查下周三是否为工作日                            │
└──────────────────────────────┬──────────────────────────────┘
                               v
                    ┌──────────────────────┐
                    │ 检查结果              │
                    └──────────┬───────────┘
                               │
          ┌────────────────────┼────────────────────┐
          v                    v                    v
   ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
   │ 正常工作日   │     │ 法定节假日   │     │ 调休工作日   │
   │ 创建成功     │     │ 返回 Warning │     │ 创建成功     │
   └─────────────┘     │ 提示用户     │     │ 但可能疲劳   │
                       └─────────────┘     └─────────────┘

返回示例：
- 正常: "会议已安排在下周三 14:00"
- 节假日: "下周三是端午节，您确定要在假期开会吗？"
- 调休: "下周三是调休工作日，建议安排在下午"
```

---

## 6. 配置管理 (Configuration)

### 6.1 配置文件结构

**appsettings.json**
```json
{
  "Database": {
    "Path": "程序目录/data.db"
  },
  "Widget": {
    "Position": { "X": 100, "Y": 100 },
    "FontSize": 14,
    "FontColor": "#FFFFFF",
    "Transparency": 0.9,
    "ShowLunarDate": true,
    "ShowHolidayInfo": true
  },
  "Reminder": {
    "DefaultOffsetMinutes": 15,
    "HealthReminders": {
      "BreakIntervalMinutes": 60,
      "DrinkWaterIntervalMinutes": 120
    }
  },
  "Holiday": {
    "ApiBaseUrl": "https://cdn.jsdelivr.net/npm/chinese-days/dist/years/{0}.json",
    "AutoUpdateEnabled": true,
    "UpdateCheckTime": "02:00:00",
    "CacheExpiryDays": 365
  },
  "LunarCalendar": {
    "ShowTraditionalFestivals": true,
    "ShowSolarTerms": false
  },
  "HotKeys": {
    "PrivacyMode": {
      "Enabled": true,
      "Modifiers": "Ctrl|Alt",
      "Key": "P"
    },
    "Settings": {
      "Enabled": true,
      "Modifiers": "Ctrl|Alt",
      "Key": "C"
    }
  },
  "Toast": {
    "EnableFullScreenDetection": true,
    "DefaultSnoozeMinutes": 10
  },
  "MCP": {
    "Enabled": true,
    "Endpoint": "http://localhost:5000/mcp"
  },
  "Logging": {
    "LogLevel": "Information",
    "AuditLogPath": "程序目录/audit.log"
  }
}
```

### 6.2 配置热更新

```csharp
public class ConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<WidgetOptions> _options;

    // 监听配置变化
    public ConfigurationService(IOptionsMonitor<WidgetOptions> options)
    {
        _options = options;
        _options.OnChange(newOptions =>
        {
            _messenger.Publish(AppEvent.SettingsChanged, newOptions);
        });
    }
}
```

---

## 7. 安全设计 (Security)

### 7.1 MCP 安全机制

| 风险 | 防护措施 |
| :--- | :--- |
| **模糊删除** | 强制 `search_events` → `delete_event(id)` 流程 |
| **误操作** | 软删除 + 7 天回收站 |
| **无限制访问** | 仅绑定 127.0.0.1 |
| **恶意 Prompt** | System Prompt 注入安全规则 |

### 7.2 数据安全

```csharp
public class SafetyMiddleware
{
    public async Task<bool> CanDeleteEvent(int id)
    {
        // 1. 检查是否为重复操作
        var recentDeletes = await _auditLog
            .GetRecentDeletesAsync(TimeSpan.FromMinutes(5));
        if (recentDeletes.Count >= 3)
            return false;  // 疑似 AI 幻觉，暂停操作

        // 2. 检查事件重要性
        var evt = await _eventRepository.GetByIdAsync(id);
        if (evt?.Priority == Priority.High)
            return false;  // 高优先级事件需二次确认

        return true;
    }
}
```

---

## 8. 性能优化 (Performance)

### 8.1 数据库优化

- **索引设计**：
  ```sql
  CREATE INDEX idx_events_start_time ON Events(StartTime);
  CREATE INDEX idx_events_deleted ON Events(IsDeleted);
  ```

- **查询优化**：
  ```csharp
  // 仅查询必要字段
  ctx.Events
      .Where(e => !e.IsDeleted && e.StartTime > now)
      .OrderBy(e => e.StartTime)
      .Take(3)
      .Select(e => new EventDisplayModel
      {
          Id = e.Id,
          Title = e.Title,
          StartTime = e.StartTime
      });
  ```

### 8.2 UI 渲染优化

```csharp
// 静态内容缓存
public class WidgetRenderer
{
    private WriteableBitmap _cachedBackground;

    public void Render()
    {
        if (_cachedBackground == null)
            _cachedBackground = RenderStaticElements();

        // 仅重绘动态部分
        RenderDynamicEvents(_cachedBackground);
    }
}
```

---

## 9. 部署架构 (Deployment)

### 9.1 安装包结构

```
DesktopAICalendar/
├── DesktopAICalendar.exe        # 主程序
├── appsettings.json             # 默认配置
├── Microsoft.Data.Sqlite.dll    # 依赖
├── (其他 NuGet 依赖)
└── (可选) installer.exe         # 安装向导
```

### 9.2 运行时文件结构

```
%APPDATA%/DesktopAICalendar/
├── data.db                      # SQLite 数据库
├── data.db-journal              # 事务日志
├── audit.log                    # 审计日志
├── backup/
│   ├── data.db.20260310.bak
│   └── ...
└── cache/
    └── holidays/                # 节假日缓存目录
        ├── 2025.json            # 2025 年节假日数据
        ├── 2026.json            # 2026 年节假日数据
        ├── 2027.json            # 2027 年节假日数据（自动下载）
        └── .last_check          # 最后更新时间戳
```

### 9.3 内置资源结构

```
AI_Calendar/
└── Resources/
    └── Holidays/                # 内置节假日数据（API 失败时的备用）
        ├── 2025.json            # 编译时嵌入的当年数据
        ├── 2026.json
        └── ...
```

---

## 10. 监控与诊断 (Monitoring)

### 10.1 日志级别

| 级别 | 场景 |
| :--- | :--- |
| **Trace** | MCP 请求/响应详情 |
| **Debug** | 窗口状态变化 |
| **Information** | 事件创建/更新 |
| **Warning** | 时间冲突、API 失败 |
| **Error** | 数据库异常、崩溃 |
| **Fatal** | 无法恢复的错误 |

### 10.2 审计日志格式

```json
{
  "timestamp": "2026-03-10T14:30:00+08:00",
  "toolName": "delete_event",
  "params": { "id": 42, "confirm": true },
  "result": "Success",
  "user": "MCP_Client_Claude_Desktop",
  "ip": "127.0.0.1"
}
```

---

## 11. 扩展点设计 (Extension Points)

### 11.1 插件系统（V2.0）

```csharp
public interface IEventPlugin
{
    string Name { get; }
    Task OnEventCreated(Event evt);
    Task OnEventUpdated(Event oldEvt, Event newEvt);
    Task OnEventDeleted(Event evt);
}

// 示例：微信通知插件
public class WeChatNotificationPlugin : IEventPlugin
{
    public async Task OnEventCreated(Event evt)
    {
        await _weChatApi.SendAsync($"新日程: {evt.Title} @ {evt.StartTime}");
    }
}
```

---

## 12. 技术债务与优化计划

| ID | 问题 | 计划版本 | 优先级 |
| :--- | :--- | :--- | :--- |
| **TD-01** | WPF 渲染在高 DPI 下模糊 | V1.1 | P1 |
| **TD-02** | SQLite 并发写入锁竞争 | V1.2 | P2 |
| **TD-03** | MCP 使用 SSE 时长连接不稳定 | V1.1 | P1 |
| **TD-04** | 节假日 API 请求超时处理 | V1.1 | P1 |
| **TD-05** | 农历节气显示功能（当前未实现）| V1.2 | P3 |
| **TD-06** | 多显示器 DPI 缩放适配 | V1.2 | P2 |
| **TD-07** | 热键冲突检测与自动替换 | V1.3 | P3 |

---

## 13. 架构决策记录 (ADR)

### ADR-001: 选择 SQLite 而非 JSON 文件存储

**背景**：事件数据需要持久化存储

**决策**：使用 SQLite

**理由**：
- 支持复杂查询（时间范围搜索）
- 事务支持，防止数据损坏
- 无需额外安装，嵌入式

**后果**：
- 依赖 Microsoft.Data.Sqlite 包
- 需要 ORM（可选）

### ADR-002: 选择 MVVM 而非 Code-Behind

**背景**：WPF UI 架构选择

**决策**：MVVM 模式

**理由**：
- 便于单元测试
- 数据绑定简化 UI 逻辑
- 分离关注点

**后果**：
- 学习曲线略陡
- 样板代码增多

### ADR-003: 选择 ChineseCalendar 库实现农历功能

**背景**：需要显示农历日期和传统节日

**决策**：使用 ChineseCalendar NuGet 库

**理由**：
- 成熟的农历算法实现
- 支持生肖、天干地支、节气
- 支持闰月判断
- 无需自行实现复杂算法
- 维护活跃，文档完善

**其他方案对比**：
| 方案 | 优点 | 缺点 | 决策 |
| :--- | :--- | :--- | :--- |
| **ChineseCalendar 库** | 功能完整，维护活跃 | 引入外部依赖 | ✅ 采用 |
| **自行实现算法** | 无依赖，可控 | 开发成本高，易出错 | ❌ 放弃 |
| **调用在线 API** | 无需本地计算 | 依赖网络，延迟高 | ❌ 放弃 |

**后果**：
- 增加 1 个 NuGet 依赖
- 包体积增加约 50KB
- 获得准确的农历转换能力

### ADR-004: 选择 chinese-days CDN API 获取节假日

**背景**：需要准确的中国法定节假日及调休数据

**决策**：使用 jsDelivr CDN 上的 chinese-days JSON 数据

**理由**：
- 数据来源权威（国务院发布）
- CDN 加速，访问快速
- JSON 格式易于解析
- 免费且无需 API Key
- 支持历年数据

**缓存策略**：
1. **三级缓存**：内存缓存 → 文件缓存 → 内置资源
2. **自动更新**：每年 12 月下载下一年数据
3. **降级方案**：API 失败时使用内置的当年数据

**其他方案对比**：
| 方案 | 优点 | 缺点 | 决策 |
| :--- | :--- | :--- | :--- |
| **chinese-days CDN** | 免费、快速、权威 | 依赖网络 | ✅ 采用 |
| **timettp.cn API** | 官方 API | 有请求限制，需付费 | ❌ 备选 |
| **硬编码数据** | 完全离线 | 每年需手动更新 | ❌ 放弃 |

**后果**：
- 首次启动或年度更新时需要网络连接
- 需要实现缓存机制
- 需要内置当年数据作为降级方案

### ADR-005: 选择 Microsoft.Toolkit.Uwp.Notifications 实现 Toast 通知

**背景**：需要实现 Windows 10/11 原生 Toast 通知功能

**决策**：使用 `Microsoft.Toolkit.Uwp.Notifications` 库

**理由**：
- ✅ 官方维护，稳定可靠
- ✅ 完整的 Windows 10/11 通知特性支持
- ✅ 支持按钮交互（延后提醒、查看详情）
- ✅ 支持富媒体内容（图片、进度条）
- ✅ 兼容 Desktop WPF 应用
- ✅ 支持计划通知（定时提醒）
- ✅ 事件回调处理完善
- ✅ 简洁的 API 设计（ToastContentBuilder）
- ✅ 文档完善，社区活跃

**核心功能实现：**

1. **基础通知**：使用 ToastContentBuilder 快速构建通知内容
2. **富媒体支持**：内联图片、进度条、状态文本
3. **交互按钮**：ToastButton 支持自定义参数和回调
4. **计划通知**：ScheduledToastNotification 实现定时提醒
5. **事件处理**：ToastNotificationManagerCompat.OnActivated 事件订阅

**其他方案对比**：
| 方案 | 优点 | 缺点 | 决策 |
| :--- | :--- | :--- | :--- |
| **Microsoft.Toolkit.Uwp.Notifications** | 功能完整，官方支持 | 需要 Compat 包 | ✅ 采用 |
| **Shell_NotifyIcon** | 轻量，无需额外库 | 功能有限，非原生 Toast | ❌ 放弃 |
| **自定义弹窗** | 完全可控 | 非原生样式，不统一 | ❌ 放弃 |

**后果**：
- 增加 1 个 NuGet 依赖
- 需要在 App 启动时调用 `ToastNotificationManagerCompat.Register()`
- 获得原生 Windows 通知体验

### ADR-006: 选择 P/Invoke user32.dll 实现热键功能

**背景**：需要实现全局热键功能（隐私模式、设置窗口）

**决策**：使用 P/Invoke 直接调用 `user32.dll` 的 `RegisterHotKey` API

**理由**：
- 系统级 API，稳定可靠
- 支持全局热键（应用无焦点时也能响应）
- 无需额外依赖
- 灵活性高，可自定义任何组合键
- 性能开销极小

**其他方案对比**：
| 方案 | 优点 | 缺点 | 决策 |
| :--- | :--- | :--- | :--- |
| **P/Invoke user32.dll** | 无依赖，系统级 | 需要手动封装 | ✅ 采用 |
| **第三方热键库** | 使用简单 | 引入额外依赖 | ❌ 放弃 |
| **WPF InputBindings** | 原生支持 | 仅应用内有效，非全局 | ❌ 放弃 |

**后果**：
- 需要手动处理 Windows 消息（WM_HOTKEY）
- 需要管理热键 ID 和回调映射
- 需要在窗口关闭时注销热键
- 获得真正的全局热键能力

### ADR-007: 选择 ModelContextProtocol.AspNetCore 实现 MCP 服务器

**背景**：需要实现 Model Context Protocol (MCP) 服务器，让 AI 助手（如 Claude Desktop）能够调用日历管理功能

**决策**：使用 `ModelContextProtocol.AspNetCore` (v1.1.0) 官方 SDK

**理由**：
- **官方维护**：由 Anthropic 官方 C# SDK 团队维护，与 MCP 协议同步更新
- **AspNetCore 集成**：与 ASP.NET Core 生态系统无缝集成，支持依赖注入
- **特性驱动**：通过 `[McpServerTool]` 等特性自动注册工具，无需手动配置
- **双协议支持**：同时支持 2025-06-18 Streamable HTTP 和 2024-11-05 HTTP with SSE
- **类型安全**：强类型的工具定义和参数验证，编译时错误检查
- **开箱即用**：内置 JSON-RPC 处理、传输层管理、会话状态管理
- **文档完善**：提供丰富的示例代码和文档

**架构优势**：
```csharp
// 1. 声明式工具定义
[McpServerToolType]
public class CalendarTools
{
    [McpServerTool]
    public string CreateEvent(string title, DateTime startTime) { }
}

// 2. 自动依赖注入
public CalendarTools(IEventService eventService) { }

// 3. 一行代码启动
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<CalendarTools>();
```

**其他方案对比**：
| 方案 | 优点 | 缺点 | 决策 |
|:---|:---|:---|:---|
| **ModelContextProtocol.AspNetCore** | 官方、类型安全、DI 支持 | 需要 ASP.NET Core Hosting | ✅ 采用 |
| **自行实现 MCP 协议** | 完全可控 | 开发成本高，需跟随协议更新 | ❌ 放弃 |
| **Stdio 传输模式** | 简单 | 不适合桌面应用（需要独立进程）| ❌ 放弃 |

**HTTP 传输模式对比**：
| 模式 | 协议版本 | 优点 | 缺点 | 决策 |
|:---|:---|:---|:---|:---|
| **Streamable HTTP** | 2025-06-18 | 现代、支持请求/响应流 | 需要较新客户端 | ✅ 主要 |
| **HTTP with SSE** | 2024-11-05 | 兼容性好、稳定 | 遗留模式 | ✅ 备用 |

**后果**：
- 项目需要使用 ASP.NET Core（而非纯 WPF 应用）
- 增加 1 个 NuGet 依赖（约 200KB）
- 需要监听 HTTP 端点（默认 localhost:5000）
- 获得类型安全、易维护的 MCP 服务器实现
- 自动支持 MCP 协议最新特性

**安全考量**：
1. **仅监听 localhost**：默认仅绑定 127.0.0.1，防止外部访问
2. **System Prompt 注入**：通过 `WithServerConfig()` 注入安全规则
3. **审计日志**：所有 MCP 调用记录到审计日志
4. **软删除机制**：删除操作可恢复，防止误操作

---

## 14. 参考资料

- [PRD 文档](../PRD.md)
- [.NET 9 文档](https://learn.microsoft.com/en-us/dotnet/core/)
- [MCP 协议规范](https://modelcontextprotocol.io/)
- [MCP C# SDK (GitHub)](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP AspNetCore 示例](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples/AspNetCoreMcpServer)
- [WPF 最佳实践](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [ChineseCalendar 库文档](https://www.nuget.org/packages/ChineseCalendar/)
- [chinese-days 数据源](https://github.com/NateScarlet/holiday-cn)

---

## 15. 更新日志 (Changelog)

### V1.6 (2026-03-11)

**文档冲突修复**：
- ✅ 移除MCP-01工具编号（服务宿主不是工具）
- ✅ 新增官方AspNetCoreMcpServer示例引用
- ✅ 明确双Host架构设计（WPF Host + MCP Web Host）
- ✅ 统一传输方式描述："Stdio（开发环境）或 HTTP/SSE（生产环境）"

**功能模块映射更新**：
- 功能模块表中MCP服务改为"服务宿主（非工具）"
- 保持7个MCP工具编号（MCP-02、MCP-03、MCP-04、MCP-05、MCP-06、MCP-07）

**决策依据**：
- 参考文档：文档冲突分析（2026-03-11）
- 与PRD.md V1.4、API-Interface-Design.md V1.3保持一致

### V1.5 (2026-03-11)

**新增内容：**
- ✅ 基于 `ModelContextProtocol.AspNetCore` v1.1.0 重构 MCP 服务器实现
- ✅ 新增完整的 MCP 服务器配置指南（Program.cs）
- ✅ 新增 5 个 MCP 工具的完整实现代码：
  - `SearchEventsTool` - 搜索事件
  - `CreateEventTool` - 创建事件
  - `UpdateEventTool` - 更新事件
  - `DeleteEventTool` - 删除事件（带安全机制）
  - `GetFreeTimeTool` - 空闲时间分析
- ✅ 新增 MCP 资源实现（`CalendarResources`）：
  - `today_schedule` - 今日日程概览
  - `weekly_summary` - 本周日程汇总
- ✅ 新增 AI 安全机制（System Prompt 注入）
- ✅ 新增 MCP 通信流程示例（JSON 格式）
- ✅ 新增 MCP 工具和资源清单

**优化内容：**
- 🔄 完全重写 4.4 节 MCP 服务部分，采用 AspNetCore 模式
- 🔄 添加 HTTP 传输协议说明（Streamable HTTP + SSE）
- 🔄 添加 MCP 工具特性标记说明（`[McpServerToolType]` 等）
- 🔄 添加依赖注入示例（工具可使用 `IEventService` 等）
- 🔄 添加项目配置示例（`.csproj` 文件）

**技术亮点：**
1. **Asp.NET Core 集成**：使用标准 ASP.NET Core 模式，无需手动管理服务器生命周期
2. **特性驱动开发**：通过 `[McpServerTool]` 等特性自动注册工具和资源
3. **依赖注入支持**：MCP 工具可注入任何已注册的服务
4. **双协议支持**：同时支持 2025-06-18 Streamable HTTP 和 2024-11-05 HTTP with SSE
5. **安全 Prompt 注入**：通过 `WithServerConfig()` 注入安全规则到 AI System Prompt

**参考来源：**
- 基于 [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) 示例项目
- 参考实现：[AspNetCoreMcpServer](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples/AspNetCoreMcpServer)

---

### V1.2 (2026-03-10)

**新增内容：**
- ✅ 添加 `Microsoft.Toolkit.Uwp.Notifications` 库集成，实现 Toast 通知
- ✅ 新增 `ToastNotificationService` 架构设计（完整实现代码）
- ✅ 新增 P/Invoke `user32.dll` 热键实现方案
- ✅ 新增 `SystemHotKey` 热键封装类
- ✅ 新增 `HotKeyService` 热键服务（完整实现代码）
- ✅ 支持全局热键（隐私模式 Ctrl+Alt+P、设置窗口 Ctrl+Alt+C）
- ✅ 新增 ADR-005（Toast 通知选型）
- ✅ 新增 ADR-006（热键实现选型）

**优化内容：**
- 🔄 更新第 2.3 节，明确 Toast 通知和热键的技术实现方案
- 🔄 添加全屏应用检测逻辑（避免打扰）
- 🔄 添加 Toast 按钮交互（延后提醒、查看详情）

**技术亮点：**
1. **原生通知体验**：使用 Windows 10/11 原生通知中心
2. **全局热键支持**：应用无焦点时也能响应快捷键
3. **免打扰模式**：检测全屏应用自动暂停通知
4. **按钮交互**：通知支持延后提醒、查看详情等操作

### V1.1 (2026-03-10)

**新增内容：**
- ✅ 添加 `ChineseCalendar` 库集成，实现农历日期转换
- ✅ 添加 chinese-days CDN API 集成，获取中国公历节假日
- ✅ 新增 `HolidayService` 架构设计（公历节假日服务）
- ✅ 新增 `LunarCalendarService` 架构设计（农历服务）
- ✅ 新增 `HolidayFileCache` 缓存机制设计
- ✅ 新增 `HolidayUpdateBackgroundService` 自动更新服务
- ✅ 新增节假日数据获取流程图（5.3 节）
- ✅ 新增农历日期转换流程图（5.4 节）
- ✅ 新增节假日提醒流程图（5.5 节）
- ✅ 新增节假日配置项到 appsettings.json
- ✅ 新增内置资源结构说明（9.3 节）
- ✅ 新增 ADR-003（ChineseCalendar 选型）
- ✅ 新增 ADR-004（chinese-days API 选型）

**优化内容：**
- 🔄 更新技术栈映射表，将农历和节假日从"需要引入"改为"已集成"
- 🔄 更新模块结构，添加 `External/Cache` 子目录
- 🔄 更新技术债务清单，移除农历相关问题

**特性亮点：**
1. **三级缓存策略**：内存 → 文件 → 内置资源，确保节假日数据始终可用
2. **智能更新**：每年 12 月自动下载下一年数据，无需手动维护
3. **离线可用**：内置当年节假日数据，完全离线环境也能正常工作
4. **AI 增强**：LLM 创建事件时会自动提示节假日和调休信息

### V1.5 (2026-03-11)

**项目类型修正（重大变更）**：
- ✅ 项目SDK从 `Microsoft.NET.Sdk.Web` 改为 `Microsoft.NET.Sdk`（WPF应用）
- ✅ TargetFramework从 `net9.0` 改为 `net8.0`
- ✅ 明确主项目为WPF桌面应用
- ✅ 通过 `ModelContextProtocol.AspNetCore` 提供MCP Server能力
- ✅ 在WPF应用内部嵌入Kestrel HTTP服务器监听localhost:37281
- ✅ 更新csproj配置，添加WPF、MCP、ChineseCalendar等依赖包

**MCP工具更新**：
- ✅ 统一为7个MCP工具
- ✅ 新增 `ListEventsTool`（MCP-02.5）
- ✅ 新增 `RestoreEventTool`（MCP-07）
- ✅ 更新所有工具列表（目录结构和代码示例）

**节假日数据源更新**：
- ✅ 从 `chinese-days CDN API` 改为 `ChineseCalendar 1.0.4` 库
- ✅ HolidayService改为使用ChineseCalendar库获取节假日
- ✅ 支持农历、节假日、调休数据本地获取

**数据库路径更新**：
- ✅ 从 `%APPDATA%/DesktopAICalendar/data.db` 改为 `程序目录/data.db`
- ✅ 使用 `Assembly.GetExecutingAssembly().Location` 获取程序目录

**决策依据**：
- 与PRD.md、Database-Design.md、API-Interface-Design.md保持一致

### V1.4 (2026-03-11)

**MCP Server 实现细节**：
- ✅ 新增完整的MCP Server实现示例代码
- ✅ 新增5个MCP工具的完整代码示例
- ✅ 补充MCP工具的依赖注入配置
- ✅ 新增安全机制和审计日志实现

### V1.3 (2026-03-11)

**MCP 传输方式明确**：
- ✅ 新增 MCP 传输方式详细说明（4.4 节）
- ✅ 明确开发环境使用 Stdio 传输（零配置，启动快速）
- ✅ 明确生产环境使用 HTTP (SSE) 传输（监听 localhost:37281）
- ✅ 新增 Claude Desktop 配置示例
- ✅ 补充 HTTP 模式启动配置代码
- ✅ 新增安全说明（仅绑定 localhost，拒绝外网访问）

**与 PRD.md V1.2 对齐**：
- 确保架构设计与最新的产品需求文档保持一致
- 补充 API 安全机制在架构层面的实现说明

### V1.0 (2026-03-10)

**初始版本：**
- 完整的分层架构设计
- MCP 服务模块设计
- 数据库和实体设计
- 安全和性能设计
- 部署架构设计

---

**文档结束**
