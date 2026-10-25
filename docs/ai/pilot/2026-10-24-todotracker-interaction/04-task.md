---
task_id: 2026-10-24-todotracker-interaction
feature: 005-todo-tracker
risk: { level: L2, triggers: ["Plugins/TodoTracker/Controllers/TodosController.cs", "Plugins/AIAgent/Services/ReactLoopAgent.cs", "ForgeSelf.Api/Services/AI/Providers/OpenAICompatibleProvider.cs"], raised_by_agent: false }
gate1: { mode: user, at: 2026-10-24, ref: "用户提供原型与交互调整要求" }
gate2: { mode: user, at: 2026-10-25, decision: APPROVED }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/TodoTracker/Controllers/TodosController.cs
  - Plugins/TodoTracker/Services/TaskQuery.cs
  - Plugins/TodoTracker/Services/TodoService.cs
  - Plugins/TodoTracker/web/src/TodoView.vue
  - Plugins/TodoTracker/web/src/actions.ts
  - Plugins/TodoTracker/web/src/store.ts
  - Plugins/TodoTracker/web/src/http.ts
  - Plugins/TodoTracker/web/src/components/TaskDetail.vue
  - Plugins/TodoTracker/web/src/components/ExecutionTimeline.vue
  - ForgeSelf.Api.Tests/Integration/TodosControllerTests.cs
  - ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts
  - ForgeSelf.Abstractions/UnifiedChatModels.cs
  - Plugins/AIAgent/Services/ReactLoopAgent.cs
  - ForgeSelf.Api/Services/AI/Providers/OpenAICompatibleProvider.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs
  - ForgeSelf.Api.Tests/Unit/OpenAICompatibleProviderToolChoiceTests.cs
  - docs/02-features/005-todo-tracker.md
  - docs/ai/pilot/2026-10-24-todotracker-interaction
rollback: "git revert <commit>"
writeback: { feature_doc: done, reason: "docs/02-features/005-todo-tracker.md 已同步本次交互变更" }
---

# Agent 工作单

## 任务编号

TodoTracker 交互调整 · 2026-10-24

## 目标

根据用户提供的 `C:\Users\12504\Desktop\待办.html` 原型，使用现有后端契约调整 TodoTracker 插件交互。

## 范围

### 允许修改

- `Plugins/TodoTracker/web/src/TodoView.vue`
- `Plugins/TodoTracker/web/src/store.ts`
- `Plugins/TodoTracker/web/src/actions.ts` 与 `types.ts`
- `Plugins/TodoTracker/web/src/components/TaskDetail.vue`
- `Plugins/TodoTracker/web/src/components/ExecutionTimeline.vue`
- `Plugins/TodoTracker/Controllers/TodosController.cs`、`Services/TaskQuery.cs`、`Services/TodoService.cs` 及聚焦的 TodoTracker API 测试（为现有列表 API 增加可选优先级筛选）。
- `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts` 与必要的可重复回归测试（插件只保留一条冒烟 e2e）。
- `ForgeSelf.Abstractions/UnifiedChatModels.cs`、`Plugins/AIAgent/Services/ReactLoopAgent.cs`、`ForgeSelf.Api/Services/AI/Providers/OpenAICompatibleProvider.cs` 与聚焦的 AIAgent/提供方测试：计划驱动回合挂载出口工具时，确保请求要求工具调用。
- `docs/11-troubleshooting/`：记录本次模型无工具调用收束问题类及案例。
- TodoTracker 功能文档。
- 只有用户可见行为版本确需更新时，才修改 `Plugins/TodoTracker/plugin.json`。

### 禁止修改

- 数据库结构或持久化；新增 API 路由或更改 TodoTracker 响应契约。
- Home 插件、旧接口语义、宿主设计变量、无关插件及工作区原有改动。
- 将原型模拟的阶段图或示例数据复制进产品代码。
- 未经单独授权向宿主发布、操作用户运行实例，或执行 git 提交/推送。

## 验收清单

- [ ] `02-spec.md` 中 AC1–AC14 全部通过。
- [ ] 阶段选项只依据服务端 `allowedTargets`。
- [ ] 看板采用有界、分列分页；流水线/关注摘要数量准确。
- [ ] 插件仅保留一条冒烟 e2e，相关构建和测试均通过。
- [ ] 减少原有 e2e 场景前，已将必要断言迁移至可重复的非 e2e 测试。
- [ ] 源码宿主走查和截图检查通过，且没有遗留本任务的测试记录。
- [ ] 在隔离源码预览环境中真实下发一条只读任务；真实 Agent 完成并回写记录后，由人工验收到「完成」，刷新后核实服务端终态和完整历史。无真实 Agent 或执行失败时标记 BLOCKED，不得模拟通过。
- [ ] 计划驱动回合挂载出口工具时，上游请求实际包含 `tool_choice: required`，且普通回合维持原行为。
- [ ] 实现后补齐 `05-evidence.md`、`06-review.md` 和 `07-final-report.md`。

## 预期文件

见 `03-plan.md`；另包括实现后的证据、审查、最终报告及一份 TodoTracker 文档更新。

## 验证命令

```powershell
cd Plugins/TodoTracker/web; pnpm run build
dotnet build
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoTracker"
cd ForgeSelf.Web; pnpm exec playwright test --config=playwright.config.ts e2e/plugins/todo-tracker --workers=1 --output=.temp/pw-out-todo-tracker
pwsh scripts/dev-stack.ps1
```

仅在闸门1批准后执行。依照 `AGENTS.md §5.0` 设置 `NO_PROXY` 与仓库本地 `TEMP`；以日志内容作为命令结果证据，不只看退出码。
