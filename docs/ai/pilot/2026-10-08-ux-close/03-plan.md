# Plan

> 阶段：Stage 3｜具体到真实文件路径。
> Task ID：2026-10-08-ux-close

## Files To Change

### 前端（todo-tracker web）
- `Plugins/TodoTracker/web/src/TodoView.vue`
  reason: 新建任务行加「项目」下拉（FR-1.1，create 传 projectId）；列表项加**实时委派状态徽标** + taskKey 短显（FR-3.1/3.2，数据 = store 批量轮询结果，兜底阶段推断）。
- `Plugins/TodoTracker/web/src/components/TaskDetail.vue`
  reason: 项目区加「选择项目」模式（FR-1.2/1.4）；委派按钮下方可见原因文案 + agents 空态引导 + 去登记链接（FR-2.1/2.2）；委派区 agent 名 + 「查看执行记录」锚点 + 记为执行记录后滚动（FR-3.3/3.4）。
- `Plugins/TodoTracker/web/src/store.ts`
  reason: `createTodo` 增加 projectId 参数（FR-1.1）；`loadAgentStatus` 后置 agent 名（配合 DTO）；新增 **列表批量轮询**（`loadAgentStatuses(ids)` → `state.agentStatuses: Record<taskId, AgentStatus>`，15s 定时器，挂载/刷新/卸载生命周期）（FR-3.2）；详情页单条轮询生命周期（start/stop，15s，终态停 + toast）（FR-3.3）；selectTodo/closeDetail 停详情轮询。
- `Plugins/TodoTracker/web/src/types.ts`
  reason: `TodoSaveRequest` 加 `projectId?: number`；`AgentStatus` 加 `agentName?: string | null`（FR-3.3）。
- `Plugins/TodoTracker/web/src/actions.ts`
  reason: 新增纯函数：`agentStatusLabel(status)` / 实时徽标映射与阶段兜底（FR-3.1），供 TodoView/TaskDetail 与 e2e 断言共用。

### 后端（todo-tracker）
- `Plugins/TodoTracker/Models/DispatchDtos.cs`
  reason: `AgentStatusDto` 加 `AgentName`（string?）（FR-3.3）。
- `Plugins/TodoTracker/Services/TodoDispatchService.cs`
  reason: `AgentStatusAsync` 填 `AgentName`（按 todo.AgentId 从 gateway.AvailableAgents() 解析，未知→null）；新增 **`AgentStatusesAsync(IEnumerable<int> ids)`** 批量（仅查有 agentTaskKey 的任务，Task.WhenAll 并行，填充 AgentName）（FR-3.0）。
- `Plugins/TodoTracker/Controllers/TodoDispatchController.cs`
  reason: 新增 `GET /api/todos/agent-statuses/batch?ids=1,2,3` 端点（校验 ids、接缝缺席返回空列表不报错、异常 500）（FR-3.0）。

### 测试
- `ForgeSelf.Api.Tests/Plugins/TodoTrackerTests/`（契约测试）
  reason: AgentStatusDto 字段清单同步 + AgentName 填充断言 + **AgentStatusesAsync 批量用例**（空 ids、混合有/无委派、接缝缺席返回空、名称填充、并行结果一致）（FR-3.0/3.3、Compatibility）。
- `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`
  reason: 新增交互用例：AC-1 新建选项目自动关联；AC-3 委派禁用可见文案（四态）；AC-4 agents 空态引导；AC-5 列表实时徽标（造委派记录 → 批量接口 → 徽标）；AC-6 轮询（详情 15s 刷新至终态停 / 列表批量轮询刷新）。

### 流程文档（FR-4）
- `docs/18-templates/ai-pilot/02-spec.tpl.md`：加「交互设计」节（模板占位）。
- `docs/04-standards/ai-native-engineering-workflow.md`：Spec 定义处声明交互设计必写（一行级）。
- `.agents/skills/plugin-development/SKILL.md` §3.4：交互设计落进 Spec + 走查必验 + e2e 覆盖交互路径（强化措辞）。
- `AGENTS.md` §11：Spec 必含交互设计节一句。

## Implementation Steps

1. 后端：DispatchDtos.cs 加 AgentName → TodoDispatchService.AgentStatusAsync 填充（todo.AgentId → gateway.AvailableAgents() 匹配 name；未知 null）；新增 AgentStatusesAsync(ids)（仅查 agentTaskKey 非空，Task.WhenAll 并行）。
2. 后端控制器：新增 GET /api/todos/agent-statuses/batch?ids=（ids 解析与校验：空/非法 → 400 或空列表；接缝缺席 → 空列表不报错；异常 500）。
3. 契约测试：字段清单加 AgentName；AgentStatusesAsync 批量用例（空/混合/接缝缺席/名称/并行）。
4. 前端 types：TodoSaveRequest.projectId、AgentStatus.agentName。
5. actions.ts：`agentStatusLabel(status)` + 阶段兜底映射纯函数。
6. store.ts：createTodo(title, projectId?)；列表批量轮询（`loadAgentStatuses` → state.agentStatuses，15s 定时器 + 挂载/卸载/刷新生命周期）；详情单条轮询（start/stop，终态停 + toast）；selectTodo/closeDetail 清详情定时器。
7. TodoView.vue：新建行加项目下拉（复用 projectOptions）；create 传 projectId；列表项 `task.agentTaskKey` 非空时渲染实时徽标（agentStatuses 数据 → agentStatusLabel，缺失按 stage 兜底）+ taskKey 短显；onMounted/onUnmounted 管理列表轮询。
8. TaskDetail.vue：项目区「选择项目 / 输入路径」模式切换；委派按钮下方 delegateHint 可见文案；agents 空态引导 + forgeOpenPage('/agent-hub')；委派状态区 agent 名 + 「查看执行记录」锚点；recordAgentResult 成功后滚动。
9. 插件前端 build（`pnpm run build`）。
10. e2e 新增用例（零 mock；列表徽标/批量轮询用真实委派路径——登记 opencode → 委派 → 断言徽标；复用 E1 setup）。
11. 流程文档 4 处更新（FR-4）。
12. 走查：隔离实例浏览器点交互清单 + 截图读图（.temp 起实例，避 7102，数据根隔离）。
13. 收口：05-evidence / 06-review / 日志 / TODO。

## Test Plan

1. 后端：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoTracker"`（含契约字段清单 + AgentName 填充新增断言）。
2. 宿主前端：`pnpm run check` + `pnpm run test`（vitest 742 基线不回归）；`npx vue-tsc -b`（发布链同参）。
3. 插件前端：`cd Plugins/TodoTracker/web && pnpm run build`。
4. e2e：`cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1`（既有 10 条 + 新增交互用例）。
5. 走查：隔离实例浏览器交互清单逐项核对 + 截图读图。

## Verification

### Build
```bash
cd ForgeSelf.Api && dotnet build        # 0 error
cd Plugins/TodoTracker/web && pnpm run build   # dist OK
```

### Unit Test
```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TodoTracker"   # 含新增 AgentName 断言，全绿
cd ForgeSelf.Web && pnpm run test       # 742 基线不回归
```

### Integration Test
```bash
# N/A：插件层集成验证走 e2e（零 mock，e2e-testing 铁律）
```

### E2E
```bash
cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1
# 环境前置：NO_PROXY/TEMP 每命令内设；地址真源 e2e/helpers/e2e-env.ts
```

### Other Checks
```bash
cd ForgeSelf.Web && npx vue-tsc -b && pnpm run check   # 发布链同参 + lint（0 err）
# 走查：隔离实例截图读图（交互清单 AC-1..AC-7 逐项）
# 流程文档核对：02-spec.tpl.md 含交互设计节等 4 处落盘
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
