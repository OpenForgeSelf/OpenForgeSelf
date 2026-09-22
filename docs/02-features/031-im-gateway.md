# 031 - IM 多渠道网关（im-gateway 插件）

> 插件形态：`ForgeSelf.Api/Plugins/ImGateway/`，运行时 id `im-gateway`，当前版本 **2.0.0**。

## 功能定位

把企业微信（智能机器人）的消息统一接入 AI Agent：
平台协议差异关在 `IImChannel` 适配器里，AI 能力经 `IChatCompletion` 契约（由 AIAgent 插件提供）获取。
**v2.0.0 起只保留「企微 WebSocket 长连接」单通道形态，回调（Callback）形态整体移除。**

## 架构：单通道长连接（2.0.0 起）

`IImChannel.TransportMode` 保留（`Callback`/`WebSocket`），但企微为唯一通道且是 `WebSocket`：

```
WeComChannel.StartAsync → WeComAiBotClient（外连 wss://openws.work.weixin.qq.com）
  → aibot_subscribe(BotId, Secret) 订阅（errcode=0 才算就绪）
  → 收 aibot_msg_callback → 入队（有界队列 512）→ 独立消费任务
      → WeComFrameParser 归一 → ImGatewayRouter → AI 回复
      → aibot_respond_msg（msgtype=stream，透传 req_id，finish=true 结束）
  → 心跳 ping 保活（30s JSON 命令帧）+ 95s 无下行判死 + 指数退避重连 3s→30s
  → 收 disconnected_event（被新连接踢）→ _kicked 标志停重连（防互踢）
```

公共基础设施（v2.0.0 全面改为 SQLite 存储，不再落 `sessions.json`）：

| 组件 | 存储 | 说明 |
|---|---|---|
| 配置（含密文 Secret） | SQLite `ImGatewayChannel` 表（单行） | Secret 经宿主 `ISecretEncryptionService` 加密后落 `SecretCipher` 列 |
| 会话映射 | SQLite `ImGatewaySession` 表 | `ChannelKey`（通道:会话:用户）唯一 → Agent 会话 |
| 消息去重 | SQLite `ImGatewayProcessedMsg` 表 | `ChannelType+MsgId` 唯一，幂等忽略冲突 |
| 长连接生命周期 | 内存 | `ImGatewayConnectionManager` + `ImGatewayConnectionHostedService` |

- 建表：`Data/ImGatewayTables.cs` `EnsureCreated()` = `DAL.Create("ImGateway").Db.ServerVersion` 探活即建表（Migration=On）。
- 实体真源：`Data/Model.xml` → `xcode Model.xml` 生成（生成件不手改，业务写 `.Biz.cs`）。
- 数据源：ConnName=`"ImGateway"` → `Data/ImGateway.db`（生产数据根 `~/.forgeself/Data`）。

## 企业微信长连接协议要点

依据官方文档[《智能机器人长连接》](https://developer.work.weixin.qq.com/document/path/101463)：

| 方向 | 命令 | 要点 |
|---|---|---|
| 发 | `aibot_subscribe` | 连接后发 `{bot_id, secret}` 做身份校验，`errcode=0` 才算就绪。**有频率保护，不得反复订阅** |
| 发 | `ping` | 心跳命令帧（JSON），官方建议 30s 一次 |
| 发 | `aibot_respond_msg` | 被动回复。**长连接下普通文本回复也必须用 `msgtype=stream`**（不是 `text`），`finish=true` 结束流式；`headers.req_id` 必须透传回调帧的值 |
| 发 | `aibot_send_msg` | 主动推送（无回调触发）。`chattype`：1=单聊（传 userid）、2=群聊（传 chatid）；缺省服务端按群聊解析会导致单聊发不出去 |
| 收 | `aibot_msg_callback` | 消息回调：`{msgid, chatid, chattype(single/group), from.userid, msgtype, text.content}` |
| 收 | `aibot_event_callback` | 事件回调：`enter_chat` / `template_card_event` / `feedback_event` / `disconnected_event` |

**硬性限制**：
- 同一机器人**同时只允许一个长连接**；新连接会踢掉旧连接，并给旧连接推 `disconnected_event`。
  客户端收到该事件后**停止重连**（否则多实例会互踢），用户可在前端手动点「重连」恢复。
- 群聊 @机器人 时正文带 `@机器人名 ` 前缀，解析层会剥离后再交给 AI。

协议解析与帧构造集中在 `WeComFrameParser`（纯函数，无 IO），并有 8 个单测锁定契约。

## D1/D2 修复（2.0.0）

- **D1（入站消息阻塞接收泵）**：`WeComAiBotClient.ConnectAndPumpAsync` 原为接收泵内同步分发处理，
  单条消息处理慢会阻塞整条接收泵（心跳/被踢事件都收不到）。修复：
  - `Channel.CreateBounded<string>(512)`（SingleWriter，FullMode=Wait）有界消息队列 + `SemaphoreSlim(4,4)` 并发闸；
  - 接收泵只做「入队消息帧」（`IsMessageFrame` 以含 `"aibot_msg_callback"` 判定）；事件帧（含被踢判定）仍同步及时处理；
  - 独立 worker `ConsumeMessagesAsync` 读队列异步分发处理；连接结束 `TryComplete()` 并等待消费任务排空。
- **D2（被踢后无手动重连）**：`WeComChannel.ReconnectAsync`（`ResetKicked()` + StopInternal + EnsureRunning）；
  `ImGatewayConnectionManager.Reconnect(channelType)`；`POST /api/im-gateway/wecom/reconnect`
  （目标非长连接返回 409）；前端 `.ig-btn--reconnect` 重连按钮。

## 密钥存储与加密（2.0.0）

- Secret 不落明文：`DbConfigStore.Save` 经宿主 `ISecretEncryptionService.Encrypt` 写 `SecretCipher` 密文列；
  `Load` 时 `TryDecrypt` 还原，解密失败记日志返回空串待重填。
- 加密实现：`ForgeSelf.Api/Security/AesSecretEncryptionService`（AES-256-CBC、`v2:` 版本前缀、密钥解析链配置>env>机器派生）。
- 接口上移：`ISecretEncryptionService` 位于 `ForgeSelf.Abstractions`（宿主与插件共享契约），
  已在 `PluginManager.HostProvidedServiceContracts` 登记；`DbConfigStore` 运行期**惰性解析**
  （构造收 `Func<ISecretEncryptionService?>`，缺失兜底明文仅保启动不崩）。
- **旧 v1.1.0 `config.json`（`channels[]` 多通道格式）不做迁移**：直接添加新的即可；
  旧文件仅留档为 `config.json.imported-<ts>`（绝不删除），DB 为空时走手填/扫码重新配置。

## 扫码授权（自动回填）与手填

`Services/WeComScanAuthService.cs`（方案 A，已真机验证）：

```
POST /api/im-gateway/wecom/scan-auth
  → 会话 id + 隔离目录 cli-auth/<id>
  → spawn wecom-cli auth init --noninteractive
      env: WECOM_CLI_CONFIG_DIR/LOG_DIR/TMP_DIR 隔离（不落默认 ~/.config/wecom）
  → 流式解析扫码链接 https://work.weixin.qq.com/ai/qc/gen?source=wecom_cli_external&scode=<scode>
  → 返回 {id, state: waitingscan, qrLink, qrText}
GET /api/im-gateway/wecom/scan-auth/{id}
  → CLI 退出（≤10 分钟，超时 Kill）→ 读 credentials.enc + .encryption_key
  → 解密（AES-256-GCM：key=base64(.encryption_key)，nonce 前 12 / tag 末 16，明文 {bot:{id,secret,create_time},token}）
  → 自动回填 BotId+Secret → Save（加密入库）→ Apply 重建长连接 → state: succeeded（仅回显 BotId）
```

- **体验不降级**：扫码自动回填与手填并存（扫码失败/无 CLI 时手填兜底；前端有「扫码授权（自动回填）」按钮）。
- CLI 路径解析链：配置 `CliPath` → 环境变量 `WECOM_CLI_PATH` → 本机已知路径 → AppContext.BaseDirectory 相对路径。
- CLI 二进制：`@wecom/cli` 平台子包 `wecom-cli-win32-x64.exe`（MIT，运行时不依赖 node）。
- 凭据铁律：真实 BotId/Secret 严禁写入代码/测试/文档/git；扫码产生的凭据存隔离目录 + 加密入库，需要时读取/索取。

## 端点

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/im-gateway/config` | 读配置（Secret 解密还原，NoStore 防缓存） |
| POST | `/api/im-gateway/config` | 保存配置（Secret 加密入库），并**立即按新配置校正长连接**（即时生效，无需重启宿主） |
| GET | `/api/im-gateway/status` | 通道状态：enabled + `transport=websocket` + 长连接 `connection.state/message/connectedAt` |
| POST | `/api/im-gateway/wecom/reconnect` | 被踢/断开后手动重连（目标非长连接返回 409） |
| POST | `/api/im-gateway/wecom/scan-auth` | 发起扫码授权（返回 {id, state, qrLink, qrText}；CLI 缺失返回 409） |
| GET | `/api/im-gateway/wecom/scan-auth/{id}` | 轮询扫码会话（state: starting/waitingscan/succeeded/failed + botId/error） |

> v2.0.0 已无回调端点（`/api/im-gateway/{channel}/callback` 删除）。

## 数据模型（SQLite，`Data/Model.xml` 真源）

| 表 | 关键列 | 说明 |
|---|---|---|
| `ImGatewayChannel` | `Id` / `Enabled` / `BoundAgentId` / `BoundChatModelId` / `BotId` / `SecretCipher` / `CliPath` / `UpdatedAt` | 配置单行（含密文列） |
| `ImGatewaySession` | `ChannelKey`（唯一） / `SessionId` / `AgentId` / `ModelId` / `UpdatedAt` | 通道会话 → Agent 会话映射 |
| `ImGatewayProcessedMsg` | `ChannelType`+`MsgId`（唯一） / `ProcessedAt` | 消息去重 |

## 前端配置页

`Plugins/ImGateway/web/`（Vite lib 模式，导出 `ImGatewayView`，原生 HTML + `--el-*` tokens）。
- 企微卡片：BotId + Secret 手填表单 + 「扫码授权（自动回填）」按钮（原生模态弹窗展示扫码链接）+ 状态灯
  （已就绪 `ig-dot--connected` / 连接中 / 失败重试 / 被抢占 / 未连接）+ 「重连」按钮。
- 2.0.0 修复：`statusOf()` 曾因压缩合并丢失导致插件视图空白（`n.statusOf is not a function`），
  已补回并在 e2e 加渲染守卫。

## 测试

- `ForgeSelf.Api.Tests/ImGateway/`：14 个单测——`WeComFrameParserTests`(8)、
  `DbConfigStoreTests`(4，类级随机临时库夹具 + `PurgeChannel()` 清本类单行配置)、
  `ChannelConfigConverterTests` / `FileConfigStoreRoundTripTests`（BotId/Secret 占位符）。
- 测试隔离：类级夹具（`XCodeTestFixture` 已含 `"ImGateway"`，每类一个随机临时库，互不冲突，基本不清理数据）。
- e2e：`ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts`（单通道断言：标题「IM 网关」、
  `{weCom:{...}}` 结构、1 通道 websocket、resetConfig 清空；enabled=false + 占位凭据避免真外连）。

## 版本历史

| 版本 | 变更 |
|---|---|
| 1.0.0 | 首版：4 通道适配 + 网关路由 + 配置页 |
| 1.0.1 | 修复 Save/Load 序列化契约不一致致配置读回空；修复开关控件被轨道遮挡；Load 失败记日志不再静默 |
| 1.1.0 | 企业微信由「自建应用回调」改为「智能机器人 WebSocket 长连接」：凭据简化为 BotId+Secret，无需公网 IP/回调域名/消息加解密；内核新增长连接形态（订阅/心跳/重连/被踢停重连）；解析与帧构造抽为 `WeComFrameParser` 并补协议单测 |
| **2.0.0** | **大改**：① 移除 Callback 形态（只留企微长连接单通道）；② 配置/会话/去重改 SQLite 存储；③ Secret 加密入库（宿主 `ISecretEncryptionService` 体系，接口上移 Abstractions）；④ 扫码授权自动回填 BotId+Secret（CLI 隔离目录 + AES-256-GCM 解密）+ 手填并存（体验不降级）；⑤ 修 D1（消息处理阻塞接收泵 → 有界队列+并发闸+独立消费）/ D2（被踢后手动重连端点+按钮）；旧 config.json 不迁移仅留档 |

## 已知边界

- 企微长连接的**流式输出**尚未接入：当前为一次性完整回复（`finish=true` 单帧）。
  流式需让 AI 补全按 chunk 回调并分片推送（同 `stream.id`，末片 `finish=true`，10 分钟内完成）。
- `aibot_send_msg` 主动推送已实现但**未经真机验证**（帧格式依据官方 SDK 对齐，待真实场景确认）。
- 扫码授权依赖 CLI 二进制（`@wecom/cli`）；CLI 缺失时扫码入口 409，手填不受影响。
- 旧 v1.1.0 多通道配置不做迁移（用户拍板「直接添加新的即可」），留档文件 `config.json.imported-<ts>` 可手动参考。
- **历史消息拉取**：企微 WS 模式不提供「拉历史」API，只能从接入时刻起实时收；要完整聊天正文需企业开通「会话存档」（企业级，PRD 六期）。

## 2026-09-22 增补（v2.0.0 收尾）

### 消息闭环真机验证 ✅

全链路跑通（2026-09-22）：企微单聊发消息 → `aibot_msg_callback` 入库存档 → IChatCompletion 调 LLM（qwythos-9b-v2，28 字 / 32 秒）→ `aibot_respond_msg` stream finish=true 回发 → 用户企微侧收到回复。

### 两个根因修复

1. **IChatCompletion 契约未注册**：AIAgent 侧定义了 `RegisterChatCompletion` 扩展方法但 `AIAgentPlugin.Apply()` 里漏调——im-gateway 收消息后 `ctx.Get<IChatCompletion>()` 返回 null。已补 `services.RegisterChatCompletion(ctx)`。
2. **长连接热重载后不自恢复**：初版把启动挂在宿主级 `IHostedService.StartAsync`——插件热重载后 .NET 不会再跑一次 `StartAsync`，新实例永远建不了连。改为 `ImGatewayConnectionManager` 构造函数自调 `EnsureStarted()`（自起 CTS + 注册回调 + 拉起连接），幂等。铁律 14 已写进 `plugin-development` 技能。

### UI 修复（5 项）

1. 插件根视图必展示版本号（`.ig-version` 徽标，读 `GET /api/plugin` 解包 `.data`）——铁律 13 已写进技能
2. 扫码按钮改主色填充白字（原弱描边不明显）
3. 弹窗遮罩加深 `rgba(0,0,0,0.6)` + backdrop blur(4px)，与页面分层
4. 二维码 CSS 修正（正方形无变形、无横向滚动条）
5. 版本号读 `/api/plugin` 响应需解包 `.data`（非裸数组）

### Agent 配置面（当前能力边界）

| 项 | 现状 | 配置面 |
|---|------|--------|
| 绑定 Agent | `BoundAgentId`（空=默认） | im-gateway 前端表单 |
| 绑定模型 | `BoundChatModelId`（空=默认） | im-gateway 前端表单，格式 `provider:modelId` |
| 工具白名单 | 无 Agent 级字段（全量 11 工具） | 待二期加字段 |
| 提示词 | `AgentDefinition.SystemPrompt` | AIAgent 工作台改 |
| 工作目录/审批模式 | 无 Agent 级字段 | 宿主级 |

### 二期规划（待拍板）

详见 `Plugins/ImGateway/README.md`「二期规划」节：P1 流式输出 / Agent 级工具白名单；P2 CLI 安装探测 / 主动推送真机验证；P3 Agent 级工作目录 / 群聊完整验证。
