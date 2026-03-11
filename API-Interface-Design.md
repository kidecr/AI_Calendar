# 接口设计说明书 (API Interface Design Document)

| 文档版本 | V1.3 (明确Priority枚举设计、更新MCP工具清单) |
|:---|:---|
| **项目名称** | Desktop AI Calendar (DAC) |
| **协议类型** | MCP (Model Context Protocol) |
| **传输方式** | Stdio / SSE |
| **编写日期** | 2026-03-11 |
| **编写人** | AI Assistant |

---

## 1. 接口概述 (Interface Overview)

### 1.1 MCP 协议简介

**MCP (Model Context Protocol)** 是一种开放协议，用于连接 AI 助手与外部系统。本项目实现 **MCP Server**，允许大模型（如 Claude、GPT-4）直接调用日历功能。

### 1.2 接口清单

| 接口类别 | 接口数量 | 说明 |
|:---|:---|:---|
| **工具接口 (Tools)** | 7 个 | list_events、search、create、update、delete、get_free_time、restore |
| **资源接口 (Resources)** | 2 个 | 今日事件摘要、统计信息 |
| **提示接口 (Prompts)** | 1 个 | 日程管理助手提示模板 |
| **总计** | 9 个 | - |

### 1.3 传输架构

```
┌─────────────┐      MCP Protocol      ┌──────────────────────┐
│   LLM       │ ←───────────────────→  │  MCP Server (DAC)    │
│  (Claude)   │  Stdio / HTTP (SSE)    │  localhost:37281     │
└─────────────┘                        └──────────┬───────────┘
                                                 ↓
                                        ┌──────────────────────┐
                                        │   EventService       │
                                        │  (业务逻辑层)         │
                                        └──────────┬───────────┘
                                                 ↓
                          ┌──────────────────────┴──────────────────┐
                          ↓                                          ↓
                   ┌──────────────────┐                   ┌──────────────┐
                   │  SQLite Database │                   │  IMessenger  │
                   │  (数据持久化)     │                   │  (消息总线)   │
                   └──────────────────┘                   └──────┬───────┘
                                                             ↓
                                                      ┌──────────────┐
                                                      │  WPF UI      │
                                                      │  (ViewModels)│
                                                      └──────────────┘
```

### 1.4 设计原则

1. **安全第一**：修改/删除操作必须基于 ID，禁止模糊匹配
2. **幂等性**：重复调用同一接口不会产生副作用
3. **上下文注入**：初始化时注入今日事件，减少 LLM 反问
4. **详细审计**：所有操作记录日志，支持追溯

---

## 2. 协议规范 (Protocol Specification)

### 2.1 传输方式

本项目支持 **两种 MCP 传输方式**，根据部署环境自动选择：

#### 方式 A: Stdio（推荐用于开发环境）

**传输方式**：标准输入/输出流
**适用场景**：本地开发、调试
**配置示例**：

```json
{
  "mcpServers": {
    "desktop-calendar": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/AI_Calendar.csproj"],
      "env": {
        "MCP_TRANSPORT": "stdio"
      }
    }
  }
}
```

#### 方式 B: HTTP/SSE（推荐用于生产环境）

**传输方式**：HTTP (Server-Sent Events)
**监听地址**：`http://localhost:37281`
**适用场景**：独立部署、跨进程调用

**实现方式**：
```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport();
```

**传输方式选择**：
- **Stdio（开发环境）**：通过标准输入输出通信，简单直接
- **HTTP/SSE（生产环境）**：支持跨进程调用，可远程访问（仅限localhost），性能更稳定

### 2.2 消息格式（JSON-RPC 2.0）

```json
// 请求格式
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "search_events",
    "arguments": {
      "query": "会议",
      "start": "2026-03-11T00:00:00+08:00",
      "end": "2026-03-12T23:59:59+08:00"
    }
  }
}

// 响应格式
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"events\":[{\"id\":123,\"title\":\"产品评审会议\",\"startTime\":\"2026-03-11T10:00:00+08:00\"}]}"
      }
    ]
  }
}
```

### 2.3 时间格式

所有时间字段遵循 **ISO 8601** 标准，默认时区为 **UTC+8**（北京时间）：

```
示例：2026-03-11T14:30:00+08:00
```

---

## 3. 工具接口设计 (Tools API)

### 3.1 list_events - 获取事件列表

#### 接口描述
获取当前所有有效的日历事件列表（**自动排除软删除**），支持时间范围筛选和数量限制。**便于 LLM 直接查看用户现有日程**。

#### 请求参数

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
|:---|:---|:---|:---|:---|
| `limit` | integer | 否 | 50 | 返回数量限制（1-200） |
| `start` | string | 否 | null | 起始时间（ISO 8601，可选） |
| `end` | string | 否 | null | 结束时间（ISO 8601，可选） |
| `includeDeleted` | boolean | 否 | false | 是否包含已删除事件（默认 false） |

#### 请求示例

```json
{
  "name": "list_events",
  "arguments": {
    "limit": 20,
    "start": "2026-03-11T00:00:00+08:00"
  }
}
```

#### 响应示例

```json
{
  "content": [
    {
      "type": "text",
      "text": "{\n  \"events\": [\n    {\n      \"id\": 42,\n      \"title\": \"每日晨会\",\n      \"startTime\": \"2026-03-11T09:00:00+08:00\",\n      \"endTime\": \"2026-03-11T09:30:00+08:00\",\n      \"priority\": 1,\n      \"isAllDay\": false\n    },\n    {\n      \"id\": 43,\n      \"title\": \"产品评审会议\",\n      \"startTime\": \"2026-03-11T14:00:00+08:00\",\n      \"priority\": 2\n    }\n  ],\n  \"total\": 2,\n  \"filter\": {\n    \"limit\": 20,\n    \"start\": \"2026-03-11T00:00:00+08:00\",\n    \"includeDeleted\": false\n  },\n  \"note\": \"仅显示有效事件\"\n}"
    }
  ]
}
```

#### 错误响应

| 错误码 | HTTP 状态 | 说明 |
|:---|:---|:---|
| `INVALID_LIMIT` | 400 | limit 超出范围（1-200） |
| `INVALID_DATE_FORMAT` | 400 | 日期格式不符合 ISO 8601 |

---

### 3.2 search_events - 搜索事件

#### 接口描述
搜索指定时间范围内的事件，支持模糊匹配标题和描述。**所有修改/删除操作的前置步骤**。**默认排除已删除事件**。

#### 请求参数

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
|:---|:---|:---|:---|:---|
| `query` | string | 否 | "" | 搜索关键词（模糊匹配标题、描述） |
| `start` | string | 是 | - | 起始时间（ISO 8601） |
| `end` | string | 是 | - | 结束时间（ISO 8601） |
| `limit` | integer | 否 | 20 | 最大返回结果数（1-100） |
| `includeDeleted` | boolean | 否 | false | 是否包含已删除事件（默认 false） |

#### 请求示例

```json
{
  "name": "search_events",
  "arguments": {
    "query": "晨会",
    "start": "2026-03-11T00:00:00+08:00",
    "end": "2026-03-11T23:59:59+08:00",
    "includeDeleted": false
  }
}
```

#### 响应示例

```json
{
  "content": [
    {
      "type": "text",
      "text": "{\n  \"events\": [\n    {\n      \"id\": 42,\n      \"title\": \"每日晨会\",\n      \"description\": \"讨论今日工作计划\",\n      \"startTime\": \"2026-03-11T09:00:00+08:00\",\n      \"endTime\": \"2026-03-11T09:30:00+08:00\",\n      \"location\": \"会议室 A\",\n      \"priority\": 1,\n      \"isAllDay\": false\n    },\n    {\n      \"id\": 43,\n      \"title\": \"产品晨会\",\n      \"startTime\": \"2026-03-11T10:00:00+08:00\",\n      \"endTime\": \"2026-03-11T10:30:00+08:00\",\n      \"location\": \"腾讯会议 888888\",\n      \"priority\": 2,\n      \"isAllDay\": false\n    }\n  ],\n  \"total\": 2\n}"
    }
  ]
}
```

#### 错误响应

| 错误码 | HTTP 状态 | 说明 |
|:---|:---|:---|
| `INVALID_TIME_RANGE` | 400 | 时间范围无效（start > end） |
| `INVALID_DATE_FORMAT` | 400 | 日期格式不符合 ISO 8601 |

---

### 3.3 create_event - 创建事件

#### 接口描述
创建新事件。自动解析自然语言时间，检测时间冲突并返回警告。

#### 请求参数

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
|:---|:---|:---|:---|:---|
| `title` | string | 是 | - | 事件标题（1-200 字符） |
| `startTime` | string | 是 | - | 开始时间（ISO 8601） |
| `endTime` | string | 否 | null | 结束时间（默认为 startTime + 1小时） |
| `location` | string | 否 | null | 地点或会议链接 |
| `description` | string | 否 | null | 事件详细描述 |
| `priority` | integer | 否 | 0 | 优先级（0=普通，1=重要，2=紧急） |
| `reminderOffset` | integer | 否 | 15 | 提前提醒分钟数（0=不提醒） |
| `isAllDay` | boolean | 否 | false | 是否为全天事件 |
| `recurrenceRule` | string | 否 | null | 重复规则（RRULE 格式） |

#### 请求示例

```json
{
  "name": "create_event",
  "arguments": {
    "title": "客户需求评审",
    "startTime": "2026-03-12T14:00:00+08:00",
    "endTime": "2026-03-12T15:30:00+08:00",
    "location": "腾讯会议 999999",
    "description": "讨论 V2.0 需求变更",
    "priority": 2,
    "reminderOffset": 30,
    "recurrenceRule": "FREQ=WEEKLY;BYDAY=MO,WE,FR"
  }
}
```

#### 响应示例（成功）

```json
{
  "content": [
    {
      "type": "text",
      "text": "{\n  \"success\": true,\n  \"event\": {\n    \"id\": 44,\n    \"title\": \"客户需求评审\",\n    \"startTime\": \"2026-03-12T14:00:00+08:00\",\n    \"endTime\": \"2026-03-12T15:30:00+08:00\",\n    \"priority\": 2,\n    \"reminderOffset\": 30\n  },\n  \"message\": \"事件创建成功\"\n}"
    }
  ]
}
```

#### 响应示例（冲突警告）

```json
{
  "content": [
    {
      "type": "text",
      "text": "{\n  \"success\": true,\n  \"event\": {\n    \"id\": 44,\n    \"title\": \"客户需求评审\",\n    \"startTime\": \"2026-03-12T14:00:00+08:00\",\n    \"endTime\": \"2026-03-12T15:30:00+08:00\",\n    \"priority\": 2,\n    \"reminderOffset\": 30\n  },\n  \"warnings\": [\n    {\n      \"type\": \"TIME_CONFLICT\",\n      \"message\": \"与现有事件「产品评审会议」时间冲突\",\n      \"conflictingEventId\": 42,\n      \"conflictingTime\": \"2026-03-12T14:00:00+08:00\"\n    }\n  ],\n  \"suggestion\": \"建议使用 get_free_time 查询其他可用时间段\"\n}"
    }
  ]
}
```

**说明**：
- `event`：创建的事件对象（不包含warnings字段）
- `warnings`：警告数组，包含冲突、重复等信息
- `suggestion`：可选的建议信息

#### 错误响应

| 错误码 | 说明 |
|:---|:---|
| `INVALID_TITLE` | 标题为空或超过 200 字符 |
| `INVALID_TIME` | 时间格式错误或 startTime 在过去 |
| `INVALID_PRIORITY` | 优先级不在 0-2 范围内 |
| `INVALID_RECURRENCE_RULE` | RRULE 格式错误 |

---

### 3.4 update_event - 更新事件

#### 接口描述
**必须基于 ID 更新**，禁止仅凭标题模糊更新。支持部分更新。

#### 请求参数

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
|:---|:---|:---|:---|:---|
| `id` | integer | 是 | - | 事件 ID（必须先通过 search_events 获取） |
| `changes` | object | 是 | - | 要修改的字段（与 create_event 参数相同） |

#### 请求示例

```json
{
  "name": "update_event",
  "arguments": {
    "id": 44,
    "changes": {
      "title": "客户需求评审（已变更）",
      "startTime": "2026-03-12T15:00:00+08:00",
      "priority": 1
    }
  }
}
```

#### 响应示例

```json
{
  "content": [
    {
      "type": "text",
      "text": "{\n  \"success\": true,\n  \"event\": {\n    \"id\": 44,\n    \"title\": \"客户需求评审（已变更）\",\n    \"startTime\": \"2026-03-12T15:00:00+08:00\",\n    \"updatedAt\": \"2026-03-11T10:30:00+08:00\"\n  }\n}"
    }
  ]
}
```

#### 错误响应

| 错误码 | 说明 |
|:---|:---|
| `EVENT_NOT_FOUND` | 指定 ID 的事件不存在 |
| `EVENT_ALREADY_DELETED` | 事件已被软删除，请先恢复 |
| `NO_CHANGES_PROVIDED` | 未提供任何要修改的字段 |

---

### 3.5 delete_event - 删除事件

#### 接口描述
**软删除**事件，保留 7 天可恢复。需传入 `confirm=true` 二次确认。

#### 请求参数

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
|:---|:---|:---|:---|:---|
| `id` | integer | 是 | - | 事件 ID（必须先通过 search_events 获取） |
| `confirm` | boolean | 是 | false | **确认删除标记**（必须显式传入 true） |

#### 请求示例

```json
{
  "name": "delete_event",
  "arguments": {
    "id": 44,
    "confirm": true
  }
}
```

#### 响应示例

```json
{
  "content": [
    {
      "type": "text",
      "text": "{\n  \"success\": true,\n  \"message\": \"事件已移至回收站，7 天内可使用 restore_event 恢复\",\n  \"deletedEventId\": 44,\n  \"deletedAt\": \"2026-03-11T10:35:00+08:00\",\n  \"restoreDeadline\": \"2026-03-18T10:35:00+08:00\"\n}"
    }
  ]
}
```

#### 错误响应

| 错误码 | 说明 |
|:---|:---|
| `EVENT_NOT_FOUND` | 事件不存在 |
| `CONFIRMATION_REQUIRED` | `confirm` 未设为 true |
| `EVENT_ALREADY_DELETED` | 事件已被删除 |

---

### 3.6 get_free_time - 查询空闲时间

#### 接口描述
分析指定日期的日程安排，返回可用时间段建议。

#### 请求参数

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
|:---|:---|:---|:---|:---|
| `date` | string | 是 | - | 日期（YYYY-MM-DD） |
| `duration` | integer | 否 | 60 | 需要的时长（分钟） |
| `workHoursOnly` | boolean | 否 | true | 仅返回工作时间（9:00-18:00） |

#### 请求示例

```json
{
  "name": "get_free_time",
  "arguments": {
    "date": "2026-03-12",
    "duration": 90,
    "workHoursOnly": true
  }
}
```

#### 响应示例

```json
{
  "content": [
    {
      "type": "text",
      "text": "{\n  \"date\": \"2026-03-12\",\n  \"duration\": 90,\n  \"freeSlots\": [\n    {\n      \"start\": \"2026-03-12T09:00:00+08:00\",\n      \"end\": \"2026-03-12T10:30:00+08:00\",\n      \"availableMinutes\": 90\n    },\n    {\n      \"start\": \"2026-03-12T13:00:00+08:00\",\n      \"end\": \"2026-03-12T18:00:00+08:00\",\n      \"availableMinutes\": 300\n    }\n  ],\n  \"totalFreeTime\": 390,\n  \"message\": \"当天有 2 个空闲时段可供安排\"\n}"
    }
  ]
}
```

#### 错误响应

| 错误码 | 说明 |
|:---|:---|
| `INVALID_DATE_FORMAT` | 日期格式错误（应为 YYYY-MM-DD） |
| `DATE_IN_PAST` | 不能查询过去的日期 |
| `NO_FREE_TIME` | 当天无足够空闲时间 |

---

### 3.7 restore_event - 恢复已删除事件

#### 接口描述
从回收站恢复已软删除的事件。

#### 请求参数

| 参数名 | 类型 | 必填 | 默认值 | 说明 |
|:---|:---|:---|:---|:---|
| `id` | integer | 是 | - | 已删除事件的 ID |

#### 请求示例

```json
{
  "name": "restore_event",
  "arguments": {
    "id": 44
  }
}
```

#### 响应示例

```json
{
  "content": [
    {
      "type": "text",
      "text": "{\n  \"success\": true,\n  \"message\": \"事件已恢复\",\n  \"event\": {\n    \"id\": 44,\n    \"title\": \"客户需求评审\",\n    \"startTime\": \"2026-03-12T15:00:00+08:00\"\n  }\n}"
    }
  ]
}
```

#### 错误响应

| 错误码 | 说明 |
|:---|:---|
| `EVENT_NOT_FOUND` | 事件不存在 |
| `EVENT_NOT_DELETED` | 事件未被删除，无需恢复 |
| `RESTORE_EXPIRED` | 超过 7 天保留期，无法恢复 |

---

## 4. 资源接口设计 (Resources API)

### 4.1 today://summary - 今日事件摘要

#### 接口描述
返回今日事件的简要摘要，用于 MCP 初始化时的 **上下文注入**。

#### 资源 URI

```
today://summary
```

#### 响应示例

```json
{
  "contents": [
    {
      "uri": "today://summary",
      "mimeType": "application/json",
      "text": "{\n  \"date\": \"2026-03-11\",\n  \"totalEvents\": 5,\n  \"upcoming\": [\n    {\n      \"time\": \"09:00\",\n      \"title\": \"每日晨会\",\n      \"priority\": \"important\"\n    },\n    {\n      \"time\": \"14:00\",\n      \"title\": \"客户需求评审\",\n      \"priority\": \"urgent\"\n    },\n    {\n      \"time\": \"16:00\",\n      \"title\": \"周报总结\",\n      \"priority\": \"normal\"\n    }\n  ],\n  \"freeTimeSlots\": [\n    \"10:00-12:00\",\n    \"15:00-16:00\"\n  ]\n}"
    }
  ]
}
```

---

### 4.2 stats://overview - 统计信息

#### 接口描述
返回日历使用统计信息。

#### 资源 URI

```
stats://overview
```

#### 响应示例

```json
{
  "contents": [
    {
      "uri": "stats://overview",
      "mimeType": "application/json",
      "text": "{\n  \"totalEvents\": 1234,\n  \"thisWeekEvents\": 45,\n  \"todayEvents\": 5,\n  \"upcomingReminders\": 3,\n  \"deletedEventsInTrash\": 2,\n  \"storageUsed\": \"18.5 MB\"\n}"
    }
  ]
}
```

---

## 5. 提示接口设计 (Prompts API)

### 5.1 calendar-assistant - 日程管理助手

#### 接口描述
为 LLM 提供日程管理的安全指令模板。

#### 提示模板

```text
你是一个专业的日程管理助手，集成了 Desktop AI Calendar。

# 安全规则（CRITICAL）
1. **绝对禁止**在没有获取事件 `id` 的情况下进行更新或删除操作
2. 用户说"删除明天的会议"时，必须先调用 `search_events` 获取精确的 `id`
3. 如果 `search_events` 返回多个结果，要求用户澄清具体是哪一个
4. 所有删除都是软删除，7 天内可恢复
5. 执行破坏性操作前，必须向用户确认

# 工作流程
1. **理解用户意图**：创建/查询/更新/删除事件，或查询空闲时间
2. **搜索前置**：涉及特定事件时，先用 `search_events` 定位
3. **冲突检测**：创建事件时，若返回冲突警告，主动建议其他时间
4. **确认操作**：删除/更新前向用户确认，特别是重要事件
5. **提供反馈**：操作成功后，清晰说明结果

# 示例对话
用户："帮我删除明天下午3点的会议"
助手：
- 调用 `search_events(query="下午", start="2026-03-12T00:00:00+08:00", end="2026-03-12T23:59:59+08:00")`
- 找到 2 个结果：15:00 的"产品评审"和 16:00 的"客户会议"
- 询问用户："明天下午有 2 个会议，请问要删除哪一个？\n1. 15:00 产品评审\n2. 16:00 客户会议"

# 时间表达
- 支持自然语言："明天上午"、"下周三下午3点"、"3月15日全天"
- 自动转换为 ISO 8601 格式

当前日期时间：{current_datetime}
今日事件摘要：{today_summary}
```

---

## 6. 数据格式定义 (Data Schema)

### 6.1 Event 对象

```typescript
interface Event {
  id: number;                      // 事件 ID
  title: string;                   // 标题（1-200 字符）
  description?: string;            // 描述（可选，最多2000字符）
  startTime: string;               // ISO 8601 格式
  endTime?: string;                // ISO 8601 格式（可选）
  location?: string;               // 地点或链接（可选）
  priority: 0 | 1 | 2;            // 优先级：0=低，1=中，2=高
  priorityLabel?: "普通" | "中等" | "重要";  // 优先级标签（可选，用于UI显示）
  reminderOffset: number;          // 提前提醒分钟数
  isAllDay: boolean;               // 是否全天事件
  recurrenceRule?: string;         // 重复规则（RRULE）
  isLunar: boolean;                // 是否农历事件
  createdAt: string;               // 创建时间
  updatedAt: string;               // 更新时间
}
```

**Priority 类型说明**：

- **API层（TypeScript）**：使用字面量类型 `0 | 1 | 2`
- **数据库层**：存储为 `INTEGER`（值：0, 1, 2）
- **C# 代码层**：使用枚举 `Priority.Low | Priority.Medium | Priority.High`
- **对应关系**：
  - `0` = 低优先级/普通（`Priority.Low`）
  - `1` = 中等优先级（`Priority.Medium`）
  - `2` = 高优先级/重要（`Priority.High`）
- **可选字段** `priorityLabel`：提供中文标签，方便UI直接显示

**EF Core 值转换器配置**：
```csharp
// C# 侧实现
modelBuilder.Entity<Event>()
    .Property(e => e.Priority)
    .HasConversion(
        p => (int)p,           // enum -> int (写入数据库)
        p => (Priority)p       // int -> enum (从数据库读取)
    );
```

### 6.2 Error 对象

```typescript
interface ErrorResponse {
  success: false;
  error: {
    code: string;                  // 错误码
    message: string;               // 错误描述
    details?: Record<string, any>; // 额外详情
  };
}
```

### 6.3 Warning 对象

```typescript
interface Warning {
  type: "TIME_CONFLICT" | "NEAR_DUPLICATE" | "PAST_TIME";
  message: string;
  conflictingEventId?: number;
  conflictingTime?: string;
}
```

---

## 7. 错误码定义 (Error Codes)

### 7.1 通用错误码

| 错误码 | HTTP 状态 | 说明 |
|:---|:---|:---|
| `INVALID_REQUEST` | 400 | 请求参数格式错误 |
| `UNAUTHORIZED` | 401 | 未授权（MCP 未绑定 localhost） |
| `RATE_LIMIT_EXCEEDED` | 429 | 请求频率超限 |
| `INTERNAL_ERROR` | 500 | 服务器内部错误 |

### 7.2 事件相关错误码

| 错误码 | 说明 |
|:---|:---|
| `EVENT_NOT_FOUND` | 事件不存在 |
| `EVENT_ALREADY_DELETED` | 事件已被删除 |
| `INVALID_TITLE` | 标题为空或超长 |
| `INVALID_TIME_RANGE` | 时间范围无效 |
| `INVALID_PRIORITY` | 优先级无效 |
| `INVALID_RECURRENCE_RULE` | 重复规则格式错误 |
| `NO_CHANGES_PROVIDED` | 未提供修改内容 |

### 7.3 操作限制错误码

| 错误码 | 说明 |
|:---|:---|
| `CONFIRMATION_REQUIRED` | 需要确认（删除操作未传 confirm=true） |
| `MAX_EVENTS_EXCEEDED` | 单日事件数量超限（100 个） |
| `RESTORE_EXPIRED` | 超过恢复期限 |

---

## 8. UI 同步机制 (UI Synchronization)

### 8.1 设计原理

**核心问题**：当 MCP Server 接收 LLM 指令修改数据库后，如何通知 WPF UI 更新显示？

**解决方案**：采用应用层消息总线（`IMessenger`），实现发布-订阅模式。

```
数据流向：
1. LLM → MCP Server → EventService → SQLite Database (数据写入)
2. EventService → IMessenger.Publish (发布消息)
3. IMessenger → ViewModels (通知订阅者)
4. ViewModels → WPF UI (更新界面)
```

### 8.2 IMessenger 接口定义

```csharp
// Application/Messenger/IMessenger.cs
namespace AI_Calendar.Application.Messenger;

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
    EventCreated,        // 事件创建
    EventUpdated,        // 事件更新
    EventDeleted,        // 事件删除
    SettingsChanged,     // 设置变更
    RefreshRequested,    // 刷新请求
    PrivacyModeToggled,  // 隐私模式切换
    HolidayDataUpdated   // 节假日数据更新
}
```

### 8.3 MCP 工具集成示例

#### 8.3.1 create_event 工具实现

```csharp
// Infrastructure/MCP/Tools/CreateEventTool.cs
using AI_Calendar.Application.Messenger;
using AI_Calendar.Application.Services;

public class CreateEventTool : IMcpTool
{
    private readonly IEventService _eventService;
    private readonly IMessenger _messenger;

    public CreateEventTool(IEventService eventService, IMessenger messenger)
    {
        _eventService = eventService;
        _messenger = messenger;
    }

    public async Task<JsonNode> ExecuteAsync(JsonObject arguments)
    {
        // 1. 解析参数
        var dto = new EventDto
        {
            Title = arguments["title"].GetValue<string>(),
            StartTime = DateTime.Parse(arguments["startTime"].GetValue<string>()),
            // ...
        };

        // 2. 调用业务服务（自动写入数据库 + 发布消息）
        var created = await _eventService.CreateAsync(dto);

        // 3. EventService.CreateAsync 内部已调用：
        //    _messenger.Publish(AppEvent.EventCreated, created);

        // 4. 返回结果
        return new JsonObject
        {
            ["success"] = true,
            ["event"] = SerializeEvent(created)
        };
    }
}
```

#### 8.3.2 EventService 实现

```csharp
// Application/Services/EventService.cs
public class EventService : IEventService
{
    private readonly IEventRepository _repository;
    private readonly IOperationLogRepository _auditLog;
    private readonly IMessenger _messenger;

    public async Task<Event> CreateAsync(EventDto dto)
    {
        // 1. 创建事件（写入数据库）
        var created = await _repository.AddAsync(evt);

        // 2. 记录审计日志
        await _auditLog.AddAsync(new OperationLog
        {
            ToolName = "create_event",
            Params = JsonSerializer.Serialize(dto),
            Result = $"Success (ID: {created.Id})"
        });

        // 3. ✅ 关键：发布事件创建消息
        _messenger.Publish(AppEvent.EventCreated, created);

        return created;
    }

    public async Task<Event> UpdateAsync(int id, EventDto changes)
    {
        // ... 更新逻辑 ...

        // ✅ 发布更新消息
        _messenger.Publish(AppEvent.EventUpdated, updated);

        return updated;
    }

    public async Task SoftDeleteAsync(int id, bool confirm)
    {
        // ... 删除逻辑 ...

        // ✅ 发布删除消息
        _messenger.Publish(AppEvent.EventDeleted, id);
    }
}
```

### 8.4 ViewModel 订阅示例

```csharp
// Presentation/ViewModels/DesktopWidgetViewModel.cs
using AI_Calendar.Application.Messenger;

public partial class DesktopWidgetViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly IEventService _eventService;

    [ObservableProperty]
    private ObservableCollection<EventModel> _upcomingEvents = new();

    public DesktopWidgetViewModel(
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

### 8.5 依赖注入配置

```csharp
// Program.cs 或 App.xaml.cs
public void ConfigureServices(IServiceCollection services)
{
    // 注册消息总线（单例）
    services.AddSingleton<IMessenger, MessengerHub>();

    // 注册业务服务
    services.AddScoped<IEventService, EventService>();

    // 注册 ViewModels
    services.AddTransient<DesktopWidgetViewModel>();
    services.AddTransient<SettingsViewModel>();
}
```

### 8.6 完整调用流程示例

```
用户在 Claude 中输入："明天下午3点创建会议"

1. Claude 调用 MCP 工具：
   POST /mcp/tools/call
   {
     "name": "create_event",
     "arguments": { "title": "会议", "startTime": "2026-03-12T15:00:00+08:00" }
   }

2. MCP Server 处理：
   CreateEventTool.ExecuteAsync()
     ↓
   EventService.CreateAsync(dto)
     ↓ (1) 写入数据库
   AppDbContext.Events.Add(created)
     ↓ (2) 发布消息
   IMessenger.Publish(AppEvent.EventCreated, created)
     ↓ (3) 通知订阅者
   DesktopWidgetViewModel.OnEventCreated(created)
     ↓ (4) 更新 UI
   UpcomingEvents.Add(new EventModel(created))
     ↓
   WPF 界面实时显示新事件 ✅
```

### 8.7 优势说明

| 优势 | 说明 |
|:---|:---|
| **解耦性** | MCP Server 不需要知道 UI 的存在，只发布消息 |
| **可测试性** | 可以单元测试消息发布和订阅 |
| **可扩展性** | 新增订阅者（如日志记录、统计）无需修改发布者 |
| **类型安全** | 使用泛型保证消息类型安全 |
| **单进程架构** | 简化部署，无需跨进程通信 |

---

## 9. 安全机制 (Security)

### 8.1 访问控制

```csharp
// MCP Server 仅绑定 localhost
public class McpServer
{
    private const string AllowedHost = "127.0.0.1";

    public void Start()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:37281/");
        listener.Start();
    }
}
```

### 8.2 操作审计

所有工具调用均记录日志：

```sql
INSERT INTO OperationLogs (ToolName, Params, Result, Timestamp)
VALUES (
    'delete_event',
    '{"id": 44, "confirm": true}',
    'Success',
    '2026-03-11T10:35:00+08:00'
);
```

### 8.3 Prompt 注入防护

```csharp
public string SanitizeUserInput(string input)
{
    // 移除可能的 Prompt 注入
    return input
        .Replace("IGNORE PREVIOUS INSTRUCTIONS", "")
        .Replace("DISREGARD ALL ABOVE", "")
        .Substring(0, Math.Min(input.Length, 200));
}
```

---

## 10. 调用示例 (Usage Examples)

### 9.1 典型对话流程

#### 场景：创建会议

```
User: "明天下午3点帮我安排一个客户需求评审会议，时长1.5小时"

LLM 处理流程：
1. 解析时间：明天 = 2026-03-12，下午3点 = 15:00
2. 调用 get_free_time 检查冲突
3. 调用 create_event 创建事件
4. 返回确认信息
```

**MCP 调用序列**：

```json
// Step 1: 检查空闲时间
{
  "name": "get_free_time",
  "arguments": {
    "date": "2026-03-12",
    "duration": 90
  }
}

// Step 2: 创建事件
{
  "name": "create_event",
  "arguments": {
    "title": "客户需求评审会议",
    "startTime": "2026-03-12T15:00:00+08:00",
    "endTime": "2026-03-12T16:30:00+08:00",
    "priority": 1,
    "reminderOffset": 15
  }
}
```

#### 场景：删除事件

```
User: "把周五下午的健身约了取消了"

LLM 处理流程：
1. 调用 search_events 搜索"健身"
2. 返回多个结果，询问用户确认
3. 用户确认后，调用 delete_event（confirm=true）
4. 返回删除成功信息
```

**MCP 调用序列**：

```json
// Step 1: 搜索事件
{
  "name": "search_events",
  "arguments": {
    "query": "健身",
    "start": "2026-03-14T00:00:00+08:00",
    "end": "2026-03-14T23:59:59+08:00"
  }
}

// Step 2: 用户确认后删除
{
  "name": "delete_event",
  "arguments": {
    "id": 67,
    "confirm": true
  }
}
```

---

### 9.2 Claude Desktop 配置示例

```json
{
  "mcpServers": {
    "desktop-calendar": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Apps\\DesktopAICalendar\\AI_Calendar.csproj"
      ],
      "env": {
        "MCP_LOG_LEVEL": "debug",
        "CALENDAR_DB_PATH": "C:\\Users\\%USERNAME%\\AppData\\Roaming\\DesktopAICalendar\\data.db"
      }
    }
  }
}
```

---

## 11. 性能指标 (Performance)

| 指标 | 目标值 | 说明 |
|:---|:---|:---|
| **响应时间** | < 100ms | P50 延迟（不含 LLM） |
| **吞吐量** | > 100 req/s | 单实例并发处理能力 |
| **冷启动时间** | < 3s | 应用启动到可用 |
| **内存占用** | < 50MB | 空闲时内存 |

---

## 12. 版本管理 (Versioning)

### 11.1 API 版本策略

- **当前版本**：v1.0
- **版本号**：通过 URL 路径或 Header 传递
- **兼容性**：主版本号变更时，保留旧版本 6 个月

### 11.2 版本升级流程

```csharp
// 在 MCP 握手阶段声明版本
{
  "protocolVersion": "2024-11-05",
  "serverInfo": {
    "name": "desktop-calendar",
    "version": "1.0.0"
  },
  "capabilities": {
    "tools": {},
    "resources": {},
    "prompts": {}
  }
}
```

---

## 13. 测试接口 (Testing)

### 12.1 健康检查

```
GET http://localhost:37281/health
```

响应：
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "uptime": 12345
}
```

### 12.2 调试模式

```bash
# 启用调试日志
export MCP_LOG_LEVEL=debug
dotnet run

# 查看所有 MCP 通信
export MCP_TRACE=1
```

---

## 14. 附录：MCP Manifest 示例

完整的 MCP Server 声明文件（用于自动注册）：

```json
{
  "protocolVersion": "2024-11-05",
  "serverInfo": {
    "name": "desktop-calendar",
    "version": "1.0.0",
    "description": "AI-powered desktop calendar with MCP support"
  },
  "capabilities": {
    "tools": {
      "list_events": {
        "description": "获取当前所有有效的日历事件列表（默认排除软删除）",
        "inputSchema": {
          "type": "object",
          "properties": {
            "limit": { "type": "integer", "default": 50, "minimum": 1, "maximum": 200 },
            "start": { "type": "string", "format": "date-time" },
            "end": { "type": "string", "format": "date-time" },
            "includeDeleted": { "type": "boolean", "default": false }
          },
          "required": []
        }
      },
      "search_events": {
        "description": "搜索指定时间范围内的事件（默认排除已删除）",
        "inputSchema": {
          "type": "object",
          "properties": {
            "query": { "type": "string" },
            "start": { "type": "string", "format": "date-time" },
            "end": { "type": "string", "format": "date-time" },
            "limit": { "type": "integer", "default": 20 },
            "includeDeleted": { "type": "boolean", "default": false }
          },
          "required": ["start", "end"]
        }
      },
      "create_event": {
        "description": "创建新事件",
        "inputSchema": {
          "type": "object",
          "properties": {
            "title": { "type": "string", "minLength": 1, "maxLength": 200 },
            "startTime": { "type": "string", "format": "date-time" },
            "endTime": { "type": "string", "format": "date-time" },
            "location": { "type": "string" },
            "priority": { "type": "integer", "enum": [0, 1, 2] },
            "reminderOffset": { "type": "integer", "default": 15 }
          },
          "required": ["title", "startTime"]
        }
      },
      "update_event": {
        "description": "更新现有事件（必须提供 ID）",
        "inputSchema": {
          "type": "object",
          "properties": {
            "id": { "type": "integer" },
            "changes": { "type": "object" }
          },
          "required": ["id", "changes"]
        }
      },
      "delete_event": {
        "description": "软删除事件（需要 confirm=true）",
        "inputSchema": {
          "type": "object",
          "properties": {
            "id": { "type": "integer" },
            "confirm": { "type": "boolean" }
          },
          "required": ["id", "confirm"]
        }
      },
      "get_free_time": {
        "description": "查询可用时间段",
        "inputSchema": {
          "type": "object",
          "properties": {
            "date": { "type": "string", "format": "date" },
            "duration": { "type": "integer", "default": 60 },
            "workHoursOnly": { "type": "boolean", "default": true }
          },
          "required": ["date"]
        }
      },
      "restore_event": {
        "description": "恢复已删除的事件",
        "inputSchema": {
          "type": "object",
          "properties": {
            "id": { "type": "integer" }
          },
          "required": ["id"]
        }
      }
    },
    "resources": {
      "today://summary": {
        "description": "今日事件摘要",
        "mimeType": "application/json"
      },
      "stats://overview": {
        "description": "统计信息",
        "mimeType": "application/json"
      }
    },
    "prompts": {
      "calendar-assistant": {
        "description": "日程管理助手提示模板",
        "arguments": []
      }
    }
  }
}
```

---

## 15. 版本历史 (Version History)

### V1.4 (2026-03-11)

**传输方式描述优化**：
- 统一传输方式描述："Stdio（开发环境）或 HTTP/SSE（生产环境）"
- 添加传输方式选择说明（适用场景、实现方式）
- 更新传输架构图：Stdio / HTTP (SSE)
- 明确监听地址：`http://localhost:37281`

**决策依据**：
- 参考文档：文档冲突分析（2026-03-11）
- 与PRD.md V1.4、System-Architecture.md V1.5保持一致

### V1.3 (2026-03-11)

**Priority类型优化**：
- 明确说明使用C#枚举 + EF Core值转换器设计
- API层使用字面量类型 `0 | 1 | 2`
- 数据库层存储为INTEGER
- 添加EF Core值转换器配置示例

**Description字段确认**：
- 确认Event对象包含description字段（可选，最多2000字符）
- 与Database-Design.md V1.2保持一致

**MCP工具清单确认**：
- 明确为7个工具接口
- 包含list_events和restore_event
- 移除MCP-01服务宿主（非工具）

**决策依据**：
- 与Database-Design.md V1.2、Detailed-Design.md V1.1保持一致

### V1.2 (2026-03-11)

**MCP功能增强**：
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

---

**文档结束**

> 本文档定义了 Desktop AI Calendar 的完整 MCP 接口规范，包括 7 个工具接口、2 个资源接口和 1 个提示接口。所有接口均遵循 MCP 协议标准，支持与 Claude、GPT-4 等大模型无缝集成。
