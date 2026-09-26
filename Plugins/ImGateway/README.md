# im-gateway 插件 README（给后续插件开发者）

> 本文件记录**设计思路 + 踩坑 + 二期规划**，不是 API 文档。
> API/端点/数据模型见 `docs/02-features/031-im-gateway.md`，PRD 见 `specs/034-im-gateway/PRD.md`。

## 这个插件解决什么

把企业微信智能机器人的消息接入 AI Agent：用户在企微 @机器人 说话 → 本插件直连企微 WS 收消息 → 调宿主 IChatCompletion → AI 回复经企微 WS 回发。

**为什么不用企微「自建应用回调」**：回调模型要公网 IP + 回调域名 + Token/AESKey 加解密，个人用户玩不起。智能机器人 WS 长连接只要 BotId+Secret，本端主动外连，零公网暴露。

## 架构骨架（5 个关键决策）

### 1. 平台协议关在 IImChannel 适配器里

```
IImChannel（接口）
  ├─ TransportMode: Callback | WebSocket
  ├─ StartAsync(onMessage, ct) / StopAsync(ct)
  └─ SendAsync(outbound, ct)
WeComChannel : IImChannel  ← 企微协议全关这里
飞书Channel / 钉钉Channel  ← 未来加，内核零改动
```

**内核（ImGatewayRouter）只认 InboundMessage/OutboundMessage，不认企微/飞书协议**。加新平台 = 新增一个 IImChannel 实现。

### 2. AI 能力经 IChatCompletion 契约（跨插件解耦）

im-gateway **不自己实现 LLM 调用**——它经宿主 `IChatCompletion` 契约调 AIAgent 插件：

```
im-gateway 收消息 → ImGatewayRouter.ProcessInboundAsync
  → ctx.Get<IChatCompletion>()  ← AIAgent 插件注册的实现
  → chat.CompleteAsync(new ChatCompletionRequest { SessionId, Message, AgentId, ModelId })
  → 经 WeComChannel.SendAsync 回发企微
```

**踩过的坑**：`IChatCompletion` 契约在 AIAgent 侧定义了 `RegisterChatCompletion` 扩展方法，但**没人调它**——AIAgentPlugin.Apply 里漏了一行注册。结果 im-gateway 收消息后 `ctx.Get<IChatCompletion>()` 返回 null，日志报「未找到 IChatCompletion 契约」。

**教训**：跨插件契约的提供方必须在 `Apply()` 里显式 `ctx.Register<T>(impl)`，光定义扩展方法不调 = 没注册。消费方 `ctx.Get<T>()` 返回 null 时要打「契约未注册」错误，不要静默吞。

### 3. 长连接自管生命周期（不靠宿主级 HostedService）

**踩过的坑**：初版把长连接启动挂在宿主级 `IHostedService.StartAsync`——插件热重载（销毁→重建）时，.NET 不会再跑一次 `StartAsync`，新插件实例的 Manager 永远建不了连，只能靠重启宿主恢复。

**正确做法**：插件自己的管理器在**构造函数**里自初始化：

```csharp
public ImGatewayConnectionManager(IEnumerable<IImChannel> channels, ImGatewayRouter router)
{
    _channels = channels;
    _router = router;
    try { EnsureStarted(); } catch (Exception ex) { /* 记日志，首次 Apply 时重试 */ }
}

private void EnsureStarted()
{
    if (_onMessage != null) return;  // 幂等
    _cts = new CancellationTokenSource();
    _hostToken = _cts.Token;
    _onMessage = (msg, ct) => _router.ProcessInboundAsync(msg, ct);
    Apply();
}
```

**铁律**（已写进 plugin-development 技能铁律 14）：插件自带的长连接/后台任务必须自管生命周期，禁止依赖宿主级 HostedService。

### 4. 凭据加密入库（不写明文）

- Secret 经宿主 `ISecretEncryptionService`（AES-256-CBC）加密后落 SQLite `SecretCipher` 列
- 配置单行（`ImGatewayChannel` 表），会话映射（`ImGatewaySession`），消息去重（`ImGatewayProcessedMsg`）
- 旧 v1.1.0 `config.json` 明文文件不迁移，改名留档 `config.json.imported-<ts>`

### 5. 消息处理不阻塞接收泵

**踩过的坑**：初版在接收泵里同步处理消息——单条消息调 LLM 要 30 秒，期间心跳/被踢事件都收不到。

**正确做法**：有界队列 512 + SemaphoreSlim(4,4) 并发闸 + 独立消费任务。接收泵只做「入队消息帧」，事件帧（被踢/disconnect）仍同步及时处理。

## 企微 WS 协议要点（改协议前必读）

| 要点 | 说明 |
|------|------|
| 端点 | `wss://openws.work.weixin.qq.com` |
| 订阅 | 连接后发 `aibot_subscribe{bot_id, secret}`，errcode=0 才就绪 |
| 心跳 | JSON 命令帧 `ping`（30s），**不是 WS 协议层 ping** |
| 被动回复 | **必须 `msgtype=stream`（非 text）**，`headers.req_id` 透传回调帧，`finish=true` 结束 |
| 主动推送 | `aibot_send_msg`，需 chattype（1=单聊传 userid，2=群聊传 chatid） |
| 硬限制 | 同一机器人同时只允许一个长连接，新连踢旧；收 `disconnected_event` 后停重连防互踢 |

## 扫码授权（方案 A：CLI 仅作扫码工具）

- 本端不存 CLI 管的凭据——CLI 只用来生成扫码链接 + 用户扫码后解密 credentials.enc 拿 BotId/Secret
- 拿到后立即回填 DB（加密入库），之后直连 WS 收发，**CLI 不再参与运行时**
- 凭据隔离：`WECOM_CLI_CONFIG_DIR` 指向插件隔离目录，不污染默认 `~/.config/wecom`
- AES-256-GCM 解密：key=base64(.encryption_key)，nonce=前 12B，tag=末 16B

## Agent 配置面（当前能力边界）

im-gateway 调 AI 时：
- **BoundAgentId**（空=默认 Agent）：im-gateway 前端可绑
- **BoundChatModelId**（空=默认模型）：im-gateway 前端可绑，格式 `provider:modelId`
- **工具**：默认 Agent 挂全量工具（当前无 Agent 级工具白名单字段，`AllowedTools` 是请求级参数，im-gateway 调用时没传）
- **提示词**：去 AIAgent 工作台改 `AgentDefinition.SystemPrompt`
- **工作目录/审批模式**：Agent 级无配置，作用于宿主级（当前登记项目 / agent-hub 委派审批）

## 真机验证结论（v2.0.0，2026-09-22）

- ✅ 长连接订阅（`wss://openws.work.weixin.qq.com` → `aibot_subscribe` errcode=0）
- ✅ 收企微消息（`aibot_msg_callback` 入库存档）
- ✅ 调 AI Agent（IChatCompletion 契约注册后，LLM 补全 28 字 / 32 秒）
- ✅ 回发企微（`aibot_respond_msg` stream finish=true，用户企微侧收到回复）
- ⚠️ 流式输出未接（当前一次性 finish=true 单帧，回复等待 30+ 秒）
- ❌ 历史消息拉取：企微 WS 不提供「拉历史」API，只能从接入时刻起实时收

## 二期规划（待拍板，本次不做）

| 优先级 | 项 | 说明 | 预估 |
|--------|-----|------|------|
| P1 | 流式输出 | AI 按 chunk 回调 → 企微 stream 分片推送（同 stream.id，末片 finish=true），消除 30 秒等待 | 1-2 人日 |
| P1 | Agent 级工具白名单 | AgentDefinition 加 `AllowedTools` 字段，im-gateway 绑专用 Agent 时只挂白名单工具 | 1 人日 |
| P2 | CLI 安装探测 | 前端检测 wecom-cli 是否在 PATH/已知路径，缺失给安装指引（方案 A：内嵌 CLI 二进制 ~0.5 人日；方案 B：仅探测提示 ~0.2 人日） | 0.2-0.5 人日 |
| P2 | 主动推送真机验证 | `aibot_send_msg` 帧格式按官方 SDK 对齐，待真实群聊场景确认 | 0.5 人日 |
| P3 | Agent 级工作目录/审批模式 | AgentDefinition 加字段，企微机器人独立工作目录 + 危险工具人工审批 | 2-3 人日 |
| P3 | 群聊 @机器人 完整验证 | 当前单聊已通，群聊 @ + 前缀剥离 + chatid 路由待真机 | 0.5 人日 |

## 遗留问题（非本轮引入）

- 飞书/钉钉 Send 未真实验证（当前只有企微通道）
- 全量测试 45 个既有环境性失败（与 im-gateway 无关）
- `aibot_send_msg` 主动推送未真机验证

## 给后续插件的 checklist

- [ ] 跨插件契约：提供方 `Apply()` 里显式 `ctx.Register<T>(impl)`，消费方 `ctx.Get<T>()` 判 null 打错误
- [ ] 长连接/后台任务：管理器构造函数自初始化，不依赖宿主级 HostedService
- [ ] 消息处理：有界队列 + 并发闸，不阻塞接收泵
- [ ] 凭据：宿主 `ISecretEncryptionService` 加密入库，不写明文
- [ ] 实体：`Model.xml` 真源 → xcode 生成，业务写 `.Biz.cs`
- [ ] 测试：类级夹具（每类一个临时库），不 mock 真实数据
- [ ] UI：插件根视图必展示版本号（铁律 13）
- [ ] 发布：payload 先、plugin.json 最后，宿主 watcher 热重载
