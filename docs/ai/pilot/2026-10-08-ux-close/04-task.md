---
task_id: 2026-10-08-ux-close
feature: 054-todo-agent-dispatch          # 对应 docs/02-features；todo-tracker 委派链功能域
risk: { level: L2, triggers: ["Plugins/TodoTracker/web/src/components/TaskDetail.vue", "Plugins/TodoTracker/web/src/store.ts", "Plugins/TodoTracker/Models/DispatchDtos.cs"], raised_by_agent: true }
gate1: { mode: user, at: 2026-10-08, ref: "用户回复「B」= 批准方案并选定列表实时状态（新增批量状态接口）" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/TodoTracker/web/src/TodoView.vue
  - Plugins/TodoTracker/web/src/components/TaskDetail.vue
  - Plugins/TodoTracker/web/src/store.ts
  - Plugins/TodoTracker/web/src/types.ts
  - Plugins/TodoTracker/web/src/actions.ts
  - Plugins/TodoTracker/Models/DispatchDtos.cs
  - Plugins/TodoTracker/Services/TodoDispatchService.cs
  - Plugins/TodoTracker/Controllers/TodoDispatchController.cs
  - ForgeSelf.Api.Tests/Plugins/TodoTrackerTests/
  - ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts
  - docs/18-templates/ai-pilot/02-spec.tpl.md
  - docs/04-standards/ai-native-engineering-workflow.md
  - .agents/skills/plugin-development/SKILL.md
  - AGENTS.md
rollback: "git checkout -- <上述文件>（未提交，工作区可回退）"
writeback: { feature_doc: pending, reason: "todo-tracker 功能文档交互说明随收口更新" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 Agent 可直接执行的工作单元，零自我决策空间。
> 前序工件：00/01/02/03 齐备且经闸门 1 确认。

## Task ID

2026-10-08-ux-close

## Objective

修好 todo-tracker 三个 UX 缺陷（项目选择 / 委派禁用提示 / 进度可见），并把「交互设计」固化为开发工件必写环节；全部门禁绿 + 走查读图通过。

## Scope

### Allowed



* 03-plan.md Files To Change 列出的全部文件（前端 5、后端 3、契约测试、e2e、流程文档 4）。

* 交互设计节写入本批 02-spec（示范）与 02-spec.tpl.md 模板。

* 契约测试字段清单同步（AgentStatusDto.AgentName）。

* 新增批量委派状态接口 `GET /api/todos/agent-statuses/batch`（用户闸门1 选 B，列表实时徽标数据源）。

### Forbidden



* 不改 AgentHub 插件自身 UI / 逻辑；不改委派接缝后端语义（IsAvailable/SeamNotAvailableMessage/CanDelegate）。

* 除 agent-statuses/batch 外不新增其它批量接口；不新增前端测试基建；不新增依赖。

* 不碰宿主代码（导航桥仅消费既有 `forgeOpenPage` / `window.__FORGE_OPEN_PAGE__`）。

* 不碰用户运行实例（:51888）；不 git commit/push；不停 / 启 / 杀宿主进程。

## Acceptance Criteria



* [ ] AC-1 新建任务项目下拉列出宿主项目；选中创建后 GET /api/todos/{id} 的 projectId>0 且 projectRoot 非空。

* [ ] AC-2 详情「选择项目」选中后页面显示项目名 + 完整地址且落库一致；「输入路径」模式仍可用。

* [ ] AC-3 委派按钮四种禁用态下按钮下方可见对应文案（e2e 断言文本，非 title）。

* [ ] AC-4 agents 空态显示「去 Agent 中枢登记」引导 + 可跳 /agent-hub。

* [ ] AC-5 列表行已委派任务显示**实时委派状态徽标**（批量接口：排队/执行中/成功/失败/超时 + agent 名，缺失按阶段兜底）+ taskKey 短显。

* [ ] AC-6 列表打开时对已委派任务 15s 轮询批量接口刷新徽标（卸载停止）；详情委派区显示 agent 名、非终态 15s 自动刷新至终态停、手动刷新可用。

* [ ] AC-7 「记为执行记录」成功后引导并滚动到执行记录区。

* [ ] AC-8 后端定向测试绿（含契约字段清单 + AgentName 断言）。

* [ ] AC-9 宿主 check/vitest 绿、vue-tsc -b 0err、插件前端 build 过、todo e2e 全量绿（含既有 10 条不回归）。

* [ ] AC-10 隔离实例走查截图读图（交互清单逐项）。

* [ ] AC-11 流程文档 4 处落盘（模板 / 规范 / 技能 / AGENTS.md）。

## Expected Files



* Plugins/TodoTracker/web/src/{TodoView.vue, components/TaskDetail.vue, store.ts, types.ts, actions.ts}

* Plugins/TodoTracker/Models/DispatchDtos.cs

* Plugins/TodoTracker/Services/TodoDispatchService.cs

* Plugins/TodoTracker/Controllers/TodoDispatchController.cs

* ForgeSelf.Api.Tests/Plugins/TodoTrackerTests/（契约测试改动 / 新增）

* ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts

* docs/18-templates/ai-pilot/02-spec.tpl.md

* docs/04-standards/ai-native-engineering-workflow.md

* .agents/skills/plugin-development/SKILL.md

* AGENTS.md

## Verification Commands



```
# 后端（环境前置：TEMP/TMP/NO_PROXY 每命令内设，判据看日志正文）
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TodoTracker"

# 宿主前端
cd ForgeSelf.Web && pnpm run check && pnpm run test && npx vue-tsc -b

# 插件前端
cd Plugins/TodoTracker/web && pnpm run build

# e2e（零 mock；地址真源 e2e/helpers/e2e-env.ts）
cd ForgeSelf.Web && npx playwright test e2e/plugins/todo-tracker --workers=1
```