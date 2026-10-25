# 实施计划

## 实施步骤

1. **查询、流水线、看板与排序** — 修改 `Plugins/TodoTracker/Controllers/TodosController.cs`、`Services/TaskQuery.cs`、`Services/TodoService.cs` 及对应 TodoTracker API 测试；以及 `Plugins/TodoTracker/web/src/TodoView.vue`、`store.ts`、`http.ts`、`actions.ts` 和类型定义。为现有查询增加向后兼容的优先级参数；实现列表/看板切换、带数量流水线、关注摘要、人工处理优先排序、看板分列分页和快捷键。
2. **任务详情与执行记录** — 修改 `Plugins/TodoTracker/web/src/components/TaskDetail.vue`、`ExecutionTimeline.vue`：增加常驻标题/阶段/操作栏与三个页签；统一完成入口到阶段流转；补充原因提示与二次确认；执行记录改为中文合法阶段目标。
3. **回归覆盖** — 修改 `ForgeSelf.Api.Tests/Plugins/TodoTracker/` 与 `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`：将目前过多的插件 e2e 场景收敛成且只保留一条真实宿主冒烟主链路；在减少 e2e 场景前，把必要的查询、兼容性和操作安全回归迁移至可重复的单元/集成测试。
4. **插件元数据与文档** — 只有发布流程要求调整用户可见行为版本时才改 `Plugins/TodoTracker/plugin.json`；实现后更新 TodoTracker 功能文档。
5. **真实 Agent 出口工具约束** — 修改 `ForgeSelf.Abstractions/UnifiedChatModels.cs`、`Plugins/AIAgent/Services/ReactLoopAgent.cs`、`ForgeSelf.Api/Services/AI/Providers/OpenAICompatibleProvider.cs`，使仅在配置了出口工具的计划驱动回合发送 `tool_choice: required`；在 AIAgent 回合测试与 OpenAI 兼容提供方请求测试中覆盖，防止模型用无工具纯文本结束计划/步骤回合。

## 测试计划

- 增加或扩展可重复断言，覆盖优先级筛选/排序、摘要精确数量、查询分页、合法目标、阻塞必填原因、终态二次确认及执行记录阶段选项。
- 插件仅保留一条主链路冒烟 e2e：打开 TodoTracker → 用流水线筛选 → 切换看板 → 打开详情 → 执行阶段/页签操作 → 截图。
- 保留 TodoTracker 后端回归与 Home 兼容性测试。
- AIAgent 计划驱动回归：断言 exit-tool 回合请求要求工具选择，普通回合不强制；OpenAI 兼容适配器将 `tool_choice` 序列化到上游请求。
- **真实任务闭环验收（必做，不能以 e2e 收到委派回执代替）**：在隔离源码预览数据根创建一条只读待办；选择本机真实可用的 Agent 引擎/Agent 并下发；确认服务端状态经过已下发/执行中，Agent 实际成功完成后自动进入「待验收」，且执行结果与只追加记录已持久化；人工检查结果并按合法目标流转到「完成」，刷新页面后再次核对终态和记录。任务限定为无副作用的只读检查，例如读取仓库 `README.md` 的一级标题并按验收判据回报；使用 `read-only` 权限，禁止写入工作区。

## 验证

- 插件前端：`cd Plugins/TodoTracker/web && pnpm run build`（必跑）；新增单元覆盖按项目现有测试入口运行。
- 后端：运行 `dotnet build` 与 `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoTracker"`（新增优先级查询必须覆盖）。
- AIAgent 与适配器：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~ReactLoopAgentTests|FullyQualifiedName~OpenAICompatibleProviderToolChoiceTests"`。
- e2e：`cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/todo-tracker --workers=1 --output=.temp/pw-out-todo-tracker`（插件 e2e 文件最终只留一条冒烟；按技能执行真实隔离宿主并检查截图）。
- 源码环境走查：使用隔离数据运行 `pwsh scripts/dev-stack.ps1`，按各项交互验收标准走查，检查截图、深浅主题和窄屏，并清理本任务创建的测试记录。
- 真实委派闭环必须在隔离数据根中完成；如果没有可用真实 Agent/引擎，或任务未实际成功完成，则记录为 BLOCKED，不得用 mock、手动改状态或仅有「已入队」回执判通过。
- 按插件维护闭环完成六步。源码验收通过后才发布；不得停止、重启或终止用户运行的宿主。只有用户更新运行实例后才进行只读复验。

## 风险与回滚

- 任务较多时，看板可能请求多个分页；每列独立加载，避免初次打开时一次载入全部数据。
- 完成入口改为阶段流转可能暴露其与旧完成/重开语义的差异。保留 API，并在实现通过前验证 Home 兼容性。
- 改代码前记录 `git status`；若需回滚，只撤销本任务自己的改动，保留工作区已有的其他改动。
