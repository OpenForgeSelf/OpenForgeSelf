# Specification

> 阶段：Stage 2｜Task ID：PILOT-078｜从 00 真实盘点推导；不确定点在 §Unknown 显式登记。

## Functional Requirements

- **FR-1 消息落库**：新增 `ImGatewayMessage` 表（先改 `Data/Model.xml` → `xcode` 生成，铁律 9）：
  `Id(Int64 identity PK)`、`ChannelKey(String 200, NotNull, Master)`、`ChannelType(String 50, NotNull)`、
  `Direction(String 10, NotNull)`（`in`=收到 / `out`=回复）、`Content(String -1, NotNull)`、
  `SessionId(String 64, Nullable)`、`PlatformMsgId(String 100, Nullable)`、
  `Status(String 10, NotNull, DefaultValue="ok")`（`ok`/`error`）、`ErrorText(String 1000, Nullable)`、
  `CreatedAt(DateTime)`；索引 `ChannelKey+Id`。
  落库点（`ImGatewayRouter.ProcessInboundAsync`，全部 try/catch 包裹，**记录失败绝不影响网关主链**）：
  ① 会话映射后记入站（direction=in, Content=inbound.Content, PlatformMsgId=inbound.MsgId, SessionId）；
  ② AI 失败记出站失败（direction=out, Status=error, ErrorText=result.Error 截断 1000, SessionId）；
  ③ 回发成功记回复（direction=out, Status=ok, Content=result.Content, SessionId）。
- **FR-2 联系人列表端点**：`GET api/im-gateway/conversations` → 按联系人（`ImGatewaySession` 每行=1 联系人）返回：
  `channelKey / channelType / displayName（ChannelKey 末段）/ sessionId / lastMessage {direction, status, preview(Content 截 60 字符), createdAt} / lastActiveAt（最后消息时间，无消息用会话 UpdatedAt）/ messageCount`。
  排序：有消息按 lastMessage 时间倒序，无消息按 UpdatedAt 倒序排后。camelCase。
- **FR-3 消息线程端点**：`GET api/im-gateway/messages?channelKey={key}&limit=50&beforeId={id}` →
  指定联系人消息，返回 `beforeId` 之前（不含）的最新 `limit` 条，**按 Id 升序**输出（便于直接渲染）。
  响应包裹形状（实现期钉死，2026-10-30 im-dev 定稿 lead 批准）：`{ "messages": [ {id, direction, status, content, errorText, sessionId, platformMsgId, createdAt} ... ], "hasMore": bool }`（camelCase）。
  `limit` 钳制 1..200。channelKey 走 query 参数（避开路由编码）。
- **FR-4 管理面鉴权（同类修复）**：`ImGatewayController` 类级补 `[Authorize("ApiKeyPolicy")]`（铁律 17，对齐 mcp-center 先例）。
- **FR-5 前端聊天式布局**（`/im-gateway` 重构）：
  - **左栏联系人列表**：`GET conversations`，条目=通道图标（企微）+ 显示名 + 最后消息预览（单行省略）+ 相对时间；选中态高亮；12s 轮询，**先比对再赋值防闪动**；`GET status` 的连接状态灯置于列表顶部（沿用既有 CONN_TEXT）。
  - **右栏消息线程**：选中联系人 → `GET messages` 渲染气泡：`in`=左侧、`out`=右侧；`status=error` 气泡带失败标记 + ErrorText 提示；顶部显示联系人名；底部「加载更多」按钮（hasMore 时）经 `beforeId` 翻旧消息并保持视口位置；12s 轮询增量刷新（仅追加，不重绘全列表）。
  - **配置视图**：header「配置」按钮切换 会话视图 ↔ 配置视图；配置视图=既有通道卡/扫码模态/保存逻辑**原样迁移**（功能不减）。
  - **版本徽标**：保留 `.ig-version`（读 plugin.json version）。
- **FR-6 插件文档**：`Plugins/ImGateway/README.md` 增补会话面板节；新建 `Plugins/ImGateway/docs/ui-design-brief.md`（面向设计师）；`plugin.json` Version 2.0.1→2.1.0 + Description 补会话面板。

## Input

- 数据源：`ImGatewaySession`（联系人全集）+ `ImGatewayMessage`（新，消息内容）；`GET status` 既有连接状态。
- 用户操作：点击联系人 / 滚动加载更多 / 切换配置视图。

## Output

- 端点见 FR-2/FR-3（camelCase JSON，对齐既有 JsonOpts）；页面布局见 FR-5。

## Business Rules

1. 消息落库为**尽力而为**：任何落库异常仅 XTrace 记录，不中断消息处理（网关可用性优先）。
2. `Event` 类消息与空内容消息不入消息表（与不入 AI 的现边界一致，Router 46-56）。
3. 查询端点只读；无任何删除/修改端点（无破坏性操作 → 无需二次确认）。
4. 鉴权：所有 `api/im-gateway/*` 端点需 ApiKeyPolicy（FR-4）。

## Boundary Conditions

- 全新库（无会话无消息）：`conversations` 返回 `[]` → 前端引导空态「还没有 IM 用户发来消息」+ 配置引导。
- 有会话映射但零消息（历史库升级后）：联系人照常显示，预览「暂无消息记录」，线程空态「该联系人还没有消息记录」。
- `beforeId` 不存在/越界：返回空数组 + `hasMore=false`（不报错）。
- `limit` 缺省 50、钳制 1..200；超界取边界值。
- 单联系人消息量超大：仅按 Id 游标翻页，一次最多 200 条。
- 通道停用（enabled=false）：联系人列表仍展示历史（数据是事实，不因通道停用消失）。

## Error Handling

- 落库异常：XTrace Error，主链继续（网关绝不阻塞）。
- 端点异常：400/空态兜底；未授权 401（鉴权后统一）。
- 前端：请求失败不闪空列表（保旧数据 + 错误条；轮询失败静默重试）。

## Compatibility

- **升级路径**：既有库升级 → 新表 `ImGatewayMessage` 由 XCode 建表（插件库连接名已登记，PC-003 ✓）；存量 `ImGatewaySession` 照常展示（零消息形态）。
- 404 修复批（AIAgent，未提交）与本批无文件交集；`[Authorize]` 补加后前端与 e2e 均已带 token（http.ts:13 / e2e helpers），无破坏。
- plugin.json 2.0.1 → 2.1.0（ minor：功能新增无破坏）。

## Non-functional Requirements

- 轮询 12s（与仓库惯例一致），比较后赋值防闪动（todo-tracker 先例）。
- 消息查询走索引（ChannelKey+Id）；单次 ≤200 条。
- 样式零裸色值：`--el-*`/design token（既有冻结模式延续，design:check 棘轮）。
- 插件根视图滚动容器子区块 `flex-shrink:0`（铁律 8）。

## Interaction Design（交互设计 · 必写节）

> 编写自查用 `.agents/skills/ui-ux-design`（CRAP）；五态（加载/空/出错/无权限/正常）逐点给。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 联系人列表 | 进入页面 | 左栏渲染联系人条目（图标+名+预览+时间），按最近消息倒序 | 加载中骨架 / 正常 / 错误条 | 无 toast（被动刷新） | 「还没有 IM 用户发来消息」+「去配置」引导按钮 | 单行省略；通道停用仍显示 |
| 选中联系人 | 点击条目 | 条目高亮，右栏加载该联系人消息线程 | 选中/未选中 | 无弹窗 | 线程空态「该联系人还没有消息记录」 | 快速连点防抖（同一 key 不重复拉取） |
| 消息气泡 | 线程渲染 | 收=左气泡、回=右气泡；时间戳逐条显示 | ok/error 分态 | error 气泡顶部错误条（ErrorText 摘要） | 见上 | 长文本 word-break 换行不破版 |
| 加载更多 | 点击「加载更多」 | 以最旧 Id 为 beforeId 拉旧消息**前插**，视口位置保持 | 有 hasMore 显示按钮 / 无则隐藏 | 按钮加载态 | — | 已在首屏 → 隐藏按钮 |
| 配置按钮 | 点击 header「配置」 | 切到配置视图（既有通道卡+扫码+保存，原样） | 会话/配置双视图 | — | — | 返回按钮回会话视图 |
| 轮询刷新 | 每 12s | 列表/线程增量更新（比对后赋值，选中项新消息自动追加并滚到底仅当用户已在底部） | — | 无闪动 | — | 失败静默保留旧数据 |
| 连接状态灯 | 随列表轮询 | 顶部状态灯+文案（沿用 CONN_TEXT）；非 connected 且已启用时显示「重连」按钮 | connected/connecting/failed/kicked/disconnected | — | — | 沿用既有 canReconnect 逻辑 |

### 交互验收标准（并入 AC）

- AC-1 进入 `/im-gateway` 看到左栏列表 + 顶部状态灯 + 右栏空态/线程（空库时左侧引导空态可见）。
- AC-2 dev 库种子 3 联系人 × 多条收/发消息后：列表顺序=最近消息倒序；点条目右栏渲染对应气泡，收左/回右/时间逐条可见。
- AC-3 AI 失败记录在气泡中可见错误标记与 ErrorText 摘要。
- AC-4 「加载更多」前插旧消息且视口不跳（单测断言请求参数 beforeId=当前最旧 Id）。
- AC-5 配置视图切换后既有功能全在（开关/BotId/Secret/扫码/保存/状态灯/重连）。
- AC-6 12s 轮询前后 DOM 节点复用（比对后赋值），无整列表闪烁（e2e 断言节点持久性或视觉无闪）。
- AC-7 未带 token 直调 `GET /api/im-gateway/conversations` → 401。

### 走查符合性（DoD 绑定）

- 按 ui-ux-design「走查 UI 符合性清单」逐项核对：CRAP 对齐（左右气泡贴边一致、列表密度一致）、长文本/窄屏不破版、点选反馈即时、轮询无闪动、空态引导可点。
- e2e 冒烟覆盖：页面加载 → 左栏空态/列表 → 配置切换 → 版本徽标；气泡渲染以单测+走查取证。

## Acceptance Criteria

- AC-B1 `ImGatewayMessage` 由 Model.xml 生成（xcode 幂等），实体成对（.Biz.cs 人工维护）。
- AC-B2 路由器成功链路：种子 scripted IChatCompletion → `in`+`out`(ok) 各 1 条落库，Content/SessionId 正确。
- AC-B3 路由器失败链路：AI 失败 → `in` + `out`(error, ErrorText=错误摘要)；落库抛异常不影响 CompleteAsync 已有返回。
- AC-B4 `GET conversations`：分组正确、displayName=末段、排序正确、messageCount 正确、camelCase。
- AC-B5 `GET messages`：升序 + limit 钳制 + beforeId 游标 + hasMore 正确；channelKey 缺省 400。
- AC-B6 控制器类级 `[Authorize("ApiKeyPolicy")]`；无 token 401，带 token 200。
- AC-B7 前端布局/交互逐条满足「交互验收标准」AC-1..AC-7。
- AC-B8 插件前端 `pnpm run build` 零错误（产物自检：`grep 'from "vue-router"'` 无需——本插件不引 router；零宿主 import）。
- AC-B9 后端 `dotnet build` + ImGateway 域过滤集绿；全量基线对表 0 归本批。
- AC-B10 冒烟 e2e（`e2e/plugins/im-gateway/im-gateway.spec.ts` 扩展）绿。
- AC-B11 dev-stack 走查截图读图逐项过 ui-ux 符合性清单。
- AC-B12 plugin.json 2.1.0；README + ui-design-brief 更新。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 企微 PlatformUserId 是否含可读昵称 | displayName 可能不友好（显示 ID） | 保守假设：显示 ChannelKey 末段原文；后续如企微侧能取昵称再增强（不做假数据） |
| 消息表增长速度（个人工具量级小） | 分页/索引策略是否足够 | 保守假设：游标分页 + ChannelKey+Id 索引已够；不做归档策略 |

## 修订记录

| 日期 | 修订 | 依据 |
| --- | --- | --- |
| 2026-10-31 | **FR-5/交互规格表方向语义变更**：气泡方向由「收=左、回=右」改为「**回复（out）=左、用户发来（in）=右**」——原按通用聊天惯例假定视角、未经需求方确认，用户 :51888 实测指认后拍板。修复与守卫见 `docs/ai/pilot/2026-10-31-imgateway-layout-direction-fix/`（e2e 语义锁+几何锁）。 | 用户输入9·2026-10-31 |
| 2026-10-31 | **D1 布局缺陷登记**：`.ig-chat__side` 缺 `min-width:0`，60 字符 nowrap 预览 min-content 撑破 300px 基准（实测左~788/右~460）；同批修复 + 长文本压力守卫（DESIGN.md 新建页面 §7 / agent-workflow B3 flex 尺寸双面铁律）。 | 同上 |
