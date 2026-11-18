# 统一 Agent 能力接口方案（供讨论）

- 作者：后端工程师
- 日期：2026-11-18（Asia/Shanghai）
- 状态：已与 A / 前端方案对齐，按此实现；未提交
- 对照：`Plugins/AIAgent/docs/chat-page-redesign/01-requirements.md`（含 F27、F28、I13）、`03-frontend-component-scheme.md`
- 对齐结论（2026-11-18）：入口 `/api/agent`；发送回 202，页面用 `GET .../events?after=seq` 订阅；`surface` 为 page/panel；会话按 `pluginId` + `surfaceKey` 隔离；`context` 用前端已定字段；`toolScope` 只能收缩；`insertText` 只写回宿主编辑面，不走执行通道

## 1. 结论

新建一套会话级入口 `/api/agent`，作为本页和后续数据库、智能终端等插件调用 Agent 的唯一 HTTP 面。旧的 `/api/ai-agent/chat`、`/api/ai-agent/runs`、`/api/ai-agent/workflow` 本轮冻结，新页面和新嵌入面板都不要再调。

运行时仍复用现有 Agent 注册表、计划编排、审批闸门和工作流服务，不另起第二套循环。`IChatCompletion` 只保留给即时通讯等「只要补全、不要执行过程」的调用，不做本页入口。

## 2. 现有接口为何不够

三套面、三种事件、对不上这版页面：

| 现有入口 | 能做什么 | 缺口 |
|----------|----------|------|
| `POST /api/ai-agent/chat/stream` | 开放对话 SSE：`content` / `tool_call` / `tool_result` / `done` | 无计划、无审批事件、无会话状态（待确认/待审批/卡住）、附加上下文不完整 |
| `POST /api/ai-agent/runs` | 计划驱动 SSE：规划后立刻执行 | 没有「计划待确认」状态；确认前不应开跑（F05） |
| `GET/POST /api/ai-agent/workflow` | 规划/执行/轮询状态 | 不是同一条事件流，面板无法持续跟随 |
| `GET/POST /api/ai-agent/approvals` | 列表 + 决定 | 不推送到对话流，主区审批卡片接不上 |
| `IChatCompletion` | 跨插件文本补全 | 无执行过程、无计划/审批/介入，不能作为助手面板入口 |

另外，流绑在「发送」这一次 HTTP 请求上。刷新、改计划后再执行、中途审批、插件侧栏同时观察，都需要按会话订阅，而不是把流挂在发送请求上。

## 3. 设计原则

1. **一个会话、一条事件流、一组动作。** 开放任务和计划任务共用会话与事件，用 `mode` 区分。
2. **发送与订阅分离。** `POST` 只投递动作并返回 `202`；前端（整页或右侧面板）用 `GET .../events` 订阅并断线续传。
3. **嵌入预留。** 请求携带 `surface`、`pluginId`、`context`、`toolScope`。本轮页面与嵌入面板走同一套接口，插件不要各自再做聊天 API。
4. **人在回路不过旁路。** 计划确认、高风险审批、卡住介入、停止/重试都是会话动作，结果只通过事件回推。智能体不能自己批准。
5. **面板写回必须显式。** 记忆写回、设计入库存、待办下发都走明确 POST，不得静默覆盖。

## 4. HTTP 面

前缀：`/api/agent`（由智能体插件提供，路径不绑死在聊天页路由上，方便插件复用）。

### 4.1 会话

- `POST /api/agent/sessions`  
  `{ mode: "open"|"planned", agentId, modelId, title?, embed? }`  
  `embed.context.pluginId` 必填；`surfaceKey` 缺省 `"default"`。会话归属该 `pluginId` + `surfaceKey`，之后列表与订阅都按这对键隔离。
- `GET /api/agent/sessions?pluginId={id}&surfaceKey={key}`  
  **必须按 `pluginId` + `surfaceKey` 过滤**，只返回该嵌入槽位的会话，避免整页与某插件侧栏、或两个终端标签抢同一份列表。`pluginId` 必填；`surfaceKey` 缺省按 `"default"`。  
  列表项：id、标题、模式、状态（`idle` / `running` / `awaiting_plan` / `awaiting_approval` / `stuck` / `completed` / `failed` / `cancelled`）、`pluginId`、`surfaceKey`、更新时间。不返回其它槽位的会话。
- `GET /api/agent/sessions/{id}`  
  消息、当前 run、工作区摘要（11 个面板的状态，供右侧入口）
- `PATCH /api/agent/sessions/{id}`  
  切换模式 / 智能体 / 模型 / 标题（进行中不可切模式）
- `DELETE /api/agent/sessions/{id}`

### 4.2 下任务

`POST /api/agent/sessions/{id}/messages`

```json
{
  "text": "……",
  "attachments": [
    { "kind": "skill|prompt|memory|mcp|project|image", "id": "…", "name": "…" }
  ],
  "embed": {
    "surface": "page|panel",
    "context": {
      "pluginId": "ai-agent",
      "surfaceKey": "default",
      "projectId": null,
      "cwd": null,
      "objectType": null,
      "objectId": null,
      "title": null,
      "extras": {}
    },
    "toolScope": {
      "allowTools": [],
      "denyTools": [],
      "allowCapabilities": []
    }
  }
}
```

返回 `202 { sessionId, runId }`。计划模式只规划，发 `plan.proposed` 后进入 `awaiting_plan`，确认前不执行。开放模式直接进入 `running`。

`embed.context` 字段与前端 `AgentEmbedContext` 对齐：`pluginId`（必填）、`surfaceKey`（缺省 `default`）、`projectId`、`cwd`、`objectType`、`objectId`、`title`、`extras`。`extras` 仅供展示与提示注入，不扩大工具权限。未知字段忽略，不报错。`context` 随每次发起任务提交；进行中的 run 不因 props 变化而暗改已经发出的上下文。

`toolScope` 与前端 `AgentToolScope` 对齐：`allowTools`、`denyTools`、`allowCapabilities`。服务端取该智能体白名单与本次 scope 的交集，只能收缩，不能放宽；`denyTools` 优先。未授权能力不出现在工具面板数据里。

### 4.3 事件订阅（实时）

`GET /api/agent/sessions/{id}/events?after={seq}`

`text/event-stream`，camelCase。每条统一信封：

```json
{
  "type": "session.status|message.delta|message.completed|tool.started|tool.completed|plan.proposed|plan.updated|step.started|step.progress|step.completed|step.failed|approval.required|approval.resolved|workflow.updated|workspace.updated|run.stuck|run.failed|run.completed|run.cancelled|error",
  "sessionId": "…",
  "runId": "…",
  "seq": 12,
  "ts": "2026-11-18T01:10:00+08:00",
  "payload": { }
}
```

- `message.delta`：文本增量  
- `tool.started` / `tool.completed`：名称 + 摘要；细节按需展开，默认不推原始大结构  
- `plan.proposed`：目标、步骤、约束  
- `step.*`：步骤状态与轮次（如 `3/20`）  
- `approval.required`：风险说明、将执行的动作、审批 id（对话主区卡片用）  
- `run.stuck`：原因 + `allowedActions: ["continue","skip","cancel"]`  
- `workspace.updated`：右侧 11 项入口状态（无记录 / 待确认 / 进行中 · 3/20 / 待审批 / 卡住 等）  
- 用 `after=seq` 续传，刷新或面板重开可补齐

本轮不用新的 SignalR Hub。

### 4.4 介入动作

都是 `POST`，成功后只改状态并通过事件流回推：

| 路径 | 作用 |
|------|------|
| `/sessions/{id}/plan/confirm` | 确认并执行（F05） |
| `/sessions/{id}/plan/revise` | 改计划（步骤或一段说明） |
| `/sessions/{id}/approvals/{approvalId}/decide` | `{ approved, note }`；未批准不得执行（F06） |
| `/sessions/{id}/runs/{runId}/stop` | 停止，之后不再调工具（F04 / F26） |
| `/sessions/{id}/runs/{runId}/intervene` | `{ action: "continue"|"skip"|"override"|"retry"|"cancel", input?, stepId? }`（F07 / F08） |
| `/sessions/{id}/workflows/{workflowId}/pause` | 暂停工作流 |
| `/sessions/{id}/workflows/{workflowId}/cancel` | 取消工作流；高风险步骤仍走 `approval.required`（F14） |

`continue`：补充信息后继续。`skip`：跳过当前步。`override`：人工补位产出。`retry`：失败重试。`cancel`：取消本轮。

### 4.5 工作区面板

- `GET /api/agent/sessions/{id}/workspace?panel=plan|records|workflow|memory|design|suggest|project|todo|skills|tools|snippets`  
  不传 `panel` 时只返回 11 项入口状态。
- 显式写回（均需确认，不得静默）：  
  - `POST .../workspace/memory/writeback`  
  - `POST .../workspace/design/commit`  
  - `POST .../workspace/todos/{todoId}/dispatch`  
  - `POST .../workspace/snippets`

面板只读展示（技能启停、工具执行）不走这里；高风险命令仍在对话里审批。

### 4.6 目录

- `GET /api/agent/catalog/models`：宿主已启用的对话模型，不引导另开账号（F25）  
- `GET /api/agent/catalog/agents`：可选智能体

## 5. 嵌入契约（给前端与插件）

同一套会话与事件，两种表面：

| 字段 | 说明 |
|------|------|
| `surface` | `page` 整页三栏；`panel` 收成右侧助手面板 |
| `pluginId` | 宿主插件 id（`ai-agent` / `db-admin` / `smart-terminal` 等） |
| `surfaceKey` | 同一插件多个槽位的隔离键，例如终端标签 id；缺省 `default`。会话列表与归属都按 `pluginId` + `surfaceKey` 过滤 |
| `context` | 见第 4.2 节。随任务提交，不暗改进行中的 run |
| `toolScope` | `allowTools` / `denyTools` / `allowCapabilities`，只能收缩 |

前端用同一组件，靠 `layout=page|panel` 切换，不按插件各写一套。本轮先在智能体页落地；数据库、智能终端只预留字段与调用方式，不在本轮改那两个插件的聊天实现。

## 6. insertText（不是执行通道）

`insertText` 是前端组件向宿主编辑面（例如终端输入区、SQL 编辑器）发出的写回事件，只把文本插入光标处，不得自动执行。它不经过 `/api/agent` 的执行、审批或工具通道，与「批准后执行」不是同一条路。

本接口不提供 `insertText` 执行端点。若事件或代码片段里带有可插入文本，由前端自行 `emit('insertText')` 写回宿主。真要跑一条高风险命令，必须走对话里的 `approval.required` 与 `/approvals/{id}/decide`。

## 7. 本轮实现边界


做：

- 新控制器与会话/事件/动作/工作区读模型
- 计划模式增加 `awaiting_plan`，确认前不执行
- 把现有开放循环、计划编排、审批、工作流进度接到同一事件总线
- 停止、审批、卡住介入（继续 / 跳过 / 取消）、失败重试
- 工作区入口状态 + 计划 / 执行记录 / 工作流等面板读取
- 单测覆盖事件类型、确认前门闩、审批拒绝不执行、介入动作

不做：

- 改旧聊天页或旧接口行为（保持可用，但不给新页使用）
- 重写运行时
- 把即时通讯外部会话并进本页
- 本轮改数据库 / 智能终端插件去接面板（只预留契约）

## 8. 已对齐项与残留

已对齐，按此实现：

1. 路径用 `/api/agent`。
2. 发送与订阅分离，重连用 `after=seq`。
3. `context` 字段按前端方案落地；会话按 `pluginId` + `surfaceKey` 隔离，列表必须带这对过滤条件。
4. `toolScope` 只能收缩；`insertText` 只写回宿主编辑面。

本轮仍不改数据库 / 智能终端页面的实际挂载，只把契约与接口落地。
