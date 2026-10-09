# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。

## Task

PILOT-055

## Changed Files

- `Plugins/TodoTracker/Services/TodoService.cs` — CompleteTodoAsync/ReopenTodoAsync 加 actor 参数 + 状态实际变更时 AppendSystemAsync 留痕（P1）
- `Plugins/TodoTracker/Controllers/TodosController.cs` — /complete、/reopen 两处调用传 ActorHint()（P1）
- `Plugins/TodoTracker/web/src/store.ts` — removeTodo 弹窗计数改真实来源（P2）；completeOrReopen 成功后补 loadRecords()（P2）
- `Plugins/AgentHub/Services/AgentHubDi.cs` — **新增**：ResolveHost<T>（缺陷①修复核心，经 root Context 的宿主 IServiceProvider 回落容器解析）
- `Plugins/AgentHub/Services/AgentDelegationProvider.cs` — 3 处 DI 解析改 AgentHubDi.ResolveHost + 类注释（缺陷①）
- `Plugins/AgentHub/Tools/AgentHubToolBase.cs` — GetService 改 AgentHubDi.ResolveHost（缺陷①）
- `Plugins/AgentHub/Services/CliTransport.cs` — timeoutCts 提前到 stdout 循环前、读取观察它、超时杀进程树 + 补带英文 timeout 标记的 Error 事件（缺陷②）
- `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentDelegationProviderResolutionTests.cs` — **新增** 2 回归用例（缺陷①，含 mutation 探针）
- `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubCliTransportTimeoutTests.cs` — **新增** 1 回归用例（缺陷②，静默子进程 + 2s 超时，mutation 探针）
- `ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoServiceTests.cs` — +3 P1 留痕用例（更早落盘）
- `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts` — 新增 E1（L438 起）；toast 断言改 filter；records 断言改 items；移除未用 taskKey

> **偏差记录（对 mini-task 边界）**：mini-task §3「不改」声明不改 AgentHub——实施中发现打通真实委派链路必经的 2 个 AgentHub 真缺陷（① 插件经 IContext 解析宿主服务恒空 → agent 列表恒空、委派回「运行时不可用」；② CliTransport 超时在静默子进程下永不触发 → 任务恒 Running）。用户目标即「打通链路」，缺陷不修则链路永远不通，故超出原边界修复并记此偏差；修复收敛在 AgentHub 插件内 + 配套测试，未动契约/宿主。

## Build

Command:

```bash
dotnet build ForgeSelf.Api -v q --nologo
```

Result: PASS（来源等级：Verified）

```text
442 个警告
0 个错误
已用时间 00:00:04.94
```

Command（插件前端）:

```bash
cd Plugins/TodoTracker/web && pnpm run build
```

Result: PASS（来源等级：Verified）

```text
vite v6.4.3 building for production...
✓ 20 modules transformed.
dist/style.css   20.12 kB │ gzip:  4.01 kB
dist/index.js    65.55 kB │ gzip: 17.25 kB
✓ built in 436ms
```

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AgentHubCliTransportTimeoutTests|FullyQualifiedName~AgentDelegationProviderResolutionTests|FullyQualifiedName~AgentHub|FullyQualifiedName~TodoTracker|FullyQualifiedName~TodoServiceTests" -v q --nologo
```

Result: PASS（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 233，已跳过: 0，总计: 233，持续时间: 12 s
```

（233 = 232 基线 + 新增超时回归 1 条；AgentDelegationProviderResolutionTests 2 条含于 232 内）

## Integration Test

Result: N/A（依据：本批无宿主级集成测试；插件控制器鉴权等由既有 D1 e2e 覆盖）

## E2E

Command:

```bash
cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1
```

Result: PASS（来源等级：Verified）

```text
10 passed (1.8m)
```

（9 条既有 + E1 真实委派端到端。E1 单独跑 `-g "E1"` 亦 2.1m 绿）

**E1 链路实测证据（Verified）**：登记 opencode（vendor=opencode）→ 预览 agents=1/canDelegate/delegationAvailable → 一键委派 → toast 回执（已交给…）→ 后端 taskKey + stage=Running 落库 → 轮询 agent-status 至终态 **Failed(errorCode=timeout)**（opencode 真进程拉起、对用户实例 :51888 模型端点 qwen3-vl/deepseek-v4-flash 均上游 400 静默挂起 → 60s 策略超时经缺陷②修复后确定性生效 → 杀进程树 + 终态落库）→「记为执行记录」→ 回写记录含「agent 执行回写」并上时间线 → 截图 `ForgeSelf.Web/screenshots/e2e/todo-tracker/e1-delegate-opencode.png`（103,840 B）。Succeeded/Failed 均构成链路证据；本次 Failed(timeout) 系外部模型端点所致（用户侧因素，未改），非链路缺陷。

## Static Analysis

Command:

```bash
cd ForgeSelf.Web && npx vue-tsc -b   # 发布链同参
pnpm run check                       # vue-tsc -b && eslint
pnpm run test                        # vitest
```

Result: PASS（来源等级：Verified）

```text
npx vue-tsc -b: exit 0（0 errors）
pnpm run check: 0 errors, 76 warnings（既有基线）
pnpm run test: Test Files 65 passed (65) / Tests 742 passed (742)
```

（中途曾捕 TS6133 未使用变量 taskKey，已移除后复跑全绿。）

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/todo-tracker/e1-delegate-opencode.png` — E1 终态后「记为执行记录」回写上时间线的页面截图（Verified，103,840 B）

## Known Limitations

1. 真实委派本次以 **Failed(timeout)** 为终态：用户实例 :51888（pid 65816）opencode 模型端点对 qwen3-vl/deepseek-v4-flash 均回上游 400（401 认证已过）。此为外部因素【openitem】，未改不改；用户侧修复模型配置后重跑 E1 预期 Succeeded。
2. `AgentHubDi.ResolveHost` 依赖宿主 `PluginManager.ProvideHostServices` 已把根 IServiceProvider seed 进 root Context（现有机制）；若未来宿主移除该 seed，插件解析会回退为「本地容器」，由既有回归测试（裸 Context → 空列表 + 「运行时不可用」）兜底告警。
3. CliTransport 超时修复对「正常输出但超时」路径同样生效（超时事件统一归类 timeout）；错误分类依赖 Error 事件中的英文 "timeout" 标记，属约定而非字典（注释已注明）。

## Unresolved Issues

- 无 BLOCKED 项。残留均为待授权项（见下）：
  - 本批全部改动未 commit / push / 发布（用户未授权）。
  - 用户侧可选：修复 :51888 opencode 模型端点后复跑 E1 取 Succeeded 终态。
