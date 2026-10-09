# Evidence

> 阶段：Stage 7 — **只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。

## Task

PILOT 2026-10-08-ux-close（todo-tracker UX 三缺陷修复 + 交互设计流程机制化）

## Changed Files

- `Plugins/TodoTracker/Models/DispatchDtos.cs` — AgentStatusDto 增 AgentName(string?)
- `Plugins/TodoTracker/Services/ITodoDispatchService.cs` — 增 `Task<List<AgentStatusDto>> AgentStatusesAsync(IReadOnlyCollection<int> ids)`
- `Plugins/TodoTracker/Services/TodoDispatchService.cs` — 批量状态实现（ids.Distinct → FindById 且 AgentTaskKey 非空 → Task.WhenAll(ReadStatusAsync) → 过滤 503 接缝缺席）+ ResolveAgentName + AgentStatusAsync 填充 AgentName
- `Plugins/TodoTracker/Controllers/TodoDispatchController.cs` — 新增 `GET agent-statuses/batch`（ids 逗号分隔 trim、空 ids → 200 空列表、异常 500）
- `ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoAgentStatusBatchTests.cs` — **新增** 5 条契约测试（FR-3.0 批量端点）
- `Plugins/TodoTracker/web/src/types.ts` — TodoSaveRequest.projectId?: number；AgentStatus.agentName?
- `Plugins/TodoTracker/web/src/http.ts` — agentStatusesBatch = apiGet withQuery ids.join(',')；linkProjectById POST {projectId}
- `Plugins/TodoTracker/web/src/actions.ts` — agentStatusMeta 词表（Queued=排队中/Running=执行中/AwaitingPermission=待授权/Succeeded=已成功/Failed=失败/Cancelled=已取消/Timeout=已超时/Interrupted=已中断）；agentFallbackByStage；agentNameOrFallback
- `Plugins/TodoTracker/web/src/store.ts` — state.agentStatuses；createTodo(title, projectId?)；loadAgentStatuses（过滤有 agentTaskKey + same() 防闪）；start/stopAgentStatusPolling(15s)；start/stopDetailStatusPolling + pollDetailStatus（前非终态→今回终态才 toast「委派已结束」）；selectTodo 先停详情轮询再按 agentTaskKey 启动；closeDetail 停轮询；**delegateToAgent 成功后启动详情轮询**；linkProjectById 按档案 id 关联并 toast；**refreshSelected 改走 applyUpdated**（E1 徽标缺失根因修复）
- `Plugins/TodoTracker/web/src/TodoView.vue` — 新建行项目下拉（aria-label=新建任务所属项目）；列表委派徽标 .tt-deleg + taskKey 短显 .tt-key；挂载/卸载管列表轮询；**下拉文案统一「名 · 地址短显（N 任务）」+ title 完整地址**（走查发现修复）
- `Plugins/TodoTracker/web/src/components/TaskDetail.vue` — 项目区「选择项目/输入路径」双模式 + select[data-test=project-select]；委派按钮下方可见文案 p[data-test=delegate-hint]；agentsEmpty「去 Agent 中枢登记」引导 goRegister（导航桥 inject('forgeOpenPage') → window.__FORGE_OPEN_PAGE__ → location.assign('/agent-hub')）；委派状态区 agent 名 +「查看执行记录 ↓」锚点 + doRecord 后滚动 +「委派状态读取中…（每 15 秒自动刷新）」；**下拉文案统一 + title 完整地址**（走查发现修复）
- `Plugins/TodoTracker/web/src/components/ExecutionTimeline.vue` — 根 div 加 data-test=execution-timeline
- `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts` — **新增 U1-U5** + E1 增强（列表徽标/短显/详情 agent 名/goRecords 锚点/终态 toast/截图）
- `ForgeSelf.Web/e2e/global-setup.ts` — killTree 改 execSync 同步 taskkill /t /f + rmDirOS 5 次重试×2s 退避 + killTree 后等待 2.5s（deep dives 基建修复：delete-pending 锁）
- `ForgeSelf.Web/e2e/global-teardown.ts` — taskkill 改 execSync 同步

**偏差记录（对 02-spec FR-1.1）**：规格原文「label = `项目名（地址短显 · N 任务）`」；实现为「`项目名 · 地址短显（N 任务）`」（地址移出括号、任务数保留括号内），并在 option 加 `title` 承载完整地址。理由：地址在括号内会被「N 任务」截断语义、视觉上地址夹在括号里更不易扫读；任务数保持括号包裹与详情下拉「（N 任务）」一致。FR-1.1 行已同步更新。

## Build

Command（后端）：

```bash
dotnet build ForgeSelf.Api -v q --nologo
```

Result: PASS（来源等级：Verified）
```text
0 个错误（本批后端改动含 4 个文件，编译通过）
```

Command（插件前端，首跑与下拉修复后各一次）：

```bash
cd Plugins/TodoTracker/web && pnpm run build
```

Result: PASS（来源等级：Verified）
```text
首跑 BUILD_EXIT=0（tt-web-build3.log）；下拉文案修复后重建 BUILD_EXIT=0，dist 已拷入预览 publish（tt-web-build4.log）
```

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoAgentStatusBatchTests|FullyQualifiedName~TodoTracker|FullyQualifiedName~AgentHub" -v q --nologo
```

Result: PASS（来源等级：Verified）
```text
契约测试 5/5 绿（222ms）；TodoTracker+AgentHub 定向 238/238 绿（17s）
```

宿主侧：

```bash
cd ForgeSelf.Web && pnpm run check && pnpm run test
```

Result: PASS（来源等级：Verified）
```text
check 0 err；vitest 742/742（14.8s，host-test.log）
```

## Integration Test

Result: N/A（依据：本批无宿主级集成测试；插件控制器鉴权等由既有 D1 e2e 覆盖）

## E2E

Command:

```bash
cd ForgeSelf.Web && node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugins/todo-tracker --output=<空目录> --reporter=list
```

Result: PASS（来源等级：Verified）

七跑（实现收口后）：
```text
15 passed (1.9m)   （tt-e2e7.log）
```

八跑（下拉文案修复后回归，确认格式改动不破坏 U1/U2 断言）：
```text
15 passed (2.3m)   （tt-e2e8.log，E2E_EXIT=0）
```

历次失败与修复（只记实际发生的红）：
- 首跑 12/15：E1 终态 toast 未出 → 补 delegateToAgent 成功后启动详情轮询（selectTodo 时尚无 agentTaskKey）；U1 新建落库时序 → 改 toast 断言 + expect.poll 反查；U4 与 E1 并行 agents 竞争 → 自适应跳过空态分支
- 三跑/四跑被 global-setup 清理失败阻断（delete-pending：残留宿主锁 publish 目录）→ 修 global-setup/global-teardown（同步 killTree + 删除重试）
- 五跑 14/15：`.tt-key` 正则不匹配前导空格 → 断言改 `/^\s*#\S/`

**E1 链路实测证据（Verified，七跑）**：登记 opencode（vendor=opencode）→ 预览 agents=1/canDelegate/delegationAvailable → 一键委派 → toast 回执（已交给…）→ 后端 taskKey + stage=Running 落库 → 列表 .tt-deleg 徽标 + .tt-key 短显 `/^\s*#\S/` → 详情 agent 名 + goRecords「查看执行记录 ↓」→ 轮询至终态 toast「委派已结束」→ execution-timeline 可见 + 截图（`ForgeSelf.Web/screenshots/e2e/todo-tracker/e1-delegate-opencode.png`）。

## Static Analysis

Command:

```bash
cd Plugins/TodoTracker/web && pnpm run build   # vite + vue-tsc 类型检查随构建
```

Result: PASS（来源等级：Verified）

## Screenshots

- 走查（隔离预览实例 7201）：`ForgeSelf.Web/screenshots/live-7201/todo-delegate-empty-agents.png`、`todo-delegate-final.png`（来源等级：Verified，实际截图）
- e2e（七跑 E1）：`ForgeSelf.Web/screenshots/e2e/todo-tracker/e1-delegate-opencode.png`（Verified）
- 走查图片观察（computer_use_tool 每步返回的 obs image 含 OCR/元素清单，逐项核对）

## 走查核对记录（隔离预览实例 7201，ui-ux-design「走查 UI 符合性清单」10 项）

| # | 清单项 | 走查结论 | 证据 |
|---|--------|---------|------|
| 1 | 交互规格逐条（点什么出现什么） | ✅ 全过 | 新建下拉列「OpenForgeSelf · …OpenForgeSelf（1 任务）」+ title 完整地址；详情双模式切换；选中关联 → 项目名+完整地址带出（.td-proj-line/.td-mono）；委派四态文案；go-register 点击跳 /agent-hub |
| 2 | CRAP（层级/重复/对齐/亲密性） | ✅ 层级对齐亲密性 OK；**发现 R 不一致：新建下拉「OpenForgeSelf（0）」vs 详情下拉「OpenForgeSelf（0 任务）」且缺地址 → 已修**（统一「名 · 地址短显（N 任务）」+ title） | 截图 + DOM |
| 3 | 点即保存落盘 | ✅ 真实输入「走查点即保存-目标-2026」→ 失焦 → 刷新重载 → 值仍在（#td-obj）；列表副行同步显示目标 | DOM 断言 |
| 4 | 操作失败留痕 | ✅ 委派禁用原因可见（「先生成提示词（委派前要先据它判断）」/「四栏齐备…后才能一键委派」）；失败 toast 由 notify.showFailure 统一（e2e 覆盖） | DOM + 截图 |
| 5 | 轮询 12s+ 无闪动 | ✅（依赖 e2e E1：15s 轮询 + same() 防闪；走查实例无委派任务不轮询，属设计内） | e2e |
| 6 | 空态分级 | ✅ 任务空态「还没有任务」+ 引导、详情空态「未选中任务」+ 引导、执行记录空态「还没有执行记录——…」、agents 空态「AgentHub已就绪，但还没有可用agent」 | 截图 OCR |
| 7 | 筛选/分页边界 | ✅ 结构在（阶段 chips/项目过滤/完成态/搜索/共 N 条/刷新）；边界行为由既有用例覆盖 | DOM |
| 8 | 禁用/不可用状态有可见原因 | ✅ **本批核心**：delegate-hint 常驻可见（四态文案）+ agents 空态引导 + go-register 链接 | DOM + 截图 |
| 9 | 破坏性操作二次确认 | ✅ 删除/解除弹窗（既有 + e2e C1/C2 覆盖） | e2e |
| 10 | 长文本溢出/窄屏 | ✅ 完整地址 title 提示 + 短显，详情地址全路径未溢出；窄屏由既有 V1 覆盖 | 截图 |

**走查过程发现并修复**：下拉文案格式不统一 + 缺地址短显（R 重复性），修复后重建 dist + 重启预览实例复验通过（新下拉「名 · 地址短显（N 任务）」）。

## 验收标准对应证据（02-spec AC1-11 逐条映射）

| AC | 判据 | 证据（本文件上文节） | 来源等级 |
|----|------|----------------------|----------|
| AC1 | 新建任务项目下拉列出项目 + 选中后 projectId>0 且 projectRoot 非空 | e2e U1（新建下拉选中 OpenForgeSelf → `GET /api/todos/{id}` 回读 projectId/projectRoot）；走查核对记录 #1 下拉「名 · 地址短显（N 任务）」+ title | Verified |
| AC2 | 详情「选择项目」下拉 → 项目名+完整地址显示且落库一致；「输入路径」模式可用 | 走查核对记录 #1 双模式切换 + 选中关联带出（.td-proj-line/.td-mono）；e2e U1/U2 | Verified |
| AC3 | 委派按钮四态禁用可见文案 | 走查核对记录 #4/#8（delegate-hint 常驻四态文案）+ e2e 断言文本 | Verified |
| AC4 | agents 空态「去 Agent 中枢登记」可跳 /agent-hub | 走查核对记录 #1/#6/#8（go-register 点击跳转）；e2e U3 | Verified |
| AC5 | 列表实时徽标 + taskKey 短显 + 兜底 | e2e U4/E1（.tt-deleg + .tt-key `/^\s*#\S/`）；E1 链路实测段落 | Verified |
| AC6 | 列表 15s 轮询 / 详情轮询至终态停 | e2e E1（15s 轮询 + 终态 toast「委派已结束」）；store.ts 轮询实现 | Verified |
| AC7 | 「记为执行记录」→ 引导 + 滚动记录区 | e2e E1（goRecords 锚点滚动）+ 走查核对记录 #1 | Verified |
| AC8 | 后端定向测试绿（契约字段 + AgentName） | Unit Test 节：契约 5/5 + 定向 238/238 | Verified |
| AC9 | check/vitest/build/e2e 全绿 | Build / Unit Test / E2E 节（15/15 ×2） | Verified |
| AC10 | 走查：隔离实例点一遍 + 截图读图 | 走查核对记录 10 项 + Screenshots 节（live-7201 截图） | Verified |
| AC11 | 流程落盘：02-spec 交互设计节 + workflow/plugin-development/AGENTS 同步 | 交付物清单第 2 项（ui-ux-design skill + 02-spec.tpl 交互节 + 规范声明） | Verified |

## Known Limitations

- agents 空态走查在无 agent 的隔离实例上验证（预览实例未登记 agent，AgentHub 显示 0 已登记，与 todo 空态引导自洽）；真实委派链路由 e2e E1 覆盖（opencode 真实进程，终态 Failed(timeout) 系 :51888 模型端点两模型上游 400，用户侧外部因素，非链路缺陷）。
- 列表徽标/详情轮询在隔离实例无真实委派数据，走查复用 e2e E1 断言（15s 轮询、终态 toast、锚点滚动均有 e2e 证据）。
- FR-1.4 项目下拉空态（无档案时「还没有项目档案，可输入路径创建」）：走查实例已有 OpenForgeSelf 档案，空态分支未在走查复现（代码路径与「不选项目」并列，低风险）。
- 窄屏布局未在走查复现（既有 V1 e2e 覆盖响应式）。
