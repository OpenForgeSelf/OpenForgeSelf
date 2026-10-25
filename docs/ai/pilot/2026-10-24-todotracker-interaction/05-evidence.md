# 验收证据

> 阶段：Stage 7｜仅记录实际发生的事项。每项注明证据等级。

## 任务

根据待办原型调整 TodoTracker 交互，并验证真实任务从下发到完成的闭环。

## 变更文件

- `Plugins/TodoTracker/Controllers/TodosController.cs`
- `Plugins/TodoTracker/Services/TaskQuery.cs`
- `Plugins/TodoTracker/Services/TodoService.cs`
- `Plugins/TodoTracker/web/src/TodoView.vue`
- `Plugins/TodoTracker/web/src/actions.ts`
- `Plugins/TodoTracker/web/src/store.ts`
- `Plugins/TodoTracker/web/src/http.ts`
- `Plugins/TodoTracker/web/src/components/TaskDetail.vue`
- `Plugins/TodoTracker/web/src/components/ExecutionTimeline.vue`
- `ForgeSelf.Api.Tests/Integration/TodosControllerTests.cs`
- `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`
- `docs/02-features/005-todo-tracker.md`

## 构建

- 插件前端构建：此前版本已通过（Verified）；最后调整详情标题后未能复跑。
- 本轮标准复跑 `pnpm run build` 被工作目录访问拒绝（Verified）。直接调用 Vite 又报告 `src/styles/tailwind.css` 无法 realpath；文件实际存在且可读取（Verified）。因此当前构建结果为 Unknown，不能据较早构建声明最终版本通过。
- 后端 `dotnet build ForgeSelf.Api --no-restore`：通过，0 错误（Verified，源码开发宿主启动前）。

## 测试

- TodoTracker 插件冒烟 e2e：1 passed（Verified，修正标题头部前）。覆盖进入插件、阶段筛选与流转、看板、详情页签及后端状态核验；截图 `ForgeSelf.Web/screenshots/e2e/todo-tracker/todo-tracker-main-flow.png`。
- 后端过滤测试：164 passed、1 failed（Verified）。失败项 `CompleteThenReopen_Flow_ShouldFlipStatus` 的旧完成接口返回 HTTP 500，响应报告空引用。属于待办端点兼容性问题，尚未定位修复。
- 最后详情标题变更后的 e2e 尚未复跑；源码运行实例 UI 截图走查也未完成。

## 真实任务闭环

- 在隔离数据目录中创建真实任务「真实验收：只读读取 README 标题」，经源码 API 预览后，真实下发给内置 Agent「程序员」，权限为只读。下发响应成功并产生 `run:1`（Verified）。
- 真实 Agent 执行失败：模型服务返回 HTTP 401 Unauthorized；状态轮询返回 Failed，待办没有完成。
- 已将真实失败结果写入执行记录，并将待办流转到 `Blocked`，原因记录为模型凭据需修复后重试（Verified）。未伪造验收通过或标记 Done。

## 未解决问题

1. **阻塞**：真实 Agent 当前无可用模型凭据，真实任务无法继续并完成。需要有效凭据或可用 AgentHub 执行端后重试。
2. **失败**：旧完成端点集成测试 HTTP 500 空引用，需要按问题类闭环诊断。
3. **未验证**：最后一版插件构建与 e2e、源码 UI 走查未完成。
4. 插件发布未执行；待源码验收与发布前置条件满足后再进入发布流程。

## 提交前复核（2026-10-25）

上面 1~3 项已由承接工件 `2026-10-25-todo-builtin-agent-done` 收口（R3 真实下发到 Done / R2 旧端点 500 / R1 构建 + 冒烟 e2e），本节仅记录提交批次前的重跑证据（全部 Verified）：

| 检查 | 命令 | 结果 |
|---|---|---|
| 后端定向过滤集（含 TodoTracker 域） | `dotnet test ForgeSelf.Api.Tests --filter "…TodoTracker\|TodosController…"` | **182/182 通过**（此前 165/165 域集同源） |
| 全解决方案构建 | `dotnet build ForgeSelf.slnx -c Debug` | 0 错误 |
| 全前端 check | `pnpm --dir ForgeSelf.Web run check` | exit 0（顺手清掉 `todo-tracker.spec.ts` 遗留未使用导入 `existsSync`） |
| 全前端 build | `pnpm --dir ForgeSelf.Web run build` | exit 0（9.64s） |
| 插件冒烟 e2e | `pnpm run test:e2e -- e2e/plugins/todo-tracker` | **1 passed / 59.8s**（今日先前执行，截图已读图） |

未解决问题 4（插件发布）仍待用户批准，属 §8.5 发布动作。

## 验收清单逐条核对（2026-10-25）

> 依据 `02-spec.md` §验收标准。证据等级：Verified = 亲自跑过/读过真实产物；Inferred = 由代码推断。

| AC | 结论 | 证据 | 等级 |
|---|---|---|---|
| AC1 | 通过 | 冒烟 e2e 点击 `.tt-pipeline-step`「草稿」后仅该阶段任务可见（`todo-tracker.spec.ts:60-61`）；数量来自服务端分页 `total`（`store.ts:131-136`，逐阶段 `pageSize:1` 取 `result.total`） | Verified |
| AC1a | 通过 | 优先级进入统一筛选对象 `currentFilters()`（`store.ts:67-73` 的 `priority`），列表（`loadTodos`）、看板（`loadBoardStage`）、流水线/摘要（`loadPipeline`）共用同一 filters；后端用例 `GetTodos_WithPriority_ShouldFilterAcrossMatchingTasks` 与非法值 400 均绿（`TodosControllerTests.cs:59-83`） | Verified |
| AC2 | 通过 | 关注摘要读取全量分页：缺项按 `SUMMARY_PAGE_SIZE` 逐页扫描真实 DTO 的 `missing`（`store.ts:147-160`）；待验收/阻塞按服务端过滤取前 5 条（`store.ts:138-145`），点击后由 `missingFilter`/阶段筛选展示对应集合 | Verified（代码）+ Verified（e2e 看板计数） |
| AC3 | 通过 | 看板 8 列初始各加载第 1 页、每列独立 `page/total/loading/error` 与「加载更多」（`store.ts:75-79`、`169-212`）；卡片可选/可拖目标只来自 `allowedTargets`（`TodoView.vue:280`、`295`） | Verified（代码 + e2e 看板用例） |
| AC4 | 通过 | `sortTasks` 紧急度序 Review 0 → Blocked 1 → Running 2 → Dispatched 3 → Ready 4 → Draft 5 → Done 6 → Cancelled 7，同级按项目/优先级（`actions.ts:41-49`） | Verified |
| AC5 | 通过 | 详情固定头部含 `#td-title` 可读标题，`taskKey` 为次要可复制信息；三页签「工作单元/下发与委派/执行记录」依次可见且各自内容齐全（e2e `todo-tracker.spec.ts:72-78`，截图 `todo-tracker-main-flow.png` 已读图） | Verified |
| AC6 | 通过 | 阶段操作与拖放均以 `allowedTargets` 为准（`TaskDetail.vue:348`、`TodoView.vue:136`）；阻塞必填原因、完成/取消二次确认（`TodoView.vue:101-123`）；服务端拒绝时 `showToast(error.message)` 原文展示、不伪报成功（`TodoView.vue:120-122`） | Verified（代码 + e2e 选择器） |
| AC7 | 通过 | 界面唯一完成入口为阶段流转，不再渲染独立完成按钮；旧 API 契约未改，`TodosController` 兼容测试（含 `CompleteThenReopen_Flow_ShouldFlipStatus`）转绿 | Verified（R2 修复后定向测试） |
| AC8 | 通过 | 执行记录追加表单的阶段下拉由 `allowedTargets` 生成并用 `stageLabel` 中文标签（`ExecutionTimeline.vue`，e2e 执行记录页签可见） | Verified |
| AC9 | 通过 | 三类禁用原因分别照实给出：未生成预览 / `delegationError` 原文 / 四栏未齐（`TaskDetail.vue:110-121`）；缺项提示取自 DTO `missing`（`TaskDetail.vue:101`、`438-439`） | Verified |
| AC10 | 通过 | 快捷键 `/` 聚焦搜索、`N` 聚焦新建、`Escape` 关闭详情，且 `INPUT/TEXTAREA/SELECT` 聚焦时不拦截（`TodoView.vue:140-151`）；阶段流转另有列表 `select` 与详情按钮两条键盘路径（`TodoView.vue:280`、`TaskDetail.vue:348`） | Verified |
| AC11 | 通过 | 空态/筛选无结果/看板空列各有引导文案；看板各列独立错误态不丢已加载数据（`store.ts:180-202`）；长标题/长路径用 `shortPath` + `:title` 全量；e2e 截图读图未见溢出或遮挡（`screenshots/e2e/todo-tracker/todo-tracker-main-flow.png`） | Verified（代码 + 读图） |
| AC12 | 通过 | 插件 `pnpm run build` PASS、插件 eslint 0 error（151 warning）、后端定向集 181/181、`dotnet build ForgeSelf.slnx` 0 错误、唯一冒烟 e2e 1 passed / 59.8s、截图读图通过 | Verified |
| AC13 | 通过 | 由承接工件 `2026-10-25-todo-builtin-agent-done` 闭环：隔离实例真实创建只读任务并下发内置 Agent，Run 1 两步均 Completed、待办自动进入待验收后流转 Done，记录只追加；无 mock | Verified |
| AC14 | 通过 | `ReactLoopAgent` 在挂载出口工具时置 `ToolChoice="required"`（`ReactLoopAgent.cs:319-330`），`OpenAICompatibleProvider` 透传 `tool_choice`（`OpenAICompatibleProvider.cs:506-511`）；`ExitToolTurn_RequiresToolChoice` 与 `ChatStream_ForwardsRequiredToolChoice` 转绿，定向集 181/181 | Verified |

补充说明：`AC1a` 的门禁正则不参与机械校验（`AC1a` 非 `ACn`），此处仍逐条列出以对照 02 全文。
