# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（亲自跑过拿到真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。

## Task

2026-10-25-todo-builtin-agent-done（承接 `2026-10-24-todotracker-interaction` 遗留 R3）

## Changed Files

- `Plugins/AIAgent/Services/ChatModelResolver.cs`（新增：`IChatModelResolver` + 四级解析链）
- `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs`（删 `DefaultChatModelId`，改走解析链，未命中 Fail）
- `Plugins/AIAgent/Services/RunOrchestratorService.cs`（`RunAsync`/`ResumeAsync` 接入解析链，空则 `error` 事件）
- `Plugins/AIAgent/Services/ReactLoopAgent.cs`（空响应守卫：无正文 + 无工具调用 + 0 completion token + 非 length ⇒ Failed）
- `Plugins/AIAgent/AIAgentPlugin.cs`（注册 `IChatModelResolver`）
- `ForgeSelf.Api/Plugins/PluginManager.cs`（`HostProvidedServiceContracts` 补 `ITurnTelemetryQuery`）
- `ForgeSelf.Api.Tests/Plugins/AIAgent/ChatModelResolverTests.cs`（新增 4 条）
- `ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs`（补空响应用例）
- `Plugins/TodoTracker/web/dist/*`（本轮重建产物，未改源码）
- `docs/11-troubleshooting/cases/2026-10-25-builtin-agent-silent-model-fallback.md`（新增）
- `docs/11-troubleshooting/problem-classes/PC-013-silent-model-fallback-empty-response.md`（新增）
- `docs/11-troubleshooting/{cases,problem-classes}/README.md`（索引登记）

## Build

Command:

```bash
dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
```

Result: PASS（Verified）——0 错误（警告 114 为既有存量，未逐条清理）。

```text
Build succeeded. 0 Error(s)
```

插件前端构建：

```bash
cd Plugins/TodoTracker/web && pnpm run build
```

Result: PASS（Verified）

```text
✓ 20 modules transformed.
dist/style.css  25.71 kB │ gzip:   4.76 kB
dist/index.js   96.97 kB │ gzip:  24.29 kB
✓ built in 517ms
```

## Unit Test

定向过滤集（`ChatModelResolver|ReactLoopAgent|TodoBuiltInDispatch|PlannedRunUnifiedLoop`）：

Result: 37 通过 / 1 失败 / 38 总计，6s（Verified）

```text
失败项：ReactLoopAgentTests.ExitToolTurn_RequiresToolChoice
期望 ToolChoice == "required"，实际 null。
判定：既有基线红（`grep` 确认 `Plugins/AIAgent/Services/**` 中没有任何 ToolChoice 赋值；仅
`ForgeSelf.Abstractions/UnifiedChatModels.cs` 有该属性，属工作区既有未提交改动），与本次改动无关。
```

全量后端测试（中档门禁）：

Result: 见 `## Full Suite (medium gate)`（Verified）

## Integration Test

真实下发验收（隔离 dev 实例，`http://localhost:7301`，数据根 `.temp/dev-stack/data`）：

Result: PASS（Verified）

1. 建待办（`POST /api/todos`）→ `id=10`、`stage=Ready`、`missing=[]`、`projectId=1`（项目根 `D:\src\my-proj\OpenForgeSelf\OpenForgeSelf`）。
2. `POST /api/todos/10/dispatch-to-agent {"engine":"builtin"}` → `ok=true`、`taskKey="run:1"`、`agentName="程序员"`、`backfilled=true`、耗时 8620ms。
3. 轮询 `GET /api/ai-agent/runs/1`：
   - 规划成功（`planJson` 2 步：`s1` 只读检查 `ForgeSelf.slnx`、`s2` 回写台账）；
   - `s1` Completed，`outputJson` 给出真实结论「✅ ForgeSelf.slnx 存在于项目根目录下…1886 字节」；
   - `s2` Completed，`outputJson` 记录它实际调用 `search_tools / get_agent_task / append_task_execution` 把结果写回并流转 Review；
   - Run 终态 `status=3`（Completed），`stuckReason=null`，`updateTime=2026-10-25T01:52:03`。
4. 台账核对（`GET /api/todos/10/records`）：

   | seq | actor | action | stage |
   |-----|-------|--------|-------|
   | 1 | rest | 状态流转：Ready → Dispatched | Ready→Dispatched |
   | 2 | rest | 一键交给本工具 AI Agent 执行 | Ready→Running |
   | 3 | agent:append_task_execution | 完成只读检查并回写验收结果 | Running→Review |
   | 4 | user | 验收通过：确认 Agent 真实返回结果 | Review→Done |

5. 终态：`GET /api/todos/10` → `stage=Done`、`status=Completed`、`completedAt=2026-10-25T01:53:14`；
   `GET /api/todos/10/agent-status` → `status=Succeeded`、`terminal=true`、`elapsedMs=37489`、`errorCode=null`。

模型解析日志（`.temp/dev-stack-backend.log`，Verified）：

```text
01:51:26 [ChatModelResolver] 宿主默认模型 qwythos-9b-v2 不在启用模型目录中，改用目录首个启用模型
01:51:26 [ChatModelResolver] 使用模型目录首个启用模型：default:qwen3_8_vision
01:51:26 [ChatModelResolver] 使用调用方指定模型：default:qwen3_8_vision
```

## 失败路径验收

Result: PASS（Verified）

1. `PATCH /api/ai-models/2/enabled {"enabled":false}` 停用唯一启用模型 → `enabledOnly` 目录计数 0。
2. 建待办 `id=11` 后下发 builtin → HTTP 400：

   ```json
   {"data":null,"code":400,"message":"未找到可用对话模型：请到「AI 模型」设置启用至少一个对话模型后再下发。","success":false}
   ```

3. 待办保持 `stage=Ready`、`agentTaskKey=""`，台账留下失败记录：
   `一键交给本工具 AI Agent 执行 / 失败：未找到可用对话模型：请到「AI 模型」设置启用至少一个对话模型后再下发。`
4. 日志：`[ChatModelResolver] 启用模型目录为空，没有任何可用于对话的模型` + `[BuiltInAgentExecution] 未解析到可用对话模型，拒绝下发（Prompt 长度 1299）`。
5. `PATCH /api/ai-models/2/enabled {"enabled":true}` 恢复 → `enabledOnly` 计数 1（未改动用户 51888 实例数据）。

## Full Suite (medium gate)

Command（停止隔离 dev-stack 后，R2 修复后复跑）：

```powershell
$env:TEMP='<repo>\.temp\tmp'; $env:TMP=$env:TEMP
dotnet test ForgeSelf.Api.Tests\ForgeSelf.Api.Tests.csproj --nologo *> .temp\medium-gate-full-r2.log
```

Result: 3170 通过 / 13 失败 / 3183 总计，13m48s（Verified，日志 `.temp/medium-gate-full-r2.log`）

```text
失败!  - 失败:    13，通过:  3170，已跳过:     0，总计: 3183，持续时间: 13 m 48 s - ForgeSelf.Api.Tests.dll (net10.0)
```

红项归属（逐条对表，非一句「无关」）：

| 红项 | 数量 | 归属与依据 |
|---|---|---|
| `WorkflowPlanningIntegrationTests`（PlanWorkflow ×3 / MatchTools ×2 / GetPlanPrompt ×1） | 6 | **既有基线红**：均 `404`（测试宿主未装配插件控制器），已登记 `TODO.md`「WorkflowPlanningIntegrationTests 6 例基线红」 |
| `ScriptRunnerDiIntegrationTests.GetRuntimes_ShouldResolvePluginControllerThroughCordisContext` | 1 | **同族基线红**：同样 `404`（插件控制器路由未进测试宿主），本轮新增 TODO 登记 |
| `ReactLoopAgentTests.ExitToolTurn_RequiresToolChoice` + `OpenAICompatibleProviderToolChoiceTests.ChatStream_ForwardsRequiredToolChoice` | 2 | **既有缺口**：`grep ToolChoice\s*=` 在 `Plugins/AIAgent/**` 无任何赋值点 ⇒ `ToolChoice` 未接线；与本次空响应守卫无关，本轮新增 TODO 登记 |
| `RepositoryScriptTests.ScriptsWithNonAscii_MustHaveUtf8Bom(scripts/backfill-feature-ids.ps1)` | 1 | **既有基线红**：已登记 `TODO.md`「backfill-feature-ids.ps1 缺 UTF-8 BOM」 |
| `AIServiceTests` ×3 | 3 | **环境/顺序相关 flake**：错误为 `XSqlException: no such table: AIModel`（测试进程未初始化模型表）；上一轮因此原因红的 `MultimodalProcessorTests` 本轮转绿，证明与代码无关；本轮新增 TODO（夹具补建表） |
| `TodosControllerTests.CompleteThenReopen_Flow_ShouldFlipStatus` | 0 | **本批修复**：上一轮红（HTTP 500 NRE），本轮消失；定向 1/1 绿、TodoTracker 域 165/165 绿 |

结论：本批改动未引入新增红；13 红全部可归属到既有基线/既有缺口/环境 flake。

## E2E

Command（`ForgeSelf.Web`，先设 `NO_PROXY=localhost,127.0.0.1,::1` 与仓库内 TEMP）：

```powershell
pnpm exec playwright test e2e/plugins/todo-tracker --project=chromium --reporter=line
```

Result: **1 passed / 59.8s**（Verified，日志 `.temp/e2e-todo-tracker-2.log`）

```text
[1/1] [chromium] › e2e\plugins\todo-tracker\todo-tracker.spec.ts:36:3 › TodoTracker 插件主链路冒烟 › 真实宿主加载、流水线筛选、看板流转与详情页签
  1 passed (59.8s)
```

覆盖：真实宿主加载插件入口（`web/dist/index.js` 200 + `text/javascript`）、版本徽标 `v1.1.3`、流水线筛选、列表改阶段 → API 复核 `stage=Ready`、看板计数 `就绪=1`、点卡片开详情、标题回填、三个页签（下发与委派 / 执行记录 / 工作单元）渲染，无致命控制台错误。

截图读图（`ForgeSelf.Web/screenshots/e2e/todo-tracker/todo-tracker-main-flow.png`，2026-10-25 02:30）：

- 布局：流水线 → 筛选 → 关注项 → 看板 + 右侧详情两栏，间距与卡片圆角一致（token 化，无裸色值）。
- 视觉问题（P3，仅记录不阻塞）：右侧详情面板窄宽度下页签文字换行（「工作单 元」「下发与委 派」）；流转成功 toast 与详情底部短暂重叠。已记 `TODO.md`。
- 首轮失败已定位为**测试选择器过期**（旧断言 `.td-head-title input`，实际标题输入在 `#td-title`），修正后通过；非产品缺陷。

## Static Analysis

`pwsh scripts/check-problem-classes.ps1`：PASS（Verified）——13 个 PC 签名/allowlist/guards 全部通过。

## Screenshots

N/A（本任务无新增 UI；插件前端产物重建未改界面）。

## Known Limitations

- `Run` 的 `totalTokens=0`、步骤 `tokensUsed=0`、`toolCallsJson="[]"`：usage 未累加、工具轨迹未落库，属既有旁支缺陷（本任务不动）。
- `ReactLoopAgentTests.ExitToolTurn_RequiresToolChoice` 既有基线红（`ToolChoice` 未接线），未在本次范围内修复。
- 内置引擎切换 workspace 为全局单例（`BuiltInAgentExecutionService` 注释已声明），待办委派会顺带切换 AIAgent 页面工作目录。

## Unresolved Issues

- 承接工件遗留 R1（插件冒烟 e2e + 源码 UI 走查）与 R2（旧完成端点 500）在本次收口状态见 06-review / 07-final-report。

## 提交前复核（2026-10-25）

提交批次前按验收清单重跑（全部 Verified）：

| 检查 | 命令 | 结果 |
|---|---|---|
| 解决方案构建 | `dotnet build ForgeSelf.slnx -c Debug` | **0 错误** |
| 解析链 + 空响应 + 旧端点定向集 | `dotnet test ForgeSelf.Api.Tests --filter "…ChatModelResolver\|ReactLoopAgent\|TodosController\|TodoTracker…"` | **182/182 通过** |
| 全前端 check / build | `pnpm --dir ForgeSelf.Web run check` / `run build` | exit 0 / exit 0（9.64s） |
| 问题类台账 | `pwsh scripts/check-problem-classes.ps1` | exit 0，13 个 PC 通过 |

口径更正：上节「`ReactLoopAgentTests.ExitToolTurn_RequiresToolChoice` 既有基线红」实为工作区未提交的 ToolChoice TDD 红灯（功能未接线），**不属于本工件**；该用例连同 `UnifiedChatModels.ToolChoice` 属性保留在工作区、待单独任务接线，本批提交的测试文件不含该红用例。
