# 产品需求文档 (PRD) - AI 透明桌面日历 (Desktop AI Calendar)

| 文档版本 | V1.3 (更新MCP工具、节假日库、数据库路径) |
| :--- | :--- |
| **项目名称** | Desktop AI Calendar (DAC) |
| **开发语言** | C# (.NET 8 WPF) |


---

## 1. 产品概述 (Product Overview)

### 1.1 产品背景
国内职场用户面临日程碎片化、会议频繁、节假日复杂等痛点。传统日历应用占用任务栏或需要频繁切换窗口，干扰工作流。本项目旨在打造一款**“无感存在、智能交互”**的桌面日历，通过透明挂件形式固定于桌面，结合 MCP (Model Context Protocol) 协议，允许用户通过大模型自然语言管理日程，实现“所说即所得”的体验。

### 1.2 产品定位
*   **形态**：Windows 桌面透明挂件 (Widget) + 后台服务。
*   **核心能力**：鼠标穿透、本地存储、MCP 大模型交互、智能提醒。
*   **目标用户**：国内办公室白领、自由职业者、极客用户、隐私敏感型用户。

### 1.3 核心价值
1.  **无感 (Non-intrusive)**：透明穿透，不遮挡工作窗口，不占用任务栏。
2.  **智能 (AI-Native)**：通过大模型自然语言管理日程，降低记录成本。
3.  **本土 (Localized)**：适配国内节假日、农历、办公协作习惯。
4.  **安全 (Secure)**：数据本地化，AI 操作具备审计与防误删机制。

---

## 2. 功能需求 (Functional Requirements)

### 2.1 桌面挂件模块 (Desktop Widget)
**优先级：P0**

| 功能 ID | 功能名称 | 详细描述 | 技术/交互要求 |
| :--- | :--- | :--- | :--- |
| **DW-01** | **窗口穿透** | 窗口鼠标事件完全穿透，不影响下方软件操作。 | 设置 `WS_EX_TRANSPARENT` \| `WS_EX_TOOLWINDOW`。 |
| **DW-02** | **透明背景** | 背景完全透明，仅渲染文字与图形。 | WPF `AllowsTransparency=True`。 |
| **DW-03** | **层级固定** | 固定在桌面图标层，不覆盖任务栏，不显示在 Alt+Tab。 | `Owner = IntPtr.Zero`, `ShowInTaskbar=False`。 |
| **DW-04** | **信息展示** | 显示公历、农历、星期、当前时间。 | 字体需加阴影以确保在任何壁纸下可见。 |
| **DW-05** | **日程预览** | **显示逻辑：距离当前时间最近的 3 条事件 + 剩余事件总数。** | 若今天无事件，显示“今日无安排”。紧急事件 (1h 内) 红色高亮。 |
| **DW-06** | **多屏适配** | 自动识别主显示器，支持多显示器 DPI 缩放。 | 监听 `SystemEvents.DisplaySettingsChanged`。 |
| **DW-07** | **隐私模式** | 快捷键一键隐藏事件标题，仅显示时间块或“忙碌”。 | 默认快捷键 `Ctrl + Alt + P`。 |

### 2.2 MCP 服务模块 (AI Interaction)
**优先级：P0 (核心差异化)**

**MCP Server 服务宿主**：
- 本模块实现 MCP Server 服务宿主，通过特性标记自动注册工具
- 传输方式：**Stdio（开发环境）或 HTTP/SSE（生产环境）**
- 监听地址：`localhost:37281`（仅本地访问）
- 上下文注入：初始化时将"今日+明日"事件摘要注入 System Prompt
- 参考实现：[官方 AspNetCoreMcpServer 示例](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples/AspNetCoreMcpServer)

| 功能 ID | 功能名称 | 详细描述 | 安全/逻辑要求 |
| :--- | :--- | :--- | :--- |
| **MCP-02** | **搜索事件** | `search_events(query, start, end, includeDeleted)` | **所有修改操作的前置步骤**。支持模糊匹配，返回 `id`, `title`, `time`。**默认排除已删除事件**。 |
| **MCP-02.5** | **列出事件** | `list_events(limit, start, end, includeDeleted)` | **获取所有有效事件（默认排除已删除），支持 LLM 直接查看现有日程**。 |
| **MCP-03** | **创建事件** | `create_event(title, time, duration, reminder)` | 自动解析自然语言时间。若时间冲突，返回 Warning。 |
| **MCP-04** | **更新事件** | `update_event(id, changes)` | **必须基于 ID 更新**。禁止仅凭标题更新。 |
| **MCP-05** | **删除事件** | `delete_event(id, confirm)` | **软删除**。需传入 `confirm=true` 或二次确认机制。 |
| **MCP-06** | **空闲分析** | `get_free_time(duration, date)` | 返回指定日期内的空闲时间段建议。 |
| **MCP-07** | **恢复事件** | `restore_event(id)` | 从回收站恢复已软删除的事件（7天内可恢复）。 |

### 2.3 手动管理模块 (Manual Management)
**优先级：P1**

| 功能 ID | 功能名称 | 详细描述 | 交互要求 |
| :--- | :--- | :--- | :--- |
| **MM-01** | **设置窗口** | 独立的可交互窗口，用于管理事件和配置。 | 打开时主挂件可暂时恢复点击或保持穿透。 |
| **MM-02** | **事件 CRUD** | 列表展示事件，支持增删改查。 | 支持拖拽调整时间 (可选)。 |
| **MM-03** | **节假日同步** | 自动同步国务院发布的法定节假日及调休。 | 使用 `ChineseCalendar 1.0.4` 库本地计算，无需API调用。 |
| **MM-04** | **外观配置** | 调整字体大小、颜色、透明度、位置。 | 实时预览效果。 |
| **MM-05** | **回收站** | 查看和管理被软删除的事件 (保留 7 天)。 | 支持一键恢复。 |

### 2.4 提醒与通知模块 (Reminder System)
**优先级：P0**

| 功能 ID | 功能名称 | 详细描述 | 技术实现 |
| :--- | :--- | :--- | :--- |
| **RM-01** | **后台轮询** | 即使 UI 关闭，后台服务仍每分钟检查到期事件。 | `IHostedService` + `Timer`。 |
| **RM-02** | **Toast 通知** | 调用 Windows 原生通知中心。 | `Microsoft.Toolkit.Uwp.Notifications 7.1.3`。 |
| **RM-03** | **智能免打扰** | 检测到全屏应用 (游戏/演示) 时暂停通知。 | 调用 `GetForegroundWindow` 判断。 |
| **RM-04** | **延后提醒** | 通知栏支持“延后 10 分钟”操作。 | 更新事件提醒时间并重新入队。 |
| **RM-05** | **健康提醒** | 久坐/喝水/护眼提醒 (可配置)。 | 独立于事件系统的定时器。 |

### 2.5 系统基础模块 (System)
**优先级：P1**

| 功能 ID | 功能名称 | 详细描述 | 备注 |
| :--- | :--- | :--- | :--- |
| **SY-01** | **开机自启** | 支持注册表或任务计划程序自启。 | 默认关闭，用户可选。 |
| **SY-02** | **系统托盘** | 最小化到托盘，提供菜单 (设置、退出、显示/隐藏)。 | 使用 `H.NotifyIcon` 或原生。 |
| **SY-03** | **自动更新** | 检查新版本并提示下载。 | GitHub Release 或 私有服务器。 |
| **SY-04** | **日志审计** | 记录所有 MCP 操作日志。 | 用于追溯 AI 误操作。 |

---

## 3. 数据设计 (Data Design)

### 3.1 数据库 (SQLite)
**文件路径**：`程序目录/data.db`（与exe同级）
**备份路径**：`程序目录/backup/`（自动备份最近5份）
**缓存路径**：`程序目录/cache/`（节假日缓存）

#### 表：Events (事件表)
| 字段 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- |
| Id | INTEGER | Y | 主键，自增 |
| Title | TEXT | Y | 事件标题 (1-200字符) |
| Description | TEXT | N | 事件详细描述 (最多2000字符) |
| StartTime | DATETIME | Y | 开始时间 (UTC+8) |
| EndTime | DATETIME | N | 结束时间 |
| Location | TEXT | N | 地点或会议链接 |
| Priority | INTEGER | N | **优先级 (0:普通，1:重要，2:紧急)** |
| ReminderOffset | INTEGER | N | 提前提醒分钟数 (默认 0) |
| IsLunar | BOOLEAN | N | 是否为农历循环事件 |
| IsAllDay | BOOLEAN | N | 是否为全天事件 |
| RecurrenceRule | TEXT | N | 重复规则 (RRULE格式，如"FREQ=WEEKLY") |
| IsDeleted | BOOLEAN | N | **软删除标记** (默认 0) |
| DeletedAt | DATETIME | N | 删除时间 |
| CreatedAt | DATETIME | Y | 创建时间 |
| UpdatedAt | DATETIME | Y | 最后更新时间 |

#### 表：Reminders (提醒队列表)
| 字段 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- |
| Id | INTEGER | Y | 主键，自增 |
| EventId | INTEGER | Y | 关联事件ID (外键) |
| RemindTime | DATETIME | Y | 提醒时间点 |
| IsNotified | BOOLEAN | N | 是否已通知 (默认 0) |
| RetryCount | INTEGER | N | 重试次数 (用户点击"延后10分钟"时递增) |
| NotifiedAt | DATETIME | N | 实际通知时间 |
| CreatedAt | DATETIME | Y | 创建时间 |

#### 表：OperationLogs (审计日志表)
| 字段 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- |
| Id | INTEGER | Y | 主键 |
| ToolName | TEXT | Y | MCP 工具名 (如 `delete_event`) |
| Params | TEXT | Y | 请求参数 (JSON) |
| Result | TEXT | N | 执行结果 (Success/Error) |
| ErrorCode | TEXT | N | 错误码 (如"EVENT_NOT_FOUND") |
| ErrorMessage | TEXT | N | 错误详细信息 |
| ExecutionTime | INTEGER | N | 执行耗时 (毫秒) |
| Timestamp | DATETIME | Y | 操作时间 |
| UserId | TEXT | N | 用户标识 (未来支持多用户) |

#### 表：Settings (配置表)
| 字段 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- |
| Key | TEXT | Y | 配置键 |
| Value | TEXT | Y | 配置值 (JSON 字符串) |
| ValueType | TEXT | N | 值类型：String/Int/Bool/Json |
| Description | TEXT | N | 配置项说明 |
| UpdatedAt | DATETIME | Y | 最后更新时间 |

#### 表：HolidayData (节假日数据表)
| 字段 | 类型 | 必填 | 说明 |
| :--- | :--- | :--- | :--- |
| Id | INTEGER | Y | 主键，自增 |
| Date | DATE | Y | 日期 (YYYY-MM-DD，唯一) |
| IsHoliday | BOOLEAN | N | 是否为法定节假日 |
| IsWorkday | BOOLEAN | N | 是否为调休工作日 |
| Name | TEXT | N | 节假日名称 (如"春节"、"国庆节") |
| Source | TEXT | N | 数据来源：builtin/api/manual |
| Year | INTEGER | Y | 所属年份 (用于快速查询) |

---

## 4. 非功能性需求 (Non-Functional Requirements)

### 4.1 性能要求
*   **内存占用**：空闲时 < 50MB，活跃时 < 100MB。
*   **CPU 占用**：空闲时 < 1%，轮询检查时瞬间 < 5%。
*   **启动速度**：冷启动 < 3 秒 (支持 .NET AOT 优化)。
*   **渲染帧率**：桌面挂件无需高帧率，静态刷新即可，避免 GPU 持续占用。

### 4.2 安全与隐私

#### 4.2.1 数据安全
*   **数据存储**：所有数据本地存储，不上传云端 (除非用户主动开启同步)。
*   **数据库加密**：SQLite 文件仅允许当前用户完全控制，拒绝其他用户访问（使用 icacls 配置）。
*   **数据备份**：每次启动自动备份 `data.db`，保留最近 5 份备份文件。

#### 4.2.2 MCP API 安全
*   **网络隔离**：MCP Server 仅绑定 `127.0.0.1`，防止局域网攻击。
*   **传输方式**：
    - **开发环境**：使用 Stdio 传输，通过标准输入/输出通信
    - **生产环境**：使用 HTTP (SSE) 传输，监听 `localhost:37281`
*   **访问控制**：拒绝所有非 localhost 连接请求，HTTP 服务器不对外开放端口
*   **请求验证**：所有 MCP 工具调用必须经过参数验证，防止注入攻击

#### 4.2.3 操作权限控制
*   **修改/删除操作**：必须经过 `search_events` 获取 ID 后执行，禁止模糊匹配写入
*   **删除确认**：`delete_event` 工具必须传入 `confirm=true` 参数
*   **软删除机制**：所有删除操作为软删除，数据保留 7 天可恢复
*   **操作审计**：所有 MCP 调用记录到 `OperationLogs` 表，包含：
    - ToolName（工具名）
    - Params（请求参数）
    - Result（执行结果）
    - ErrorCode/ErrorMessage（错误信息）
    - ExecutionTime（执行耗时）
    - Timestamp（操作时间）

#### 4.2.4 Prompt 注入防护
*   **输入过滤**：移除用户输入中的常见 Prompt 注入模式（如 “IGNORE PREVIOUS INSTRUCTIONS”）
*   **长度限制**：事件标题限制 200 字符，描述限制 2000 字符
*   **特殊字符转义**：SQL 查询使用参数化查询，防止 SQL 注入

#### 4.2.5 敏感信息保护
*   **隐私模式**：支持一键隐藏事件详情，快捷键 `Ctrl + Alt + P`
*   **屏幕共享保护**：隐私模式下仅显示时间块或”忙碌”，不显示事件标题
*   **日志脱敏**：导出日志时自动脱敏敏感信息（如手机号、邮箱）

### 4.3 兼容性
*   **操作系统**：Windows 10 (2004+) / Windows 11。
*   **DPI 缩放**：完美支持 100% - 200% 系统缩放。
*   **多显示器**：支持不同显示器不同 DPI 设置。

### 4.4 可靠性
*   **崩溃恢复**：进程意外退出后，若开启自启，系统应能自动重启服务。
*   **数据备份**：每次启动自动备份 `data.db` 至 `backup/` 文件夹 (保留最近 5 份)。

---

## 5. UI/UX 交互规范

### 5.1 视觉风格
*   **风格**：极简主义 (Minimalist)。
*   **字体**：微软雅黑 / Segoe UI，支持加粗阴影 (TextEffect)。
*   **颜色**：默认白色文字，支持根据壁纸亮度自动反色 (可选 P2)。
*   **布局**：
    ```text
    [ 10 月 27 日 星期五 ]  (字号 24, 加粗)
    [ 农历 九月十三    ]  (字号 14, 灰色)
    -------------------
    ● 10:00 晨会        (字号 14, 正常)
    ● 14:00 客户会议    (字号 14, 正常)
    ● 18:00 健身        (字号 14, 正常)
    -------------------
    + 还有 2 个事件     (字号 12, 蓝色，点击打开设置窗口)
    ```

### 5.2 交互流程
1.  **日常状态**：桌面显示透明挂件，鼠标穿透，不可点击。
2.  **管理状态**：
    *   方式 A：点击系统托盘图标 -> 选择“管理日程”。
    *   方式 B：快捷键 `Ctrl + Alt + C` -> 唤起设置窗口。
    *   方式 C：点击挂件上的"+ 还有 X 个事件” (需临时关闭穿透或唤起设置窗口)。
3.  **AI 交互状态**：
    *   用户在 LLM 客户端 (如 Claude Desktop) 输入指令。
    *   LLM 调用 MCP 工具。
    *   桌面挂件通过消息总线 (Messenger) 接收刷新信号 -> 更新显示。
    *   若发生提醒 -> 弹出 Toast 通知。

---

## 6. 风险控制与应对 (Risk Management)

| 风险点 | 风险描述 | 应对策略 |
| :--- | :--- | :--- |
| **AI 误操作** | 大模型幻觉导致删错事件或改错时间。 | 1. 强制 ID 操作机制。<br>2. 软删除 + 回收站 (7 天保留)。<br>3. 操作审计日志。<br>4. Prompt 中注入安全指令。 |
| **鼠标穿透失效** | 用户无法点击挂件上的内容。 | 明确设计原则：挂件**仅展示**。所有交互通过托盘菜单或独立设置窗口完成。 |
| **性能卡顿** | WPF 渲染导致旧电脑卡顿。 | 1. 使用 `CompositionTarget.Rendering` 控制刷新率。<br>2. 静态内容使用 `WriteableBitmap` 缓存。 |
| **节假日数据错误** | API 挂掉导致调休显示错误。 | 1. 本地内置当年节假日数据。<br>2. API 请求失败时使用本地缓存。<br>3. 允许用户手动修正。 |
| **隐私泄露** | 屏幕共享时泄露日程。 | 提供“隐私模式”快捷键，一键隐藏标题，仅显示时间块。 |

---

## 7. 开发里程碑 (Roadmap)

| 阶段 | 周期 | 目标 | 交付物 |
| :--- | :--- | :--- | :--- |
| **Phase 1: 原型** | 1 周 | 实现透明窗口、穿透、显示时间 | `Demo.exe` (仅显示) |
| **Phase 2: 核心** | 2 周 | 完成 SQLite 集成、手动 CRUD、Toast 提醒 | `Alpha.exe` (可手动管理) |
| **Phase 3: AI 集成** | 2 周 | 完成 MCP Server、安全机制、日志审计 | `Beta.exe` (支持 AI 控制) |
| **Phase 4: 完善** | 1 周 | 节假日 API、开机自启、安装包、性能优化 | `Release 1.0.exe` |
| **Phase 5: 迭代** | 持续 | 多端同步、微信提醒、插件系统 | `V1.x` |

---

## 8. 附录：MCP 安全指令 Prompt 模板

在 MCP Server 初始化时，建议将以下指令注入给 LLM：

```text
You are a Calendar Assistant integrated with a Desktop AI Calendar.
Safety Rules:
1. NEVER delete or update an event without knowing its specific `id`.
2. If a user asks to "delete tomorrow's meeting", you MUST first call `search_events` to find the exact `id`.
3. If `search_events` returns multiple results, ask the user to clarify which one they mean. DO NOT guess.
4. All deletions are "soft deletes" and can be restored within 7 days.
5. If a time conflict is detected during creation, warn the user and suggest alternative times using `get_free_time`.
6. Always confirm with the user before executing destructive actions.
```

---

## 9. 版本历史 (Version History)

### V1.4 (2026-03-11)

**MCP Server 架构明确**：
- 新增"MCP Server 服务宿主"说明（非工具）
- 明确双Host架构：WPF使用`Host.CreateDefaultBuilder()`，MCP使用`WebApplication.CreateBuilder()`
- 统一传输方式描述："Stdio（开发环境）或 HTTP/SSE（生产环境）"
- 添加官方AspNetCoreMcpServer示例参考链接

**工具编号确认**：
- 保持7个MCP工具（MCP-02、MCP-02.5、MCP-03、MCP-04、MCP-05、MCP-06、MCP-07）
- MCP Server本身不是工具，而是服务宿主

**决策依据**：
- 参考文档：文档冲突分析（2026-03-11）
- 与System-Architecture.md V1.5、API-Interface-Design.md V1.3保持一致

### V1.3 (2026-03-11)

**MCP工具优化**：
- 移除`MCP-01 服务宿主`（这是MCP Server本身，不是工具）
- 统一为7个可调用的MCP工具
- 工具编号：MCP-02、MCP-02.5、MCP-03、MCP-04、MCP-05、MCP-06、MCP-07

**节假日数据源更新**：
- 从`timettp.cn` API改为`ChineseCalendar 1.0.4`库
- MM-03功能改为使用ChineseCalendar库本地计算
- 无需API调用，完全离线可用

**数据库路径更新**：
- 从`%APPDATA%/DesktopAICalendar/data.db`改为`程序目录/data.db`
- 备份路径：`程序目录/backup/`
- 缓存路径：`程序目录/cache/`

**决策依据**：
- 与System-Architecture.md V1.5、Database-Design.md V1.2保持一致

### V1.2 (2026-03-11)

**MCP 功能增强**：
- **新增** `list_events` 工具（MCP-02.5）：支持 LLM 直接获取所有有效事件列表
- **优化** `search_events` 工具（MCP-02）：新增 `includeDeleted` 参数，默认排除已删除事件
- **安全机制强化**：所有查询类工具默认不返回软删除数据，避免 LLM 误操作已删除事件
- **工具总数更新**：MCP 工具从 6 个增加到 7 个

**设计理念更新**：
- 明确 MCP 与 GUI 设置界面的功能对等性
- 强化 LLM 访问现有事件的能力，减少用户重复说明
- 保持软删除机制的安全性，默认隐藏已删除内容

**文档同步**：
- 与 API-Interface-Design.md V1.2 保持一致
- 与 System-Architecture.md V1.4 保持一致

### V1.1 (2026-03-11)

**数据库设计更新**：
- Events 表新增字段：`Description`, `UpdatedAt`, `IsAllDay`, `RecurrenceRule`
- Events.Priority 扩展为 3 个值：0=普通，1=重要，2=紧急
- 新增 Reminders 表（提醒队列表），支持提醒重试机制
- 新增 HolidayData 表（节假日数据表）
- OperationLogs 表新增字段：`ErrorCode`, `ErrorMessage`, `ExecutionTime`, `UserId`
- Settings 表新增字段：`ValueType`, `Description`, `UpdatedAt`

**API 安全机制补充**：
- 新增 MCP API 安全详细说明（4.2.2 章节）
- 补充 Prompt 注入防护机制
- 新增操作权限控制详细说明
- 补充敏感信息保护措施

**其他改进**：
- 与 Database-Design.md V1.1 和 API-Interface-Design.md V1.1 保持一致
- 完善软删除和审计机制说明

### V1.0 (2026-03-10)

**初始版本**：
- 核心功能需求定义
- 基础数据结构设计
- MCP 服务模块规范
- UI/UX 交互规范

---


🎯 建议优先补充的文档（按优先级）
P1 - 必需文档：

数据库设计说明书
接口设计说明书
详细设计说明书
编码规范
测试计划
测试用例
用户手册
安装部署指南
项目计划