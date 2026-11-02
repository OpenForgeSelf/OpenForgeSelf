# mini-task — B3 任务中心（Task/Run 驱动补全）

> Task ID：`2026-11-02-maf-runtime-hardening/02-b3-taskcenter`｜日期：2026-11-02｜负责人：taskcenter-dev
> 战役：`2026-11-02-maf-runtime-hardening`（00-campaign-workflow.md §1 B3；用户已令开工，闸门1 视为已批）
> 模式：**轻量**（mini-task.md 合并 Intent/Plan/Task + 02-spec.md；Evidence/Review 完成后补 05/06/07）

## Intent（为什么 / 做什么 / 到什么程度）

MAF 一等公民架构下，任务的三个入口（开放任务会话 Run、计划任务 AgentRun、Scheduler AgentRun）分散在三处，
用户没有一个「跨入口任务总览」。B3 补全 L1 交互工作台的「任务表单/可视化」职责：

1. **跨入口任务总览 API**：新文件 `Plugins/AIAgent/Controllers/TasksOverviewController.cs`，统一 DTO（来源/标题/状态/开始结束时间/失败原因/可重试标记）。
2. **宿主任务中心视图**：新文件 `ForgeSelf.Web/src/views/TaskCenterView.vue`（列表 + 状态徽标 + 失败原因 + 重试/续跑入口），注册进 features.ts / tabs.ts。
3. **后台并行 Run 基础**：任务中心轮询状态（复用既有 REST），Run 与聊天视图解耦展示；**不做执行引擎改动**（B2 写面）。

完成程度（验收，来自 00-campaign-workflow §1 B3）：三个入口的任务在同一列表可见、状态准确；失败任务可见重试入口；
门禁 = 快档（pnpm check/test/design:check + dotnet build + dotnet test --filter TasksOverview）+ 集成验证 + 走查截图。

## Plan（具体到真实文件）

| # | 动作 | 文件 | 预期 |
|---|------|------|------|
| 1 | 后端总览控制器（新） | `Plugins/AIAgent/Controllers/TasksOverviewController.cs` | `GET api/ai-agent/tasks-overview` 返回统一 DTO（open+planned 两桶） |
| 2 | 后端单测（新） | `ForgeSelf.Api.Tests/Integration/TasksOverviewControllerTests.cs` | Mock 服务层，覆盖状态投影/失败原因/重试标记/过滤 |
| 3 | 前端 API 层（新） | `ForgeSelf.Web/src/services/taskCenterApi.ts` | 汇聚 overview API + scheduler API → 统一 TaskOverviewItem；resume/run-now 动作 |
| 4 | 任务中心视图（新） | `ForgeSelf.Web/src/views/TaskCenterView.vue` | PageHead/ForgeCard/EmptyState 五态齐备，轮询 15s + 防闪 |
| 5 | 功能注册 | `ForgeSelf.Web/src/data/features.ts` | 新增 task-center 条目（signals 齐全，过 check-features 孤儿检查） |
| 6 | 标签栏注册 | `ForgeSelf.Web/src/stores/tabs.ts` | DEFAULT_TABS 增「任务中心」 |
| 7 | 静态路由（写面偏差，见下） | `ForgeSelf.Web/src/router/index.ts` | `/task-center` 指向 TaskCenterView，否则入口 404 = 假能力 |
| 8 | 前端单测（新） | `ForgeSelf.Web/src/services/__tests__/taskCenterApi.test.ts` | scheduler 行映射 / 状态归一 / 重试标记纯函数 |

**写面偏差声明（2 处，均新增行为、无冲突面）**：
- `router/index.ts` 加一条静态路由：lead 写面清单未列，但不加则 `/task-center` 落 RouteMissingView（交付动线成本铁律 22：入口不可达 = P1 UX 债）。verify-dev 写面不含此文件。
- `services/taskCenterApi.ts`（新文件）：视图需要 API 层；新建文件无冲突。DESIGN.md「统一走既有 API 层」即指本层。

## Task（Allowed / Forbidden + 验证命令）

**Allowed**：上表 8 个文件；工件目录 `docs/ai/pilot/2026-11-02-b3-taskcenter/`。

**Forbidden**：`Plugins/AIAgent/MafLoop/`、`ForgeSelf.Api/Services/ToolRegistry.cs`、AIAgent 既有 Controller（Approvals/Project/AgentRuns 只读参考）、`:51888` 实例、`ForgeSelf.Web/src` 其余文件（verify-dev / qa-gate 写面）、执行引擎（RunOrchestrator 内部、审批挂起逻辑）。

**验证命令**（快档 + 构建窗口约束，见 05-evidence）：
```
# 前端（无构建窗口依赖，先跑）
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
# 后端（等 lead 构建窗口开，DLL 锁由 verify-dev 管理）
dotnet build (ForgeSelf.Api)  +  dotnet test --filter TasksOverview
# 集成验证
Invoke-RestMethod http://localhost:7301/api/ai-agent/tasks-overview -Headers @{Authorization="Bearer <token>"}
```

## 关键决策（详见 02-spec §8）

- **D1 Scheduler 桶聚合位置=前端服务层**：AIAgent 插件不可引用 Scheduler 插件程序集（csproj 仅 Core+Abstractions）；
  HTTP 自调被既有裁决禁止（ScheduledAgentRunContracts.cs 注释）；Abstractions 无读侧契约。扩写面到 Scheduler 插件
  违反最小写面（verify-dev 并行中）。后端控制器聚合 open+planned，scheduler 行由 taskCenterApi.ts 调既有
  `GET /api/scheduler/tasks`（过滤 TaskType=3）映射为同一 DTO 形状。
- **D2 开放任务状态投影**：会话枚举（IPluginMessageService.GetSessionsAsync，插件自有服务）+ ISessionStore.Replay
  逐会话扫 turn 事件（宿主契约经 IContext 运行期解析，铁律 12c）。
- **D3 统一 DTO 状态字典**：running/planning/pending/completed/failed/stuck/cancelled/scheduled/unknown。
- **D4 鉴权**：类级 `[Authorize("ApiKeyPolicy")]`（铁律 17 + ImGateway PILOT-078 先例）。
