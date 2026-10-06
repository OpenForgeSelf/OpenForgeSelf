# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：PILOT-033 ｜ 日期：2026-10-03 ｜ 证据等级标注见每节

## 项目结构

仓库根 `D:/src/my-proj/OpenForgeSelf/OpenForgeSelf`（.NET 10 + Vue 3 单体 + 插件体系）。

| 路径 | 角色 |
| --- | --- |
| `ForgeSelf.slnx` | 解决方案（.slnx 新格式） |
| `ForgeSelf.Api/` | ASP.NET Core 宿主后端（实体、服务、控制器） |
| `ForgeSelf.Abstractions/` | 跨插件共享契约层（`ILlmRuntime`/`IToolRegistry`/`SessionEvents` 等） |
| `ForgeSelf.Api.Tests/` | 后端 xUnit 测试 |
| `ForgeSelf.Web/` | Vue 3 + Vite + TS 前端（pnpm） |
| `Plugins/` | 插件目录（`AIAgent`/`Home`/`SystemMonitor`/`DesignSystem` 等，各带 `plugin.json`） |
| `dsh-ui-bundle/` | DSH（DeepSeek Harness）overlay bundle，含 `my-dsh-activity-timeline-client` |
| `docs/ai/pilot/` | 本规范工件链落位（`YYYY-MM-DD-<task-id>/`） |
| `docs/04-standards/` | 工程规范真源（`ai-native-engineering-workflow.md` 等） |
| `specs/` | **已弃用**（AGENTS.md §9），且被 `.gitignore` 忽略；但 032 等历史工件仍留在其中 |

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10 + ASP.NET Core | `ForgeSelf.Api/*.csproj`、`Program.cs`、`AppBuilder.cs` |
| ORM | **NewLife.XCode（唯一 ORM）** | 全部实体继承 `Entity<T>` / `IEntity<T>`，用 `[BindColumn]`/`[BindIndex]` 特性 |
| 数据库 | SQLite（XCode.Sqlite 正式依赖） | `TODO.md` 输入38 记载已装 `XCode.SQLite` |
| 日志 | NewLife `XTrace` | 全仓 `XTrace.Log.Info/Debug/Error` |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia | `ForgeSelf.Web/package.json` |
| 包管理 | **pnpm**（非 npm），Node ≥ 20 | `AGENTS.md` §2.3 |
| 样式 | 仅 Element Plus `--el-*` + `color-mix()`，**0 自定义 token** | user-level skill `frontend-zero-custom-token` |
| 端口 | Backend `:7102` / Frontend `:7002`（默认回落）；发布实例 `:51888` | `AGENTS.md` §2.3；可用 `FORGESELF_PORT` / `--server-port` 覆盖（PILOT-050） |

## 架构特点

**三层心智模型**：Intent / Orchestration / Capability，对应 MAF 的 Agent / Workflow / Tool 层。协作流「架构师分解 → 工程师实现 → QA 验证」。

**插件机制**：`Plugins/<Name>/` + `plugin.json` 清单注册，插件自带 `Data/Model.xml`（XCode 真源）、`web/` 前端。铁律：插件自建表只写插件自有库（铁律 12），对宿主库只读（铁律 10）；管理面控制器必须类级 `[Authorize]`（铁律 17）。

**数据落盘方式**：NewLife.XCode 实体 → SQLite。真相源唯一原则：`SessionEventEntity`（append-only 事件日志）为会话真相源，模型历史/UI/统计一律从它派生。

**LLM 代理链**：`OpenAICompatibleProvider` 统一代理多家供应商（OpenAI/Anthropic 等），对外暴露 OpenAI 兼容端点 `Controllers/UnifiedAI/`（`OpenAIChatController`/`OpenAIResponsesController`/`AnthropicMessagesController`）。

**能力接缝（已存在，Important）**：
- `ToolRegistry`（`ForgeSelf.Api/Services/ToolRegistry.cs`）：六闸门执行管线，`FinalizeAsync` 写使用统计、`EmitAsync("tools/result")` 广播。
- `WebSocketBroadcaster`（`ForgeSelf.Api/Services/WebSocketBroadcaster.cs`）：维护所有 `/ws` 连接，信封 `{ type, data }`。
- `dsh-ui-bundle` Slot 框架：插件面板经 `ctx.inject` + `slots.register` 注入（D 对齐线）。

## 测试方式

| 层 | 入口 | 目录 |
| --- | --- | --- |
| 后端单测/集成 | `dotnet test` | `ForgeSelf.Api.Tests/`（Unit + Integration） |
| 前端单测 | `pnpm run test`（vitest） | `ForgeSelf.Web/src/**/*.test.ts` |
| 前端类型检查 | `pnpm run check`（vue-tsc） | `ForgeSelf.Web/` |
| e2e | Playwright（**禁 mock、真实登录**） | `ForgeSelf.Web/e2e/`、`e2e/plugins/<id>/` |
| 插件层 e2e | 同上，走 e2e 隔离实例 | `e2e/helpers/e2e-env.ts`（动态端口） |

**红线**：禁止手写一次性 `temp/*.cjs` 作为验证手段（AGENTS.md 红线）；结论必须沉淀为 §5.0 可重复测试。

## 构建命令

```bash
# 后端
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test

# 前端
cd ForgeSelf.Web && pnpm run check && pnpm run test

# 插件前端（以 DesignSystem 为例）
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/Entities/` | XCode 实体（`ChatTurn`/`ChatSession`/`SessionEventEntity`/`UsageRecord`/`AIModel`/`AIProvider`） |
| `ForgeSelf.Api/Services/` | 业务服务（`ChatTurnStreamRecorder`/`ChatTurnService`/`WebSocketBroadcaster`/`ToolRegistry`/`AI/Providers/`） |
| `ForgeSelf.Api/Controllers/` | HTTP 端点（`ChatRecordsController`/`UsageStatsController`/`UnifiedAI/*`） |
| `ForgeSelf.Web/src/views/` | 页面（`ChatView`/`ChatRecordsView`/`SystemMonitorView`） |
| `ForgeSelf.Web/src/components/chatrecords/` | 聊天记录详情 UI（`ChatRecordDetail.vue`） |
| `ForgeSelf.Web/src/data/features.ts` | **前端功能 SSOT**（CI 双向校验 `scripts/check-features.mjs`） |
| `Plugins/AIAgent/Data/Entities/` | `AgentRun`/`AgentStepRun`（多步 Agent 执行轨迹） |

## 代码组织方式

后端按 `Controllers/Services/Entities` 分层，实体用 XCode 特性生成 `.cs`（`.Biz.cs` 放扩展查询）。插件用 `plugin.json` + `EntryAssembly` + `EntryType` 注册。前端 `src/services/*Api.ts` 服务层 + Pinia store + `data/features.ts` SSOT。

## 现有工程规范

对本任务有约束力的条目：

1. **AGENTS.md §0 预飞铁律**：写日记 → 建 TODO → 读规范 → 查技能 → Context/Plan/Execute/Verify → 出口清单。
2. **AGENTS.md §11 + `docs/04-standards/ai-native-engineering-workflow.md` v1.1.0**：九阶段 + 三道闸门，**开发任务唯一流程依据**；工件落 `docs/ai/pilot/YYYY-MM-DD-<task-id>/`。
3. **规范 §1 硬性约束**：不改生产环境、不改 DB 结构（迁移须升级审批）、不改鉴权/权限/支付/安全核心逻辑、不新增大规模依赖、不无关重构、不改无关文件、不为展示 Agent 能力扩范围、必须能跑真实测试验证、结论基于真实仓库、验证失败不伪造。
4. **`docs/04-standards/agent-workflow.md` Part B**：项目不变工程规则。
5. **提交铁律**：绝对禁止自动 `git commit`/`push`，须用户显式授权。

---

## 现状盘点：LLM 可观测性能力（Verified，本回合实测）

> 这是本任务的事实基座。**每条都经本回合亲自读码/grep 确认**，非推测。

### 已有（强项，已对齐行业标杆）

| 能力 | 落点 | 实测依据 |
| --- | --- | --- |
| 请求/响应全字段落库 | `ForgeSelf.Api/Entities/ChatTurn.cs` | 逐字段读码确认：`RequestBody`/`ResponseBody`/`RequestHeaders`/`ResponseHeaders`/`ResponseStatus`/`Model`/`Style`/`Temperature`/`MaxTokens`/`MessageCount`/`ToolCallCount`/`HasReasoning`/`ErrorMessage` |
| Token 三件套 + 延迟 | 同上 | `PromptTokens`/`CompletionTokens`/`TotalTokens`（:167-189）、`FirstTokenMs`（:191）、`DurationMs`（:247） |
| 流式节流落库 | `Services/ChatTurnStreamRecorder.cs` | 满 1000 字符或 500ms 节流；`RequestId = Guid.NewGuid()` 作关联 ID |
| Provider usage 解析 | `Services/AI/Providers/OpenAICompatibleProvider.cs` | 强制 `stream_options.include_usage=true`，映射 `UnifiedUsage` |
| 实时通道 | `Services/WebSocketBroadcaster.cs` + `ChatTurnStreamRecorder.cs` | 事件 `chat_record_chunk`（`{requestId,sessionId,text,isDone:false}`）/ `chat_record_completed`（`{requestId,sessionId,recordId,isDone:true,error?}`） |
| 多步 Agent 轨迹 | `Plugins/AIAgent/Data/Entities/AgentRun.cs` + `AgentStepRun.cs` | `AgentRun` 有 `AgentId`/`SessionId`/`StepCount`/`TotalTokens`/`CreateTime`（:41-145）；`AgentStepRun` 有 `InputJson`/`OutputJson`/`ToolCallsJson`/`TokensUsed`/`DurationMs` |
| 单轮详情 UI | `ForgeSelf.Web/src/components/chatrecords/ChatRecordDetail.vue` | 已展示模型、`tokens: input/output/total`、耗时、MaxTokens、请求/响应 JSON 树、reasoning 思维链 |
| 工具调用统计 | `Services/UsageStats/UsageStatsService.cs` + `Controllers/UsageStatsController.cs` | `UsageRecord`（`PluginId`/`ToolId`/`ActionType`/`DurationMs`/`MetadataJson`）——**工具维度，非 LLM** |

### 缺口（对照行业四层：trace / 成本 / 质量 / 可视化）

| 缺口 | 实测依据 | 行业标杆做法 |
| --- | --- | --- |
| **无成本换算** | `ChatTurn.cs` 逐字段读过，**无 `Cost` 列**；`UsageRecord.Biz.cs` 读码确认是工具维度（`PluginId`/`ToolId`/`ActionType`） | Langfuse/LiteLLM 核心能力；价目表 + 纯函数计算 |
| **无分布式 trace 关联** | `ChatTurn` 有 `RequestId` 但**无 `TraceId`/`ParentSpanId`/`AgentRunId`**；`ChatTurn` 与 `AgentRun` **无外键** | Langfuse run 下挂多 span 瀑布 |
| **无 LLM 专项 dashboard** | `UsageStatsController` 是工具维度；`ChatRecordDetail` 只有单轮 token/耗时，**无成本、无延迟分布** | 成本时序/延迟 P50-P95/错误率/模型分布 |
| **无质量评估** | 全仓无 faithfulness/hallucination/judge 管线 | DeepEval/RAGAS/Promptfoo |
| **dsh timeline 是 mock** | `dsh-ui-bundle/my-dsh-activity-timeline-client/src/panel.tsx:53-58` `MOCK_ENTRIES`；:145 `entries ?? MOCK_ENTRIES`；:28 footer 文案自陈「wire to ctx.sessions for live data」 | 真实可观测数据源 |

---

## 与 PILOT-032 的关系（已并入，2026-10-05 整合）

> **2026-10-05 整合**：`specs/` 已废弃，全部改用 pilot 工件。本次相关的 spec **仅 `specs/032-cost-scope/`**（其余 036/037/040/041/042 为独立功能，不迁移）。已将 032 三份原文（research/feasibility/design）**归档**至本 pilot 的 `archive/032-cost-scope/`，并**删除** `specs/032-cost-scope/`。本 pilot 现为 **统一 LLM 可观测性 pilot**：成本阶段0（来自 032）+ trace 阶段1 + 可视化阶段2。

**原结论（整合前）**：PILOT-032「CostScope」已把阶段0（成本）设计完整（单价目录 CRUD 4 表、纯函数成本引擎、惰性物化日汇总、模型→供应商 4 级解析、预算规则、T001–T011、U1–U8）。本任务原定位为其增量，只补 trace+可视化。

**整合后结论**：032 已并入本 pilot，**成本阶段0 正式纳入本 pilot 范围**（见 02-spec FR-3 / 03-plan / 04-task）。032 原 design.md 的详细成本设计（API 14 端点、4 表、任务书 T001–T011）保留于 `archive/032-cost-scope/design.md` 作为可追溯真源；本 pilot 的 02/03/04 在其上做**统一口径整合**并**更正了 U6 错误**。

**实测复核（整合时亲手跑）**：

| 复核项 | 命令 | 结果 |
| --- | --- | --- |
| `ITurnTelemetryQuery` 是否已存在 | `ls ForgeSelf.Abstractions/ \| grep -i "telemetry\|Turn"` | **NOT FOUND** → 032 尚未实施，契约层干净 |
| U6 阻断点 | `grep -n "Usage" ForgeSelf.Api/Controllers/ChatController.cs` | **两处** `:131` 与 `:216` 均 `Usage: null` 硬编码 |
| trace 关联可行性 | 读 `AgentRun.cs:41-145` | 有 `AgentId`/`SessionId`/`StepCount`/`TotalTokens` → 可作锚点 |
| 现有 CallId | `grep CallId ForgeSelf.Abstractions/SessionEvents.cs` | `:45`/`:50`/`:119` 已有（040-B1 引入） |

## 候选低风险任务

满足：低风险、小范围、改文件少、易测试、不涉生产/DB 结构/权限/核心架构。

1. **P1-a 补 U6 用量缺口**（⚠️ 032 初判「2 行透传」**不成立**，详见 02-spec「依赖前提」与 U-2）：`ChatController` 注入的是 `IAIService`（`ChatAsync` 返回 `Task<string>`，签名无 usage），修复须迁到带 usage 的 Unified 面（`IChatCompletion`/`IAIProvider`），属**接缝迁移**，非一行。列为闸门1 裁决点 U-2。
2. **P1-b 新增 `ChatTurn.AgentRunId` 列**：XCode 加列（自动迁移，向后兼容），供 trace 关联。⚠️ 触及规范 §1 硬性约束 2「不改 DB 结构」→ **须升级审批**（即闸门1 裁决项）。
3. **P1-c trace 关联与瀑布查询**：宿主只读聚合 `ChatTurn`+`AgentRun`+`AgentStepRun`，供前端画瀑布。**只读，不写库**。
4. **P1-d LLM 观测聚合端点 + dashboard**：成本/延迟/错误率/模型分布，**只读聚合**。
5. **P1-e dsh activity-timeline 接真实数据**：把 `MOCK_ENTRIES` 换成真实数据源，props shape 不变（注释已给切换点）。

## 选择该任务的原因

用户明确要求「根据以上方案完成工件输出」，且前置研究/设计已就绪。本任务**不新建能力领域**，而是补齐已确认的三处真空缺（成本归因缺失、trace 断链、可视化不足），且：

- 全部可在**既有地基上最小改动**完成，不引入新依赖、不做无关重构；
- 关键收益点（成本/token/延迟）**数据已落库**，只差聚合与呈现；
- 与已存在的 032 设计**互补而非冲突**（032 负责成本引擎，本任务负责 trace + 可视化，共用同一 `ITurnTelemetryQuery` 契约思路）；
- 各项均有明确可测的验收判据，符合「可验证性优先」。

**须上escalate、不擅自决定的**（列为闸门1 裁决点）：
- `ChatTurn` 加列属 DB 结构变更（规范 §1 硬性约束 2）；
- ~~本工件范围是否含阶段0（与 032 的分工）~~ → **已并入**：032 已合入本 pilot，成本阶段0 纳入范围（见 02-spec FR-3）。
- U6/usage 修法选型（接缝迁移 vs 双源聚合 vs 显式标注）→ 闸门1 裁决点 U-2。
