# Evidence

> 阶段：Stage 7｜Task ID：2026-10-10-aiagent-tools-features-fix｜只记录实际发生的事。

## Task
五子项全部实现；版本 AIAgent 1.7.4 / WorkflowEngine 1.0.1 已 stage 至 `publish/Plugins`。

## Changed Files
见 03-plan 表（全部落地）；另新增 `ForgeSelf.Api.Tests/Plugins/WorkflowEngineTests/WorkflowEnginePluginRegistrationTests.cs`、临时走查日志 `temp/dev-stack-walkthrough.log`、`temp/pub-*.log`（不入库）。

## Build
- Verified：`dotnet build` AIAgent（0 错误/94 警告，均为存量 CS86xx）、WorkflowEngine（0 错误）；宿主 `vue-tsc -b` 0 error；插件出树 vite build 成功（产物仅 index.js 129.76kB + style.css 136.03kB）。

## Unit Test
- Verified：`dotnet test` filter ToolScope|UniversalTool|TerminalCommand|Workflow → 125 中 119 通过；**6 失败为存量基线**（WorkflowPlanningIntegrationTests，404=测试宿主未加载插件控制器；经 stash 改动后复跑同样失败验证）。
- Verified：新增 WorkflowEnginePluginRegistrationTests 1/1 通过（修复前该断言必红）。
- Verified：宿主 vitest 全量 66 文件 765/765 通过（含新增 rankFeatures 4 例）。

## Integration Test
N/A（未新增集成接缝；存量在 dotnet test 内）。

## E2E
- Verified：`e2e/all-features-plugins.spec.ts` 5/5 通过（含「所有功能」卡片网格与插件双入口）。
- Verified：`e2e/plugins/ai-agent/ai-agent.spec.ts` 7/7 通过。
- Known limitation：`tool-gateway-031.spec.ts` 场景 2-5（4 例）在隔离实例失败——真实 LLM 上游 401（隔离宿主无有效提供方凭据），证据落 `screenshots/e2e/ai-agent/tool-gateway-031-scene*-*.log`；该 spec 头部注明应经 agent-execute.config 直连 51888 运行实例，待实例恢复后复验。

## Static Analysis
Verified：eslint（3 改动文件）0 error 0 warning。

## Screenshots
- `screenshots/live-7399/all-features-pin.png`（钉住前）
- `screenshots/live-7399/all-features-pinned-tab.png`（钉住「快捷链接」→卡片置顶 + 顶栏出现常驻标签）
- `screenshots/live-7399/composer-tools-all-plugins.png`（工具 popover 112 项全插件+徽标；顶栏插件图标在位）
- `screenshots/live-7399/agent-edit-tools-dropdown.png`（Agent 编辑工具下拉多选）
- `screenshots/live-7399/plugin-icon-to-plugins.png`（二轮复验：点插件图标直达 /plugins）
- 实测数据（Playwright MCP evaluate）：
  - 工具 popover `total=112, hasUniversal=true, hasTerminal=true, todoTools=[complete_todo,create_todo,list_todos]`；
  - 钉住后顶栏标签序列=首页/快捷链接/AI Agent/技能管理/系统监控；
  - 二轮复验：顶栏按钮序=`[插件管理, 所有功能, 设置]`，点击后 URL=/plugins；重启实例后「快捷链接」仍排所有功能页第一位（pinStates[0]=true），顶栏钉住标签无 ×、动态标签有 ×（钉住/动态去重正确）。

## 二轮复验（2026-10-10 09:3x，用户要求确认）
- Verified：重启 dev-stack 后重验 ④⑤（上述 evaluate 实测值）；③ 补强——WorkflowEnginePluginRegistrationTests 新增 `WorkflowServiceResolver.Resolve(ctx)` 断言（= AIAgent 工作流工具运行时的真实解析路径，两插件共存组合），1/1 通过。

## 验收标准对应证据（门禁对齐）

| AC | 证据（05-evidence 原文） | 等级 |
|----|------------------------|------|
| AC1 | 注册 UniversalTool/RunTerminalCommandTool：Changed Files + Build（AIAgent 0 错误）+ Unit Test（UniversalTool 相关用例通过） | Verified |
| AC2 | composer 工具全量+插件徽标：Screenshots 节 composer-tools-all-plugins.png，实测 `total=112, hasUniversal=true, hasTerminal=true` | Verified |
| AC3 | 挂载语义不变+候选池放开：Unit Test 节 AIAgentToolScopeTests 仍绿；新增 `ResolveSelectableToolDefinitions` | Verified |
| AC4 | Agent 编辑工具 ElSelect 多选：Screenshots 节 agent-edit-tools-dropdown.png（多选下拉） | Verified |
| AC5 | WorkflowEnginePlugin.Apply ctx.Register 三契约 + 工作流工具可读错误：Unit Test 节 WorkflowEnginePluginRegistrationTests 1/1；二轮复验 `WorkflowServiceResolver.Resolve(ctx)` 断言 | Verified |
| AC6 | 所有功能页排序复用首页口径+钉住：Screenshots 节 all-features-pin*.png（钉住后置顶+顶栏标签）；Unit Test 节 rankFeatures 4 例 | Verified |
| AC7 | TopNavbar 钉住常驻标签+插件管理按钮：Screenshots 节 plugin-icon-to-plugins.png（点击直达 /plugins）；顶栏按钮序 `[插件管理, 所有功能, 设置]` | Verified |

## Known Limitations
- 工作流「执行」链路的端到端成功回报需真实 LLM（031 同因），本次验证到「契约可解析 + 错误文案可读」层；
- 51888 运行实例只读复验未执行（实例未运行，无 401 风险点可验）；下次实例启动即用 staged 1.7.4/1.0.1。
