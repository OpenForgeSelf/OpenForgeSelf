# mini-task：todo 下发 → 本工具内置 AI Agent（计划驱动执行）链路

> 目录：`docs/ai/pilot/2026-10-09-todo-builtin-agent/`
> 日期：2026-10-09｜任务级别：轻量（新增能力接缝 + 引擎分流，≤8 文件）｜状态：Implement 前（闸门1）

## 1. Intent（为什么 / 做什么 / 到什么程度）

**背景**：用户澄清「将任务交给本工具的 AI Agent」= AI Agent 插件内置的七角色 agent（协调者/分析师/评论家/通用助手/写作者/研究员/程序员，`GET /api/agents` 实测在册），**不是** AgentHub 外部 agent（opencode 等 CLI）。已实测内置「计划驱动执行」端点 `POST /api/ai-agent/runs`（SSE：plan_created → step_started → tool_call/tool_result → step_completed → done）：
- qwen3.5-4b（4B）：**不调任何工具直接收束 → run_stuck**（弱模型工具循环不可靠，与飞轮实测一致）；
- ornith-1.0-9b（9B）：完整走通「规划 → write_file → read_file 验证 → complete_step → done」，产物 `hello-ai-agent.txt` 落项目根、内容正确。

**做什么**：todo-tracker「一键交给 agent」增加**第二个执行引擎 = 本工具内置 AI Agent**（与既有 AgentHub 引擎并列可选）。todo 任务委派时：
1. 引擎选择：AgentHub（外部 CLI）/ 本工具 AI Agent（内置七角色）；
2. 内置引擎选角色（默认按任务能力 best-match，前端可手选）；
3. 委派后 todo 进 Running，徽标显示「本工具AI·角色名·状态」；
4. 执行完成（Run Completed）→ todo 进 Review（待验收），时间线回写「agent 执行回写」记录（含文件变更、耗时、结果摘要）；
5. 卡住（Stuck）/失败（Failed）/取消（Cancelled）→ 状态可解释展示，可重试（resume/重开）。

**到什么程度**：内置引擎一条真实任务端到端跑通 + 既有 AgentHub 引擎零回归 + UI/UX 走查（引擎/角色控件符合交互规格）。**不做**：内置引擎的人工介入 UI（skip/override 走 AIAgent 既有页面）；多任务队列；模型选择器（内置引擎默认固定可用模型）。

## 2. Spec

### 2.1 架构（接缝而非直连）

- 插件间禁止直连 HTTP / 共享文件 / 静态类（architecture-design 铁律 2/3；`TodoAgentDelegationTests` 静态守卫已覆盖）。
- **新增宿主能力接缝** `IBuiltInAgentExecution`（`ForgeSelf.Abstractions`），与既有 `IAgentDelegation`（AgentHub）对称：
  - 提供方：`ai-agent` 插件（`Apply(IContext)` eager 构造并 `ctx.Register<IBuiltInAgentExecution>(impl)`，卸载自动摘除）；
  - 消费方：todo-tracker，`ctx.Get<IBuiltInAgentExecution>()` 每次用每次取，禁止缓存为字段；
  - 接缝缺席（未装/未启用 ai-agent）：todo 映射 503 + 明确文案，禁止静默当成功（同 IAgentDelegation 降级口径）。

### 2.2 契约（ForgeSelf.Abstractions/BuiltInAgentExecutionContracts.cs）

```csharp
public interface IBuiltInAgentExecution
{
    // 触发一次计划驱动执行（后台跑完），立即返回 runId + 初始状态。失败返回 Success=false + Error 原文，不抛业务异常。
    Task<BuiltInAgentOutcome> StartAsync(BuiltInAgentRequest request, CancellationToken ct = default);
    // 按 runId 读回当前快照。不存在返回 null（消费方映射 404）。
    Task<BuiltInAgentSnapshot?> FindAsync(long runId, CancellationToken ct = default);
    // 当前可用角色 agent（仅 enabled）。无候选/接缝不可用返回空列表。
    IReadOnlyList<BuiltInAgentOption> ListAgents();
}
public class BuiltInAgentRequest { string Prompt(必填); string? AgentRoleId; string? ChatModelId; string? CreatedBy; }
public class BuiltInAgentOutcome { bool Success; string? Error; long RunId; string? AgentName; string Status; }
public class BuiltInAgentSnapshot {
    long RunId; string Status; // Pending|Planning|Running|Completed|Stuck|Failed|Cancelled
    bool Terminal; // Completed|Failed|Cancelled = 终态；Stuck 非终态（可 resume/介入）
    long ElapsedMs; string? ResultSummary; List<string> FilesChanged; string? ErrorMessage; string? AgentName;
}
public class BuiltInAgentOption { string RoleId; string Name; string Vendor = "builtin"; }
```

### 2.3 AIAgent 实现（Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs）

- **StartAsync**：`Task.Run` 后台消费 `IRunOrchestratorService.RunAsync(req)` 迭代器（事件丢弃，仅让 Run 落库跑完）；用 `TaskCompletionSource` 在首个 `step_started`（或 `done/error`）事件捕获 runId，前台 `await tcs.Task.WaitAsync(15s)` 拿到 runId 后立即返回；
  - `ChatModelId` 默认 `default:ornith-1.0-9b`（9B，实测唯一走通工具循环的本地模型；4B 会恒 stuck——记录 TODO 引导用户换默认模型）；
  - 失败（taskInput 空 / agent 不存在）返回 `Fail(error)` 原文。
- **FindAsync**：`GetRunDetailAsync(runId)` → 映射快照：
  - Status 词表直转（camelCase）；`Terminal = Status is Completed or Failed or Cancelled`；
  - `ResultSummary`：末条步骤 `OutputJson`（complete_step 声明产出）非空取之，否则「已完成 N 步 / 卡住：{StuckReason} / 失败：{ErrorMessage}」；
  - `FilesChanged`：扫描各步骤 `ToolCallsJson` 中 `write_file`/`patch`/`create_file` 的路径参数（解析 JSON，取 path 字段）；
  - `ElapsedMs`：UpdateTime − CreateTime。
- **ListAgents**：`IAgentRegistryService` 七角色（`agent.coordinator/analyst/critic/generalist/writer/researcher/programmer`，Name 中文名，仅 enabled）。
- **注册**：AIAgent `Apply(IContext)` 里 `ctx.Register<IBuiltInAgentExecution>(new BuiltInAgentExecutionService(...))`。

### 2.4 todo 改动

| 文件 | 改动 |
|---|---|
| `Data/Model.xml` + `Data/Entities/Todo*.cs` | Todo 实体加列 `AgentEngine`（String 16，默认 `agenthub`，描述「委派引擎：agenthub=外部 AgentHub / builtin=本工具 AI Agent」） |
| `Models/DispatchDtos.cs` | `DelegateToAgentRequest` 加 `Engine`（string?，缺省 agenthub）+ `AgentRoleId`（string?，内置角色 id）；`DispatchPreviewDto` 加 `BuiltInAvailable` + `BuiltInAgents`（List<AgentOptionDto>，RoleId 映射 int 1..7 顺序 coordinator..programmer）；`AgentStatusDto` 语义不变（内置状态词表映射后写入） |
| `Services/TodoDispatchService.cs` | `DelegateAsync`：`engine=="builtin"` → `ctx.Get<IBuiltInAgentExecution>()`（缺席→503 文案「本工具 AI Agent 能力缺席（未启用 ai-agent 插件）」）→ `StartAsync(prompt, roleId)` → 回填 `AgentTaskKey=$"run:{runId}"`、`AgentEngine="builtin"`、`AgentId=roleIndex`；`ReadStatusAsync`：`AgentTaskKey` 以 `run:` 开头 → `FindAsync` → 状态映射：`Pending→Queued`、`Planning/Running→Running`、`Completed→Succeeded`、`Stuck→Running`（非终态 + ResultSummary 带卡住原因）、`Failed→Failed`、`Cancelled→Cancelled`；`MarkDelegationCompleteAsync`：builtin 引擎无外部回报语义，返回 `Ok(true)`（无操作） |
| `Controllers/TodoDispatchController.cs` | `Delegate` 透传 Engine/AgentRoleId（已有）；503/400 分流逻辑复用 SeamMissing 标志 |
| 前端 `TaskDetail.vue` + `store.ts` + `http.ts` | 委派区加「执行引擎」选择（AgentHub 外部 / 本工具 AI Agent）；内置引擎显示七角色下拉（默认按任务 best-match 提示）；委派 body 带 `engine`/`agentRoleId`；状态徽标 `本工具AI·程序员·Running`；AIAgent 未装 → 内置引擎置灰 + 文案 |

### 2.5 交互设计（ui-ux-design 规格：「点什么出现什么」）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
|---|---|---|---|---|---|---|
| 执行引擎选择 | 点击 radio | 切换引擎候选区：AgentHub → 外部 agent 下拉；本工具 AI Agent → 七角色下拉 | 两引擎候选互斥 | 无 | 接缝缺席 → 对应引擎置灰 + 说明文案 | 引擎切换不清空已选权限模式 |
| 内置角色选择 | 点击下拉 | 选中角色（默认提示 best-match） | 七选一 | 无 | 无 | 角色 id 非法 → 委派失败原文提示 |
| 一键委派（内置） | 点击「交给本工具 AI Agent」 | 请求委派 → 成功：toast「已交给 本工具AI·角色」+ 任务进 Running + 徽标更新；失败：toast 原文 + 记录留痕 | 可委派/不可委派（必填缺口/接缝缺席） | toast + 按钮 loading | 七角色为空 → 提示启用 ai-agent | 必填缺口 → 按钮禁用（沿用现有） |
| 状态回读 | 轮询（沿用现有周期） | 徽标状态随快照变化；Completed → 任务进 Review + 时间线「agent 执行回写」记录 | Running/Review/不动 | 无 | Run 不存在 → 「委派任务不存在」降级文案（沿用） | Stuck/Failed/Cancelled 不推状态，仅记录 |

### 2.6 验收标准（闸门2 逐条）

- A1：todo 委派区出现「执行引擎」选择，选「本工具 AI Agent」出现七角色下拉；ai-agent 插件在场时可选、缺席时置灰并说明原因。
- A2：委派一条简单任务给「本工具 AI Agent·程序员」→ 任务进 Running，徽标显示「本工具AI·程序员」与进行态；轮询后 Completed → 任务进 Review（待验收），时间线出现「agent 执行回写：Succeeded」记录（含文件变更路径、耗时、结果摘要）。
- A3：内置引擎产物真实存在（如 `ok-builtin.txt` 内容正确）。
- A4：内置引擎 Stuck/Failed/Cancelled → 徽标/记录可解释（不假装成功、不推 Review）；可重新委派。
- A5：既有 AgentHub 引擎路径零回归：todo e2e 全绿（含 E1 opencode 真实委派）。
- A6：后端单测覆盖：引擎分流（builtin/agenthub）、快照映射（Completed/Failed/Stuck/Cancelled）、FilesChanged 解析、接缝缺席 503。
- A7：UI 走查截图（e2e 隔离实例）：委派区引擎/角色控件、徽标「本工具AI·程序员·Running」、Review 态时间线记录。

## 3. Plan

1. 宿主契约 `ForgeSelf.Abstractions/BuiltInAgentExecutionContracts.cs`（新，~120 行）
2. AIAgent `Services/BuiltInAgentExecutionService.cs`（新，~180 行）+ `AIAgentPlugin.cs` 注册
3. Todo 实体 `AgentEngine`（Model.xml + Todo.cs/Biz 生成）
4. `DispatchDtos.cs` 扩展（Engine/AgentRoleId/BuiltInAgents）
5. `TodoDispatchService.cs` 引擎分流 + 状态映射
6. 前端 `TaskDetail.vue`/`store.ts`/`http.ts` 引擎/角色控件 + 徽标
7. 后端单测（TodoDispatchService 分流 + BuiltInAgentExecutionService 映射）
8. e2e（todo-tracker spec 增内置引擎用例）
9. 门禁：`dotnet build` + 过滤集单测 + `pnpm run check/test` + todo e2e
10. 发布：本地打包（release-local -Sign → updates）+ 复验（plugin-publish-verify 只读走查）

## 4. Task（Allowed / Forbidden + 验证）

- **Allowed**：仅上述清单文件；沿用既有 `IAgentDelegation` 降级模式；内置引擎默认模型 ornith-1.0-9b；测试走项目唯一体系（xUnit / vitest / Playwright e2e）。
- **Forbidden**：todo 直连 `/api/ai-agent/*` HTTP（静态守卫）；改 AgentHub 既有语义；自定义新状态词表（映射到既有）；git 提交/推送（未授权）；停/启/杀宿主。
- **验证命令**：后端 `dotnet build`（0 错）+ `dotnet test --filter`（本批相关集）；前端 `pnpm run check` + `pnpm run test`；e2e `e2e/plugins/todo-tracker`（定向，快档）；发布 `pwsh scripts/release/release-local.ps1 -Version v2.3.5 -Sign -UpdateDir D:\src\my-proj\OpenForgeSelf\updates`。

## 5. 证据（实施后补）

- 05-evidence.md：门禁日志（build/test/check/e2e）、内置引擎真实委派产物（ok-builtin.txt）、截图（隔离实例 + 运行实例）。
- 06-review.md：八问 + Final Decision。

## 6. 风险

- 内置引擎模型通道（ornith-1.0-9b）若上游不可用 → Stuck/Failed 可解释展示，不阻塞交付（TODO 记默认模型引导）。
- Run 后台执行与宿主重启：Run 实体落库，重启后 FindAsync 仍可读（Completed 保留）；执行中断态保留（resume 入口在 AIAgent 页面）。
- 契约新增对宿主版本要求：宿主与插件同包发布（本地打包），无独立部署问题。
