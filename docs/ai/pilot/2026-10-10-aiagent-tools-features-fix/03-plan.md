# Plan

> 阶段：Stage 3｜Task ID：2026-10-10-aiagent-tools-features-fix

## Files To Change
| 文件 | 变更 |
| --- | --- |
| `Plugins/AIAgent/AIAgentPlugin.cs` | AC1 注册 UniversalTool/RunTerminalCommandTool；AC5 配套 `WorkflowServiceResolver` 助手 + 3 处工作流工具改软解析+可读错误 |
| `Plugins/AIAgent/Services/AIAgentService.cs` | AC3 `ResolveOwnToolDefinitions`→`ResolveToolDefinitions(scopePluginsOnly)`，新增 `ResolveSelectableToolDefinitions`；两处挂载点（RunAgentLoopAsync / CreateAgent 链）显式勾选时放开候选池 |
| `Plugins/WorkflowEngine/WorkflowEnginePlugin.cs` | AC5 Apply 内 ctx.Register 三契约 |
| `Plugins/AIAgent/web/src/AiAgentView.vue` | AC2 loadAgentTools 去 pluginId 白名单、剔除运行流特殊工具、排序 |
| `Plugins/AIAgent/web/src/components/ChatPanel.vue` | AC2 插件徽标 + 搜索含 pluginId + 底注文案 + 样式 |
| `Plugins/AIAgent/web/src/components/AgentEditDialog.vue` | AC4 TagInput→ElSelect 多选 + toolOptions 懒加载 + 样式 |
| `Plugins/AIAgent/web/src/index.ts` | AC4 补 select/option/tooltip 组件样式引入 |
| `ForgeSelf.Web/src/data/homeEntries.ts` | AC6 新增 featureUsageKey / rankFeatures |
| `ForgeSelf.Web/src/views/AllFeaturesView.vue` | AC6 rankFeatures 排序 + 钉住按钮 + 快照防抖 + 样式 |
| `ForgeSelf.Web/src/components/TopNavbar.vue` | AC7 pinnedTabs/displayTabs + 图钉取消 + 插件入口按钮 |
| `ForgeSelf.Web/src/data/__tests__/homeEntries.test.ts` | rankFeatures/featureUsageKey 单测 |
| `ForgeSelf.Api.Tests/Plugins/WorkflowEngineTests/WorkflowEnginePluginRegistrationTests.cs` | AC5 回归测试（新建） |
| `Plugins/AIAgent/plugin.json` / `Plugins/WorkflowEngine/plugin.json` | 版本 1.7.3→1.7.4 / 1.0.0→1.0.1 |

## Implementation Steps
1. 后端：注册工具 → 作用域双轨 → WorkflowEngine ctx.Register → 工作流工具软解析。编译过。
2. 插件前端：列表/徽标/下拉/样式；出树构建（.plugin-build-aiagent，wrapper publicDir=false）。
3. 宿主前端：rankFeatures → AllFeaturesView → TopNavbar；vue-tsc+eslint+vitest。
4. 版本号 bump → publish-plugin.ps1 stage 到 publish/Plugins（1.7.4 / 1.0.1）。
5. 走查（dev-stack :7399 + Playwright MCP）+ 截图存 screenshots/live-7399/。

## Test Plan
- 单测：homeEntries rankFeatures 4 例；WorkflowEnginePluginRegistrationTests 1 例；既有 ToolScope/FinishTool/UniversalTool 等回归。
- e2e：`e2e/plugins/ai-agent/{ai-agent,tool-gateway-031}.spec.ts`、`e2e/all-features-plugins.spec.ts`。

## Verification
### Build
`dotnet build`（两插件 + Api.Tests）、宿主 `vue-tsc -b`、插件出树 vite build。
### Unit Test
`dotnet test`（filter）/ `vitest run` 全量 765+。
### Integration Test
N/A（无新增集成接缝；既有集成测试在 dotnet test 全量内）。
### E2E
见 Test Plan；031 场景 2-5 需 51888+真实 LLM，本环境仅跑隔离实例可跑部分。
### Other Checks
eslint 0 error；features 双向校验不受影响（未动 features.ts）。
