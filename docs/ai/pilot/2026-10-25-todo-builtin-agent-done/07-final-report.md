# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜状态只报事实（对齐 AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

我确认了：

- 真实下发链路 = TodoTracker `POST /api/todos/{id}/dispatch-to-agent` → `TodoDispatchService.DelegateToBuiltInAsync` → `BuiltInAgentExecutionService.StartAsync` → `RunOrchestratorService`（规划 + 逐步 `ReactLoopAgent`）。
- 模型解析原为硬编码 `default:ornith-1.0-9b`（模型表不存在）+ `AIAgentService.ResolveProvider` 静默回退默认 provider；空流被当作「本步成功」⇒ 029 判 Stuck。
- `ResumeAsync` 不持久化 ChatModelId，恢复时退化到宿主默认模型（本机 `qwythos-9b-v2`，实测空流）。
- 插件容器可用宿主契约：`IConfigurationService.GetAIConfig()`、`ITurnTelemetryQuery.GetModelCatalogAsync()`（仅返回启用模型）；契约必须进 `PluginManager.HostProvidedServiceContracts` 才能被插件 `ctx.Get` 解析。

## 2. Selected Task

2026-10-25-todo-builtin-agent-done —— 让「一键交给本工具 AI Agent」（builtin）在无显式模型时也能真实执行到 Done，无可用模型时明确失败。

## 3. Changed Files

- `Plugins/AIAgent/Services/ChatModelResolver.cs`（新增：`IChatModelResolver`，四级有序解析链）
- `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs`（删硬编码模型，未命中 `Fail`）
- `Plugins/AIAgent/Services/RunOrchestratorService.cs`（Run/Resume 统一走解析链）
- `Plugins/AIAgent/Services/ReactLoopAgent.cs`（空响应判 `TurnFailed`）
- `Plugins/AIAgent/AIAgentPlugin.cs`（注册 resolver）
- `ForgeSelf.Api/Plugins/PluginManager.cs`（契约表补 `ITurnTelemetryQuery`）
- `Plugins/TodoTracker/Controllers/TodosController.cs` / `TaskExecutionsController.cs` / `TodoDispatchController.cs`（R2：`ActorHint()`/基址拼接空安全）
- `ForgeSelf.Api.Tests/Plugins/AIAgent/ChatModelResolverTests.cs`（新增 4 条）、`ReactLoopAgentTests.cs`（空响应用例）
- `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`（过期选择器 `.td-head-title input` → `#td-title`）
- `docs/11-troubleshooting/cases/2026-10-25-builtin-agent-silent-model-fallback.md`、`problem-classes/PC-013-*.md`、两份 README 索引
- `docs/02-features/005-todo-tracker.md`（补内置引擎模型解析约束）
- `docs/ai/pilot/2026-10-25-todo-builtin-agent-done/`（本工件）

## 4. Validation

Build: PASS（Verified）——`dotnet build ForgeSelf.Api.Tests` 0 错误；`pnpm --dir Plugins/TodoTracker/web run build` 20 modules / dist/index.js 96.97 kB / 517ms。

Unit Test: PASS（Verified）——定向过滤集 37/38（唯一红为既有 `ToolChoice` 基线）；R2 定向 1/1；TodoTracker 域 165/165；**全量中档门禁 3170 通过 / 13 失败 / 3183 / 13m48s**，13 红逐条归属见 `05-evidence.md`「Full Suite (medium gate)」（6 WorkflowPlanning 404 基线 + 1 ScriptRunnerDi 同族 + 2 ToolChoice 未接线 + 1 BOM 基线 + 3 AIModel 表顺序 flake；**R2 红已消失，无新增红**）。

E2E: PASS（Verified）——`e2e/plugins/todo-tracker` 1 passed / 59.8s；截图 `ForgeSelf.Web/screenshots/e2e/todo-tracker/todo-tracker-main-flow.png` 已读图（P3 视觉小问题已记 TODO）。

真实下发（隔离实例 `:7301`）: PASS（Verified）——待办 10 → `ok=true, taskKey=run:1` → Run 1 `status=3 Completed`、两步 `outputJson` 非空 → 台账 4 条（Ready→Dispatched→Running→Review→Done）→ `stage=Done / status=Completed`；失败路径（停用全部模型）→ HTTP 400 明确文案，待办保持 Ready。

## 5. Evidence

见 `05-evidence.md`（全部标 Verified）：构建输出、定向/全量测试数字、e2e 行、真实下发接口响应与台账、模型解析日志、失败路径响应、静态守卫 `check-problem-classes.ps1` PASS。

## 6. Review

`06-review.md` → **APPROVED**（八问逐条回答；Findings 仅 3 条 Minor：视觉模型可能被选为「目录首个启用」、AIModel 表顺序 flake、ToolChoice 未接线）。

## 7. Risk

L1 —— 低风险。残余：宿主默认模型不在启用目录时选「目录首个启用模型」，可能是视觉模型，依赖上游转向（本次实测成功，失败会落到空响应明确失败）。

## 8. Problems Found

1. 模型硬编码 + 静默降级 → 空响应被当成功（PC-013，已建档 + 守卫）。
2. `ResumeAsync` 丢模型（同 PC-013，已走解析链）。
3. 三处 `ActorHint()`/基址拼接在单元直构控制器下 NRE → 500（R2，同类扫查 3 文件，已修）。
4. 宿主契约 `ITurnTelemetryQuery` 未进共享表（PC-001 族，已补）。
5. e2e 选择器过期（`.td-head-title input` → `#td-title`，测试侧问题）。
6. 全量门禁 13 红归属：6 基线 404 + 1 同族 404 + 2 ToolChoice + 1 BOM + 3 DB 表顺序 flake（全部已入 TODO，非本批引入）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

- **PC-013（新建，首见）**：`docs/11-troubleshooting/problem-classes/PC-013-silent-model-fallback-empty-response.md`
- **case**：`docs/11-troubleshooting/cases/2026-10-25-builtin-agent-silent-model-fallback.md`（11 节完整档案，含 5 Whys + 同类扫查 7 条 + 守卫 G1/G2）
- 索引已同步：`problem-classes/README.md`（tags 表 + 台账）、`cases/README.md`（时间线）
- 机器校验：`pwsh scripts/check-problem-classes.ps1` exit 0（13 个 PC 全过）

## 11. 重构提案

**R0（不重构）**：模型选择已收敛到单点 `ChatModelResolver`，再抽「模型选择网关」只会增加一层间接；`ActorHint()` 是三个控制器各自的请求上下文读取，抽公共基类收益不足（改动 3 行级别且语义稳定）。待出现第四个同形控制器或需要统一鉴权主体解析时再议（触发条件）。

## 12. 悬空教训清单（必须为空）

- 空。

（本轮发现的每条问题均已落成 PC-013 / case 文件，或在 `TODO.md` 有逐字可检索条目：ToolChoice 未接线、AIModel 表 flake、ScriptRunnerDi 404、详情页签换行、backfill BOM、WorkflowPlanning 6 红。）

## 交付状态

- 代码与工件：已完成并验证（本文件 + 05/06）。
- **发布与复验（2026-10-25 用户批准后完成）**：本地目录出包 `2.4.14.2610250925`（签名有效 / SHA256 一致 / L1-L2 通过），`:51888` 经 update-agent 自更新；只读复验通过（`todo-tracker`=1.2.0、`ai-agent`=1.9.5、徽标 v1.2.0、主链路与中间态实走 + 6 张截图读图）。
- git 提交：版本 bump 与文档更新尚在工作区（提交待用户批准），前序代码批次已入库（`b4fdd25` / `9a0d915` / `5753760`）。
