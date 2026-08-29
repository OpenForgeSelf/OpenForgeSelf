# 前端架构参考

> 前端为 Vue 3.5 SPA，使用 Vite 6 + TypeScript 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia。
> 包管理器：pnpm（非 npm），Node >= 20。
> 最后更新：2026-08-20

## 目录结构

```
ForgeSelf.Web/src/
├── __tests__/          # 单元测试（vitest）
├── components/         # 可复用组件（80+ 个 .vue）
│   ├── capture/        # 代理抓包组件
│   ├── chatrecords/    # 聊天记录组件
│   ├── devtools/       # 开发工具组件（JSON/YAML/加密/编码/正则等）
│   ├── filetools/      # 文件工具组件
│   ├── quicklinks/     # 快捷链接组件
│   ├── scriptrunner/   # 脚本运行器组件
│   ├── settings/       # 设置面板组件
│   ├── systemmonitor/  # 系统监控组件
│   ├── texttools/      # 文本工具组件
│   ├── todo/           # 待办组件
│   ├── workflow/       # 工作流组件
│   └── (根目录)        # 通用组件（TopNavbar/Sidebar/MessageList 等）
├── composables/        # 组合式函数
├── data/               # 数据定义（features.ts 等）
├── router/             # 路由定义
├── services/           # API 调用层（31 个 .ts）
├── stores/             # Pinia 状态管理（20 个 .ts）
├── styles/             # 全局样式
├── types/              # TypeScript 类型定义（24 个 .ts）
├── utils/              # 工具函数
└── views/              # 页面级组件（32 个 .vue）
```

## 路由映射

### 静态路由（29 条）

| 路径 | 页面组件 | 说明 |
|------|----------|------|
| `/` | HomeView | 首页仪表盘 |
| `/ai-agent` | AgentView | AI 代理聊天 |
| `/prompts` | PromptsView | 提示词管理 |
| `/skills` | SkillsView | 技能管理 |
| `/mcp-tools` | McpToolsView | MCP 工具 |
| `/settings` | SettingsView | 系统设置 |
| `/memory` | MemoryView | 记忆系统 |
| `/agents` | AgentsManageView | 代理管理 |
| `/all-features` | AllFeaturesView | 全部功能 |
| `/system-monitor` | SystemMonitorView | 系统监控 |
| `/code-snippets` | CodeSnippetsView | 代码片段 |
| `/workflows` | WorkflowLibrary | 工作流库 |
| `/todo` | TodoView | 待办事项 |
| `/profile` | ProfileView | 用户画像 |
| `/plugins` | PluginStore | 插件市场 |
| `/plugins/:id` | PluginDetail | 插件详情 |
| `/plugins/updates` | PluginUpdates | 插件更新 |
| `/plugins/import-export` | PluginImportExport | 插件导入导出 |
| `/plugins/scaffolder` | PluginScaffolder | 插件脚手架 |
| `/chat-records` | ChatRecordsView | 聊天记录 |
| `/quick-links` | QuickLinksView | 快捷链接 |
| `/chat` | ChatView | 聊天 |
| `/text-tools` | TextToolsView | 文本工具 |
| `/file-tools` | FileToolsView | 文件工具 |
| `/dev-tools` | DevToolsView | 开发工具 |
| `/script-runner` | ScriptLibrary | 脚本库 |
| `/capture` | CaptureView | 代理抓包 |

### 动态插件路由

插件通过前端清单（`pluginManifest store`）注册动态路由：

- `setupPluginRoutes(menuItems)` — 从插件菜单项注册路由
- `setupManifestRoutes(manifest)` — 从清单注册路由
- 动态路由挂载在 `/plugin-view` 命名空间下

## Stores 职责（20 个）

| Store 文件 | Store 名 | 职责 |
|-----------|----------|------|
| `agent.ts` | agent | AI 代理状态（代理列表、当前计划、执行结果） |
| `appearance.ts` | appearance | 外观设置（背景图、透明度） |
| `chat.ts` | chat | 聊天状态（消息列表、会话、流式响应） |
| `codeSnippet.ts` | codeSnippet | 代码片段管理 |
| `counter.ts` | counter | 示例计数器 |
| `devTools.ts` | devTools | 开发工具状态 |
| `fileTools.ts` | fileTools | 文件工具状态 |
| `home.ts` | home | 首页聚合（派生自多个 store） |
| `memory.ts` | memory | 记忆系统状态 |
| `plugin.ts` | plugin | 插件管理（市场/详情/更新/脚手架） |
| `pluginManifest.ts` | pluginManifest | 前端清单加载 |
| `quickLinks.ts` | quickLinks | 快捷链接 |
| `scriptRunner.ts` | scriptRunner | 脚本运行器 |
| `systemMonitor.ts` | systemMonitor | 系统监控（CPU/内存/网络/进程） |
| `tabs.ts` | tabs | 导航标签栏（默认：home/ai-agent/skills/mcp-tools/system-monitor） |
| `textTools.ts` | textTools | 文本工具状态 |
| `theme.ts` | theme | 主题模式（light/dark/system） |
| `todo.ts` | todo | 待办事项 |
| `workflow.ts` | workflow | 工作流管理 |

## Services 层（31 个 API 调用模块）

```
agentApi.ts          — AI 代理 API
aiModelsApi.ts       — 模型列表 API
aiProvidersApi.ts    — 提供方配置 API
api.ts               — 基础 API 配置
apiServerApi.ts      — API 服务器设置
applicationRestart.ts— 应用重启
authInit.ts          — 认证初始化
captureApi.ts        — 代理抓包 API
chatRecordsApi.ts    — 聊天记录 API
codeSnippetApi.ts    — 代码片段 API
devToolsApi.ts       — 开发工具 API
fileToolsApi.ts      — 文件工具 API
memoryApi.ts         — 记忆系统 API
mcpApi.ts            — MCP 工具 API
monitorHub.ts        — 监控 SignalR Hub
planningApi.ts       — 规划 API
pluginApi.ts         — 插件管理 API
pluginManifestApi.ts — 前端清单 API
portConfigApi.ts     — 端口配置 API
quickLinksApi.ts     — 快捷链接 API
request.ts           — HTTP 请求封装
scriptHub.ts         — 脚本 SignalR Hub
scriptRunnerApi.ts   — 脚本运行器 API
settingsApi.ts       — 设置 API
skillsApi.ts         — 技能 API
systemMonitorApi.ts  — 系统监控 API
textToolsApi.ts      — 文本工具 API
todoApi.ts           — 待办 API
usageStatsApi.ts     — 用量统计 API
websocket.ts         — WebSocket 连接管理
workflowApi.ts       — 工作流 API
```

## 组件树概览

### 通用布局组件

```
App.vue
├── TopNavbar.vue          — 顶部导航栏
├── Sidebar.vue            — 侧边栏
└── <router-view>
    └── (页面组件)
```

### 设置面板（7 个子面板）

```
SettingsView.vue
├── GeneralPanel.vue       — 通用设置
├── AppearancePanel.vue    — 外观设置
├── AiProvidersPanel.vue   — AI 提供方
├── AiAgentPanel.vue       — AI 代理
├── ApiServerPanel.vue     — API 服务器
├── DataStoragePanel.vue   — 数据存储
├── PluginsPanel.vue       — 插件管理
└── AboutPanel.vue         — 关于
```

### 聊天页面

```
ChatView.vue
├── MessageList.vue
│   └── MessageItem.vue
└── MessageInput.vue
```

### 工作流页面

```
WorkflowLibrary.vue
├── WorkflowCard.vue
├── WorkflowEditor.vue
├── WorkflowExecution.vue
├── StepTimeline.vue
├── ScriptStepEditor.vue
├── ScriptSelector.vue
├── ExecutionLog.vue
└── AIWorkflowGenerator.vue
```

## 关键设计约定

- **Element Plus 组件禁止显式 `import { ElXxx } from 'element-plus'`**（type 导入如 `FormInstance`/`FormRules` 除外）：依赖 unplugin-vue-components 按需注入，显式导入会绕过自动解析导致组件无样式
- **全屏背景图**：用固定定位 `<img>` 元素（`position:fixed; inset:0; object-fit:cover; z-index:0; pointer-events:none`）+ 内容层 `z-index` 叠放，不用 CSS `background-image: url(外链)`
- **样式体系**：Element Plus 官方 `--el-*` 变量 + Tailwind 布局原语，不定义独立色值
- **颜色**：只走 `--el-*` 变量，不写死 hex 值
