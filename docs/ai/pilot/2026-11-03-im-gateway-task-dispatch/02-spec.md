# Specification

> 阶段：Stage 2｜Task ID：PILOT-079（2026-11-03-im-gateway-task-dispatch）
> 从 00-repository-understanding 与 01-intent 推导；不确定点在文末 Unknown 表显式记录。
> ⚠️ 2026-11-04 恢复注：本文件曾因 lead 脚本 bug 清空，由 lead 按原始内容+历次修订重建；product/design 两节追加件由各自重放恢复。

## Functional Requirements

### FR-1 跨插件契约 IAgentRunDispatch（新文件 ForgeSelf.Abstractions/AgentRunDispatchContracts.cs）

形状对齐既有先例（IAgentDelegation 的 Submit/Find + IScheduledAgentRunTrigger 的同进程驱动；契约注释写明提供方/消费方/禁 HTTP 自调）：

```csharp
public interface IAgentRunDispatch
{
    /// 提交一次任务（后台驱动，立即返回）。失败返回 Success=false + Error 原文，不抛业务异常。
    Task<AgentRunDispatchOutcome> SubmitAsync(AgentRunDispatchRequest request, CancellationToken ct = default);
    /// 按任务键读回快照；不存在/键失效返回 null。
    Task<AgentRunDispatchSnapshot?> FindAsync(string taskKey, CancellationToken ct = default);
```
- AgentRunDispatchRequest：TaskInput（必填任务原文）、AgentRef（string?；AgentId 或显示名；空=实现方默认链）、SessionId（必填，约定 imtask: 前缀）、ChatModelId?、CreatedBy?（如 imgateway:wecom）。
- AgentRunDispatchOutcome：Success、TaskKey（GUID N 形式）、RunId（long?，规划完成前为 null）、AgentId、AgentName、Status、Error（中文原文可直接展示）。
- AgentRunDispatchSnapshot：TaskKey、RunId、Status（复用任务中心统一词表 running/planning/pending/completed/failed/stuck/cancelled/unknown）、Terminal（bool；stuck 不算终态）、ResultText、AgentName、StartedAt/EndedAt。
- 提供方：ai-agent 在 Apply eager 构造 + ctx.Register<IAgentRunDispatch>（提供即 effect）；消费方 ImGateway 每次 ctx.Get，禁缓存字段。

### FR-2 AIAgent 派发服务（新文件 Plugins/AIAgent/Services/AgentRunDispatchService.cs）

- Submit：校验（TaskInput/SessionId 非空）→ AgentRef 解析（IAgentRegistryService.GetAgent(id) → miss 再 GetAllAgents() 按显示名精确匹配 → 仍 miss：Success=false + Error 列出可用 agent id/名清单）→ 生成 taskKey → **后台 Task** 驱动 IRunOrchestratorService.RunAsync（模式照抄 ScheduledAgentRunTrigger：迭代事件流、捕 runId（首个携带 runId 的事件）、记录 done/run_stuck/error 终局文本）→ 立即返回 Outcome（TaskKey 必有；RunId 尽力）。
- 任务原文尾缀追加审批策略说明（沿用 ScheduledAgentRunTrigger.ComposePrompt 语义：写操作仍走现有审批，不代跑系统命令）。
- 并发规则：同 SessionId 同时只允许 1 个未终态 Run（inflight 字典 + 开放态查询含 Stuck 双层）；重复提交返回 Success=false「该会话已有任务在执行（#id）」。
- Find：先查内存终态/在途快照；miss 且 key 为纯数字 → 按 RunId GetRunAsync 投影（**宿主重启后的恢复路径**）。
- RunRequest 组装：AgentId=解析结果、SessionId=request.SessionId、TaskInput=原文+审批说明、ChatModelId=request.ChatModelId（空值交给 orchestrator 既有 IChatModelResolver 链明确失败，PC-013 lens：不新增回退猜测）。

### FR-3 ImGateway 命令识别（改 Plugins/ImGateway/Services/ImGatewayRouter.cs，约 +15 行）

- 既有步骤全部不变：去重 → 事件消息跳过 → 会话映射 → 消息落库。
- 落库后新增分支：TaskGateway.Enabled 且 Content 命中前缀（默认两元素数组：/task␣ 与 任务：，可配）→ 委派 ImTaskDispatcher.HandleAsync(...) 后 return；否则走原聊天链路（**零改动**）。
- AgentRef 优先级：消息显式「@名字 」> 会话绑定 AgentId（GetOrCreateSession 既有返回）> 通道 BoundAgentId > 空（AIAgent 默认链）。
- Run 的 SessionId = imtask:{聊天会话Id}（稳定、可回溯；独立于聊天会话避免任务中心开放桶双投影）。
- 「任务状态」指令（可带 #RunId）：查该会话最近绑定 → 回快照；无绑定回「暂无任务，发 /task 创建」。

### FR-4 绑定持久化与 Watcher（新文件 Plugins/ImGateway/Services/ImTaskDispatcher.cs、ImTaskWatcher.cs；新实体 Data/Entities/ImGatewayTask.cs）

- 实体 ImGatewayTask：TaskKey、RunId?、ChannelKey、ChannelType、ConversationId、PlatformUserId、ChatType、ReplyReqId?、InboundMsgId、TaskInput 摘要（200 字）、Status、CreatedAt、RepliedAt?、PromptedApprovalIds（审批提示幂等）。登记进 ImGatewayTables.cs（插件自有库，XCode 自动建表）。
- ImTaskDispatcher：命令解析（前缀/@agent 剥离）→ ctx.Get<IAgentRunDispatch>().SubmitAsync → ack 回执（成功：「已建任务（编号 xxxxxxxx）已指派 {AgentName}，完成后自动回复」；此刻 ReplyReqId 仍热，用它回）→ 绑定行落库。
- ImTaskWatcher（HostedService，先例 ImGatewayConnectionHostedService）：每 PollIntervalSeconds（默认 10s）扫未终态绑定 → FindAsync(taskKey)（拿到 RunId 后改按 RunId 查）→ 状态回写；**终态 → 组回复并经通道主动推送**（SendAsync 用 ChatType 解析，不用可能过期的 ReplyReqId）→ 记 RepliedAt（幂等，不重发）。
- MaxWaitHours（默认 24h）超时 → 回复「仍在执行，请去任务中心查看」并不再跟进。
- 宿主重启恢复：Watcher 启动扫库未终态绑定，按 RunId 续跟（taskKey 丢失窗口仅限规划期，可接受，已记 Unknown）。

- **【2026-11-04 用户拍板修订（T16，验收#8）】** 执行观察节奏：轮询节拍 PollIntervalSeconds 默认 **60（1 分钟）**（原文「默认 10s」废止）；快照指纹（状态/步骤进度 CurrentStepIndex/StepCount/结果文本）有变化 → **即时推送简短进展消息**（`ProgressPushEnabled` 子开关默认开，可关防刷屏；首轮只建基线防重启轰炸）；**连续 `StalledMinutes`（默认 5）分钟无变化才判卡住并通知**（与 AIAgent 侧无进展阈值 300s 对齐，双源先到先推+行级幂等去重）；**终态推送不受节拍影响（终态即推）**。

### FR-5 结果回复模板

- completed：`✅ 任务 #{RunId} 完成\n{ResultText 截断}`（MaxReplyChars 默认 1500，超出加「…全文见任务中心 #{RunId}」）。
- failed：`❌ 任务 #{RunId} 失败\n{失败原因}`。
- stuck：`⏸ 任务 #{RunId} 等待处理\n{原因}。请到宿主任务中心/审批中心处理，处理后在智能体页续跑。`
- ResultText 取值链：done 事件 content → 末步 Output → StuckReason → 错误原文（实现期确认 done 语义，见 Unknown）。

- **【2026-11-04 用户拍板修订（T12，验收#5）：截断→分段发送】** 取消静默截断：回复超通道单条上限 **`MaxCharsPerMessage`（默认 2000，落 TaskGatewayConfig）**时**分段多次发送**——段间顺序保证（await 顺序不并发）、段尾加「(i/n)」标记、短文单段无标记；**`MaxReplyChars` 改总保险丝（默认 6000，0=关）**，超限才截断并加尾注「…全文见任务中心 #{RunId}」（原「默认 1500 超出截断」废止）；**分段中途失败=全量重发**（RepliedAt 全段成功才置位，防重复推送优先；断点续发需持久化进度属后续票）；aibot stream 每段独立帧。

### FR-6 配置（改 Plugins/ImGateway/Core/ImGatewayConfig.cs）

WeCom.TaskGateway：Enabled=false；CommandPrefixes 默认「/task␣」「任务：」两条；PollIntervalSeconds=10；MaxReplyChars=1500；MaxWaitHours=24；DefaultAgentId=null（空=跟随会话绑定；design D2 2026-11-03 增补，否则默认 Agent 下拉无落点）；ImApprovalEnabled=true；ApproveKeywords 默认[同意,批准]；RejectKeywords 默认[拒绝,驳回]。经既有 config GET/POST 端点透传，不改端点签名。

### FR-7 设置面板小节（2026-11-03 用户拍板：包含）

ImGateway web 设置面板加「任务网关」卡片（开关/前缀/默认 Agent/轮询间隔/审批子开关/关键字）（已拍板包含，fe-dev 实现）。

#### FR-7 视觉与交互规格（design · 2026-11-03）

> 依据：实读 `Plugins/ImGateway/web/src/ImGatewayView.vue`（v2.1.1 面板结构/样式/保存逻辑）+ `web/src/http.ts` + 仓库根 `DESIGN.md` + `.agents/skills/ui-ux-design/SKILL.md`。本节为 FR-7 实现级细化（fe-dev 按此实现），只追加不改既有；与上文「插件设置面板」交互表冲突处，以本节 D1/D2 为准。

**D 设计决策（先读）**

- **D1 保存模型=面板既有统一保存，不做「切换即保存」**：上文交互表「总开关｜切换｜即时保存（既有 config PUT）」与面板实读不符——实际链路是底部唯一「保存配置」按钮 → `POST /api/im-gateway/config` **整包**保存 → 回读 → 顶部提示条（`http.ts` 头注释 L8-9、saveConfig L48-50；`ImGatewayView.vue` save() L193-205；动词是 POST 非 PUT）。本小节跟随整包模型：避免「开关已开、字段未存」的半保存态，同类设置一个心智模型（CRAP-Repetition）。
- **D2 新字段依赖（须 T2 落地）**：FR-6 的 TaskGateway 块未列「默认 Agent」，面板下拉需要 `TaskGateway.DefaultAgentId: string?`（空=跟随会话绑定）作落点；config 是整包透传，后端缺字段则保存后回读丢失。`MaxReplyChars`/`MaxWaitHours`（FR-6 已有）**不进面板 UI**，走默认值 1500/24（拍板 7 字段之外不上界面）。

**1. 布局分区**（配置视图内、企业微信通道卡之后新增一卡，完全复用 `.ig-channel` 卡形态：fill 底 + 边框 + `--glass-radius` + 16px 内边距）

```
┌ 任务网关 ─────────────────────────────[已启用/未启用]──[总开关] ┐
│ 卡头：名称「任务网关」 + 徽标 + 一句职责说明                      │
│ ─ 分区 A · 派发规则 ─                                           │
│ 命令前缀    [tag 输入：chip ✕｜输入回车/失焦成 chip]   （≥1 条）  │
│ 默认 Agent  [select：跟随会话绑定 ▾ + 智能体名册]                │
│ 轮询间隔    [number 输入] 秒（默认 10，允许 3–3600）             │
│ ─ 分区 B · IM 审批 ─（子开关行：说明文案 + 右侧开关）             │
│ 同意关键字  [tag 输入：同意 / 批准]                              │
│ 拒绝关键字  [tag 输入：拒绝 / 驳回]                              │
│ 尾注 ig-note（默认值 · 生效时机 · 关键字不拦截普通聊天；跨两列）    │
└────────────────────────────────────────────────────────┘
保存不新增按钮：跟随面板底部既有「保存配置」（ig-footer 唯一主操作）。
```

**2. 字段控件形态**（7 字段；插件禁 EP 组件 → 全部原生 HTML + token，控件形态对齐面板既有类）

| 字段 | 控件形态 | 默认 | 约束/校验 | 联动 |
| --- | --- | --- | --- | --- |
| Enabled 总开关 | 既有 `.ig-switch`（44×24，checked 金底）置卡头右侧 | off | — | off → A/B 分区整体禁用（§3） |
| CommandPrefixes 命令前缀 | tag 输入：32px 文本框（placeholder「如 /task 或 任务：」）+ 回车/失焦成 chip；chip=既有 `.ig-badge` 形态（mono 11px + `--radius-sm` + `--paper-tint` 底 + fg-secondary 字 + 右侧 ✕，**hover 才金边金字**，禁主色实心填充=技能 J 条）；chip 用 mono 展示原文（`/task␣` 尾随空格属匹配规则，由尾注说明） | 「/task␣」「任务：」 | ≥1 条；去重；空串不入列；空列表禁保存（§4） | — |
| DefaultAgentId 默认 Agent | 原生 `<select>`（32px、`--el-bg-color` 底、`--el-border-color` 边、`--radius-md`、13px）；首项「跟随会话绑定」=空值；选项= `GET /api/agents`（AIAgent AgentsController，实读确认），显示 `Name`、值 `Id`，仅列 IsEnabled | 空（跟随会话绑定） | 名册加载失败 → select 置灰 + hint「名册加载失败，可留空走默认链」，不白屏 | — |
| PollIntervalSeconds 轮询间隔 | `<input type="number">` + 行内后缀「秒」 | 10 | 整数 3–3600（防 0/负数打爆后端与超大值失效）；越界 → 行内红字 + 保存阻断（§4） | — |
| ImApprovalEnabled 审批子开关 | 行内布局：label「IM 审批」+ desc「任务卡住时经 IM 推送审批卡，回复关键字即可决策」+ 右侧 `.ig-switch` | on | — | off → 两个关键字字段禁用（§3） |
| ApproveKeywords 同意关键字 | 同 CommandPrefixes 的 tag 输入 | 「同意」「批准」 | ≥1 条（审批开启时）；去重 | 子开关 off 禁用 |
| RejectKeywords 拒绝关键字 | 同上 | 「拒绝」「驳回」 | 同上 | 同上 |

**3. 禁用态与可见原因**（无声禁用=违规，对齐技能「禁用写具体原因」）

- 总开关 off：A/B 分区整体禁用（控件加 disabled；视觉 opacity .6 + cursor not-allowed，沿用 `.ig-btn--primary:disabled` 形态），分区上方一行提示「任务网关未启用——打开右上开关后可编辑」；徽标=`ig-badge--off`「未启用」。
- 子开关 off：两个关键字字段禁用 + 行内提示「IM 审批未启用，关键字暂不生效」。
- 禁用≠隐藏：字段仍可见，数值保留不丢（可预期性）。

**4. 保存反馈与校验**（全部复用既有链路，不新增反馈形态）

- 触发：仅底部「保存配置」（saving 时按钮「保存中…」+ disabled）。
- 前置校验（保存时，先校验后请求）：前缀空列表 → 顶部 `ig-alert--err`「至少保留 1 条命令前缀」+ 阻断；轮询间隔越界 → 行内红字 + 阻断（错误不只靠红色，带文字原因）。
- 成功：既有提示「已保存并重新加载配置」（`ig-alert--ok`）+ 回读表单。
- 失败：`ig-alert--err`「保存失败：{后端原文}」；表单保留用户输入不回滚。
- 生效时机（尾注文案）：保存后下一轮轮询（≤ 轮询间隔）生效；改前缀不影响已建任务。

**5. 五态**（小节级，全部映射既有形态，不新造组件）

| 态 | 表现 |
| --- | --- |
| 加载 | 表单先以默认值渲染（不空屏），config 返回后回填（既有 load() 行为） |
| 空 | 无「无配置」态：后端缺省值兑底（off + 默认前缀/关键字），永远有可编辑表单 |
| 出错 | load 失败 → 顶部 `ig-alert--err`「加载失败：{原文}」，表单仍可改（保存即覆盖）；名册失败 → select 置灰见 §2 |
| 无权限 | 401 由宿主统一登录提示处理（既有 brief 口径），插件不重复处理 |
| 正常 | 表单可编辑；徽标/开关/字段联动如 §2-§4 |

**6. token 引用清单**（零新增裸色值，全部复用面板既有 token；design:check 棘轮守卫兜底）

- 色：`--gold-400` / `--gold-300` / `--gold-tint`（focus 晕、hover）/ `--bronze-500`（徽标 on）/ `--cinnabar-400`（错误字 + color-mix 派生底）/ `--ink-950`（金底按钮字）/ `--paper-tint` / `--paper-tint-weak`（chip 底、note 底）/ `--el-bg-color` / `--el-fill-color-light` / `--el-border-color` / `--el-color-success`
- 字：`--font-sans-cn`（表单正文）/ `--font-serif-cn`（卡头名）/ `--font-mono`（chip、数字后缀、徽标）
- 形/动：`--glass-radius` / `--glass-shadow` / `--radius-sm` / `--radius-md` / `--dur-fast` / `--ease-out`（hover `translateY(-1px)` 沿用 `.ig-btn`）
- 布局：字段沿用 `.ig-field`/`.ig-bind` 的 220px label + 1fr 控件两列栅格与间距节奏；分区间距沿用卡内既有节奏，不引入新间距刻度

**7. 走查补充点**（实现后按 ui-ux-design 清单全项核对，此处列本小节特有项）

- [ ] 总开关 off→on：字段解禁、原因提示消失、禁用期间数值不丢
- [ ] 前缀删到 0 条：保存被阻断，错误条写明字段与原因
- [ ] 关键字 chip 默认玻璃淡白底、hover 才金边（禁主色实心填充）
- [ ] 保存成功重进页面：7 字段（含 DefaultAgentId）回读一致（整包透传验证）
- [ ] 名册空/失败：select 不白屏，可留空保存走默认链
- [ ] 窄屏 1024px：两列栅格不破版（无横向滚动）

### FR-8 模拟入站端点（测试/自检触发器 · 2026-11-03 输入4 增补）

- POST api/im-gateway/simulate-inbound（类级 [Authorize("ApiKeyPolicy")] 既有，不改鉴权；2026-11-04 偏差修正：按控制器类级真实路由前缀 api/im-gateway，规格原文 /api/imgateway/... 与实际不符）。
- Body：channelType / conversationId / platformUserId / content / chatType?（msgId 缺省由端点生成，避免既有幂等去重拦截）→ 组装与生产同形状 InboundMessage → **直接调 ImGatewayRouter.ProcessInboundAsync（零旁路）** → 200 {success, note}。
- 用途：① e2e/集成测试「主动触发 IM 收到消息」的唯一自动化入口（零 mock：真实 Router→真实契约→真实 AIAgent 真 Run）；② 以后通道→路由链路的运维自检工具。本批不做管理面按钮。
- 边界：仅文本消息；不伪造事件类消息；端点本身不做任何任务语义（派发与否完全由 Router 分支决定）。

### FR-9 IM 审批闭环（2026-11-03 输入5 用户增补）

机制事实（读码确认）：审批记录由宿主 PersistentApprovalService 持有（IApprovalService = 宿主契约，AppBuilder 注册）；MAF 循环在**下一回合**才经 ConsumeContinuationsAsync 消费决策（MafTaskLoopAgent.cs:205）⇒ 批准后 Run 不会自动续跑，需要显式 resume。

- **审批卡片推送**（ImTaskWatcher）：轮询发现 Status=stuck 时，`ctx.Get<IApprovalService>().ListBySessionAsync(imtask 会话Id)` 查 Pending 审批；命中且该审批 Id 未提示过 → 发卡片：「⏸ 任务 #12 等待审批\n工具：{ToolName}（风险 {RiskLevel}）\n{Description 截断 200}\n回复『同意 #12』放行，『拒绝 #12』拒绝」。每个审批 Id 仅提示一次（记 ImGatewayTask.PromptedApprovalIds 列，逗号分隔，上限 10 个）；审批 Expired/已决策不再提示。
- **审批关键字决策**（ImTaskDispatcher，分支优先级：任务前缀 > 审批关键字 > 任务状态 > 聊天）：消息命中 ApproveKeywords（默认 同意/批准）或 RejectKeywords（默认 拒绝/驳回）且本通道会话存在已提示待审批任务 → 解析目标（#12 指定；缺省且唯一待审批任务则取之）→ **决策**：`ctx.Get<IApprovalService>().DecideAsync(approvalId, approved, note:「imgateway:{channelKey} 经 IM 决策 @时间」)`（宿主契约直调，非 HTTP）→ **续跑**：调契约新方法 `IAgentRunDispatch.ResumeAsync(taskKey)`（进程内 orchestrator.ResumeAsync，后台驱动同 Submit）→ ack：「已同意 #12（write_file），任务继续执行」/「已拒绝 #12」。
- **安全边界**：① 只能决策本通道会话自己任务的审批（会话绑定校验，跨会话拒绝）；② note 落 IM 来源留痕（审计）；③ 子开关 ImApprovalEnabled（默认随主开关开）；④ 无待审批时关键字不拦截，走原聊天链路（零误伤）；⑤ 审批 Expired（fail-closed）→ 回「该审批已超时，请在任务中心重新发起/续跑」。
- **契约增量**（归 T1）：IAgentRunDispatch 增第三方法 `ResumeAsync(string taskKey, string? note, CancellationToken)`（仅 Stuck/Failed 允许，语义同 orchestrator.ResumeAsync；后台驱动，立即返回 Outcome）——同时服务未来「续跑 #12」指令。

- **【2026-11-04 用户拍板修订（T12/T16 联动）】** TaskGateway 配置块更新：`PollIntervalSeconds` 默认 10→**60**；`MaxReplyChars` 默认 1500→**6000**（语义改总保险丝，0=关）；新增 **`MaxCharsPerMessage`=2000**（分段通道阈值）、**`ProgressPushEnabled`=true**（进展推送子开关）、**`StalledMinutes`=5**（无变化判卡分钟数）。其余字段不变，config 端点整包透传不改签名。
## Input

- 入站 IM 文本消息（WeCom 通道，InboundMessage）；配置（TaskGateway 块）；会话映射（GetOrCreateSession）。

## Output

- 即时 ack 回执；终态主动回复；绑定表行；AgentRun（任务中心可见）；契约快照；XTrace 日志。

## Business Rules

1. 状态词表与任务中心 TaskOverviewStatus 完全一致（B3 §3.2），不造第二套。
2. 任务中心零改动：imtask: Run 自动进 planned 桶（TasksOverviewController 已投影全部 Run）。
3. 契约失败一律 Success=false + Error 中文原文，不抛业务异常（对齐 IAgentDelegation）；网关侧全程吞异常不阻塞（对齐 Router 现有语义）。
4. 审批不绕行：任务内写操作照常触发 ToolApproval → Run 进 stuck（等待审批），IM 侧如实回复并指引宿主处理。
5. 每会话 1 个未终态任务上限；超限拒绝并提示现有任务号。
6. IM 审批安全边界：仅限本通道会话自身任务的审批；决策必带 IM 来源 note（审计）；ImApprovalEnabled 子开关；无待审批时关键字不拦截（零误伤）；Expired fail-closed 不改写。

7. 【2026-11-04 用户拍板修订（T14，验收#7/#8）】stuck 语义校正：**等待审批≠无进展超时**——挂起原因三分类（**等待审批 / 无进展超时 / 未声明完成**，ComposeSuspendReason+PendingApprovalProbe 分类可辨）；单步骤无进展阈值 45s→**300s 默认，`AIAgent_StepTimeoutSeconds` 环境变量可配（10..7200 clamp）**；tasks-overview stuck 行下发 resume 与 **CancelUrl**（终止入口）。

## Boundary Conditions

- 前缀命中但内容为空 → 回用法提示，不建任务。
- @名字 不存在/重名 → Submit 层拒绝（Error 列候选），不建 Run。
- Run 被人工在宿主侧 resume/cancel → Watcher 按最新状态回复。
- 通道发送失败 → RecordError 落库 + 进程内 _sendAttempted 去重不重发（宿主重启恢复路径可再尝试一次，有界不刷屏；RepliedAt 仅成功后置位）——2026-11-04 偏差裁决（be-dev 实现，lead 批准：防重复推送优先，失败在消息页可见）。
- 超长任务原文：TaskInput 全文进 Run（orchestrator 既有行为），绑定表只存 200 字摘要。

## Error Handling

- 契约实现内部 try/catch 全包；网关 Dispatcher/Watcher 单轮询周期异常 try/catch 不终止 HostedService；关键事件（派发/终态/回复失败）XTrace 落日志。

## Compatibility

- Enabled=false：Router 分支短路，行为与 v2.1.1 完全一致（含既有 e2e）。
- 不改既有契约（IChatCompletion / IScheduledAgentRunTrigger）、不改 TasksOverviewController / RunOrchestratorService 既有行为。
- 版本：ImGateway 2.1.1→2.2.0；AIAgent 1.9.9→1.10.0（新契约；基线偏差见 03-plan）。

## Non-functional Requirements

- Watcher 单实例、轮询 O(未终态绑定数)；内存 inflight 上限 100 条（超限最旧快照淘汰，恢复路径靠 DB 兜底）。
- 回复限长可配；日志三类关键事件落 XTrace。

## Failure Modes（v1.2.0 节；新功能任务，命中 lens 列示）

| 命中 | 问题 | 本任务如何满足 |
| --- | --- | --- |
| PC-013 lens | IM→AI 链路模型解析回退猜测致 404 | 任务路径 ChatModelId 空值复用 orchestrator 既有 IChatModelResolver 明确失败语义，不新增回退 |
| PC-014 lens | 测试手搓 DAL.AddConnStr 换库污染全局态 | 新测试一律 XCodeTestFixture 模式（04-task Forbidden 明令） |
| （既有教训迁移）Router 吞异常 | 失败静默 | 关键路径失败均落 XTrace + 消息表 out/error，用户可见 |

## Interaction Design（插件任务必写节 · 2026-10-30 用户立）

### 无新增宿主页面

任务中心/智能体页零改动，复用既有视图与交互（imtask: 任务在计划任务桶以既有行样式展示，点击跳智能体页）。

### IM 侧交互（主交互面=IM 通道本身）

| 交互点 | 触发（用户发） | 结果（网关回） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 建任务 | 「/task 写个xx报告」 | ack：已建任务（编号 xxxxxxxx）已指派 X，完成后自动回复 | 无→已建（绑定落库） | 即时回执 | — | 会话已有任务→回「已有任务在执行 #N」；前缀后空内容→回用法 |
| 指定 Agent | 「/task @程序员 整理xx」 | 同上，AgentName=解析结果 | 同上 | 同上 | — | 名字不存在/重名→回候选清单 |
| 查状态 | 「任务状态」/「任务 #12」 | 最近（或指定）任务一行快照：状态/指派/耗时/原因 | 非终态/终态 | 即时回执 | 「暂无任务，发 /task 创建」 | #id 不存在→回未找到 |
| 终态推送 | （无触发，后台） | 主动推送 FR-5 模板文本 | 终态即回，RepliedAt 幂等 | — | — | 发送失败记 out/error 不重发 |
| 审批卡片 | （后台，任务 stuck 且有 Pending 审批） | 主动推送审批卡（工具/风险/描述+关键字指引），每审批仅一次 | 待审批/已提示 | 即时推送 | — | Expired 不提示不决策 |
| 审批决策 | 回复「同意 #12」/「拒绝 #12」 | 调统一审批（DecideAsync+IM 留痕）→ 触发续跑 → ack | Pending→Approved/Denied | 即时回执 | 无待审批→走原聊天链路 | 跨会话拒绝；Expired→指引任务中心 |
| 非任务消息 | 任意其他文本 | 走原聊天链路（完全不变） | — | — | — | Enabled=false 全部走原链路 |

### 插件设置面板（已拍板包含；保存口径修正见下注）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 总开关 | 切换 | 即时保存（既有 config PUT） | on/off | toast「已保存」 | 默认配置兑底 | off 时其余字段禁用且可见原因 |
| 前缀编辑 | 增删 tag | 下一条消息起生效 | 列表≥1 | toast | 默认两条兑底 | 空列表禁保存+原因可见 |
| 默认 Agent | 下拉（既有 agent 名册） | 保存 | 单选 | toast | 「跟随会话绑定」选项 | — |

> ⚠️ 口径修正（design D1，2026-11-03 实读 http.ts:48）：面板为统一「保存配置」按钮**整包 POST** /api/im-gateway/config，非逐字段即时保存——本表「即时保存」行以「FR-7 视觉与交互规格（design 节）」为准。

五态（面板）：加载=卡片骨架；空=默认配置兑底不空屏；出错=错误条+重试；无权限=401 引导设置页；正常=表单。视觉：复用面板既有 token，无新色值（design:check 棘轮守卫）；走查按 ui-ux-design 清单全项核对。

### 用户旅程验收剧本（product · 2026-11-03）

> 本剧本为产品验收真源：把用户已核对的 7 条旅程固化为可执行走查单，产品/QA 走查时按旅程逐条执行、每步对照「系统行为断言点」核验并留痕（对应 04-task AC11）。本小节为追加件，不改既有规格。

| 旅程 | 步骤（用户动作 → 系统响应） | 每步系统行为断言点 | 对应 AC |
| --- | --- | --- | --- |
| A 建任务主链路 | ①发「/task 写个xx报告」→收 ack ②任务中心计划任务桶查看 ③Agent 后台执行 ④企微收终态主动回复 | ① 1s 内 ack「已建任务（编号 xxxxxxxx）已指派 {AgentName}，完成后自动回复」，ImGatewayTask 绑定行落库且 Run SessionId=imtask:{会话Id} ② Run 出现于任务中心计划任务桶（source=planned，零改动投影） ③ Run 状态按任务中心统一词表流转（planning→running），不造第二套词表 ④ 终态后 Watcher 主动推送「✅ 任务 #{RunId} 完成\n{ResultText≤MaxReplyChars=1500}」，RepliedAt 置位不重发 | AC2 AC3 AC4 AC5 |
| B 指定 Agent | ①发「/task @程序员 整理xx」→收 ack ②发「/task @不存在名字 整理xx」 | ① ack 指派名=解析结果（AgentRef 优先级：@名字>会话绑定>通道 BoundAgentId>默认链） ② 不建 Run，回候选清单（可用 agent id/名），可按清单重发 | AC1 AC2 AC3 |
| C 查进度 | ①发「任务状态」 ②发「任务 #12」 ③无任务会话发「任务状态」 ④发「任务 #999」（不存在） | ① 回该会话最近绑定任务一行快照（状态/指派/耗时/原因，状态词表与任务中心一致） ② 回 #12 指定快照 ③ 回「暂无任务，发 /task 创建」 ④ 回任务未找到 | AC2 AC3 |
| D 失败卡住 | ①任务执行遇写操作→审批触发 Run stuck ②企微收 stuck 主动推送 ③企微收审批卡片 | ① Run Status=stuck（统一词表） ② 主动推送「⏸ 任务 #{RunId} 等待处理\n{原因}+宿主任务中心/审批中心指引」 ③ 审批卡含工具名/风险级/描述（截断 200）+关键字指引，每审批 Id 仅推送一次（PromptedApprovalIds 幂等）；failed 终态同理推「❌ 任务 #{RunId} 失败\n{原因}」 | AC3 AC10 |
| G IM 审批闭环 | ①回「同意 #12」 ②回「拒绝 #12」 ③审批已 Expired 后回「同意 #12」 ④跨会话/无待审批时回关键字 | ① DecideAsync（note 落 IM 来源留痕）+ResumeAsync(taskKey) 被调→Run 自动续跑→ack「已同意 #12（write_file），任务继续执行」 ② ack「已拒绝 #12」，Run 按决策落终态 ③ Expired fail-closed 不改写审批→回「已超时，请在任务中心重新发起/续跑」 ④ 仅本通道会话自身任务的审批可决策（跨会话拒绝）；无待审批时关键字不拦截走原聊天链路（零误伤） | AC10 |
| E 不受影响面 | ①发普通聊天「今天天气如何」 ②Enabled=false 后重跑旅程 A | ① 非任务前缀/非审批关键字/非状态查询的消息全部走原聊天链路（Router 既有步骤零改动） ② Router 分支短路，行为与 v2.1.1 完全一致：无绑定落库、无主动回复，既有 im-gateway e2e 全绿 | AC5 AC6 |
| F 容错 | ①同会话在途时再发第二条 /task ②宿主重启后观察在途任务 ③任务执行超 24h | ① 第二条被拒「该会话已有任务在执行（#id）」（每会话 1 个未终态上限） ② 重启后 Watcher 启动扫库未终态绑定按 RunId 续跟（taskKey 丢失窗口仅限规划期） ③ 回复「仍在执行，请去任务中心查看」后不再跟进 | AC2 AC3 |

走查留痕：按 A→B→C→D→G→E→F 逐条执行，每步在 05-evidence/走查记录勾选并附截图或日志证据；任一断言点不符即记缺陷回流 02-spec。

## Acceptance Criteria

- [ ] AC1 契约单测：Request 校验（TaskInput/SessionId 空）、AgentRef 三态（id 命中/名字命中/miss 列候选）绿。
- [ ] AC2 派发服务单测：Submit 即回 taskKey；mock orchestrator 驱动至终态 → Find 返回 Terminal+ResultText；同会话并发第二条被拒；Find 纯数字按 RunId 回退（重启路径）绿。
- [ ] AC3 网关单测：前缀命中/未命中分支；@agent 剥离；绑定落库与 ack 文案；watcher 终态回复模板 + RepliedAt 幂等；重启扫库恢复 绿。
- [ ] AC4 集成实测（dev-stack）：开启后经 simulate-inbound 发「/task …」→ tasks-overview planned 桶出现该 Run（sourceRef=runId）**且** GET /api/ai-agent/runs?sessionId=imtask:* 命中同一 runId（qa 读码修正 2026-11-03：TaskOverviewDto 不含 sessionId，TasksOverviewController.cs:368-396，前缀断言须经 runs 接口）→ 终态后消息表 out/ok + 回复文本证据。
- [ ] AC5 e2e 冒烟 1 条主链路绿（真实宿主零 mock；宿主无可用 Agent/模型时，先经 /api/agents REST 种子真实测试 Agent 或 config 绑定既有 Agent——真实数据准备不算 mock）；既有 im-gateway e2e 不红。
- [ ] AC6 Enabled=false 回归：既有聊天链路 e2e 全绿。
- [ ] AC7 中档全量 dotnet test 对表基线 0 归本批；若动 web：插件前端 build+check+design:check 零新增。
- [ ] AC8 工件 00-07 齐 + pre-commit（-EnforceInteraction）绿。
- [ ] AC9 simulate-inbound 端点：鉴权生效（无 token 401）+ 触发后 Router 收到同形状 InboundMessage（单测）+ 作为 e2e 唯一消息注入入口。
- [ ] AC10 IM 审批闭环：卡片每审批 Id 仅推送一次（幂等）；「同意 #12」→ DecideAsync(note 留痕)+ResumeAsync 被调→Run 续跑；「拒绝」同理；跨会话审批被拒；Expired→指引文案；无待审批时「同意」走聊天链路（零误伤）绿。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| done 事件 content 是终局全文还是摘要/收据 | FR-5 ResultText 取值链 | 实现期读 RunOrchestratorService 终局合成段确认；快照取值链已设计多级兑底 |
| plan_created 是否携带 runId（首个 step_started 前无 runId） | ack 文案编号形态 | 契约按 RunId 可空设计，ack 用 taskKey 短码；Watcher 拿到 RunId 后回填 |
| 企微群聊/单聊主动推送 chatid 实际解析效果 | 终态回复可达性 | 走查实测（SendAsync ChatType 路径已有代码）；不可达则兑底「下条来消息时携带结果」并在 05 记录 |
| AgentRef 空时默认 agent 语义与聊天「(默认)」是否完全一致 | 不显式指派的路径 | 复用同一解析链（已对齐 AIChatController 传参语义）；实现期对齐测试 |