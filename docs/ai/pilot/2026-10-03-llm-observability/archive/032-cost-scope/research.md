# 032 · CostScope — 调研（Research）

> 阶段：立项第 1 步（`plugin-feasibility-study` 5 步流程）
> Task ID：PILOT-032 ｜ 日期：2026-09-28 ｜ 调研方式：只读仓内 grep/read，无网络
> 约束：每条结论附 `[文件:行号]` 证据；无证据写 Unknown，禁止推测。

---

## 0. 调研问题

「为当前项目创造最合适的插件」——即在 18 个既有插件之外，找一个**真实空缺**：
用户有数据、有意愿，但产品缺一个能回答某类问题的界面；且该空缺不该被宿主内置功能覆盖。

评估三条候选路径：① AI 用量成本可视化 ② 安全审计/操作留痕 ③ 插件/宿主自身健康诊断。

---

## 1. 现状：宿主已有什么可复用的数据底座

### 1.1 `ChatTurn` —— 逐轮 AI 遥测已完整持久化（关键发现）

`ForgeSelf.Api/Entities/ChatTurn.cs:27` `[BindTable("ChatTurn", Description = "聊天轮次", ConnName = "ForgeSelf")]`。
字段清单（`ForgeSelf.Api/Entities/Models/ChatTurnModel.cs`，全部 Verified）：

| 维度 | 字段 | 行号 |
| --- | --- | --- |
| 归属 | `ChatSessionId` / `SessionKey` | `:16,:24` |
| 模型 | `Model` / `Style` | `:32,:29` |
| 用量 | `PromptTokens` / `CompletionTokens` / `TotalTokens` | `:66,:69,:72` |
| 性能 | `DurationMs` / `FirstTokenMs` | `:96,:75` |
| 行为 | `Temperature` / `MaxTokens` / `MessageCount` / `ToolCallCount` / `HasReasoning` | `:81,:84,:88,:91,:93` |
| 可靠性 | `ResponseStatus` / `ErrorMessage` | `:45,:78` |
| 时点 | `CreatedTime` | `:99` |

写入路径已打通：`Services/ChatSessionService.cs:142-143` `session.TotalPromptTokens += promptTokens; session.TotalCompletionTokens += completionTokens;`；
四个统一网关控制器都在埋点：`OpenAIChatController.cs:136-141,263-300`、`OpenAIResponsesController.cs:105-110,244-249`、`AnthropicMessagesController.cs:105-110,234-239` —— 全部调用 `RecordTurnStatsAsync(...)`。

> **判定**：做「AI 用量/成本看板」所需的**原始遥测数据已经在线上持续落库**，不需要新增埋点、不需要改网关控制器。这是三条候选路径里唯一「数据底座已就位」的一条。

### 1.2 `UsageRecord` —— 插件工具用量已全量埋点

`ForgeSelf.Abstractions/ToolFunctionUsageReportingExtensions.cs:18` 提供共享扩展 `RecordUsageAsync`，
各插件工具函数统一复用（例：`Plugins/TextTools/TextToolsPlugin.cs:181,261,341,448,530`）。
落表 `ForgeSelf.Api/Entities/UsageRecord.cs:25`，字段见 `Entities/Models/UsageRecordModel.cs`：
`PluginId:18` / `ToolId:21` / `ActionType:24` / `UserAgent:27` / `IpAddress:30` / `DurationMs:33` / `Timestamp:36` / `MetadataJson:39`。

> **判定**：工具调用维度同样已有持久化数据，且天然含 `PluginId`（可跨插件聚合）。

### 1.3 `ChatSession` —— 会话级累计

`Entities/ChatSession.cs:121-130` 有 `TotalPromptTokens` / `TotalCompletionTokens`（int64 累计）。
可做会话粒度的用量对比，但无成本、无按日聚合。

---

## 2. 空缺判定 ①：成本/预算 —— 全仓零概念

```
grep -r "单价|定价|pricing|Price|Budget|预算|成本"  ForgeSelf.Api/
→ No matches found
```

- 无单价表、无定价配置、无成本计算公式、无预算字段、无告警。
- `UsageRecord` **无 token 字段**（`UsageRecordModel.cs:14-46` 全文无 token/费用列）。
- `UsageStatsController` 16 个端点全部是**次数/耗时/趋势**语义，无一个返回金额：
  `records:46` / `summary/daily:79` / `tools/top:109` / `trend:137` / `tools/ranking:163` /
  `record:189` / `workflows/*:231-390` / `personal-library:397` / `growth-curve:421` /
  `time-saved:445` / `recommendations/*:467,500`。

> **判定：真实空缺（Verified）。** 用户目前**无法回答任何一句成本问题**：
> 「今天花了多少钱」「哪个模型最贵」「上月某供应商占比多少」「这个会话烧了多少」。
> 唯一近似物是 `time-saved`（节省时间估算），那是次数折算时间，**不是钱**。

---

## 3. 空缺判定 ②：审计/操作留痕 —— 无基础设施，但已有部分留痕

| 项 | 现状 | 证据 |
| --- | --- | --- |
| 请求级审计中间件 | **无** | grep `UseRequest|RequestLogging|Middleware|audit` 在 `ForgeSelf.Api` 仅命中 `PluginFrontendFileMiddleware`（静态资源） |
| 插件审计 | **无** | grep `audit` 在 `Plugins/` 零命中 |
| 工具调用留痕 | **已有**（`UsageRecord` + `ChatTurn.ToolCallCount/RequestBody/ResponseBody`） | `ToolFunctionUsageReportingExtensions.cs:18`；`ChatTurnModel.cs:43,:52,:91` |
| API 密钥使用留痕 | **无** | `ApiKeyCredential.cs:27` 仅存凭据（名称/哈希/启用态），无使用记录字段；`docs/00-vision/02-goals.md:20` 明文将「密钥来源审计」列中期未完成目标 |
| IM 网关 / 脚本 / 定时任务执行历史 | 无统一审计表 | `sems` 有 `RunSession`（进程态，不落持久审计）、Scheduler/ScriptRunner 各存各的 |
| 已登记决策 | `not-taken-decisions.md` 无审计类不做决策（即未被否定，仍开放） | 全文 137 行无审计条目 |

> **判定：部分空缺。** 审计的**原始事件源已经存在**（UsageRecord + ChatTurn + SessionEventEntity），
> 但它们分散在不同表/不同语义（工具用量 vs 聊天轮次 vs 会话事件），且**没有任何聚合查询面**。
> 做「审计插件」实质是把这些现有表拼成一张可查询的流水——可行，但它回答的是「发生了什么」，
> 用户提问频度低于「花了多少」（见 §6 排序）。

---

## 4. 空缺判定 ③：孤儿后端 API —— 确认存在，且与安全缺口叠加

`UsageStatsController`（`[Route("api/usagestats")]` `:13`）共 16 端点，
前端 `ForgeSelf.Web/src/services/usageStatsApi.ts` 只消费其中 5 个（`:15,:34,:45,:68,:81`）：
`personal-library` / `growth-curve` / `time-saved` / `recommendations/contextual` / `recommendations/save-suggestion`。

**11 个零前端消费者的端点**（Verified：上述 service 全文仅此 5 个 URL 字符串）：

| 端点 | 行号 |
| --- | --- |
| `GET /api/usagestats/records` | `:46` |
| `GET /api/usagestats/summary/daily` | `:79` |
| `GET /api/usagestats/tools/top` | `:109` |
| `GET /api/usagestats/trend` | `:137` |
| `GET /api/usagestats/tools/ranking` | `:163` |
| `POST /api/usagestats/record` | `:189` |
| `GET /api/usagestats/workflows/stats` | `:231` |
| `GET /api/usagestats/workflows/{id}/stats` | `:258` |
| `GET /api/usagestats/workflows/popular` | `:290` |
| `GET /api/usagestats/workflows/trend` | `:344` |
| `GET /api/usagestats/workflows/tools-ranking` | `:371` |

同时，`UsageStatsController` **无 `[Authorize]` 特性**（对比 `AIProviderController.cs:21`、`ApiKeysController.cs:22`、`PluginController.cs:17`、`UpdateController.cs:15` 均有 `[Authorize("ApiKeyPolicy")]`）。
违反项目铁律 17（管理/CRUD/配置控制器必须类级鉴权 → mcp-center v2.1.0 踩坑实录）。
该控制器含 `POST record`（写入口）与 `workflows/*`（可推断工作流使用模式）→ 属「管理面」。

> **判定**：孤儿 API + 缺鉴权 = 一个已存在的安全/可观测性缺口。但它是**宿主控制器的修补题**，
> 不构成独立插件的立项理由（插件不能给自己之外的宿主控制器加特性）。
> 记为 TODO（P2），不由本插件解决。

---

## 5. 既有文档与插件矩阵（确认无重名/无冲突）

18 个插件 `plugin.json` 全量核对（`Plugins/*/plugin.json`）：
`ai-agent 1.7.2` / `agent-hub 1.0.9` / `dev-tools 1.0.0` / `design-system 1.2.1` / `file-tools 1.0.0` /
`home 1.0.0` / `im-gateway 2.0.0` / `mcp-center 2.1.1` / `memory-system 1.0.0` / `quick-links 1.0.1` /
`sample 1.0.0` / `scheduler 1.0.0` / `script-runner 1.0.1` / `sems 1.0.3` / `system-monitor 1.0.0` /
`text-tools 1.0.0` / `todo-tracker 1.0.0` / `workflow-engine 1.0.0`。

- 功能文档 001–038（`docs/02-features/`）编号 029 空缺、033 空缺；032 已被 `032-agent-hub.md` 占用 →
  **新插件功能文档应取 039**（见 design §11）。
- 无插件与「用量/成本/仪表盘/观测」语义重名。`system-monitor` 是**主机资源**监控（CPU/内存/磁盘/网络，
  `docs/02-features/015-system-monitor.md:26-29`），与 AI 用量成本无交集。
- `docs/02-features/024-usage-stats.md:8` 声称「**无独立 REST 控制器**」→ **文档已过期**（实际控制制器存在于
  `ForgeSelf.Api/Controllers/UsageStatsController.cs:12-14`）。记为 TODO（P3 文档反同步）。

---

## 6. 关键判断（可直接读出的结论）

1. **用户是否能回答「今天花了多少钱」？** ❌ 不能。无任何金额维度（§2）。
2. **用户是否能回答「某模型单价多少 / 某供应商本月占比多少」？** ❌ 不能。同上。
3. **数据是否已经在采集？** ✅ 已经在采集，且无需新增埋点。
   `ChatTurn` 逐轮 tokens/model/duration/firstTokenMs（§1.1）+ `UsageRecord` 工具调用（§1.2）。
4. **有没有被否定的「不做」决策挡住这条路？** ❌ 没有。相反，
   `docs/07-decisions/not-taken-decisions.md:89`（006.3）明文：
   「重新审视的触发条件：**复盘需要逐 token 追查（如成本审计、幻觉溯源）时**…」——
   「成本审计」被仓库自己点名为未来触发条件。
5. **有没有未完成的产品目标指向它？** ✅ `docs/00-vision/02-goals.md:19-21` 中期目标含
   「密钥管理增强」**与**「文档治理」，均未包含用量成本——说明它是**未被规划过的真空**，
   不是被搁置的旧任务。
6. **插件能否读到这些数据？** 可行（详见 feasibility §2）。宿主库在数据根
   `ForgeSelf.Api/Data/XCodeConfig.cs:15-16` `HostDbs = { "ForgeSelf" }` → `{数据根}/ForgeSelf.db`，
   连接串注册 `:88` `DAL.AddConnStr(name, connStr, ...)`；插件可通过
   `ctx.EnsurePluginDataDirectory()` 的上层「数据根」解析出同一文件并只读打开。

---

## 7. 候选方案对比（事实层，不作推荐）

| 维度 | ① AI 用量成本可视化 | ② 安全审计流水 | ③ 插件健康诊断 |
| --- | --- | --- | --- |
| 数据底座 | ✅ 已齐全且持续写入（§1.1） | ⚠️ 事件源分散在 3 张表（§3） | ⚠️ 需新增探测逻辑 |
| 缺失程度 | 全仓零概念（§2 grep 零命中） | 部分缺失（有留痕无查询面） | 无既有实现 |
| 仓库自证需求 | 006.3 点名「成本审计」为触发条件 | 02-goals:20「密钥来源审计」列中期目标 | 无文档提及 |
| 改动宿主契约风险 | 低（纯读 + 新增表） | 中（若引入统一审计需动多处） | 中 |
| 是否已被 18 插件覆盖 | 否 | 否 | `dev-tools` 部分重叠 |
| 孤儿 API 红利 | ✅ 复用 11 个现有端点 | 无 | 无 |

---

## 8. TODO 派生（本调研发现、但**不归本插件解决**）

- **P2**：`UsageStatsController` 补类级 `[Authorize("ApiKeyPolicy")]`（铁律 17 违反，含 `POST record` 写入）。
- **P2**：`UsageStatsController` 11 个孤儿端点在界面无消费者——由 CostScope 消费其中
  `records` / `summary/daily` / `tools/top` / `tools/ranking` / `trend` 五个；`workflows/*` 六个仍无消费者。
- **P3**：`docs/02-features/024-usage-stats.md:8`「无独立 REST 控制器」表述已过期，需反同步。
- **P3**：`docs/00-vision/02-goals.md:20`「密钥来源审计」——若后续单独立项，与本插件的
  `ApiKeyCredential` 维度可复用数据源。
