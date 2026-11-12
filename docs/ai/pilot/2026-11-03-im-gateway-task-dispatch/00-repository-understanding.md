# Repository Understanding

> 阶段：Stage 0｜Task ID：PILOT-079（2026-11-03-im-gateway-task-dispatch）
> 原则：所有条目来自真实仓库内容（git log / 读码 / 既有工件），禁止凭常识推测。

## 项目结构

- 单仓库：宿主 `ForgeSelf.Api/`（.NET 10 + NewLife.XCode）+ 宿主前端 `ForgeSelf.Web/`（Vue3）+ 插件源码 `Plugins/<X>/`（根级，编译进宿主进程）+ 契约层 `ForgeSelf.Abstractions/` + 测试 `ForgeSelf.Api.Tests/`、`ForgeSelf.Web/e2e/`。
- 本任务涉及三处：`Plugins/ImGateway/`（IM 网关，企微通道）、`Plugins/AIAgent/`（智能体，MAF 执行中枢）、`ForgeSelf.Abstractions/`（跨插件契约）。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 后端 | .NET 10 + SQLite + NewLife.XCode | AGENTS.md §2.1；`ForgeSelf.Api.csproj` |
| 插件机制 | `Plugins/<X>` + `plugin.json`；宿主契约经 `IContext` 运行期解析（`ctx.Get<T>()`） | `Plugins/ImGateway/Services/ImGatewayRouter.cs:75`；`TasksOverviewController.cs:45-53` |
| 跨插件契约 | 全部定义在 `ForgeSelf.Abstractions`，提供方 `Apply` 里 `ctx.Register`，消费方每次 `ctx.Get` | `ForgeSelf.Abstractions/IChatCompletion.cs:1-19`、`ScheduledAgentRunContracts.cs:3-8`、`AgentDelegationContracts.cs:4-23` |
| 前端 | Vue 3.5 + Vite + pnpm（本批仅 ImGateway web 设置小节，待拍板） | AGENTS.md §2.1 |

## 架构特点（与本任务直接相关的铁事实）

1. **任务中心是刚上线的只读总览（2026-11-02，commit `2c6b30c`，战役 B3）**：`GET /api/ai-agent/tasks-overview`（`Plugins/AIAgent/Controllers/TasksOverviewController.cs`）聚合三入口——开放任务（会话事件投影）/ 计划任务（`IRunOrchestratorService.ListRunsAsync` 投影 AgentRun）/ 定时任务（前端聚合 Scheduler API）。**它不做任务创建**（B3 spec §2 NOT-to-do 明确「不做任务创建/编辑」）。
2. **计划任务 = AgentRun 实体**：`POST /api/ai-agent/runs`（`AgentRunsController.cs:42-86`，SSE 驱动）→ `RunOrchestratorService.RunAsync`：建 Run → 规划 → 逐步执行 → 终局合成。`RunRequest` 已含 `AgentId`（指派）、`SessionId`、`ChatModelId`、`TaskInput`、`SuccessCriteria/MaxGoalRounds/ExitCriteria`（`AgentRunModels.cs:110-143`）。终态与失败原因可从 `AgentRunDto.Status/StuckReason/ResultReceipt` 读回。
3. **同进程后台驱动 Run 的先例**：`Plugins/AIAgent/Services/ScheduledAgentRunTrigger.cs`（Scheduler → MAF Run）——同进程调 `IRunOrchestratorService.RunAsync`、迭代事件流至终态、从事件 payload 捕 runId、inflight 字典防重、不走 HTTP 自调。IM 派任务可直接套用此模式。
4. **既有同类接缝（契约形状先例）**：
   - `IAgentDelegation`（AgentHub 提供外部 CLI agent 委派）：SubmitAsync（入队即回 taskKey）+ FindAsync（快照轮询）+ MarkCompletedAsync + ListAvailableAgents；失败返回 `Success=false + Error` 原文、不抛业务异常。
   - `IScheduledAgentRunTrigger`（ai-agent 提供）：同进程触发 MAF Run，写操作仍走现有审批。
   - `IChatCompletion`（ai-agent 提供，IM 网关消费中）：聊天快答链路。
5. **IM 网关现有收发链**（`ImGatewayRouter.ProcessInboundAsync`，v2.1.1）：去重（`ImGatewayProcessedMsg`）→ 事件消息跳过 → 会话映射 `GetOrCreateSession(channel, conversation, user, BoundAgent, BoundModel)`（每会话可绑 AgentId/ModelId）→ 入站落库 → `IChatCompletion.CompleteAsync` → 通道 `SendAsync` 回发（长连接通道带 `ReplyReqId`；主动推送按 `ChatType` 解析 chatid，两字段空则无副作用——**延迟回复可用主动推送**，`ImGatewayRouter.cs:97-113` 注释）。
6. **架构真源**：`docs/01-architecture/maf-centric-architecture.md`——L1 交互工作台（「Chat 只是入口之一；驱动单元是 Task/Run」§2 图注）、L2 MAF Runtime、禁止另起 LocalBot/平行 Runtime（§1；not-taken 050）；契约跨边界进 Abstractions（§5.4）。**IM 消息派任务 = 给 MAF Run 新增第四个入口，架构方向一致。**

## 测试方式

- 后端单测：`ForgeSelf.Api.Tests/`（xUnit + Moq + FluentAssertions）；新测试禁止手搓 `DAL.AddConnStr` 换库（PC-014），用 `IClassFixture<XCodeTestFixture>` 模式。
- e2e：`ForgeSelf.Web/e2e/plugins/im-gateway/`（Playwright，零 mock，地址取 `e2e/helpers/e2e-env.ts`）。
- 插件前端：`cd Plugins/ImGateway/web && pnpm run build`（若本批触及 web）。

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "..."   # 过滤集；中档=全量
```

## 主要目录职责（本批触面）

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Abstractions/` | 跨插件契约与 DTO（新契约唯一落点） |
| `Plugins/AIAgent/` | MAF Runtime：Run 编排、Agent 注册表（`AgentRegistryService`）、任务总览读侧 |
| `Plugins/ImGateway/` | IM 通道接入、会话映射、消息收发与落库 |

## 代码组织方式

插件 = `Controllers/` + `Services/` + `Data/Entities/`（XCode 实体自动建表，无手工迁移）+ `web/`（远程加载插件页）+ `plugin.json`；宿主契约在插件内一律 `IContext` 运行期 `Get`，禁止构造注入（铁律 12c，`TasksOverviewController.cs:40-44` 注释实证）。

## 现有工程规范（约束本任务的条目）

- `architecture-design` 技能铁律 2/3/4：契约进 Abstractions；禁共享文件/静态类/直连 HTTP 当契约；`ctx.Get` 每次取、禁缓存字段。
- AGENTS.md §5.6 门禁分档：新 Abstractions 契约 = 公共接口变更（中风险）→ 执行后跑中档全量 `dotnet test`。
- 2026-10-30 用户立：插件任务 Spec 一律必写「Interaction Design」节（无界面也要写「无界面交互」）。
- 发布规范：默认本地目录更新源；发布动作须用户同意；禁停/启/杀运行中宿主。

## 候选低风险任务

本任务（IM 网关 → 任务中心派发闭环）：新增契约 + 两个插件内新服务 + 路由分支，默认关闭、不动既有聊天链路与任务中心读侧；测试可用接口替身（`IRunOrchestratorService` 可 Mock）。

## 选择该任务的原因

用户 2026-11-03 输入1 直接指定；任务中心（B3）2026-11-02 刚上线，提供统一视图但缺「外部入口」；架构真源明示「Chat 只是入口之一」，IM 入口是自然延伸；全部依赖（Run 编排、Agent 注册表、通道回发、会话映射）均已存在，属低风险拼装。

## 适用问题类（PC 检索）

按触碰区检索 `docs/11-troubleshooting/problem-classes/README.md`：

- **PC-013**（IM→AI 补全 404，模型解析链明确失败）：lens——任务路径同样带模型路由（`RunRequest.ChatModelId`），空值必须复用 orchestrator 既有 `IChatModelResolver` 明确失败语义，不得新增回退猜测。
- **PC-014**（测试手搓 `DAL.AddConnStr` 换库污染全局态）：新测试一律 `XCodeTestFixture` 模式。
- 其余（PC-016/017/018/019/021 等）与本任务触碰区无交集，写「无」。
