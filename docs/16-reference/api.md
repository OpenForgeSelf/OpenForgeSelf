# 16-reference — 接口参考（速查）

> 状态：已实现（从 Controllers 反推，人工精简子集；全量见 openwiki/，2026-08-12）
> 最后更新：2026-08-12

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
| GET | `api/skills` `api/usagestats` `api/*` | 技能/用量等 | — |

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

## 3. 前端路由（src/router/index.ts，节选）

`/` 首页 · `/chat` 聊天 · `/chat-records` 聊天记录 · `/ai-agent` `/prompts` `/skills` `/mcp-tools` `/settings` `/memory` `/agents` `/all-features` `/system-monitor` `/code-snippets` `/workflows` `/todo` `/profile` `/plugins`(`:id`/updates/import-export/scaffolder) · `/quick-links` `/text-tools` `/file-tools` `/dev-tools` `/script-runner`。
