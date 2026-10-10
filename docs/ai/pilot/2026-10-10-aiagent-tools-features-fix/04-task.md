---
task_id: 2026-10-10-aiagent-tools-features-fix
feature: ai-agent
risk: { level: L2, triggers: ["Plugins/AIAgent/AIAgentPlugin.cs", "ForgeSelf.Web/src/components/TopNavbar.vue", "ForgeSelf.Web/src/views/AllFeaturesView.vue", "ForgeSelf.Web/src/data/homeEntries.ts"], raised_by_agent: true }
gate1: { mode: user, at: 2026-10-10, ref: "用户确认五子项范围" }
gate2: { mode: reviewer, at: 2026-10-10, decision: "APPROVED" }
gate3: { mode: agent, at: 2026-10-10 }
expected_files:
  - Plugins/AIAgent/AIAgentPlugin.cs
  - ForgeSelf.Web/src/components/TopNavbar.vue
  - ForgeSelf.Web/src/views/AllFeaturesView.vue
  - ForgeSelf.Web/src/data/homeEntries.ts
  - ForgeSelf.Api.Tests/Plugins/WorkflowEngineTests/WorkflowEnginePluginRegistrationTests.cs
rollback: "git revert <commit>"
writeback: { feature_doc: none, reason: "沿用既有 ai-agent 功能文档，无新增 feature doc 需求" }
---

# Agent Task

> 阶段：Stage 4｜Task ID：2026-10-10-aiagent-tools-features-fix

## Task ID
2026-10-10-aiagent-tools-features-fix（五子项 ①~⑤）

## Objective
修复 AIAgent 工具注册/选择链三缺陷 + 补齐工作流跨插件契约 + 「所有功能/顶栏」导航一致性与插件入口。

## Scope
### Allowed
上表 13 个文件及对应测试；dev-stack 走查；publish-plugin stage；Playwright MCP 截图。

### Forbidden
- 不动 `ToolScopePluginIds` 白名单内容（默认挂载不扩权）；
- 不动 features.ts / WS 事件词表 / 全局主题 token；
- 不停/启/杀用户运行实例；不 git commit/push。

## Acceptance Criteria
1. composer 工具列表含 universal_tool / run_terminal_command / todo 三件套（全插件徽标）；
2. Agent 编辑工具为下拉多选，可保存；
3. WorkflowEnginePlugin.Apply 后 ctx.Get<IWorkflowService/Executor/Scheduler>() 均 non-null（单测回归）；
4. 所有功能页：钉住置顶、热度次序、卡片图钉；钉住项进顶栏且可图钉取消；
5. 顶栏「所有功能」左侧有插件管理按钮；
6. 门禁全绿。

## Expected Files
见 03-plan Files To Change。

## Verification Commands
```bash
dotnet build Plugins/AIAgent/AIAgent.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~WorkflowEnginePluginRegistrationTests|FullyQualifiedName~ToolScope|FullyQualifiedName~UniversalTool|FullyQualifiedName~TerminalCommand"
cd ForgeSelf.Web && node node_modules/vue-tsc/bin/vue-tsc.js -b && node node_modules/eslint/bin/eslint.js src/components/TopNavbar.vue src/views/AllFeaturesView.vue src/data/homeEntries.ts
NODE_OPTIONS=--max-old-space-size=4096 node node_modules/vitest/vitest.mjs run --pool=forks --poolOptions.forks.singleFork=true
node node_modules/@playwright/test/cli.js test e2e/all-features-plugins.spec.ts --workers=1
node node_modules/@playwright/test/cli.js test e2e/plugins/ai-agent/ai-agent.spec.ts --workers=1
```
