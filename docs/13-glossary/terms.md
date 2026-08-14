# 13-glossary — 术语表

> 状态：已实现（2026-08-12 反向更新）
> 最后更新：2026-08-12

| 术语 | 含义 |
|------|------|
| **OpenForgeSelf / 铸己匣** | 本项目：本地优先、可自我演进、插件化的个人 AI 智能体工作台 |
| **AI Provider** | AI 提供方配置（实体 `AIProvider`），含 Endpoint/ApiKey(加密)/模型/默认标志 |
| **AIModel / ChatModelId** | 上游模型本地固化；`ChatModelId = provider:upstream_model_id`，供 LLM 客户端直接粘贴 |
| **ApiServerKey** | API 服务器令牌，AES-256-CBC 加密存储，作为 `ApiKeyPolicy` 凭据 |
| **ApiKeyPolicy** | 后端鉴权策略，凭据即 ApiServerKey 明文（解密比对） |
| **统一 AI 网关** | `Controllers/UnifiedAI/`：把 OpenAI/Anthropic/Responses/Agent Framework 多协议归一后路由到 provider 上游 |
| **ChatSession** | 统一会话聚合根（app 聊天 Source='App' + 代理录制 Source='Proxy'），靠 SessionKey + Source 区分 |
| **ChatTurn** | 单次请求录制（原 `ChatRecord` 改名），关联 ChatSession（外键 + TurnIndex） |
| **ChatMessage** | app 自有聊天的逐条消息，保留独立 `SessionId` 与 ChatSession 软关联 |
| **ResolveConversationKey** | 解析稳定会话键的优先级链（x-interaction-id > 其他透传头；每请求唯一头不可作键） |
| **MultimodalProcessor** | 多模态处理：逐图识别 + 本地缓存（IImageRecognitionCache） |
| **ImageRecognitionCache** | 图片识别结果本地缓存：`<root>/<sessionId>/<sha256>.json`，键=SHA256(视觉模型|来源|内容) |
| **plugin.json** | 插件清单，宿主 `ExtensionPointManager` 运行时加载 |
| **Loop Engineering** | 项目闭环模型：Goal→Context→Plan→Execute→Verify→Iterate |
| **features.ts** | 前端功能清单单一真源；CI `check-features.mjs` 做 phantom/orphan 双向校验 |
| **ADR** | 架构决策记录（`docs/07-decisions/`） |
| **ForgeSetting.PortNumber** | 后端运行端口（默认 7102，运行期可改、重启生效） |
