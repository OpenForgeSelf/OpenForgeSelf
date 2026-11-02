# Repository Understanding

> 阶段：Stage 1｜Task ID：PILOT-078｜日期：2026-10-30
> 全部结论来自本仓真实代码（文件:行可核），无常识推测。

## 1. 任务定位

- **用户目标**（输入N+1 原文）：「在IM 网关插件做个左侧列表展示已经绑定im列表，像聊天人一样，展示收到、返回的消息」+「组建团队执行：写文档、开发、测试、验收」。
- **插件**：`Plugins/ImGateway`（运行 id `im-gateway`，v2.0.1，route `/im-gateway`，仅企微智能机器人 WebSocket 长连接一通道）。
- **性质**：插件功能新增（后端消息落库 + 2 个查询端点 + 前端聊天式布局重构）→ 插件六步维护闭环全走。

## 2. 现状盘点（Verified by code）

### 2.1 数据层（`Plugins/ImGateway/Data/Model.xml`）

| 表 | 用途 | 与本任务关系 |
|---|---|---|
| `ImGatewayChannel` | 通道配置单行（Enabled/BoundAgentId/BoundChatModelId/BotId/SecretCipher/CliPath） | 无改动 |
| `ImGatewaySession` | 会话映射（ChannelKey=`{channel}:{conversationId}:{platformUserId}` 唯一 → SessionId/AgentId/ModelId/UpdatedAt） | **「联系人列表」数据源**（每个发过消息的 IM 用户一行） |
| `ImGatewayProcessedMsg` | 平台消息去重（ChannelType+MsgId 唯一） | 无改动 |
| **缺失** | **无消息内容表** —— 收到的消息与 AI 回复当前只进 XTrace 日志，无法回放 | **本批新增 `ImGatewayMessage`** |

- `PluginDbs` 已登记 `["ImGateway"] = "im-gateway"`（`ForgeSelf.Api/Data/XCodeConfig.cs:9` 注释区，铁律 12 ✓）。
- 实体分部类约定：`Data/Entities/*.Biz.cs` 人工维护、`*.cs` 生成（铁律 9：改表先改 Model.xml 再 `xcode`）。
- 长文本列惯例：AIAgent `Model.xml:49` 用 `Length="-1"`（消息 Content 沿用）。

### 2.2 消息链路（`Services/ImGatewayRouter.cs`）

`ProcessInboundAsync`（33-109）：幂等去重（38-43）→ 事件消息跳过（46-50）→ 空内容跳过（52-56）→ 读通道绑定（59-61）→ `GetOrCreateSession` 映射（64-65）→ `IChatCompletion.CompleteAsync`（74-80）→ 失败仅记日志返回（82-86）→ `channel.SendAsync` 回发（89-103）。**全程无消息内容落库**；`InboundMessage` 携带 ChannelType/ConversationId/PlatformUserId/Content/MsgId/ReplyReqId/ChatType（`Core/ImModels.cs`）。

### 2.3 控制器（`Controllers/ImGatewayController.cs`）

现有端点：`GET/POST config`、`GET status`、`POST wecom/reconnect`、`POST wecom/scan-auth`、`GET wecom/scan-auth/{id}`；统一 camelCase `JsonOpts`（:35）。
⚠ **发现（同类扫查命中）**：控制器**无 `[Authorize("ApiKeyPolicy")]`** —— 违反 plugin-development 铁律 17（管理面类控制器必须加，对齐 mcp-center v2.1.0 先例）；前端 `web/src/http.ts:13` 已统一携带 `forge_api_token`，补加不破坏现有调用。本批顺带补齐（同类同根因修复）。

### 2.4 前端（`web/src/`）

- `ImGatewayView.vue`（699 行）：单列配置页——通道卡（开关/BotId/Secret/绑定/状态灯/重连）+ 扫码授权模态 + 保存 footer；**无会话/消息展示**。
- `http.ts`：`fetchConfig/saveConfig/fetchStatus/reconnectWeCom/startScanAuth/fetchScanAuth/fetchPluginVersion`；token 键与宿主一致（:13）。
- 铁律 4：插件独立预编译，**不能用 `<ElXxx>`**，原生 HTML + `--el-*`/design token（既有代码已遵守）。

### 2.5 测试与 e2e

- 后端：`ForgeSelf.Api.Tests/Plugins/` 下**无 ImGateway 测试目录**（新建 `ImGatewayTests/`；XCode 测试须守 PC-014——用 Reset 式换库，禁止 ctor 手搓 AddConnStr）。
- e2e：`ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts` 已存在（冒烟位）；真实后端零 mock，globalSetup 全新宿主 → **天然无 IM 消息**（长连接无注入端点）→ e2e 覆盖外壳/空态/配置视图，消息气泡以单测 + dev-stack 走查（dev 库种子数据）验证。

### 2.6 相邻约束

- AIAgent 会话投影（`AIChatMessage`）是宿主侧只读视图，属**另一插件域**——本批不跨插件读它，消息记录落在 ImGateway 自己的表（插件自治，无需新宿主契约）。
- dev-stack（:7301/:7399）当前跑着含 404 修复的重建实例；后端改动需重建 → 构建期按惯例 `-Stop`/恢复（协作区登记）。
- 404 修复批（`Plugins/AIAgent/**`，未提交）与本批写范围（`Plugins/ImGateway/**`）不相交。

## 3. 关键设计输入

1. 联系人显示名：`ChannelKey` 末段（PlatformUserId）；企微侧形如 `wecom:{conversationId}:{userId}`。
2. 会话列表排序：最近消息时间倒序（无消息的映射排后，按 UpdatedAt）。
3. 消息线程分页：游标 `beforeId`（向前翻旧消息），默认最新 50 条升序渲染。
4. 失败可见：AI 补全失败/回发失败记 `status=error`（气泡侧错误标记 + ErrorText 摘要）——对齐「成败可见」规范与本次 404 教训（静默失败不可接受）。
