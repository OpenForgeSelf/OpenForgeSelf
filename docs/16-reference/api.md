# 16-reference — 接口参考（速查）

> 状态：已实现（从 Controllers 反推，人工精简子集；全量见 openwiki/，2026-08-20）
> 最后更新：2026-08-20

> 本文件是常用 API 的**人工提炼速查**，非全量。全量契约以 `openwiki/`（CI 自动生成）为准。

## 1. 宿主 API（Controllers/）

| 方法 | 路径 | 说明 | 鉴权 |
|------|------|------|------|
| GET | `api/ai-providers` | 提供方列表（ApiKey 掩码） | ApiKeyPolicy |
| GET | `api/ai-providers/{id}` | 详情 | ApiKeyPolicy |
| POST | `api/ai-providers` | 新建（ApiKey 加密落库） | ApiKeyPolicy |
| POST | `api/ai-providers/{id}/test` | 测试连接 | ApiKeyPolicy |
| POST | `api/ai-providers/{id}/fetch-models` | 拉取上游模型 | ApiKeyPolicy |
| GET | `api/ai-models` | 模型列表 | — |
| GET | `api/api-server/status` | 服务状态 | — |
| GET | `api/api-server/init-token` | 取当前令牌 | — |
| POST | `api/api-server/regenerate` | 轮换令牌 | — |
| POST | `api/api-server/restart` | 5 秒后重启（新端口） | — |
| POST | `api/chat` | app 自有聊天 | — |
| POST | `api/chat/stream` | app 聊天流式 | — |
| GET | `api/chat/history/{sessionId}` | app 聊天历史 | — |
| DELETE | `api/chat/session/{sessionId}` | 删会话 | — |
| GET | `api/chat-sessions` | app 聊天 + 代理录制统一聚合会话列表 | — |
| GET | `api/chat-sessions/{id}` | 会话详情（含 Turns） | — |
| GET/POST | `api/portconfiguration` | 读取/设置端口（1024–65535） | — |
| GET | `api/health` | 健康检查 | — |
| GET/POST | `api/mcp/*` | MCP 服务器/工具管理 | — |
| GET | `api/plugin/*` | 插件市场/安装/更新/卸载 | — |
| GET | `api/todos` | 待办 CRUD（TodoTracker 插件） | — |
| GET | `api/skills` | 技能管理 | — |
| GET | `api/usagestats` | 用量统计 | — |
| GET | `api/settings` | 系统设置 | — |
| GET | `api/health` | 健康检查 | — |
| GET/POST | `api/mcp/*` | MCP 服务器/工具管理 | — |
| GET | `api/plugin/*` | 插件市场/安装/更新/卸载 | — |
| GET | `api/todos` | 待办 CRUD（TodoTracker 插件） | — |
| GET | `api/portconfiguration` | 端口配置 | — |

### 插件 Controller API

| 方法 | 路径 | 插件 | 说明 |
|------|------|------|------|
| POST | `api/ai-agent/chat` | AIAgent | AI 代理聊天 |
| POST | `api/ai-agent/script` | AIAgent | AI 生成脚本 |
| POST | `api/ai-agent/workflow` | AIAgent | AI 生成工作流 |
| GET | `api/ai-agent/agents` | AIAgent | 代理列表 |
| POST | `api/ai-agent/plan` | AIAgent | 规划执行 |
| GET | `api/dev-tools/*` | DevTools | 开发工具（JSON/YAML/加密/编码/正则等） |
| GET | `api/file-tools/*` | FileTools | 文件工具（清理/重命名/统计/压缩） |
| GET | `api/memory/*` | MemorySystem | 记忆系统 CRUD |
| GET | `api/quick-links/*` | QuickLinks | 快捷链接 CRUD |
| GET | `api/scheduler/*` | Scheduler | 调度任务 CRUD |
| GET | `api/code-snippets` | ScriptRunner | 代码片段 CRUD |
| POST | `api/script-runner/execute` | ScriptRunner | 执行脚本 |
| GET | `api/system-monitor/*` | SystemMonitor | 系统监控（CPU/内存/网络/进程） |
| GET | `api/text-tools/*` | TextTools | 文本工具（统计/哈希/格式化/编码） |
| GET | `api/workflows/*` | WorkflowEngine | 工作流 CRUD/执行/日志 |

## 2. 统一 AI 网关（Controllers/UnifiedAI/，向下兼容 LLM 客户端）

| 方法 | 路径 | 风格 |
|------|------|------|
| POST | `v1/chat/completions` | OpenAI Chat |
| POST | `v1/responses` | OpenAI Responses |
| POST | `v1/anthropic/messages` | Anthropic Messages |
| GET | `v1/models` | 模型列表 |
| POST | `v1/agent/chat/completions` | Agent Framework 实验接口（服务端工具样本） |

- 请求 `model` 支持 `provider:upstream_model_id` 前缀路由；
- 流式 SSE 透传 `usage`（`include_usage=true`）。

## 3. 前端路由（src/router/index.ts，完整列表）

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

动态插件路由通过 `setupPluginRoutes(menuItems)` 和 `setupManifestRoutes(manifest)` 注册到 `/plugin-view` 命名空间。
