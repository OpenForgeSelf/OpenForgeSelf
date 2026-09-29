# 13-glossary — 术语表

> 状态：已实现（2026-08-12 反向更新；2026-09-29 补会话事件与 Turn/Step 运行时词汇——dsh 对齐 040–042 收官）
> 最后更新：2026-09-29

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

### 会话事件与 Turn/Step 运行时词汇（dsh 对齐 040–042，2026-09 收官补录）

| 术语 | 含义 |
|------|------|
| **SessionEvent** | 会话事件溯源联合（13 子类，全部落日志；「一切进模型的东西必须先落日志」的唯一真相源） |
| **agent/inbox/spliced** | 收件箱拼接事件（`InboxSplicedEvent`），带五个操作计数 op（followup/steer/inject 等注入轨迹） |
| **TurnEndReason.Suspended** | turn 终态：step 超时看门狗把 turn 挂起（非 Failed），steer 后恢复继续 |
| **StepEndReason.Suspended / ExitTool** | step 终态：`Suspended` = 看门狗命中挂起；`ExitTool` = 出口工具（complete_step/request_help）声明退出 |
| **TurnFrame** | turn/step 帧联合（`Agents.cs`，dsh B5）：ReactLoopAgent 状态机的推进单元 |
| **InboxOps** | 收件箱操作枚举（followup/steer/inject 三语义的操作载体） |
| **IInboxConfirmation** | 收件箱注入确认接缝（消费方确认注入已被本轮消化） |
| **PreToolDecision** | 工具 pre-execute 三态决策：`Allow` / `Deny(Reason)` / `Ask(Reason?)`（无审批服务 fail-closed → Denied） |
| **六闸门** | 工具管线执行链：pre-execute 三态 → 单调守卫 → execute waterfall → post-execute waterfall → finalize 恰好一次 → tools/result 冻结快照 |
| **model-ordered commit** | 批量工具提交语义：N 个 `tool/call` 必有 N 个 `tool/result`，取消时剩余合成 `Skipped` |
| **spill（32KiB）** | 超过 `SpillThresholdBytes` 的工具结果在模型可见投影中截断为 `[tool_result spilled: …，CallId=…]` 引用；完整结果仍走 tools/result 事件 |
| **FORGESELF_DATA_ROOT** | 数据根环境变量重载（B9-4）：`DataLocationService` 双解析重载最前置生效，测试宿主据此隔离数据目录 |
