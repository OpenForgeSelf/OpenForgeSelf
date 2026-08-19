# 01-architecture — 架构总览

> 功能编号：—
> 状态：已实现（与代码对齐，2026-08-12 反向更新）
> 最后更新：2026-08-12

本文档从代码实现反推系统全貌：分层、模块关系、技术栈、核心数据流。**设计意图见 `00-vision/`**，**单功能设计见 `02-features/`**，**代码级细节以 `openwiki/`（CI 自动生成）为准**。

---

## 1. 技术栈（代码事实）

| 层 | 技术 | 说明 |
|----|------|------|
| 后端 | .NET 10 / ASP.NET Core 10 | `OpenForgeSelf.Backend/` |
| 后端 ORM | NewLife.XCode | 实体继承自 `Entity<T>`，SQLite 持久化（`Data/OpenForgeSelf.db`） |
| 后端 AI | NewLife.AI / Microsoft Agent Framework（实验性） | `Services/AI/` 统一网关与多模态处理 |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind v4 + Pinia | `OpenForgeSelf.Frontend/`，pnpm |
| 通信 | REST + WebSocket | 前端 `services/request.ts`（fetch 封装）+ `services/websocket.ts` |
| 运行 | 托盘常驻 Windows 桌面应用（自更新） | 后端监听动态端口（默认 **7102**），前端 dev 默认 **7002** |

> ⚠️ **端口以代码为准**：后端默认端口来自 `Models/ForgeSetting.cs` 的 `PortNumber = 7102`（运行期由 `PortConfigurationController` 改写入库，`AppBuilder` 重启生效）。前端 dev server 默认 `7002`。`AGENTS.md` 旧注 `7300/7380` 已过时。

---

## 2. 分层与模块关系

```
┌───────────────────────────── 前端 (Vue SPA) ─────────────────────────────┐
│ views/ (30+)  ·  components/  ·  stores/ (Pinia)  ·  services/ (fetch封装) │
│ composables/ (useOpenPage/tabs/...)  ·  router  ·  styles/themes/         │
└───────────────┬──────────────────────────────────────────┬───────────────┘
                │ REST / WebSocket                          │ SSE/WS 推送
                ▼                                            ▼
┌───────────────────────────── 后端 (ASP.NET Core) ────────────────────────┐
│ Controllers/ (宿主 API)                                                 │
│   ├─ AIProviderController [Authorize ApiKeyPolicy]   AI 提供方 CRUD      │
│   ├─ AIModelController           模型列表/拉取                          │
│   ├─ ApiServerController         初始化令牌/重启                        │
│   ├─ ChatController              应用自有聊天 (api/chat)               │
│   ├─ ChatRecordsController       代理录制会话视图 (api/chat-sessions)   │
│   ├─ PluginController            插件市场/安装/更新                     │
│   ├─ PortConfigurationController 端口动态配置                          │
│   ├─ McpController / SkillsController / UsageStatsController / ...      │
│   └─ UnifiedAI/  (统一 AI 网关，向下兼容 OpenAI/Anthropic 协议)         │
│        ├─ OpenAIChatController      v1/chat/completions  (OpenAI 风格)  │
│        ├─ OpenAIResponsesController v1/responses        (Responses 风格)│
│        ├─ AnthropicMessagesController v1/anthropic/messages (Anthropic) │
│        ├─ ModelsController          v1/models                        │
│        └─ AgentChatController       v1/agent/chat/completions (实验·Agent Framework 样本) │
│                                                                         │
│ Services/   AIProviderRegistry · MultimodalProcessor · ChatTurn* · ...  │
│ Entities/  AIProvider/AIModel · ChatSession/ChatTurn · Usage* · ...     │
│ Plugins/    AIAgent/DevTools/FileTools/MemorySystem/QuickLinks/Scheduler/ │
│             ScriptRunner/SystemMonitor/TextTools/TodoTracker/WorkflowEngine │
│             （另有模板插件 SamplePlugin/ 与管理基础设施 Plugins/Services/）      │
│ AppBuilder.cs  依赖注入 / 中间件 / 数据库迁移 / 端口绑定                │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 3. 核心数据流

### 3.1 统一 AI 网关（UnifiedAI）

外部 LLM 客户端（如 GitHub Copilot、各类 OpenAI 兼容客户端）按各自协议打到 `v1/*`，后端**按风格归一**后通过 `AIProviderRegistry` 路由到已配置 provider 的上游：

- **模型前缀路由**：请求 `model` 支持 `provider:upstream_model_id` 格式（如 `default:qwen/qwen3-vl-4b`）；无前缀且不在 provider 支持列表时回退默认 provider。
- **三风格归一**：OpenAI Chat / Anthropic Messages / OpenAI Responses 三种请求→统一内部表示→上游；流式走 SSE（`HandleStreamAsync` 透传 `usage`，`include_usage=true`）。
- **多模态**：`MultimodalProcessor` 逐图识别，结果走 `IImageRecognitionCache`（见 `02-features/011-multimodal-image-cache.md`）。

### 3.2 聊天会话聚合（ChatSession / ChatTurn）

> 根因与方案见 `docs/chat-record-session-redesign.md`。

两套聊天空间曾互相独立、会话无法聚合。现为**两级聚合**：

| 实体 | 职责 | 关键字段 |
|------|------|----------|
| `ChatSession` | 统一会话聚合根（app 自有聊天 Source='App' + 代理录制 Source='Proxy'） | `SessionKey`（稳定键）、`Source`、`Title`、`Model`、`Provider`、`MessageCount`、`Total*Tokens` |
| `ChatTurn` | 单次请求录制（原 `ChatRecord` 改名） | `ChatSessionId`（外键）、`TurnIndex`、`SessionKey`（冗余免 JOIN）、`Style`、`RequestBody/Response`、`Tokens` |

- **代理录制**：`UnifiedAI/*` 控制器解析 `x-interaction-id` 等透传头 → `ResolveConversationKey` → upsert `ChatSession` → 写 `ChatTurn`（外键 + TurnIndex + Preview + tokens）。
- **应用聊天**：`ChatController`（路由 `api/chat`：`POST` 收发、`POST stream` 流式、`GET history/{sessionId}` 历史）收发消息时按 `SessionId` 字符串 upsert `ChatSession`（Source='App'）。
- **会话视图**：`ChatRecordsController`（`api/chat-sessions` GET 列表 + `api/chat-sessions/{id}` 详情）为前端聊天记录面板提供聚合后的会话维度数据。

### 3.3 插件体系

宿主通过 `Plugins/Abstractions/IPlugin` + `ExtensionPointManager` 在运行时发现并加载 `Plugins/*`（每插件含 `*.csproj` + 控制器）。`PluginController` 暴露市场/安装/更新/卸载/版本回滚等 REST。内嵌插件（随主程序发布）的启用逻辑存在已知阻塞（见 TODO.md T032）。

> 演进方向：以新增的 `OpenForgeSelf.Core`（.NET 版 Cordis 内核）把插件体系重构为「一切皆插件」——设计见 [`cordis-kernel.md`](cordis-kernel.md)，功能档案见 [`02-features/027-cordis-kernel.md`](../02-features/027-cordis-kernel.md)，路线图见 [`15-roadmap/plugin-architecture.md`](../15-roadmap/plugin-architecture.md)。

---

## 4. 前后端协作约定

| 关注点 | 约定（代码事实） |
|--------|------------------|
| API 封装 | 前端 `services/*Api.ts` 统一走 `request.ts` 的 `fetch` 封装，类型在 `types/*.ts` |
| 导航 | `composables/useOpenPage.ts` 的 `openPage` 统一走 `stores/tabs.ts` 标签栏，禁止散落 `router.push` |
| 背景图 | 固定定位 `<img>`（`position:fixed; inset:0; object-fit:cover; z-index:0`）而非 CSS `background-image`（外链不渲染）；teleport 弹层透明化在 `styles/themes/bg-image-mode.css` |
| 样式铁律 | 仅官方 Element Plus `--el-*` + Tailwind 布局；canvas 取色用 `getComputedStyle` 读计算值 |
| 功能清单 | `data/features.ts` 为单一真源，CI `scripts/check-features.mjs` 做 phantom/orphan 双向校验 |

---

## 5. 构建与部署

| 动作 | 命令/文件 | 说明 |
|------|-----------|------|
| 前端构建 | `OpenForgeSelf.Frontend/vite.config.ts` → `outDir: ../OpenForgeSelf.Backend/wwwroot`，`emptyOutDir:false` | 本环境 safe-delete shim 拦截 Vite 删目录，故不清旧产物（构建前可手动清 `wwwroot/assets`） |
| 一键构建发布 | `build.ps1` | 构建前端 → 输出 `wwwroot` → 发布后端 |
| 启动 | `start.ps1` | 启动后端(7102)+前端(7002) |

---

## 6. 本目录不解释什么

- 某功能"为什么这么设计" → `02-features/` / `07-decisions/`
- 代码级实现 → `openwiki/`（CI 自动生成，勿手编）
- 端口/令牌/加密的"怎么操作" → `05-guides/`、`08-security/`
