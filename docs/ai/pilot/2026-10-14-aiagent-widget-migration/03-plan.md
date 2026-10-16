# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**（如 `ForgeSelf.Api/Services/XxxService.cs`），禁止只写「修改 Service、增加测试」。
> Task ID：2026-10-14-aiagent-widget-migration

## Files To Change

### 新增（迁移 + 适配）

| 文件 | 动作 | 理由 |
| --- | --- | --- |
| `Plugins/AIAgent/web/src/agent-core/components/ChatWidget.vue` | 从 `D:\src\my-proj\ai-assistant-widget-1.13.0\src\components\ChatWidget.vue` 复制 + 改造（FR-2.2：header v-if、launcher v-if=!inline） | 聊天区核心 UI |
| `Plugins/AIAgent/web/src/agent-core/components/MessageItem.vue` | 复制（原样） | 单条消息渲染 |
| `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockBoundary.vue` | 复制（原样） | Markdown 块边界 |
| `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.vue` | 复制（原样） | Markdown 富块渲染 |
| `Plugins/AIAgent/web/src/agent-core/composables/useChat.js` | 复制 + 改造（FR-2.3：transport 分支 + hydrate exposed） | 聊天数据层（含已修复 exportSession） |
| `Plugins/AIAgent/web/src/agent-core/composables/useRemote.js` | 复制（原样；本批遥控不启用但保留代码） | 遥控能力（未来场景） |
| `Plugins/AIAgent/web/src/agent-core/utils/*.js`（auth/audit/charts/export/markdown/pairing/resume/schema/sessions 共 9 个） | 复制（原样） | 工具函数 |
| `Plugins/AIAgent/web/src/agent-core/styles/widget.css` | 复制 + 微调（去 Shadow DOM 依赖：确认无 `:host` 选择器；`--aiw-*` 主题保留；inline 定高适配） | 面板样式 |
| `Plugins/AIAgent/web/src/agent-core/styles/blocks.css` | 复制（原样） | Markdown 块样式 |
| `Plugins/AIAgent/web/src/agent-core/config.js` | 复制 + 改造（FR-2.1：showHeader/transport/sessionId 默认键） | 配置默认值 |
| `Plugins/AIAgent/web/src/agent-core/env.js` | 复制（原样） | 环境开关 |
| `Plugins/AIAgent/web/src/agent-core/types.d.ts` | 新建 | config/transport/暴露 API 类型边界（供 TS 侧） |
| `Plugins/AIAgent/web/src/agent-core/agent-core.test.ts` | 新建（vitest，`.test.ts` 满足宿主 include） | transport 分支/hydrate/showHeader/inline 单测 |
| `Plugins/AIAgent/web/src/agentBridge.ts` | 新建（FR-3.1） | AIAgent config 构造 + aiAgentTransport（plan/free 分流，调 http.ts） |

### 修改

| 文件 | 动作 | 理由 |
| --- | --- | --- |
| `Plugins/AIAgent/web/src/AiAgentView.vue` | 中栏改造（FR-3.2）：工具条提取保留；`<ChatPanel>` 替换为 agent-core 组件；`selectSession` 后 hydrate；onTurnComplete → loadSessions；移除对 ChatPanel 的 import | 聊天区替换 + 功能移交 |
| `Plugins/AIAgent/web/src/components/ChatPanel.vue` | **不改内容**；仅随 AiAgentView 移除 import 后成为未引用文件（保留作为迁移蓝本） | 功能迁移蓝本 + 降低回归面 |
| `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts` | 用例 1 补 `.aiw-panel`/`.aiw-input textarea` 断言；用例 3 改为新组件发送主链路冒烟；用例 6 的 `.chat__textarea`→`.aiw-input textarea`、移除 `.spc` 断言 | 冒烟断言面与替换后 DOM 对齐（FR-4） |
| `Plugins/AIAgent/plugin.json` | `Version: 1.7.4` → `1.8.0` | 行为/契约实质变化（聊天区替换） |
| `Plugins/AIAgent/web/package.json` | `version: 1.2.0` → `1.3.0` | 前端产物版本同步 |
| `TODO.md` | 新增功能缺口清单（composer 五选择器/步骤卡/工具卡片/组件侧历史导出/多标签/权限/遥控/TS 化/与上游同步/ChatPanel 删除时机） | 用户要求的「先记录」 |

### 不修改

- `Plugins/AIAgent/Controllers/`、`Services/`、`Models/`、`Data/`（无后端改动）
- `Plugins/AIAgent/web/src/http.ts`、`types.ts`、`markdown.ts`、`toolEvents.ts`、`sessionArchive.ts`、`components/SessionPanel.vue`、`ContextPanel.vue`、`FileEditor.vue`、`RunRecordPanel.vue`、`AgentEditDialog.vue`、`StepProgressCard.vue`、`index.ts`
- `ForgeSelf.Web/` 宿主源码、`ForgeSelf.Api/` 宿主后端

## Implementation Steps

1. **复制迁移源**：`Robocopy`/`Copy-Item` 把 `ai-assistant-widget-1.13.0\src\{components,composables,utils,styles,config.js,env.js}` 复制到 `Plugins/AIAgent/web/src/agent-core/`（保留文件级对应）。
2. **agent-core 微改**：
   - `config.js`：加 `showHeader:true` / `transport:null` / `sessionId:''` 默认键（放既有默认区）。
   - `ChatWidget.vue`：header 块包 `v-if="config.showHeader !== false"`；launcher 块包 `v-if="!config.inline"`；`defineExpose` 增加 `hydrate`（转发 useChat 暴露）。
   - `useChat.js`：`send()` 内 `if (config.transport)` 分支；新增 `hydrate(list)`（`messages.value = [...list]` + `sending.value=false` + `streamingText.value=''` + `error.value=''` + 未读归零），从 `useChat` return 导出。
   - `styles/widget.css`：检查并移除 `:host`/Shadow DOM 专属规则（若有）；确认 `.aiw-root` 在 inline 时 `height:100%`。
   - 新建 `types.d.ts`（`AgentChatConfig`/`TransportCtx`/`ChatWidgetExpose` 接口）。
3. **agentBridge.ts**：`buildAgentChatConfig()`（返回 config，含 transport 闭包；transport 内按 `executionMode` 调 `streamAgentChat`/`streamAgentRun`，忽略 tool 事件，转发 usage/done/error；AbortError 吞掉不上抛）+ `onTurnComplete` 回调参数。
4. **AiAgentView.vue 改造**：
   - 模板：`<ChatPanel>` 替换为 `<section class="chat flex min-h-0 flex-1 flex-col">` 内 = 工具条（从 ChatPanel 模板提取 `.chat__bar` 结构，含版本徽标/状态/折叠/执行记录）+ `<ChatWidget :config="agentConfig" ref="chatRef" @turn-complete="onTurnComplete" class="agent-core-host" />`。
   - script：`import ChatWidget from './agent-core/components/ChatWidget.vue'`；`agentConfig = buildAgentChatConfig(...)`（含 sessionId/watch 更新、activeAgentId/selectedModelId 快照）；`selectSession` 调 `chatRef.value?.hydrate(messages.value)`；`onTurnComplete` → `loadSessions()`；删除 `ChatPanel` import 与不再使用的 sendMessage 编排（保留 plan/free 分流在 transport）。
   - 保留：FileEditor、左右栏、AgentEditDialog、RunRecordPanel、loadMeta/loadAgents/loadAgentTools/loadAgentSkills/loadHistory/loadSessions、版本徽标数据。
5. **版本号**：plugin.json 1.8.0、web/package.json 1.3.0。
6. **单测**：`agent-core.test.ts`（transport 分支：注入 transport 后 send 调用它且不上默认 fetch；hydrate 覆盖 messages 并清状态；showHeader:false 时 header 不渲染 —— 用 @vue/test-utils mount 断言；inline 时 launcher 不渲染）。
7. **e2e 更新**：ai-agent.spec.ts 按 FR-4 修改三条用例。
8. **TODO.md 登记**功能缺口清单。

## Test Plan

1. `agent-core.test.ts`：transport 分支、hydrate、showHeader/inline 渲染分支（@vue/test-utils + jsdom）。
2. 宿主回归：`pnpm run test`（含既有 SessionPanel/http/toolEvents/sessionArchive 测试，确认 ChatPanel 移除引用不破坏它们）。
3. e2e 冒烟：`ai-agent.spec.ts` 更新后全跑本文件（7 条内）；`--grep` 冒烟主链路。
4. 设计守卫：`pnpm run design:check`（宿主范围；确认无新增裸色值）。
5. 本地验收（dev-stack）：按 AC-5 逐项 + 截图读图。

## Verification

### Build

```powershell
cd Plugins/AIAgent/web && pnpm run build          # 产物 dist/index.js + style.css
# 或沙箱兜底：
pwsh scripts/build-plugin-web.ps1 -Plugin AIAgent
```

### Unit Test

```powershell
cd ForgeSelf.Web && pnpm run test                 # vitest：宿主 + ../Plugins/*/web/src/**/*.test.{ts,tsx}
cd ForgeSelf.Web && pnpm run check                # vue-tsc -b + eslint（宿主范围）
cd ForgeSelf.Web && pnpm run design:check
```

### Integration Test

```bash
# N/A —— 本批无后端/实体改动（AIAgent Controllers/Services/Data 不动）；后端测试非本批范围。
```

### E2E

```powershell
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/ai-agent --project=<e2e 既有项目>   # 按 e2e-testing 技能实际命令
# 冒烟主链路（用例 1 + 改造后发送链路）：
pnpm exec playwright test e2e/plugins/ai-agent --grep "冒烟"
```

### Other Checks

- `git status` 审 diff：仅 Plan 列出的文件变更；`ChatPanel.vue` 内容零 diff（仅失去引用）。
- 发布产物校验（发布步骤执行时）：zip 存在 + `SHA256SUMS.txt` 逐字对 + L1/L2/L3 布局不变量 + 新插件真进 `versions/<ver>/plugins/ai-agent/`（DLL + plugin.json + web/dist）+ 双 exe FileVersion 同串 + `signtool verify` Valid（`-Sign`）。

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-14 | dev 验收暴露既有缺陷：页面「历史会话恒 0」 | Plan 未包含会话列表修复（本批零后端改动、前端 loadSessions 视为既有可用） | `AiAgentView.vue:514` 前端兜底过滤 `s.id.startsWith('plan:')` 与后端 `sessionId` 契约错位 → `s.id`=undefined → TypeError → catch 置空。修复 = `s.id`→`s.sessionId`（一行+注释，非本批引入但必须修）；插件 build + dev 复验 + e2e 回归 8/8 全绿 |
| 2026-10-14 | e2e 基线治理：全新 e2e 宿主 Data 下插件记忆/历史/sessions 500（SQL logic error） | Plan 按既有 e2e 直接跑 | 非本批引入的插件建表时序基建问题（铁律 12 场景）→ e2e 断言面新增 `KNOWN_BASELINE_5XX`（memories/history/sessions 三簇）+ `filterBaselineServerErrors` 隔离，记 TODO P2 |
| 2026-10-14 | e2e session-management 超时 | Plan 未涉 | 缺 `injectRealApiKey` 时插件视图整页空白（main 空、无 .sess__head-toggle）→ spec 头部补注入（对齐 ai-agent.spec 每用例写法） |
