# 06 · Review — PILOT-079（2026-11-03-im-gateway-task-dispatch）

> 本文件由 product 于 2026-11-04 创建，当前仅含「产品符合性终审」节；其余 Review 节（八问 / Final Decision 等）由 lead 补齐。

## 产品符合性终审（product · 2026-11-04）

- 终审依据：02-spec「### 用户旅程验收剧本（product · 2026-11-03）」（L237-251）七条旅程逐断言点判定。
- 证据包（product 亲读）：① 05-evidence.md（qa 三轮 e2e 全绿 + §5 dev-stack 走查 + §6 Unknown/裁决）；② 走查截图 ForgeSelf.Web/screenshots/dev/p079-dev-01~04（逐张读图）；③ lead AC4f 源码级实测 .temp/tmp/p079-ac4f-1~5（Run 10 / 消息链 #25-#27，五件全文亲读，overview 中 planned:10 条目亲读确认）。

| 旅程 | 判定 | 证据指路（对照剧本断言点） |
| --- | --- | --- |
| A 建任务主链路 | 符合（failed 路径端到端实证；completed 子路径证据未覆盖，单测背书） | ① ack 0.45s：#25 入站 01:08:46.043 → #26 ack 01:08:46.489（ac4f-5-messages.json），文案「已建任务（编号 c73eb6c7）已指派 通用助手，完成后自动回复」逐字对 FR-4；Run sessionId=imtask:6ec7f546…（ac4f-3-runs.json）② 任务中心 planned 桶：overview id=planned:10 / sourceRef="10" / title=输出本周迭代总结（ac4f-4-overview.json 亲读）+ 截图 dev-01 计划任务桶置顶失败行 ③ 状态词表统一：runs status 与 overview 同源（failed + 404 原文）④ 终态主动推送 #27「❌ 任务 #10 失败\n步骤 s1 失败: …404…」out/ok（ac4f-5）；RepliedAt 幂等 + RunId 0→1 回填 = e2e ⑦（05 §3，ImGateway.db 实证）。completed 终态：本环境无上游模型不可达，与 failed 共享 Watcher 回复路径——证据未覆盖，单测背书（05 §6-1，lead 要求终审门 2 显列） |
| B 指定 Agent | 符合（默认链路径实证；@名字 显式指定与 miss 候选清单=单测背书） | ① DefaultAgentId=agent.generalist 生效：ack「已指派 通用助手」+ runs agentName=通用助手（ac4f-1/3/5）+ 截图 dev-03 默认 Agent=通用助手，AgentRef 优先级末级（默认链）实测走通 ② miss 不建 Run：e2e 一轮红「任务派发失败：未找到 Agent e2e-imtask-agent」快速失败实证 miss 语义（05 §2 一轮；lead 留档：REST 种子写路径本身通，缺陷 #2 仅派发侧可见性，05 §6-5）；候选清单文案与 AgentRef 三态解析=AC1 单测背书 |
| C 查进度 | 符合（单测背书；IM 快照回复运行时证据未覆盖） | 「任务状态」/「任务 #12」分支与快照回复文案=AC3 网关单测绿（05 §1 三轮全绿）；快照数据源 FindAsync=AC2 单测绿。主链路 e2e 未含该指令、走查未触发——运行时证据未覆盖，遗留走查项；数据源与任务中心同源已实证（runs/overview 亲读），不阻塞 |
| D 失败卡住 | 部分符合（failed 推送实证；stuck 推送与审批卡=证据未覆盖，单测背书） | failed 主动推送模板逐字对 FR-5：#27（ac4f-5）+ 截图 dev-02 / dev-04（❌ 任务 #9 失败——标签用 RunId 非 ShortCode，RunId 回填可视实证，缺陷 #1 修复跨宿主一致）；stuck 词表在任务中心可视（截图 dev-01「卡住」pill 多行）。stuck 主动推送 / 审批卡（工具/风险/描述截断 200 + 关键字指引、每审批 Id 仅一次）：本环境无真实 LLM 推不到工具审批 stuck、铁律禁 mock——证据未覆盖，单测背书 + dev-stack 人工见证归位（05 §6-2 fixme，lead 裁决） |
| G IM 审批闭环 | 证据未覆盖（单测背书 + 配置面就绪实证；运行时闭环待真实 LLM 场景补验） | 配置面就绪：截图 dev-03——IM 审批子开关 ON + 同意/批准、拒绝/驳回关键字 tags + 尾注「关键字仅在本会话存在待审批任务时拦截，其余消息照常走普通聊天（零误伤）」（FR-6/FR-7 静态实证，config 整包含 imApprovalEnabled=true 见 ac4f-1）。运行时闭环（卡片每审批一次幂等 / 「同意 #12」→DecideAsync(note IM 留痕)+ResumeAsync 续跑 / 拒绝 / Expired fail-closed / 跨会话拒绝）：e2e 不可达（同上根因），spec 内 apiTest.fixme 维持不放开（lead 裁决）——单测背书（AC10，task-2 归口；05 §6-2） |
| E 不受影响面 | 符合（e2e 实证） | AC6 默认关闭态：既有 4 用例（聊天链路/几何锁/方向锁）三轮全绿（05 §1/§3 收尾）；本轮 ac4f-1 weCom.enabled=false 而 TaskGateway 单独开启，simulate-inbound 照常走通（ac4f-2）——任务分支独立、零旁路同证；Router 既有步骤零改动由 Forbidden 约束 + 既有 e2e 不红背书 |
| F 容错 | 符合（绑定持久化 / RunId 回填实证；并发闸 / 24h 超时=单测背书） | 重启恢复基础实证：绑定落库（首查 pending）+ RunId 0→1 回填 + RepliedAt 幂等 = ImGateway.db 亲查（05 §3 ⑤⑦，缺陷 #1 修复的 e2e 端到端验证）；并发双闸（inflight TryAdd + open 态查询含 Stuck）与 MaxWaitHours=24 收尾 = AC2/AC3 单测背书（05 §6-3），e2e 未断言——证据未覆盖部分如实标注 |

### 终审结论（product）

- **产品符合性：通过（有条件）**——七条旅程中 A/B/E 具端到端真实证据（REST + 消息表 + 截图三源互证，failed 路径全链闭环，Run 9 截图轮与 Run 10 REST 轮同构互证）；C/D/F 运行时缺口与 G 整体根因同一（本环境无可用上游 LLM，推不到 completed/stuck/审批），全部有单测背书且已在 05 §6 列明 Unknown/fixme——「证据未覆盖，单测背书」如实标注，不构成剧本断言被违反。
- 闸门2 建议附条件记录（真实模型环境补人工走查，归口 05 §6）：① completed 终态路径；② AC10 审批闭环运行时；③ 「任务状态」IM 快照回复；④ 并发闸 / 24h 容错。
- 无产品侧偏差需回流 02-spec；两轮证据一致性核对通过。


## 设计符合性走查（design · 2026-11-04）

> 本节由 design 按分工（lead 2026-11-04：设计节=design、产品节=product、八问与 Final Decision=lead）撰写；因 06 文件已由 product 先行创建，本节以**纯追加**方式并入，未改动上方产品节任何内容（含文件头注，留 lead 汇总时统一整理）。

- 走查人：design（**独立判定**；qa 读图结论见 05 §5，仅作参照，未影响本节结论——两处视角差异已显式标注）。
- 判定基准：02-spec「FR-7 视觉与交互规格（design · 2026-11-03）」（L65-141）§1-§7 + 仓库根 DESIGN.md（组件规格/三禁红线/新建页面与查询规范）+ `.agents/skills/ui-ux-design/SKILL.md` 走查清单（CRAP 四原则逐张过）。
- 证据：`ForgeSelf.Web/screenshots/dev/p079-dev-01~04`（dev-stack origin 源码本地前后端走查截图，qa 采集；build=缺陷 #1/#2 修复后、#3 修复前，05 §5 已裁示不影响展示数据）。

### 一、逐张核对（CRAP 四原则 + 基准条款）

**图 1 `p079-dev-01-task-center.png`（宿主任务中心）——✅ 符合**
- 基准：交互节「无新增宿主页面：任务中心零改动」+ AC4（imtask Run 进计划任务桶）。
- 所见：Run 9「写一份本周AI进展周报」置顶于「计划任务」桶，失败 pill + 重试 + 404 错误原文（单行，标题/错误列省略链生效）；「共 21 · 异常 11」计数、来源筛选、全状态混排齐全。
- CRAP：C=状态 pill（失败/卡住/已完成）与错误原文层级分明；R=pill/时间格式全列统一；A=状态列、时间列、重试列对齐一致；P=错误紧随任务行、操作聚右。既有页无本批引入的偏差。

**图 2 `p079-dev-02-im-gateway-panel.png`（插件面板会话视图）——✅ 符合**
- 基准：PILOT-078 方向拍板（AI 回复=左气泡、用户来讯=右气泡）+ FR-4 ack 文案 + DESIGN.md PageHead 形态 + 「插件 UI 必须展示版本号」。
- 所见：eyebrow（mono 大写金）+ serif 大标题 + v2.2.0 徽标 + 副标题齐；/task 入站=右气泡，ack「已建任务（编号 ca024230）已指派 通用助手，完成后自动回复」与终态失败回复=左气泡（金 tint 底），方向与 FR-4 文案逐字一致；失败=红 ✗ 图标 + 「任务 #9 失败」文字（颜色+文字双编码，非纯色传义）；左栏「未连接」灰点诚实状态、预览单行省略。
- CRAP：C=气泡角色对比清晰；R=气泡/时间/「回复」标签样式统一；A=双栏边界与省略链有效；P=联系人信息、线程消息按时间成组。

**图 3 `p079-dev-03-task-gateway-card.png`（任务网关卡·本批核心）——✅ 符合（1 条建议见三.1）**
- 基准：规格 §1 布局分区 + §2 字段形态 + §6 token 约束。
- 所见：卡头（serif 名称 +「已启用」success 徽标 + 一句职责说明 + 右上总开关 ON 金 track）；「派发规则」分区=命令前缀 2 chip + 输入框（placeholder「如 /task 或 任务：」）、默认 Agent select=「通用助手」、轮询间隔 number「3」+「秒」后缀；「IM 审批」分区=子开关行（label + desc + ON 开关）+ 同意关键字 2 chip（placeholder「如 同意」）+ 拒绝关键字 2 chip（placeholder「如 拒绝」）；尾注 note 覆盖默认值/生效时机（保存后下一轮轮询生效、改前缀不影响已建任务）/关键字仅待审批时拦截（零误伤）/「/task␣」尾随空格说明——与规格 §4 尾注要求逐项对应。
- CRAP：C=卡头/分区/字段三级分明；R=三组 chip 同形态（淡底+边框+灰 ✕，**无主色实心填充**=技能 J 条）、两处开关同形态；A=label 列与控件列对齐一致、note 跨两列；P=派发三字段成组、子开关与两关键字成组。

**图 4 `p079-dev-04-terminal-reply-detail.png`（终态回复特写）——✅ 符合（1 条建议见三.2）**
- 基准：FR-5 失败模板「❌ 任务 #{RunId} 失败\n{失败原因}」+ 方向语义。
- 所见：「✗ 任务 #9 失败」+ 换行「步骤 s1 失败: Service request failed. Status: 404 (Not Found)」——模板结构一致；**标签用 RunId 9（回填后）而非 ack 的 ShortCode ca024230**：qa 视角=缺陷 #1 RunId 回填的跨宿主实证（05 §5.4）；design 视角补充=两编号并存构成用户关联成本（见三.2，独立判定项）。
- CRAP：C=失败标记与原因层级分明；R=气泡/时间样式与图 2 同源；A/P=左对齐气泡、原因紧随标题行。

### 二、§7 六走查点逐点判定

| # | 走查点 | 判定 | 依据/说明 |
| --- | --- | --- | --- |
| 1 | 禁用联动（主开关 off→A/B 禁用+可见原因；子开关 off→关键字禁用） | ⬜ **截图未覆盖** | 四张均为启用态/会话态；需 off 态证据（控件禁用 opacity .6 + 「任务网关未启用——打开右上开关后可编辑」提示行；子开关 off→关键字禁用 + 提示） |
| 2 | 空列表阻断（前缀删到 0 条→保存阻断+错误条） | ⬜ **截图未覆盖** | 需「前缀删空→点保存」证据：`ig-alert--err`「至少保留 1 条命令前缀」且不发起请求 |
| 3 | chip 形态（淡底、hover 才金边、禁主色实心） | ✅ **符合** | 图 3：三组 chip 静态形态合规（边框+淡底+灰 ✕、无主色实心填充）；hover 态属交互态，静态截图无法证伪，风险低（CSS 层面） |
| 4 | 回读一致（7 字段整包透传回读） | ✅ **符合** | 图 3 七字段与 05 §5 REST config 回读值逐项一致（enabled+已启用徽标 / 前缀 [/task, 任务:] / 默认 Agent=通用助手=agent.generalist / 轮询 3 秒 / IM 审批 ON / 同意 [同意,批准] / 拒绝 [拒绝,驳回]）——含 D2 新字段 DefaultAgentId 落地实证 |
| 5 | 名册兜底（名册失败 select 置灰+「可留空走默认链」） | ⬜ **截图未覆盖** | 需名册失败/空态证据（当前名册正常，无法从四张图判定） |
| 6 | 窄屏 1024px（两列栅格不破版） | ⬜ **截图未覆盖** | 现有图 1291px；需 1024px 视口截图验证无横向滚动/栅格不破 |

### 三、建议项（均非阻断，供 lead 裁决）

1. **【建议】卡内分区标签与字段 label 同名重复**：「IM 审批」分区标签与子开关行 label 同名，同屏出现两次（图 3）。**源于规格自身**（§1 分区 B 命名与 §2 行 label 同名），实现忠实规格、非实现偏差。改法：B 分区标签去重（留分隔线不落标签），或行 label 改「审批子开关」。裁决：本批可改可不改；不改则记 TODO 下批。
2. **【建议】任务编号断层（IM 侧两编号并存）**：ack 用 ShortCode「编号 ca024230」（图 2），终态/后续用「#9」（图 4）——IM 用户无法把两者关联为同一任务。源于规格 Unknown 表既定取舍（ack 时 RunId 未生成），非实现偏差。改法（供裁决）：终态模板附关联「任务 #9（编号 ca024230）失败」，或「任务状态」查询同时支持两编号。涉及 FR-5 文案=spec 级变更，须 lead/用户拍板，建议记 TODO。
3. **【建议-量测】对比度未按几何取证 SOP 量测**：placeholder（图 3「如 /task 或 任务：」）、次要文字（图 2 时间戳/预览）目测可辨，但 ui-ux-design D 条要求 ≥4.5:1（正文）/≥3:1（次要）且「禁止目测判定」。建议补 `getComputedStyle` 取色 + WCAG 数值（暗/浅两态）入 05 或本节附注。

### 四、证据缺口（非返工；qa 补采集，采集后按同基准复核，预期判定不变）

- 配置视图全貌 1 张（验证卡片位于「企业微信通道卡下方」的位置约束——四张均未含配置视图上下文）。
- 禁用联动 off 态（主开关 off + 子开关 off，可合成 1-2 张）。
- 空列表阻断（前缀删空保存）。
- 名册兜底（select 置灰 + hint）。
- 窄屏 1024px 视口。

### 五、设计符合性结论

**通过（无阻断级返工项，fe-dev 无待办）。** 可见证据全部符合 02-spec FR-7 规格节、DESIGN.md 组件规格与 ui-ux-design 走查清单；3 条建议项（2 条源于规格自身取舍、1 条量测待补）不阻断闸门；§7 六点中 2 点有证据判定符合、4 点因截图未覆盖暂记缺口（补证后按同基准复核）。与 product 终审「通过（有条件）」并行不悖：产品侧条件=真实模型环境补四项人工走查（05 §6）；设计侧缺口=五类补证截图（四节）。

---

## Review 八问（lead · 2026-11-04）

1. **需求是否达成？** ✅ Verified——六旅程主链（A/B/E/F 端到端三源互证：e2e 三轮+REST 双轮+走查截图；C/D/G 运行时缺口同根因=无上游 LLM，单测背书+配置面就绪实证）。FR-1~FR-9 全部有实现+测试落点，AC1-AC11 对表见 05 §3。
2. **验证是否覆盖？** ✅/⚠️——单测 228 域回归（212 绿，15 基线红+1 假红已甄别）+ 插件域 60/60 + 守卫 Red→Green 三组（#1 RunId 回填、#2 俘获、#3 跨 provider）+ 冒烟 e2e 最终轮 5P/1S + AC4 REST 双轮 + dev-stack 走查四图。⚠️ 全量 3491 例：3375 绿/116 红，**判读 0 归本批**（详见第 4 问与 07 附录）。
3. **偏差几何？** 6 行，全在 03-plan 偏差表（1.9.9 基线/FR-8 路由/xcode 生成物漂移/Watcher 发送失败语义/DbConfigStore 容忍/Watcher RunId 回填）；无未记录偏差。
4. **缺陷与回归？** 缺陷 #1（RunId 回填缺失）/#2（DI 俘获）/#3（5-provider 分裂）三连抓、三修复、三守卫，全在闸门2 前完成；#3 属 AGENTS 级核心接线（host-capability-seams §7 坑1 复发），逃逸分析入 §8。回归：域内零回归（AC6 三轮全绿），全量 116 红经同会话双重证伪（过滤 50/50、+TodoTracker 199/199 全绿）+ AIAgent 域测试在风暴全量中全绿 + 签名与 PILOT-051 文档的 I-6/D-10 手搓换库风暴一致 + ~30 存量违规类 grep 实证——属存量测试基建债务，非本批引入。
5. **测试是否充分？** 单测按 AC 逐条落点（新 46 例：派发 16+IM 28+守卫 2）；e2e 零 mock 哨兵法保真实宿主链路。未知项如实列 05 §6 四条。
6. **性能/安全？** ApiKeyPolicy 全端点；Secret 沿用加密链；审批不绕行（stuck 照常）；轮询有界（24h 上限+幂等）。未做压测（本批范围外，未知项）。
7. **文档是否同步？** host-capability-seams §8 第三接缝 + 031 任务网关节（含会话约定/防误读三条）+ 03-plan 偏差 6 行 + TODO 新增 2 条基建债。
8. **遗留与风险？** ① 4 项真实模型环境补走查（product 条件）② 5 类截图补证（design）③ 全量测试基建治理（P2×2，见 TODO）④ ShortCode/RunId 双制取舍待拍板（P3）。

## Final Decision

**APPROVED（提交闸门2，附条件交付）**

- 依据：需求主链端到端实证 + 三缺陷修复守卫闭环 + 域内零回归 + 文档/偏差全记录；未知项与遗留已如实分级列明，无隐瞒。
- 附条件（不阻断本批，入后续队列）：product 4 项真实模型走查；design 5 类截图补证；测试基建治理 2 项（手搓 AddConnStr 治理 + runner.json 复制修复，已 TODO P2）；ShortCode/RunId 双制拍板（P3）。
- 闸门3（git 提交归档）待用户验收通过后执行。

---

## 返工轮 Review（product · 2026-11-04）

> 触发：闸门2 用户验收 7 项反馈（拆 9 票 task-8~16）全部交付并过 qa 验证轮（9 ✅ / 2 ⚠️）。本节为**纯追加**：对照 02-spec FR-1~FR-9 基线 + acceptance-checklist.md + 三处用户拍板规格变更（lead 派单转述口径），逐票复核规格符合性；不改上方任何既有节。

### 复核基准与证据

- 基准三层：① 02-spec FR-1~FR-9（基线）；② 用户拍板变更三处（T12 分段发送 / T16 执行观察节奏 / T14 stuck 语义）；③ acceptance-checklist.md A~H。
- 证据（product 亲读）：团队任务板 task-8~16 完成记录；qa 验证轮结论回填（.temp/tmp/p079r2-reverify-draft.md 尾部，9✅/2⚠️）；03-plan 偏差表；qa 证据引用（p079r2-verify-* / 截图 / 单测名）。

### 逐票复核（9 票）

| 票 | 复核对象 | 规格符合性判定 | 证据/留痕 |
| --- | --- | --- | --- |
| T8 | 任务中心倒序（checklist B2） | 符合——「最新在上」与 B2 置顶语义一致；零后端改动（ListRunsAsync 本就 CreateTime DESC），真因=前端跨桶拼接序，修 taskCenterApi.ts 统一 startedAt 倒序+防御边界；TDD 红→绿 | 验证轮 ✅（REST planned 桶 sourceRef 10→1 严格倒序+UI 时间递减）；taskCenterApi.test 18/18+过滤 35 例 |
| T9 | 任务卡跳转带参（B2 延伸） | 符合——「点击跳智能体页」可用性闭环（?runId= 直达该 Run 执行视图，复用剧本 A②入口）；范围偏差（+Plugins/AIAgent/web 收端）lead 追认批准已登记 | 验证轮 ✅（/ai-agent?runId=14 直达现场）；check/design:check 零新增/vitest 34 例 |
| T10 | completed 终态推送（C1） | 符合——completed 推送路径修复+单测（从快照构造）；Watcher 观测盲区补日志三点位（03-plan 偏差表 2026-11-04） | 验证轮 ✅（Run 13 ✅ 推送+RunId 回填，dev-stack）；手机送达=用户环境复验，与 checklist C1【需你亲手】一致 |
| T11 | 会话自动滚底（B3 延伸） | 符合——近底部才自动滚（isNearBottom≤60px）不打扰上翻阅读；rAF×2 修大列表首帧时序；无规格文本冲突 | 验证轮 ✅（浏览器 DOM 实测 755→937 / scrollTop 守恒）；ImGateway web build ✅ |
| T12 | 分段发送（拍板变更①） | **符合拍板口径**：取消静默截断；MaxCharsPerMessage=2000 分段（顺序发送+段尾 (i/n) 标记+短文单段无标记）；MaxReplyChars=6000 总保险丝 0=关；中途失败=全量重发（RepliedAt 全段成功才置位，对齐 03-plan「防重复推送优先」裁决）；aibot stream 每段独立帧。两条注意见专项审查 | 单测×4（ImTaskDispatchTests:853/871/885/905）+回归 67/67+build 0 错（lead 全核）；验证轮 ⚠️ 分段行为面待用户环境（本地模型输出 513<2000 构造不出），推送链无截断已实证 |
| T13 | IServiceScopeFactory 根类清扫（P1） | 符合 problem-class-closure 闭环四件套：同类点位清扫+守卫测试×2（PluginRawCreateScopeGuardTests）+case 归档（docs/11-troubleshooting/cases/2026-11-04-raw-createscope-on-ctx-sweep.md）+PC-005 更新 | 验证轮 ⚠️ 静态核✅；守卫实跑待排期（复验纪律 qa 不跑 dotnet；修复人侧回归 67/67 绿在案） |
| T14 | stuck 语义+300s（拍板变更③） | **符合拍板口径**：等待审批≠无进展超时（挂起原因三分类可辨）；45s→300s+AIAgent_StepTimeoutSeconds 可配（10..7200 clamp）；fe 依赖三件套齐（词表/overview 下发 retryUrl+cancelUrl/stuck vs failed 词表区分）。Forbidden「不改 RunOrchestratorService」经用户拍板突破+03-plan 偏差表登记=合规变更 | 验证轮 ✅（能力卡死路径活体：人话语义/快照 run+steps/端点+UI 入口）；等待审批活体被 400 阻塞（附带发现已报 be-dev，非本票缺陷）；单测×5（StuckSemanticsAndTimeoutTests） |
| T15 | stuck 前端（验收7 前端） | 符合——依赖序遵守（后于 T14）；cancelUrl 全链透传+onStop 确认弹窗+继续/重试分流（stuck→继续/failed→重试）+stuckReason 摘要吃 T14 三分类文案；徽标色=--el-color-warning 官方 EP 令牌（lead 口传金色与 pending 组冲突，fe-dev 改定橙+lead 追认留痕），符合 DESIGN.md 禁独立色值 | 验证轮 ✅（badge 橙≠失败红+继续/终止双入口+行内诊断+跳现场）；check 0 错+design:check 零新增（1759<2501 基线） |
| T16 | 执行观察节奏（拍板变更②） | **符合拍板口径**：节拍 pollIntervalSeconds 默认 60（1 分钟）；快照指纹变化即推进展（模板简短不刷屏+子开关可关）；连续无变化达阈值才判超时（与 T14 300s 对齐+双源先到先推+幂等去重）；终态即推不受节拍拖慢 | 验证轮 9a ✅ 进展推送活体（▶ 任务 #14/#15/#17 执行中，指纹变化即推）/ 9b ✅ 配置回环（生效/还原）/ 9c ✅ 终态即推（❌ +1.06s 首拍；✅ 滞后=终局合成 LLM 时长非节拍）；无变化真转 stuck 活体待可控行程（单测背书：变化检测/无变化计时/去重/节拍配置） |

### 三处用户拍板变更专项审查

1. **T12 截断→分段发送**：口径三要素（MaxCharsPerMessage=2000 通道阈值 / MaxReplyChars=6000 总保险丝 / 0=关）全部落地并有单测。⚠️ **规格同步缺口**：02-spec FR-5/FR-6 仍为「MaxReplyChars=1500 截断」旧文本，变更未回写——属规格滞后非实现偏差，建议 lead 归口回写；03-plan 偏差表未见 T12 行，一并补。已登记实现偏差 1 条：MaxCharsPerMessage 落 TaskGatewayConfig 而非 WeComChannel（发送侧无通道配置访问点），通道差异化分段记 TODO 池——不阻断。
2. **T16 执行观察节奏**：口径四要素（1 分钟节拍 / 变化即推+子开关 / 5 分钟无变化才卡+双源先到先推幂等去重 / 终态即推）实现齐，9a/9b/9c 三活体+配置回环实证。⚠️ **规格同步缺口**：02-spec FR-4「PollIntervalSeconds（默认 10s）」与 FR-6「=10」未回写为 60，进展推送子开关未入 FR-6 配置清单。⚠️ 「无变化 5 分钟转 stuck」活体验证待可控行程（单测背书），随用户环境复验。
3. **T14 stuck 语义**：口径（等待审批≠无进展超时、300s 可配）落地+偏差表登记（含 clamp 边界）；与 02-spec Business Rules 4「审批不绕行」语义一致（细化非违背）。⚠️ 「等待审批」活体验证被附带发现阻塞：agent.programmer+真模型 LLM 调用 400（疑似工具 schema 被上游拒）——已报 be-dev 裁决，非本票缺陷。

### 横向复核

- **回归护栏**：A3/B1/F4 ✅（qa 复验：禁用态置灰+可见原因 / ack 0.37-0.4s 秒回 / 无待审批零误伤走聊天）；config 跨重启持久 ✓、面板↔API 一致性 ✓。
- **e2e 回归（AC6+冒烟）**：⚠️ qa 留痕=⏸ 需 dotnet publish 待排期错峰，**未见实跑记录**——lead 汇报口径「e2e 零回归」，若另有实跑证据请归档日志补痕；本节判定以留痕为准，此为验证覆盖缺口（非实现缺陷）。T10/T12/T14 动 Watcher/通道/编排侧，冒烟终态推送断言即其守卫，**闸门收口前必须补跑**。
- **验证轮 2 ⚠️ 判读**：T12（分段行为面待用户环境）、T13（守卫实跑待排期）均为环境受限，非实现缺陷；实现层各有单测×4 / 守卫×2+case 归档背书。
- **文档同步缺口（汇总，建议 lead 归口一次性补齐）**：02-spec FR-4/FR-5/FR-6 三处拍板变更未回写；03-plan 偏差表缺 T12/T16 行（T14/T10 已有）；07-final-report 未含返工批（归 lead 收口）——避免规格双源漂移。

### Final Decision（返工轮 · product）

**APPROVED（附条件）**

- 9/9 票规格符合性通过：三处用户拍板变更全部按口径落地，实现层有单测/守卫背书；验证轮 9✅/2⚠️ 的 ⚠️ 均为环境受限，非实现缺陷。
- 附条件（不阻断本判定，闸门收口前完成）：
  1. **e2e 回归补跑**：AC6 既有 4 用例+冒烟主链路（T10/T12/T14 波及面守卫）排期跑绿并归档日志（qa 留痕目前 ⏸）。
  2. **规格回写**：02-spec FR-4/FR-5/FR-6 三处拍板变更（60s 节拍+进展推送子开关 / 分段发送 2000+6000+0=关 / 三分类+300 可配）+ 03-plan 偏差表补 T12/T16 行（lead 归口）。
  3. **用户环境复验项**（随闸门2 用户验收）：T12 分段行为面（>2000 字构造）、T10 手机送达、T14/T16 等待审批与无变化转 stuck 活体、附带发现 400 根因裁决。


## 返工轮 UI 符合性走查（design · 2026-11-04 R2）

> 纯追加节（不改上方既有节）。范围=lead 指定三点（T15/T9/T11）；证据=p079r2 五张走查截图（dev）+ qa 验证轮结论（.temp/tmp/p079r2-reverify-draft.md，参照）+ 读码佐证（TaskCenterView.vue / AiAgentView.vue / ChatThread.vue）。判定独立做出。

### ① T15 stuck 徽标 warning 橙 —— ✅ 符合（附 1 条观察）

- **token 合规（读码 TaskCenterView.vue:623-628）**：`.tc-badge[data-status='stuck']` 三值全为 token 派生——`color: var(--el-color-warning)`、`border-color/background: color-mix(in srgb, var(--el-color-warning) 35%/14%, transparent)`；零新增裸色值（color-mix(token) 派生与面板/宿主既有形态一致，design:check 棘轮口径内）。
- **四向语义核对（L603-634 + 图 p079r2-t8-t15-task-center.png）**：pending 组（planning/pending/scheduled）=金（--gold-500/--gold-300/--gold-tint-strong，L603-609）；failed=朱砂 --cinnabar-400（终局，L617-621）；completed=--el-color-success（完成，L611-615）；running=--el-color-info 灰蓝；stuck=--el-color-warning 橙（待人工）。**fe 论证成立**：金已被 pending 组占用（在案），stuck 复用金将与「预期态」混淆；DESIGN.md 语义色表 warning=警示，与「等待人工处理」功能对应，满足「颜色语义必须对应功能、不得用于装饰」。
- **图证**：卡住行=橙 pill + 行内诊断摘要 + 「继续/终止」入口，failed 行=红 pill +「重试」——同屏可辨，pill 均带文字标签（非纯色传义）。
- 【观察·非偏差】--el-color-warning（#E0A030）与金组（--gold-400 #F59E0B / --gold-500 #D97706）色相相邻，小尺寸/色弱场景的区分主要靠明度差 + 文字标签；两态生命周期不重叠（planning=开始前、stuck=进行中卡住），实际混淆面小。如后续要加强：stuck pill 可配 ⏸ 图标（FR-5 模板已有 ⏸ 语汇）。DESIGN.md 无此强制，本批不动。

### ② T9 深链入口一致性 —— ✅ 符合

- **构造端（TaskCenterView.vue:193-195）**：任务行点击按 source 分流——`planned → { runId: sourceRef }`、`open → { session: sourceRef }`。
- **读取端（AiAgentView.vue:407-441、434、1377）**：`?runId=N` 经 Number 校验 + >0 容错 → openRunDetail → RunRecordPanel.selectRun 直达执行视图；`?session=<id>` → 打开对应会话；watch 双参数变化 + 挂载尾部触发（依赖就绪）。两形态与构造端一一对应，参数命名统一（runId/session），无旁路参数。
- **图证（p079r2-t9-jump-run14.png + qa DOM 实测）**：自任务中心点行直达 Run#14 执行记录——原因全文、步骤流（卡住 0.0s + 诊断原文）、规划过程、底部**五处理入口**（继续/重开/跳过/补位/取消）齐备。分流规则、参数形态、容错、五操作入口四层一致。

### ③ T11 滚动语义 —— ✅ 符合

- **读码（ChatThread.vue:27-32、49-65）**：三语义逐条在码——①**首灌无条件滚底**（0→N firstFill → scrollToBottom，rAF×2 防大列表首帧高度未定滚不到位）；②**增量近底才滚**（N→M：`!firstFill && !isNearBottom()` → return；阈值 = scrollHeight − scrollTop − clientHeight < **60**）；③**上翻不打扰**（不近底不滚）；len≤0 复位 hadMessages（切换会话语义正确）。父组件既有 wasNearBottom 调用保留为幂等双保险（L6 注释）。
- **qa DOM 实测互证（reverify-draft）**：打开滚底 atBottom ✓；上翻时新消息到达 scrollTop 守恒（内容高 755→937 增长、scrollTop 0 不动）——与代码语义一致。
- **图证（p079r2-t12-t11-thread.png）**：滚底状态视口内为最新消息（#16 完成 2500 字长文全文渲染、无截断、滚动条贴底、气泡 max-width 78% 无横向破版）。

### 附带核对（顺带证据，不计入三点判定）

- `p079r2-a3-config-panel.png`：**上轮证据缺口「配置视图位置」闭合**——任务网关卡确在企业微信通道卡下方（规格 §1 位置约束）✓；通道 off 与任务网关 on 独立共存（两开关语义独立，符合设计）✓。
- `p079r2-a3-imgw-panel.png`：会话列表 10 条真实任务消息，预览状态图标（✓绿完成 / ✗红失败 / ▶执行中）与任务中心四向语义同构 ✓；空态/省略链正常。
- **缺口更新**：上轮缺口 5 项 → 「配置视图位置」闭合；**「禁用联动 off 态（任务网关总开关 off）」仍无截图**（qa 引 a3 两图为 A3 证据，但两图均为任务网关启用态——独立复核如实记录，未覆盖即未覆盖）；空列表阻断 / 名册兜底 / 窄屏 1024 仍未覆盖。共 4 项开放，均不阻断返工批验收。

### 返工轮结论

**三点全部符合（0 阻断 / 0 代码级建议）**；T15 附 1 条非强制观察（warning 与金色相相邻，文字标签已兜底）。T9/T11 实现、qa DOM 实测与截图三源一致，无偏差。

### 条件收口（product · 2026-11-04 晚）

- **条件 1 收口**：e2e 回归已实跑——见 05 §8.3 + `.temp/tmp/p079r2-verify-e2e-run.log`（e2e 5P/1S/1.7m 零回归，含 D1/D2 几何锁+契约探针+冒烟主链路；fulltest 3393 绿/125 红对表基线净零新增；构建=返工批全修复版）。
- **Final Decision（返工轮）维持 APPROVED**，附条件收敛为 2 项：**条件 2（规格回写）已由 product 执行完毕**——02-spec 四处「2026-11-04 用户拍板修订」落位（FR-4 观察节奏 60s/变化即推/StalledMinutes=5/终态即推；FR-5 分段发送 MaxCharsPerMessage=2000/MaxReplyChars=6000 总保险丝 0=关/全量重发；FR-6 配置块同步；Business Rules 7 stuck 三分类+300s 可配+CancelUrl，三分类与参数真名均经源码注释核证）；03-plan 偏差表 T12/T16 拆为独立规范行（原以 || 粘连于 T10 行尾，非合法表格行；文字未改，补全时间列与执行人列）。**条件 3（用户环境复验项）随闸门2 用户验收**。