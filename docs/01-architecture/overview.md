# 01-architecture — 架构总览

> 功能编号：—
> 状态：已实现（与代码对齐，2026-08-12 反向更新）
> 最后更新：2026-09-30

本文档从代码实现反推系统全貌：分层、模块关系、技术栈、核心数据流。**设计意图见 `00-vision/`**，**单功能设计见 `02-features/`**，**代码级细节以 `openwiki/`（CI 自动生成）为准**。

> 📐 **架构可视化（可交互 HTML）**：[`architecture-diagram.html`](architecture-diagram.html)——四层架构总览 + 宿主启动装配链 11 步，基于 2026-09-07 静态代码调查（快照 66f3ede）。

---

## 1. 技术栈（代码事实）

| 层 | 技术 | 说明 |
|----|------|------|
| 后端 | .NET 10 / ASP.NET Core 10 | `ForgeSelf.Api/` |
| 后端 ORM | NewLife.XCode | 实体继承自 `Entity<T>`，SQLite 持久化（`Data/ForgeSelf.db`） |
| 后端 AI | NewLife.AI / Microsoft Agent Framework（实验性） | `Services/AI/` 统一网关与多模态处理 |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind v4 + Pinia | `ForgeSelf.Web/`，pnpm |
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

#### 3.2.1 会话事件溯源（唯一真相源，dsh 对齐 B1–B9）

> 设计真源：[`ai/pilot/dsh-alignment-b2-b9/02-spec.md`](../ai/pilot/dsh-alignment-b2-b9/02-spec.md)；运行时架构图见 [`dsh-runtime-architecture.md`](dsh-runtime-architecture.md)。

上面 `ChatSession`/`ChatTurn` 是**录制聚合**视角；而**会话记录的正确性唯一来源 = append-only 会话日志**（`SessionEventEntity`，落盘于 `ISessionStore.Append`），与录制表解耦：

- **不变量 1（Model-visible means logged）**：一切进入模型的消息先落日志再派生，`model ⊆ log`；旧 `ChatController.SaveMessageAsync` 双写已删除。
- **`ChatMessage` 降级为只读投影**：由 `SessionProjectionService.SyncAsync` 从日志**幂等全量重投影**（前缀对齐 + 孤儿行自愈），控制器/服务不得直写。
- **事件类型可辨识联合**：`system/user/assistant/tool` 四类投影进模型历史；`assistant/attempt`、turn/step 结构类、`request/*`、`agent/inbox/*` 落盘但不投影（轨迹/审计）。
- **运行期校验**：`SessionEventMap.EnsureKnown` 强制注册；库中出现未注册类型直接抛（旧数据删除 = 破坏性变更，必须显式迁移）。

### 3.3 插件体系（Cordis 内核驱动）

宿主通过 `ForgeSelf.Core`（.NET 版 Cordis 内核）驱动「一切皆插件」架构。详见 [`cordis-kernel.md`](cordis-kernel.md) 完整设计。

**四层架构**：

| 层 | 组件 | 职责 |
|----|------|------|
| 第1层 · 前端 | Vue 3 SPA（`pluginManifest store` + `dynamicPlugins.ts`） | 清单驱动菜单/视图，动态 import 挂载（声明路径直接生效，冲突回退 `/plugin-view`） |
| 第2层 · 宿主 | `PluginManager` / `PluginServiceRegistry` / `ExtensionPointManager` | 插件发现/加载/Fiber 生命周期/热重载/可变 DI |
| 第3层 · 内核 | `IContext` / `Context` / `Fiber` / `EventBus` + `Abstractions` 契约 | 服务定位 + 可逆副作用 + 事件总线 + 能力接缝 |
| 第4层 · 插件 | 13 个独立插件（AIAgent/WorkflowEngine/Scheduler 等） | 各自 `Apply(IContext)` 自注册 DI + 扩展点 + 副作用 |

**插件间服务互通**（2026-08-19 实施）：root 共享服务表 `ConcurrentDictionary<Type,(Instance,Provider)>`——`Register<T>` = 全局共享（对标 Cordis `provide()`，走 `Effect` 自动摘除），`RegisterLocal<T>` = 本地值（框架私有对象专用），`Get<T>` 解析顺序 = 本地值 → 共享表。首个落地案例：`IWorkflowAIAdvisor`（AIAgent → WorkflowEngine，软依赖），验证 988/988 全绿。

**热更新**：side-by-side 版本目录（N=2）+ ALC 卸载 + `FileSystemWatcher` 自动 reload + dispose 测试门禁。

> 完整架构图见 [`cordis-kernel.md`](cordis-kernel.md) 附录 SVG 源文件。功能档案见 [`02-features/027-cordis-kernel.md`](../02-features/027-cordis-kernel.md)，路线图见 [`15-roadmap/plugin-architecture.md`](../15-roadmap/plugin-architecture.md)。

### 3.4 认证链路（主密钥 + API 子密钥）

所有需要鉴权的请求走同一条 Bearer 链路（策略 `ApiKeyPolicy`，由 `ApiKeyAuthenticationHandler` → `ApiKeyService.ResolveByToken` 判定）：

```
Bearer <token>
  ├─ ① 子密钥：ApiKeyCredential.FindByKeyHash(SHA256(token))  ← O(1) 摘要定位，不解密比对
  │     停用/过期 → 401 且不回退；命中 → 200（Claims 带 KeyId/KeyName/AuthMethod，60s 节流写 LastUsedAt）
  └─ ② 回退主密钥：解密 ForgeSetting.ApiToken → FixedTimeEquals 定长比较
        表缺失/库异常 → 记 WARN 后仍走主密钥判定（认证路径绝不 500）
```

- 子密钥按摘要定位，**不校验前缀**（`sk-`、`cs-sk-`、`gpu…` 均可）；明文只在创建/轮换当次返回，库里只存密文（`KeyCipher`，机器派生密钥加密）与 SHA-256 摘要（`KeyHash`，唯一索引）。
- 加密底座为机器派生密钥 + 密文版本化（v1/v2）+ 启动期自动迁移，详见 [`02-features/030-api-keys.md`](../02-features/030-api-keys.md) 与 [`02-features/100-secret-encryption.md`](../02-features/100-secret-encryption.md)。
- 托盘「打开主界面」带 `#token=<主密钥>`，前端首屏同步消费并清空 fragment；同页已打开时由 `hashchange` 监听兜底。

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
| 前端构建 | `ForgeSelf.Web/vite.config.ts` → `outDir: ../ForgeSelf.Api/wwwroot`，`emptyOutDir:false` | 本环境 safe-delete shim 拦截 Vite 删目录，故不清旧产物（构建前可手动清 `wwwroot/assets`） |
| 一键构建发布 | `build.ps1` | 构建前端 → 输出 `wwwroot` → 发布后端 |
| 启动 | `start.ps1` | 启动后端(7102)+前端(7002) |

---

## 6. 本目录不解释什么

- 某功能"为什么这么设计" → `02-features/` / `07-decisions/`
- 代码级实现 → `openwiki/`（CI 自动生成，勿手编）
- 端口/令牌/加密的"怎么操作" → `05-guides/`、`08-security/`
