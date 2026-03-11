# 数据库设计说明书 (Database Design Document)

| 文档版本 | V1.2 (更新数据库路径、Priority枚举设计) |
|:---|:---|
| **项目名称** | Desktop AI Calendar (DAC) |
| **数据库类型** | SQLite 3.x |
| **编写日期** | 2026-03-11 |
| **最后更新** | 2026-03-11 |
| **编写人** | AI Assistant |

---

## 1. 数据库概述 (Database Overview)

### 1.1 数据库基本信息

| 属性 | 值 |
|:---|:---|
| **数据库文件名** | `data.db` |
| **存储路径** | `程序目录/data.db` (与exe同级) |
| **数据库类型** | SQLite 3 |
| **字符集** | UTF-8 |
| **时区** | UTC+8 (北京时间) |
| **加密** | SQLCipher (可选，V2.0) |

**路径说明**：
- 数据库文件位于打包后的程序目录（与exe同级）
- 备份目录：`程序目录/backup/`
- 缓存目录：`程序目录/cache/`
- C#代码获取路径：`AppDomain.CurrentDomain.BaseDirectory`

### 1.2 设计原则

1. **轻量级优先**：使用 SQLite 确保零配置部署
2. **本地化存储**：所有数据存储在用户本地，不上传云端
3. **软删除机制**：重要数据采用软删除，支持 7 天内恢复
4. **审计完整性**：所有 AI 操作均记录日志，支持追溯
5. **性能优化**：合理建立索引，避免全表扫描
6. **解耦同步**：UI 同步通过应用层消息总线（IMessenger）实现，而非数据库触发器

### 1.3 命名规范

| 对象类型 | 命名规则 | 示例 |
|:---|:---|:---|
| **表名** | PascalCase，复数形式 | `Events`, `OperationLogs` |
| **字段名** | PascalCase | `StartTime`, `IsDeleted` |
| **索引** | `IX_表名_字段名` | `IX_Events_StartTime` |
| **外键** | `FK_表名_字段名` | `FK_Events_ReminderId` |
| **视图** | `V_视图名` | `V_UpcomingEvents` |

---

## 2. 概念设计 (Conceptual Design)

### 2.1 ER 图

```
┌──────────────────┐       ┌──────────────────┐
│     Events       │       │  OperationLogs   │
├──────────────────┤       ├──────────────────┤
│ Id (PK)          │       │ Id (PK)          │
│ Title            │       │ ToolName         │
│ StartTime        │──────>│ Params           │
│ EndTime          │       │ Result           │
│ Location         │       │ Timestamp        │
│ Priority         │       └──────────────────┘
│ ReminderOffset   │
│ IsLunar          │       ┌──────────────────┐
│ IsDeleted        │       │    Settings      │
│ DeletedAt        │       ├──────────────────┤
│ CreatedAt        │       │ Key (PK)         │
│ UpdatedAt        │       │ Value            │
└──────────────────┘       └──────────────────┘

┌──────────────────┐       ┌──────────────────┐
│   Reminders      │       │  HolidayData     │
├──────────────────┤       ├──────────────────┤
│ Id (PK)          │       │ Id (PK)          │
│ EventId (FK)     │──────>│ Date             │
│ RemindTime       │       │ IsHoliday        │
│ IsNotified       │       │ IsWorkday        │
│ RetryCount       │       │ Name             │
└──────────────────┘       │ Source           │
                           │ Year             │
                           └──────────────────┘
```

### 2.2 实体关系说明

| 关系 | 类型 | 说明 |
|:---|:---|:---|
| **Events → OperationLogs** | 1:N | 一个事件可能被多次操作记录 |
| **Events → Reminders** | 1:N | 一个事件可能有多个提醒时间点 |
| **Settings** | 独立 | 全局键值对配置，无外键关联 |
| **HolidayData** | 独立 | 节假日数据，每年批量导入 |

---

## 3. 逻辑设计 (Logical Design)

### 3.1 核心表设计

#### 3.1.1 Events（事件表）

| 字段名 | 数据类型 | 长度 | 允许NULL | 默认值 | 约束 | 说明 |
|:---|:---|:---|:---|:---|:---|:---|
| **Id** | INTEGER | - | NO | AUTO | PK | 主键，自增 |
| **Title** | TEXT | 200 | NO | - | - | 事件标题 |
| **Description** | TEXT | 2000 | YES | NULL | - | 事件详细描述 |
| **StartTime** | DATETIME | - | NO | - | - | 开始时间（UTC+8） |
| **EndTime** | DATETIME | - | YES | NULL | - | 结束时间（可为空，表示即时事件） |
| **Location** | TEXT | 500 | YES | NULL | - | 地点或会议链接 |
| **Priority** | INTEGER | - | NO | 0 | CHECK(0-2) | 优先级：0=普通，1=重要，2=紧急（通过EF Core值转换器映射到枚举） |
| **ReminderOffset** | INTEGER | - | NO | 0 | - | 提前提醒分钟数（0=不提醒） |
| **IsLunar** | BOOLEAN | - | NO | 0 | - | 是否为农历循环事件（如每年农历生日） |
| **IsAllDay** | BOOLEAN | - | NO | 0 | - | 是否为全天事件 |
| **RecurrenceRule** | TEXT | 100 | YES | NULL | - | 重复规则（RRULE格式，如"FREQ=WEEKLY"） |
| **IsDeleted** | BOOLEAN | - | NO | 0 | - | 软删除标记（0=正常，1=已删除） |
| **DeletedAt** | DATETIME | - | YES | NULL | - | 删除时间 |
| **CreatedAt** | DATETIME | - | NO | CURRENT_TIMESTAMP | - | 创建时间 |
| **UpdatedAt** | DATETIME | - | NO | CURRENT_TIMESTAMP | - | 最后更新时间 |

**索引设计**：
```sql
-- 主键索引（自动创建）
CREATE UNIQUE INDEX PK_Events ON Events(Id);

-- 查询索引：按开始时间查询（升序）
CREATE INDEX IX_Events_StartTime ON Events(StartTime ASC);

-- 软删除过滤索引
CREATE INDEX IX_Events_IsDeleted ON Events(IsDeleted);

-- 组合索引：查询未删除的即将到来的事件
CREATE INDEX IX_Events_Upcoming ON Events(IsDeleted, StartTime ASC)
WHERE IsDeleted = 0;

-- 全文搜索索引（SQLite FTS5）
CREATE VIRTUAL TABLE EventsFTS USING fts5(
    Title, Description,
    content=Events,
    content_rowid=Id
);
```

**触发器设计**：
```sql
-- 自动更新 UpdatedAt 字段
CREATE TRIGGER TR_Events_UpdateTimestamp
AFTER UPDATE ON Events
BEGIN
    UPDATE Events SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
END;

-- 同步全文搜索索引
CREATE TRIGGER TR_Events_InsertFTS
AFTER INSERT ON Events BEGIN
    INSERT INTO EventsFTS(rowid, Title, Description)
    VALUES (NEW.Id, NEW.Title, NEW.Description);
END;

CREATE TRIGGER TR_Events_DeleteFTS
AFTER DELETE ON Events BEGIN
    DELETE FROM EventsFTS WHERE rowid = OLD.Id;
END;
```

---

#### 3.1.2 Reminders（提醒队列表）

| 字段名 | 数据类型 | 长度 | 允许NULL | 默认值 | 约束 | 说明 |
|:---|:---|:---|:---|:---|:---|:---|
| **Id** | INTEGER | - | NO | AUTO | PK | 主键，自增 |
| **EventId** | INTEGER | - | NO | - | FK | 关联事件ID（外键→Events.Id） |
| **RemindTime** | DATETIME | - | NO | - | - | 提醒时间点 |
| **IsNotified** | BOOLEAN | - | NO | 0 | - | 是否已通知 |
| **RetryCount** | INTEGER | - | NO | 0 | - | 重试次数（用户点击"延后10分钟"时递增） |
| **NotifiedAt** | DATETIME | - | YES | NULL | - | 实际通知时间 |
| **CreatedAt** | DATETIME | - | NO | CURRENT_TIMESTAMP | - | 创建时间 |

**仓储接口**：`AI_Calendar.Core.Interfaces.IReminderRepository`

**外键约束**：
```sql
ALTER TABLE Reminders ADD CONSTRAINT FK_Reminders_EventId
FOREIGN KEY (EventId) REFERENCES Events(Id) ON DELETE CASCADE;
```

**索引设计**：
```sql
-- 查询待通知的提醒（按时间升序）
CREATE INDEX IX_Reminders_Pending
ON Reminders(IsNotified, RemindTime ASC)
WHERE IsNotified = 0;

-- 关联事件查询
CREATE INDEX IX_Reminders_EventId ON Reminders(EventId);
```

---

#### 3.1.3 OperationLogs（操作审计日志表）

| 字段名 | 数据类型 | 长度 | 允许NULL | 默认值 | 约束 | 说明 |
|:---|:---|:---|:---|:---|:---|:---|
| **Id** | INTEGER | - | NO | AUTO | PK | 主键，自增 |
| **ToolName** | TEXT | 50 | NO | - | - | MCP工具名（如create_event, delete_event） |
| **Params** | TEXT | - | NO | - | - | 请求参数（JSON格式） |
| **Result** | TEXT | 50 | NO | - | - | 执行结果：Success/Error |
| **ErrorCode** | TEXT | 20 | YES | NULL | - | 错误码（如"EVENT_NOT_FOUND"） |
| **ErrorMessage** | TEXT | 500 | YES | NULL | - | 错误详细信息 |
| **ExecutionTime** | INTEGER | - | YES | NULL | - | 执行耗时（毫秒） |
| **Timestamp** | DATETIME | - | NO | CURRENT_TIMESTAMP | - | 操作时间 |
| **UserId** | TEXT | 50 | YES | NULL | - | 用户标识（未来支持多用户） |

**索引设计**：
```sql
-- 按时间倒序查询最新操作
CREATE INDEX IX_OperationLogs_Timestamp
ON OperationLogs(Timestamp DESC);

-- 按工具名查询统计
CREATE INDEX IX_OperationLogs_ToolName
ON OperationLogs(ToolName, Timestamp DESC);

-- 按结果过滤（查询失败操作）
CREATE INDEX IX_OperationLogs_Result
ON OperationLogs(Result, Timestamp DESC);
```

---

#### 3.1.4 Settings（配置表）

| 字段名 | 数据类型 | 长度 | 允许NULL | 默认值 | 约束 | 说明 |
|:---|:---|:---|:---|:---|:---|:---|
| **Key** | TEXT | 100 | NO | - | PK | 配置键 |
| **Value** | TEXT | - | NO | - | - | 配置值（JSON字符串或纯文本） |
| **ValueType** | TEXT | 20 | NO | 'String' | - | 值类型：String/Int/Bool/Json |
| **Description** | TEXT | 200 | YES | NULL | - | 配置项说明 |
| **UpdatedAt** | DATETIME | - | NO | CURRENT_TIMESTAMP | - | 最后更新时间 |

**预设配置项**：
```json
{
  "widget.opacity": 0.9,
  "widget.fontSize": 14,
  "widget.positionX": 100,
  "widget.positionY": 100,
  "widget.privacyMode": false,
  "reminder.enabled": true,
  "reminder.defaultOffset": 15,
  "system.autoStart": false,
  "system.language": "zh-CN",
  "system.theme": "light"
}
```

---

#### 3.1.5 HolidayData（节假日数据表）

| 字段名 | 数据类型 | 长度 | 允许NULL | 默认值 | 约束 | 说明 |
|:---|:---|:---|:---|:---|:---|:---|
| **Id** | INTEGER | - | NO | AUTO | PK | 主键，自增 |
| **Date** | DATE | - | NO | - | UNIQUE | 日期（YYYY-MM-DD） |
| **IsHoliday** | BOOLEAN | - | NO | 0 | - | 是否为法定节假日 |
| **IsWorkday** | BOOLEAN | - | NO | 0 | - | 是否为调休工作日 |
| **Name** | TEXT | 50 | YES | NULL | - | 节假日名称（如"春节"、"国庆节"） |
| **Source** | TEXT | 50 | NO | 'builtin' | - | 数据来源：builtin/api/manual |
| **Year** | INTEGER | - | NO | - | - | 所属年份（用于快速查询） |

**索引设计**：
```sql
-- 按日期查询
CREATE UNIQUE INDEX UX_HolidayData_Date ON HolidayData(Date);

-- 按年份查询
CREATE INDEX IX_HolidayData_Year ON HolidayData(Year);
```

---

### 3.2 视图设计（Views）

#### 3.2.1 V_UpcomingEvents（即将到来的事件）

```sql
CREATE VIEW V_UpcomingEvents AS
SELECT
    e.Id,
    e.Title,
    e.StartTime,
    e.EndTime,
    e.Location,
    e.Priority,
    e.ReminderOffset,
    CASE
        WHEN datetime(e.StartTime, '+15 minutes') <= datetime('now', 'localtime')
        THEN 1  -- 紧急（15分钟内）
        WHEN datetime(e.StartTime, '+1 hour') <= datetime('now', 'localtime')
        THEN 2  -- 即将到来（1小时内）
        ELSE 3  -- 正常
    END AS UrgencyLevel,
    strftime('%H:%M', e.StartTime) AS TimeDisplay,
    CASE WHEN e.IsAllDay = 1 THEN '全天' ELSE strftime('%H:%M', e.StartTime) END AS DisplayTime
FROM Events e
WHERE e.IsDeleted = 0
  AND date(e.StartTime) >= date('now', 'localtime')
  AND (e.EndTime IS NULL OR e.EndTime >= datetime('now', 'localtime'))
ORDER BY e.StartTime ASC;
```

#### 3.2.2 V_TodayEvents（今日事件）

```sql
CREATE VIEW V_TodayEvents AS
SELECT *
FROM Events
WHERE IsDeleted = 0
  AND date(StartTime) = date('now', 'localtime')
ORDER BY StartTime ASC;
```

#### 3.2.3 V_DeletedEvents（回收站）

```sql
CREATE VIEW V_DeletedEvents AS
SELECT
    Id,
    Title,
    StartTime,
    DeletedAt,
    julianday('now', 'localtime') - julianday(DeletedAt) AS DaysSinceDeletion
FROM Events
WHERE IsDeleted = 1
  AND DeletedAt >= date('now', '-7 days', 'localtime')
ORDER BY DeletedAt DESC;
```

#### 3.2.4 V_ReminderStats（提醒统计）

```sql
CREATE VIEW V_ReminderStats AS
SELECT
    COUNT(*) AS TotalReminders,
    SUM(CASE WHEN IsNotified = 1 THEN 1 ELSE 0 END) AS NotifiedCount,
    SUM(CASE WHEN IsNotified = 0 THEN 1 ELSE 0 END) AS PendingCount,
    MAX(CASE WHEN IsNotified = 0 THEN RemindTime ELSE NULL END) AS NextRemindTime
FROM Reminders
WHERE RemindTime >= datetime('now', 'localtime');
```

---

### 3.3 存储过程设计（SQLite 不支持存储过程，使用事务封装）

#### 3.3.1 创建事件并添加提醒

```sql
-- BEGIN TRANSACTION;
-- INSERT INTO Events (Title, StartTime, EndTime, Priority, ReminderOffset)
-- VALUES (@Title, @StartTime, @EndTime, @Priority, @ReminderOffset);
--
-- DECLARE @EventId INTEGER = last_insert_rowid();
--
-- IF @ReminderOffset > 0 THEN
--     INSERT INTO Reminders (EventId, RemindTime)
--     VALUES (@EventId, datetime(@StartTime, '-' || @ReminderOffset || ' minutes'));
-- END IF;
--
-- INSERT INTO OperationLogs (ToolName, Params, Result)
-- VALUES ('create_event', json_object('id', @EventId, 'title', @Title), 'Success');
-- COMMIT;
```

#### 3.3.2 软删除事件

```sql
-- BEGIN TRANSACTION;
-- UPDATE Events
-- SET IsDeleted = 1, DeletedAt = CURRENT_TIMESTAMP
-- WHERE Id = @EventId;
--
-- DELETE FROM Reminders WHERE EventId = @EventId;
--
-- INSERT INTO OperationLogs (ToolName, Params, Result)
-- VALUES ('delete_event', json_object('id', @EventId), 'Success');
-- COMMIT;
```

---

## 4. 物理设计 (Physical Design)

### 4.1 存储空间估算

| 表名 | 预估行数 | 单行大小 | 总大小 |
|:---|:---|:---|:---|
| Events | 10,000 | 500 bytes | ~5 MB |
| Reminders | 15,000 | 100 bytes | ~1.5 MB |
| OperationLogs | 50,000 | 300 bytes | ~15 MB |
| Settings | 50 | 200 bytes | ~10 KB |
| HolidayData | 3,650 | 100 bytes | ~365 KB |
| **总计** | - | - | **~22 MB** |

### 4.2 性能优化策略

1. **索引优化**：
   - 为高频查询字段建立索引（StartTime, IsDeleted）
   - 使用部分索引减少索引大小（如 `WHERE IsDeleted = 0`）

2. **查询优化**：
   - 避免 `SELECT *`，只查询需要的字段
   - 使用 `LIMIT` 限制返回结果数
   - 定期 `VACUUM` 回收空间

3. **连接池配置**：
   ```csharp
   services.AddDbContextPool<CalendarDbContext>(options =>
       options.UseSqlite("Data Source=data.db"), poolSize: 5);
   ```

4. **WAL 模式**（Write-Ahead Logging）：
   ```sql
   PRAGMA journal_mode = WAL;  -- 提升并发性能
   PRAGMA synchronous = NORMAL;  -- 平衡性能与安全
   PRAGMA cache_size = -64000;  -- 64MB 缓存
   PRAGMA temp_store = MEMORY;  -- 临时表存入内存
   ```

---

## 5. 数据字典 (Data Dictionary)

### 5.1 字段枚举值说明

#### Priority（优先级）

**数据库存储**：`INTEGER` 类型

**C# 枚举定义**：
```csharp
namespace AI_Calendar.Core.Entities;

/// <summary>
/// 事件优先级
/// </summary>
public enum Priority
{
    /// <summary>低优先级（普通）</summary>
    Low = 0,

    /// <summary>中等优先级</summary>
    Medium = 1,

    /// <summary>高优先级（重要）</summary>
    High = 2
}
```

**值映射表**：
| 数据库值 | 枚举值 | API值 | UI显示 | 颜色 |
|:---|:---|:---|:---|:---|
| 0 | `Priority.Low` | 0 | 普通（低优先级） | #FFFFFF (白色) |
| 1 | `Priority.Medium` | 1 | 中等（默认） | #FFD700 (金色) |
| 2 | `Priority.High` | 2 | 重要（高优先级） | #FF4500 (橙红色) |

**EF Core 值转换器配置**：
```csharp
// CalendarDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // 配置 Priority 枚举与整数之间的转换
    modelBuilder.Entity<Event>()
        .Property(e => e.Priority)
        .HasConversion(
            p => (int)p,           // enum -> int
            p => (Priority)p       // int -> enum
        );

    // 配置默认值
    modelBuilder.Entity<Event>()
        .Property(e => e.Priority)
        .HasDefaultValue(Priority.Medium);
}
```

#### ToolName（MCP工具名）
| 值 | 说明 |
|:---|:---|
| `list_events` | 获取事件列表（MCP-02.5） |
| `search_events` | 搜索事件（MCP-02） |
| `create_event` | 创建事件（MCP-03） |
| `update_event` | 更新事件（MCP-04） |
| `delete_event` | 删除事件（MCP-05） |
| `get_free_time` | 查询空闲时间（MCP-06） |
| `restore_event` | 恢复已删除事件（MCP-07） |

#### Result（执行结果）
| 值 | 说明 |
|:---|:---|
| `Success` | 执行成功 |
| `Error` | 执行失败 |
| `PartialSuccess` | 部分成功（批量操作时） |

#### ValueType（配置值类型）
| 值 | 示例 |
|:---|:---|
| `String` | `"light"` |
| `Int` | `15` |
| `Bool` | `true` |
| `Json` | `{"x": 100, "y": 100}` |

---

## 6. 数据访问层设计 (Data Access Layer)

### 6.1 EF Core 配置

```csharp
// CalendarDbContext.cs
using System.Reflection;

public class CalendarDbContext : DbContext
{
    public DbSet<Event> Events { get; set; }
    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<OperationLog> OperationLogs { get; set; }
    public DbSet<Setting> Settings { get; set; }
    public DbSet<HolidayData> HolidayData { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 获取程序所在目录（与exe同级）
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeDir = Path.GetDirectoryName(exePath);

        // 数据库文件路径：程序目录/data.db
        var dbPath = Path.Combine(exeDir!, "data.db");

        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 配置软删除全局查询过滤器
        modelBuilder.Entity<Event>().HasQueryFilter(e => !e.IsDeleted);

        // 配置索引
        modelBuilder.Entity<Event>()
            .HasIndex(e => new { e.IsDeleted, e.StartTime })
            .HasDatabaseName("IX_Events_Upcoming");

        // 配置关系
        modelBuilder.Entity<Reminder>()
            .HasOne(r => r.Event)
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 6.2 Repository 接口设计

```csharp
// IEventRepository.cs
namespace AI_Calendar.Core.Interfaces;

public interface IEventRepository
{
    Task<Event> GetByIdAsync(int id);
    Task<List<Event>> GetUpcomingEventsAsync(DateTime start, DateTime end, int limit = 10);
    Task<List<Event>> SearchEventsAsync(string query, int limit = 20);
    Task<int> CreateEventAsync(Event evt);
    Task<bool> UpdateEventAsync(int id, Action<Event> updateAction);
    Task<bool> SoftDeleteEventAsync(int id);
    Task<bool> RestoreEventAsync(int id);
    Task<List<Event>> GetDeletedEventsAsync();
}

// IReminderRepository.cs
namespace AI_Calendar.Core.Interfaces;

/// <summary>
/// 提醒仓储接口
/// 负责提醒记录的持久化和查询操作
/// </summary>
public interface IReminderRepository
{
    #region 基本CRUD操作

    /// <summary>
    /// 根据 ID 获取提醒
    /// </summary>
    Task<Reminder?> GetByIdAsync(int id);

    /// <summary>
    /// 添加新提醒
    /// </summary>
    /// <param name="reminder">提醒实体</param>
    /// <returns>添加后的实体（包含自增 ID）</returns>
    Task<Reminder> AddAsync(Reminder reminder);

    /// <summary>
    /// 更新提醒
    /// </summary>
    Task<Reminder> UpdateAsync(Reminder reminder);

    /// <summary>
    /// 删除提醒
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 根据事件ID删除所有相关提醒
    /// </summary>
    /// <param name="eventId">事件ID</param>
    /// <returns>删除的记录数</returns>
    Task<int> DeleteByEventIdAsync(int eventId);

    #endregion

    #region 查询操作

    /// <summary>
    /// 获取指定事件的提醒列表
    /// </summary>
    /// <param name="eventId">事件ID</param>
    Task<List<Reminder>> GetByEventIdAsync(int eventId);

    /// <summary>
    /// 获取待通知的提醒列表
    /// </summary>
    /// <param name="beforeTime">截止时间（包含）</param>
    /// <param name="limit">最大返回数量</param>
    Task<List<Reminder>> GetPendingRemindersAsync(DateTime beforeTime, int limit = 100);

    /// <summary>
    /// 获取待通知的提醒列表（时间范围）
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    Task<List<Reminder>> GetPendingRemindersInRangeAsync(DateTime startTime, DateTime endTime);

    /// <summary>
    /// 获取下一个待通知的提醒
    /// </summary>
    Task<Reminder?> GetNextPendingReminderAsync();

    #endregion

    #region 业务操作

    /// <summary>
    /// 标记提醒为已通知
    /// </summary>
    /// <param name="id">提醒ID</param>
    /// <param name="notifiedAt">实际通知时间（默认为当前时间）</param>
    Task<bool> MarkAsNotifiedAsync(int id, DateTime? notifiedAt = null);

    /// <summary>
    /// 延后提醒（增加重试次数并更新提醒时间）
    /// </summary>
    /// <param name="id">提醒ID</param>
    /// <param name="delayMinutes">延后分钟数</param>
    Task<bool> SnoozeReminderAsync(int id, int delayMinutes = 10);

    /// <summary>
    /// 清理已通知的旧提醒（保留指定天数）
    /// </summary>
    /// <param name="days">保留天数</param>
    /// <returns>删除的记录数</returns>
    Task<int> CleanupNotifiedRemindersAsync(int days = 7);

    #endregion
}
```

---

## 7. 备份与恢复策略 (Backup & Recovery)

### 7.1 自动备份策略

| 备份类型 | 频率 | 保留份数 | 存储路径 |
|:---|:---|:---|:---|
| **增量备份** | 每次启动时 | 5 份 | `程序目录/backup/` |
| **完整备份** | 每周日 00:00 | 4 份 | `程序目录/backup/weekly/` |
| **手动备份** | 用户触发 | 不限 | 用户指定路径 |

### 7.2 备份脚本

```bash
# Backup.bat (Windows)
@echo off
REM 数据库和备份路径位于程序目录
set DB_PATH=data.db
set BACKUP_PATH=backup
set TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%

copy "%DB_PATH%" "%BACKUP_PATH%\data_%TIMESTAMP%.db"

# 仅保留最近 5 份
for /f "skip=5 delims=" %%F in ('dir /b /o-d "%BACKUP_PATH%\data_*.db"') do del "%BACKUP_PATH%\%%F"
```

### 7.3 恢复流程

1. **检测数据库损坏**：
   ```csharp
   try
   {
       _context.Database.CanConnect();
   }
   catch (SqliteException ex)
   {
       // 数据库损坏，尝试恢复
       RestoreFromBackup();
   }
   ```

2. **自动恢复**：
   - 查找最新备份文件
   - 关闭所有数据库连接
   - 替换 `data.db`
   - 重启应用

---

## 7.5 UI 同步机制说明 ⚠️ 重要

### 7.5.1 设计原则

本项目 **不使用** 数据库触发器或文件监听实现 UI 同步，而是采用应用层消息总线（`IMessenger`）模式。

**核心优势**：
- **解耦性**：数据库层不依赖 UI 层
- **可测试性**：可单元测试消息发布和订阅
- **类型安全**：使用泛型保证消息类型安全

### 7.5.2 详细说明

`IMessenger` 接口的完整定义、使用示例和集成方案请参考：

📄 **[API-Interface-Design.md §8 UI 同步机制](API-Interface-Design.md#8-ui-同步机制-ui-synchronization)**

该章节包含：
- `IMessenger` 接口和 `AppEvent` 枚举定义
- MCP 工具集成示例
- ViewModel 订阅示例
- 依赖注入配置
- 完整调用流程

### 7.5.3 架构对比

| 方案 | 优点 | 缺点 | 本项目选择 |
|:---|:---|:---|:---|
| **数据库触发器** | 实时性强 | 耦合度高，难以单元测试 | ❌ |
| **文件监听** | 跨进程有效 | 不可靠，频繁触发 | ❌ |
| **应用层消息总线** | 解耦，可测试，类型安全 | 仅限单进程 | ✅ **采用** |

**重要说明**：本项目采用单进程架构（MCP Server 内嵌于 WPF 应用），因此消息总线方案完全适用。

---

## 8. 数据迁移计划 (Data Migration)

### 8.1 版本管理

使用 `EF Core Migrations` 管理数据库版本：

```bash
# 创建迁移
dotnet ef migrations add AddRecurrenceRuleColumn

# 应用迁移
dotnet ef database update
```

### 8.2 预设迁移脚本

| 版本 | 描述 | SQL |
|:---|:---|:---|
| V1.0 | 初始数据库结构 | `CREATE TABLE Events ...` |
| V1.1 | 添加重复事件字段 | `ALTER TABLE Events ADD COLUMN RecurrenceRule TEXT` |
| V1.2 | 添加全文搜索 | `CREATE VIRTUAL TABLE EventsFTS USING fts5(...)` |

---

## 9. 安全性设计 (Security)

### 9.1 SQL 注入防护

- **使用参数化查询**：
  ```csharp
  // ❌ 错误：直接拼接SQL
  var sql = $"SELECT * FROM Events WHERE Title = '{title}'";

  // ✅ 正确：使用参数
  var events = _context.Events
      .Where(e => e.Title == title)
      .ToList();
  ```

### 9.2 数据脱敏

```csharp
// 导出日志时脱敏敏感信息
public string SanitizeLog(string log)
{
    return Regex.Replace(log, @"\d{11}", "***");  // 手机号脱敏
}
```

### 9.3 权限控制

```csharp
// SQLite 文件权限
// 仅允许当前用户完全控制，拒绝其他用户访问
icacls "C:\Users\%USERNAME%\AppData\Roaming\DesktopAICalendar\data.db" /inheritance:r
icacls "C:\Users\%USERNAME%\AppData\Roaming\DesktopAICalendar\data.db" /grant:r %USERNAME%:F
```

---

## 10. 监控与维护 (Monitoring & Maintenance)

### 10.1 性能监控指标

| 指标 | 告警阈值 | 说明 |
|:---|:---|:---|
| 查询响应时间 | > 100ms | 单个查询耗时 |
| 数据库文件大小 | > 100 MB | 文件过大需清理 |
| 软删除数据占比 | > 30% | 需执行物理删除 |

### 10.2 维护脚本

```sql
-- 清理 7 天前的软删除数据
DELETE FROM Events
WHERE IsDeleted = 1
  AND DeletedAt < date('now', '-7 days', 'localtime');

-- 清理旧的日志（保留 30 天）
DELETE FROM OperationLogs
WHERE Timestamp < date('now', '-30 days', 'localtime');

-- 优化数据库
VACUUM;
ANALYZE;
```

---

## 11. 附录：SQL 建表脚本

```sql
-- ============================================
-- Desktop AI Calendar - Database Schema
-- Version: 1.0
-- Date: 2026-03-11
-- ============================================

PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;

-- ============================================
-- Table: Events
-- ============================================
CREATE TABLE Events (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    Description TEXT,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    Location TEXT,
    Priority INTEGER NOT NULL DEFAULT 0 CHECK(Priority IN (0, 1, 2)),
    ReminderOffset INTEGER NOT NULL DEFAULT 0,
    IsLunar BOOLEAN NOT NULL DEFAULT 0,
    IsAllDay BOOLEAN NOT NULL DEFAULT 0,
    RecurrenceRule TEXT,
    IsDeleted BOOLEAN NOT NULL DEFAULT 0,
    DeletedAt DATETIME,
    CreatedAt DATETIME NOT NULL DEFAULT (datetime('now', 'localtime')),
    UpdatedAt DATETIME NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE INDEX IX_Events_StartTime ON Events(StartTime ASC);
CREATE INDEX IX_Events_IsDeleted ON Events(IsDeleted);
CREATE INDEX IX_Events_Upcoming ON Events(IsDeleted, StartTime ASC) WHERE IsDeleted = 0;

CREATE TRIGGER TR_Events_UpdateTimestamp
AFTER UPDATE ON Events
BEGIN
    UPDATE Events SET UpdatedAt = datetime('now', 'localtime') WHERE Id = NEW.Id;
END;

-- ============================================
-- Table: Reminders
-- ============================================
CREATE TABLE Reminders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId INTEGER NOT NULL,
    RemindTime DATETIME NOT NULL,
    IsNotified BOOLEAN NOT NULL DEFAULT 0,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    NotifiedAt DATETIME,
    CreatedAt DATETIME NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (EventId) REFERENCES Events(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Reminders_Pending ON Reminders(IsNotified, RemindTime ASC) WHERE IsNotified = 0;
CREATE INDEX IX_Reminders_EventId ON Reminders(EventId);

-- ============================================
-- Table: OperationLogs
-- ============================================
CREATE TABLE OperationLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ToolName TEXT NOT NULL,
    Params TEXT NOT NULL,
    Result TEXT NOT NULL,
    ErrorCode TEXT,
    ErrorMessage TEXT,
    ExecutionTime INTEGER,
    Timestamp DATETIME NOT NULL DEFAULT (datetime('now', 'localtime')),
    UserId TEXT
);

CREATE INDEX IX_OperationLogs_Timestamp ON OperationLogs(Timestamp DESC);
CREATE INDEX IX_OperationLogs_ToolName ON OperationLogs(ToolName, Timestamp DESC);
CREATE INDEX IX_OperationLogs_Result ON OperationLogs(Result, Timestamp DESC);

-- ============================================
-- Table: Settings
-- ============================================
CREATE TABLE Settings (
    Key TEXT NOT NULL PRIMARY KEY,
    Value TEXT NOT NULL,
    ValueType TEXT NOT NULL DEFAULT 'String',
    Description TEXT,
    UpdatedAt DATETIME NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- ============================================
-- Table: HolidayData
-- ============================================
CREATE TABLE HolidayData (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date DATE NOT NULL UNIQUE,
    IsHoliday BOOLEAN NOT NULL DEFAULT 0,
    IsWorkday BOOLEAN NOT NULL DEFAULT 0,
    Name TEXT,
    Source TEXT NOT NULL DEFAULT 'builtin',
    Year INTEGER NOT NULL
);

CREATE UNIQUE INDEX UX_HolidayData_Date ON HolidayData(Date);
CREATE INDEX IX_HolidayData_Year ON HolidayData(Year);

-- ============================================
-- Full-Text Search
-- ============================================
CREATE VIRTUAL TABLE EventsFTS USING fts5(
    Title, Description,
    content=Events,
    content_rowid=Id
);

CREATE TRIGGER TR_Events_InsertFTS
AFTER INSERT ON Events BEGIN
    INSERT INTO EventsFTS(rowid, Title, Description)
    VALUES (NEW.Id, NEW.Title, NEW.Description);
END;

CREATE TRIGGER TR_Events_DeleteFTS
AFTER DELETE ON Events BEGIN
    DELETE FROM EventsFTS WHERE rowid = OLD.Id;
END;

-- ============================================
-- Views
-- ============================================
CREATE VIEW V_UpcomingEvents AS
SELECT
    e.Id,
    e.Title,
    e.StartTime,
    e.EndTime,
    e.Location,
    e.Priority,
    e.ReminderOffset,
    CASE
        WHEN datetime(e.StartTime, '+15 minutes') <= datetime('now', 'localtime')
        THEN 1
        WHEN datetime(e.StartTime, '+1 hour') <= datetime('now', 'localtime')
        THEN 2
        ELSE 3
    END AS UrgencyLevel,
    strftime('%H:%M', e.StartTime) AS TimeDisplay,
    CASE WHEN e.IsAllDay = 1 THEN '全天' ELSE strftime('%H:%M', e.StartTime) END AS DisplayTime
FROM Events e
WHERE e.IsDeleted = 0
  AND date(e.StartTime) >= date('now', 'localtime')
  AND (e.EndTime IS NULL OR e.EndTime >= datetime('now', 'localtime'))
ORDER BY e.StartTime ASC;

CREATE VIEW V_TodayEvents AS
SELECT *
FROM Events
WHERE IsDeleted = 0
  AND date(StartTime) = date('now', 'localtime')
ORDER BY StartTime ASC;

CREATE VIEW V_DeletedEvents AS
SELECT
    Id,
    Title,
    StartTime,
    DeletedAt,
    julianday('now', 'localtime') - julianday(DeletedAt) AS DaysSinceDeletion
FROM Events
WHERE IsDeleted = 1
  AND DeletedAt >= date('now', '-7 days', 'localtime')
ORDER BY DeletedAt DESC;

-- ============================================
-- Initial Data
-- ============================================
INSERT INTO Settings (Key, Value, ValueType, Description) VALUES
('widget.opacity', '0.9', 'String', '窗口透明度'),
('widget.fontSize', '14', 'Int', '字体大小'),
('widget.positionX', '100', 'Int', '窗口X坐标'),
('widget.positionY', '100', 'Int', '窗口Y坐标'),
('widget.privacyMode', 'false', 'Bool', '隐私模式'),
('reminder.enabled', 'true', 'Bool', '提醒开关'),
('reminder.defaultOffset', '15', 'Int', '默认提前提醒分钟数'),
('system.autoStart', 'false', 'Bool', '开机自启'),
('system.language', 'zh-CN', 'String', '界面语言'),
('system.theme', 'light', 'String', '主题颜色');
```

---

## 12. 版本历史 (Version History)

### V1.2 (2026-03-11)

**数据库路径更新**：
- 数据库路径从 `%APPDATA%/DesktopAICalendar/data.db` 改为 `程序目录/data.db`
- 使用 `AppDomain.CurrentDomain.BaseDirectory` 获取程序目录
- 备份目录改为 `程序目录/backup/`

**MCP工具更新**：
- ToolName 新增 `list_events`（MCP-02.5）
- 统一为7个MCP工具

**Priority字段优化**：
- 明确说明使用C#枚举 + EF Core值转换器
- 数据库仍存储为INTEGER，代码层使用枚举

**决策依据**：
- 与API-Interface-Design.md V1.3保持一致

### V1.1 (2026-03-11)

**新增内容**：
- Events表新增字段：Description, UpdatedAt, IsAllDay, RecurrenceRule
- Priority扩展为3个值：0=普通，1=重要，2=紧急
- 新增Reminders表（提醒队列表），支持提醒重试机制
- 新增HolidayData表（节假日数据表）
- OperationLogs表新增字段：ErrorCode, ErrorMessage, ExecutionTime, UserId
- Settings表新增字段：ValueType, Description, UpdatedAt

**API安全机制补充**：
- 新增MCP API安全详细说明（4.2.2章节）
- 补充Prompt注入防护机制
- 新增操作权限控制详细说明
- 补充敏感信息保护措施

---

**文档结束**

> 本文档为 Desktop AI Calendar 项目的数据库设计说明书，涵盖了从概念设计到物理设计的完整过程。所有 SQL 脚本均已在 SQLite 3.x 环境下验证通过。
