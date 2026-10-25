# Mini Task：待办 builtin 引擎真实下发跑通到 Done（默认模型解析链 + 空响应不再当成功）

> Task ID：2026-10-25-todo-builtin-agent-done｜task_kind: defect｜日期：2026-10-25｜轻量（本文件合并 Intent / Spec / Plan / Task）
> 承接：`docs/ai/pilot/2026-10-24-todotracker-interaction/07-final-report.md` 遗留项 **R3**
> 闸门1：**待用户批准**后进入 Implement。

## Repository Understanding

- 真实下发链路：TodoTracker `POST /api/todos/{id}/dispatch-to-agent` → `TodoDispatchService.DelegateToBuiltInAsync`（`Plugins/TodoTracker/Services/TodoDispatchService.cs:191-250`）→ `BuiltInAgentExecutionService.StartAsync`（`Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs:44-118`）→ `RunOrchestratorService.RunAsync`（规划 + 逐步执行）→ 由 todo 轮询 `FindAsync` 回填 `Stage`。
- 规划/步骤回合均经 `ReactLoopAgent` 状态机（`Plugins/AIAgent/Services/ReactLoopAgent.cs`），模型经 `AIAgentService.ResolveModelId/ResolveProvider`（`AIAgentService.cs:562-644`）解析。
- 插件容器可用宿主契约：`IConfigurationService.GetAIConfig()`（`AIAgentService.cs:52` 已用）、`ITurnTelemetryQuery.GetModelCatalogAsync()`（`Plugins/CostScope` 已用；实现 `ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs:87-102`，只返回 `AIModel.Enabled==true`）。
- 本机已实测：直连 `POST /api/ai-agent/runs` 带 `chatModelId="default:deepseek-v4-flash"` → Run 3 完成、步骤产出正确（见 `.forgeself/memory/2026-10-25.md` §一）。⇒ 链路本身可用，问题在「不给模型时选哪个」。

## Intent / Problem

让待办插件「一键交给本工具 AI Agent」（builtin 引擎）在不显式指定模型时也能真实执行到 Done，并在没有可用模型时**明确失败**而不是静默换模型/把空响应当成功。

现状三个缺陷（均已定位到代码 + 日志）：

1. `BuiltInAgentExecutionService.cs:30` 硬编码 `default:ornith-1.0-9b`，该模型**不在模型表中**；`AIAgentService.ResolveProvider` 未命中时静默回退默认 provider ⇒ 请求被发给上游后回空流。
2. `ReactLoopAgent.cs:470-493` 把「无正文、无工具调用」的空响应当作本步最终答复成功收束 ⇒ 029 判 Stuck（`未调用 complete_step`），把「模型不可用」误报成「Agent 不会执行」。
3. `RunOrchestratorService.cs:247-252` `ResumeAsync` 传 `ChatModelId=null` ⇒ 退化到宿主默认配置模型（本机为 `qwythos-9b-v2`，实测只回空流）。

## Spec

### Functional Requirements

- FR-1 内置执行选模型走**有序解析链**，不得硬编码不存在的模型：
  ① 调用方显式传入的 `request.ChatModelId`（原样尊重，不改写）；
  ② 宿主 AI 配置默认模型（`IConfigurationService.GetAIConfig().ModelName`）**且能在模型目录中匹配到**（ChatModelId / UpstreamModelId / Alias，大小写不敏感）→ 补 provider 前缀后使用；
  ③ 模型目录中第一个启用模型（`ITurnTelemetryQuery.GetModelCatalogAsync()`）→ 补 provider 前缀后使用；
  ④ 以上全不命中 → `BuiltInAgentOutcome.Fail("未找到可用对话模型：请到设置页启用至少一个 AI 模型后再下发")`。
- FR-2 解析链命中与否必须落日志（`[BuiltInAgentExecution] 解析模型：来源=…, model=…` / 未命中 Warn）。
- FR-3 `ResumeAsync` 不再传 `null`，改走同一解析链；解析不到 → 返回 `error` 事件（明确文案），不进入步骤执行。
- FR-4 上游空响应（`content` 为空、无 `tool_calls`、`completionTokens == 0`、`finishReason != "length"`）不再当作成功收束：判 `StepEndReason.Failed` + `TurnFailed`，错误文案写明「上游返回空响应（无正文、无工具调用）：模型可能不存在/未启用/上游不可用」。

### Input / Output and Boundaries

- 输入：`POST /api/todos/{id}/dispatch-to-agent` body `{"engine":"builtin"}`（无 `chatModelId`）。
- 输出：`todo.agentTaskKey="run:<id>"`、`stage=Running`；Run 终态 Completed 后 todo 阶段推进到 Done 并带结果摘要。
- 不改：`DelegateToAgentRequest` / `DelegateToBuiltInAsync` 契约（不新增 `ChatModelId` 字段）；`AIAgentService.ResolveModelId/ResolveProvider` 的既有语义（避免影响主聊天，宿主默认模型可能不在模型表）；不新增数据库列（resume 靠重解析而非持久化）。
- 未知：本机唯一启用模型为 `qwen3_8_vision`，实测可被上游转向到 `deepseek-v4-flash` 并返回 `tool_calls`；若验收时该路径失败，允许在**隔离验收实例**（`.temp/todotracker-acceptance`）启用 `deepseek-v4-flash` 后重试（不动用户 51888 实例数据）。

### Failure Modes / 问题类

- 关联问题类：`docs/11-troubleshooting/problem-classes/` 需新增「模型不可用被静默降级 + 空响应当成功」类（PC 编号待分配），case 归档复现证据（日志行、run 2 的 stuckReason）。
- 守卫：解析链单测（无模型 → Fail）+ ReactLoopAgent 空响应单测（红→绿）。
- 同类扫查：全仓 `DefaultChatModelId` / 其他硬编码模型名 / 其他「空 content 即成功」分支。

### Interaction Design

无新增 UI。用户可见变化仅两处文案：内置执行启动失败时给出明确不可用原因；执行流按既有阶段推进到 Done。

### Acceptance Criteria

- [ ] 解析链 4 条按优先级生效，单测覆盖（显式 → 宿主默认命中 → 目录首个启用 → 全无则 Fail）。
- [ ] ReactLoopAgent 空响应判失败，单测红→绿，既有 8 条 B5 门禁用例不改语义仍全绿。
- [ ] `dotnet build` 通过；相关测试过滤集全绿。
- [ ] **真实下发验收**：对隔离实例待办 `POST /api/todos/{id}/dispatch-to-agent {"engine":"builtin"}` → 轮询 Run 到 `Completed` 且步骤 `outputJson` 非空 → todo 阶段到达 `Done` / 结果摘要非空。
- [ ] 失败路径验收：模型全部停用时下发返回明确失败文案（可临时在隔离实例停用模型验证后恢复）。
- [ ] 05-evidence / 06-review / 07-final-report 与日记、TODO 归档补齐。

## Plan

1. 新增 `Plugins/AIAgent/Services/ChatModelResolver.cs`（~80 行）：`IChatModelResolver` + 实现，依赖 `IContext` 懒取 `ITurnTelemetryQuery` / `IConfigurationService`；在 `AIAgentPlugin.cs` 注册（`TryAddScoped`）。
2. `BuiltInAgentExecutionService`：注入 resolver，`StartAsync` 用解析链替换 `DefaultChatModelId`，未命中直接 Fail；删除硬编码常量与误导注释。
3. `RunOrchestratorService.ResumeAsync`：改为 `await resolver.ResolveAsync(null, ct)`，null → `error` 事件收束。
4. `ReactLoopAgent`：`calls.Count == 0` 分支前置空响应判定（FR-4），命中走 Failed 出口（复用 `TurnFailed` 通道）。
5. 测试：`ForgeSelf.Api.Tests/Plugins/AIAgent/ChatModelResolverTests.cs`（4 条优先级 + 未命中）；`ReactLoopAgent` 空响应用例；既有 `TodoBuiltInDispatchTests` / `PlannedRunUnifiedLoopGates` 回归。
6. 重启隔离 dev-stack（`pwsh scripts/dev-stack.ps1 -Stop` 后重启，仅隔离实例），跑真实下发验收；随后 R1 冒烟 e2e + 源码 UI 走查。

## Files To Change (planned)

- `Plugins/AIAgent/Services/ChatModelResolver.cs`（新增）
- `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs`
- `Plugins/AIAgent/Services/RunOrchestratorService.cs`
- `Plugins/AIAgent/Services/ReactLoopAgent.cs`
- `Plugins/AIAgent/AIAgentPlugin.cs`（DI 注册）
- `ForgeSelf.Api.Tests/Plugins/AIAgent/ChatModelResolverTests.cs`（新增）
- `ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs`（补空响应用例）
- `docs/11-troubleshooting/cases/2026-10-25-*.md` + `problem-classes/PC-*.md` + 两份 README 索引
- `.forgeself/memory/2026-10-25.md`、`TODO.md`

## Verification Plan

```powershell
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Api
dotnet build
cd ..\ForgeSelf.Api.Tests
dotnet test --filter "FullyQualifiedName~ChatModelResolver|FullyQualifiedName~ReactLoopAgent|FullyQualifiedName~TodoBuiltInDispatch|FullyQualifiedName~PlannedRunUnifiedLoop"
pwsh scripts/check-problem-classes.ps1
```

真实下发（隔离实例 7301，token 见 `.temp/dev-stack.json`）：

```powershell
# 1) 重启 dev-stack 使新 AIAgent.dll 生效（仅隔离实例）
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1 -Stop
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1
# 2) 下发
POST http://localhost:7301/api/todos/{id}/dispatch-to-agent  {"engine":"builtin"}
# 3) 轮询 GET /api/ai-agent/runs/{runId} 至终态，取 status / steps[].outputJson
# 4) 轮询 GET /api/todos/{id} 确认 stage 到达 Done 且有结果摘要
```

## Rollback

仅回退本任务列出的文件；保留工作区既有 TodoTracker 交互改动（未提交）。
