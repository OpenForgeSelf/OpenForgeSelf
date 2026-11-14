# LoopForge 功能吸收对照图（006）

> 本文定位：**吸收裁决（哪些功能可以吸收到本项目）**，是 `005-loopforge-feature-analysis.md`（LoopForge 功能需求事实）的姊妹篇。005 只写 LoopForge「已有什么」，本文只写「本项目吸不吸、怎么吸、为什么不吸」——全部吸收论证集中于此，005 §6 的前向指针指向本文。
> 证据基础：三份成员报告（`.forgeself/tmp-loopforge-analysis/lf-backend.md` 188 行 / `lf-frontend.md` 165 行 / `of-inventory.md` 140 行，均已完成并通读）+ 005 定稿（236 行，13 处证据合并全部落地）。
> 裁决人：lead（组件团队汇总）。裁决本身不标 Verified/Inferred——那是给「事实」的分级；本文的分档是**决策**，依据随条目给出。
> 边界：本文只论证吸收与否与吸收形态，不排期、不改代码；落地需按 `plugin-feasibility-study` 逐项立项。

---

## 1. 裁决总览

| 档 | 含义 | 条目 | 数量 |
|---|---|---|---|
| **A 直接吸收** | 机制平移：LoopForge 已验证的协议/表范式/UI 交互，本项目有明确落点 | A1–A7 | 7 |
| **B 改造吸收** | 机制可取但须本地化改造（去容器化/去多用户/换技术栈） | B1–B6 | 6 |
| **C 思路借鉴** | 方向认可但量级大或依赖远期立项，暂不承诺 | C1–C7 | 7 |
| **D 红线排除** | 与项目红线/定位冲突，明确不做（→ NOT-to-do 台账 059） | D1–D6 | 6 |
| **E 已有对标** | 本项目已有等价或更强能力，不重复建设，仅对标补差 | §6 表 12 行 | — |

**一句话结论**：LoopForge 与本项目重叠面大（E 档 12 项对标中 9 项「已有」），真正值得吸收的是它把「AI 交付闭环」**做扎实的那批机制件**——租约心跳、gate 文件协议、有界摘要、检查点表、审批三动作、双游标 SSE；以及两个本项目空白——**需求↔运行追溯**（A7）与**统一模型目录**（B1）。团队协作/SaaS/容器并行运行时全线排除（D 档）。

---

## 2. A 直接吸收（P1，机制平移）

> 每条给出：LoopForge 侧证据（见 005）→ 本项目落点 → 可行性 → 风险。

### A1 · 审批三动作 + 门禁人话说明 + 三端同文案契约测试（P1）
- **LoopForge 侧**：审批面板 = 通过 / 补充信息重跑当前节点 / 退回上游并重跑后续，附 turnsExhausted 双态与补充上下文输入；文案规则由 JS/Go/React 三处成对契约测试锁定（lf-frontend §④-D3、⑦-1）。
- **本项目落点**：九阶段闭环闸门 1/2/3 的审批 UI。我们目前只有「批准/拒绝」两档，缺「补充信息重跑」「退回上游」两档——LoopForge 的三动作 + 人话说明直接补齐交互语义。
- **可行性**：高。纯前端交互 + 审批服务参数扩展，无架构变更。
- **风险**：低。契约测试做法一并吸收（同文案多处出现时用成对测试锁死，防止三端漂移）。

### A2 · SSE 双游标运行实时流 + 增量 delta 合并（P1）
- **LoopForge 侧**：`after={telemetrySequence}&logsAfter={logSeq}` 两个独立游标，telemetryDelta/logDelta 增量合并 + workflowSnapshot 快照保留（lf-frontend §④-D1、⑦-2）。
- **本项目落点**：031a AgentRun/AgentStepRun 的执行流面板。.NET 侧 EventSource 等价实现；遥测与日志两条流各自断线续传，互不阻塞。
- **可行性**：高。双游标本质是「每条流一个单调序号」，与现有 SSE 基建兼容。
- **风险**：低。注意与 040 manifest surfaces 的流面权限对齐。

### A3 · 租约 + 心跳 + 双保险控制中断（P1）
- **LoopForge 侧**：MySQL 租约抢占（心跳=TTL/3，丢租约不写终态）+ 控制位**先落库再通知** worker + worker 验 `X-Agentflow-Control-Token`（005 §2/§4；`engine.go:33-35`、`run_control.go:18-110`；lf-backend ⑦-1）。
- **本项目落点**：031a 长任务与 013 WorkflowEngine 的崩溃恢复/暂停语义。当前宿主内插件任务缺同等级的「崩溃后谁接管」与「取消指令必达」保障。
- **可行性**：高。SQLite 单机语境下租约退化为「心跳时间戳 + 超时接管」，控制令牌退化为「执行会话 id 校验」，更简单。
- **风险**：中。涉及执行主循环改造，须按闸门 1 立项；不得引入分布式语义过度设计。

### A4 · gate 结果文件协议 + 独立 gate-mcp（P1）
- **LoopForge 侧**：验收结论 = 机器可读 JSON 文件（`.agentflow/gate-results/<nodeID>/attempt-<N>.json`：Decision pass/rework/fail/human + 必填 Reason + Findings 单条 ≤64 字符 + 尺寸上限 4000/50/500/64/4000；缺失文件 ≠ fail），经一个 ~21 行的最小 stdio MCP（`AGENT_GATE_RESULT_PATH`）写入，与业务引擎完全解耦（005 §3；`gate_submit.go:44-100`；lf-backend ⑦-2）。
- **本项目落点**：铸己匣插件/agent 的「验收闸门」标准协议——九阶段闭环的 Evidence/Review 环节目前靠自由文本，缺机器可读的验收结论载体。
- **可行性**：高。文件协议零依赖；本项目 MCP 基建（022/034/039 工具桥）现成，gate-mcp 的「最小 stdio 写文件」模式可直接套。
- **风险**：低。需在 03-plan 定落点目录约定（建议 `.forge/gate-results/` 风格），避免散落。

### A5 · runeval BuildFacts 有界摘要（P1）
- **LoopForge 侧**：固定上限把一次运行压缩成可喂评估模型的稳定事实集——maxDigestEvents=400 / maxDigestMessage=200 / maxFactInput=800 / maxErrorText=160 / maxToolRepeats=12（005 §5；`facts.go:12-55`；lf-backend ⑦-3）。
- **本项目落点**：031a 的 Review/复盘喂料——执行记录直接灌模型会爆上下文，有界摘要结构（事件数/单条长度/重复工具次数三重上限）是现成模板。
- **可行性**：高。纯数据整形，无新基建。
- **风险**：低。上限数值应按本项目上下文预算重标定，不照抄。

### A6 · 检查点表范式（P1）
- **LoopForge 侧**：`run_workspace_checkpoints{blob(MEDIUMBLOB), sha256, lease}` + 恢复状态机（restore_status / restore_started_at / restore_error）（005 §2；migrations 013/018；lf-backend ⑦-5）。
- **本项目落点**：「会话/工作区换手」场景——agent 长任务中途换会话/换 agent 接管时的工作区快照恢复。SQLite BLOB + sha256 + 租约列的组合直接平移。
- **可行性**：高。单表 + 状态机，XCode 建模 straightforward。
- **风险**：低。BLOB 上限需设阈值（LoopForge 用 MySQL MEDIUMBLOB 16MB；SQLite 建议同级）。

### A7 · 需求 8 状态机 + 泳道看板 + 需求↔运行追溯（P2）
- **LoopForge 侧**：需求 8 状态机（草稿→待评审→已排期→开发中→待验收→已交付/已驳回）+ 泳道看板 + 需求关联运行/工单的追溯链（lf-frontend §④-A2/A3；005）。
- **本项目落点**：**本项目空白**（of-inventory §7 未列对照项）——需求目前活在 `docs/ai/pilot/` 工件与 TODO，无结构化状态机，需求↔AgentRun 无外键级追溯。
- **可行性**：中。新建实体 + 看板 UI，量级中等；价值在打通「需求 → 00-07 工件 → Run 记录」证据链，与九阶段闭环天然契合。
- **风险**：中。须避免做成团队协作看板（红线 D1）——单用户视角的需求台账即可，泳道是展示形态不是协作形态。

---

## 3. B 改造吸收（P1–P2，须本地化改造）

### B1 · 宿主级统一模型/执行器目录（P1）
- **LoopForge 侧**：`window.AgentFlowAssistantModels` 单例模块供 6 处界面复用——模型家族图标、credits 计费标签、engineOf 引擎推断、拉取失败回退内置目录（lf-frontend §④-C4、⑦-3）。后端侧对应统一执行器目录：`GET /api/executors` 公开 3 个内置执行器只读 + migration 025 凭据层预留（005 §4 执行器口径 E1）。
- **本项目落点**：本项目多插件各自选模型，正缺宿主级统一模型选择器/目录服务。做成**宿主能力或 design-system 式共享插件**，暴露模型目录 + 家族图标 + 引擎推断；插件只消费不自带。
- **改造点**：LoopForge 是前端单例，本项目应做成服务端目录（模型注册表），前端各处消费同一份；credits 计费标签对应我们的 024 用量口径。
- **可行性**：高。风险：低——但涉及模型配置归属，需过 architecture-design（能力接缝）。

### B2 · IDE 接管状态机（P2）
- **LoopForge 侧**：只读浏览 → 「启用编辑」takeover（服务端暂停 agent、等待 drain、重试语义）→ release（含 keepalive）→ 空闲回收倒计时（lf-frontend §④-D4、⑦-4）。
- **本项目落点**：043 智能终端 / agent 工作区的「人要上手改」刚需——人与 agent 抢同一工作面时的接管/释放状态机 + busy 退避重试。
- **改造点**：LoopForge 的 takeover 挂在编辑器窗口租约（migrations 011）上；本项目对应终端会话/工作区文件的独占租约。
- **可行性**：中。风险：与 043 既有会话生命周期语义需对表，防止双租约打架。

### B3 · 定时任务 BaselineGuard + 单实例互斥（P1）
- **LoopForge 侧**：跑定时任务前先验仓库基线（存在冲突文件/对齐运行未完成 → 整仓跳过）+「已有定时任务正在运行」互斥，防定时自动化互相踩踏（005 §2 调度；`schedule` 包 + `ErrAlreadyRunning`；lf-backend ⑦-4）。
- **本项目落点**：本项目定时/自动化能力（031a 触发、013 调度）缺防踩踏层。BaselineGuard 语义（运行前先验环境干净）+ 单实例互斥直接平移。
- **可行性**：高。风险：低。

### B4 · Docker 沙箱 → 降级为「本地工作区隔离 + 检查点/租约语义」（P2）
- **LoopForge 侧**：`SandboxProfile{local|native|docker}` 三档执行隔离（005 §4 Runner）。
- **裁决**：**容器化本身不吸收**（D2 红线：宿主本机直执行 + 审批管控是运行经济学基线），但吸收其**降级形态**——本地隔离工作区目录（每次运行独立目录 + 环境快照）+ A3/A6 的租约与检查点语义。即：吸收「隔离的**语义**」，不吸收「隔离的**容器**」。
- **可行性**：高（降级后）。风险：低。

### B5 · 通知投递失败持久化 + awaiting_authorization + AI 日报（P2）
- **LoopForge 侧**：`notification_bots` + `user_bot_credentials`（加密）+ `notification_deliveries`（004）；健康码 850003/853004 → `awaiting_authorization` 挂起待授权；AI 每日摘要（007）（005 §2 通知引擎）。
- **本项目落点**：031b IM 网关（单用户维度）——投递失败落库可重试、授权过期态显式化、AI 日报推给单用户。
- **改造点**：**剥离多用户维度**（bot_user_bindings/bot_connections 多绑定协议属 D3 红线），只保留单用户单渠道的投递台账与重试。
- **可行性**：中。风险：低。

### B6 · 多 CLI 托管 managedcli（P2–P3）
- **LoopForge 侧**：`managedcli -cli-action status|update|rollback`；版本 pin（CodeBuddy required / Codex ≥0.162.1 / Claude Code ≥2.1.300）；HTTPS-only egress；`-auto-update`（005 §4 managedcli；lf-backend ⑦ 次级引用）。
- **本项目落点**：032 AgentHub 的执行器（外部 agent CLI）版本管理——当前 hub 只做探测/登记/one-shot 委派，无版本状态/更新/回滚台账。
- **改造点**：LoopForge 托管的是自家受管 CLI；我们对应托管各 code-mate CLI（Codex/OpenCode/DeepSeek Harness…），status 台账先做，update/rollback 谨慎（外部工具升级不可控，先只读报告）。
- **可行性**：中。风险：中——「替用户升级外部 CLI」越权，先做 status 只读。

---

## 4. C 思路借鉴（P3，远期立项，本文不承诺）

| # | 候选 | 依据 | 借鉴点 | 备注 |
|---|---|---|---|---|
| C1 | **可执行 SOP 编排引擎** | 005 §2 SOP 模型：节点四类型 agent/script/approval/command、`MaxAttempts`/`RetryStrategy`/`OnFailure`(reject\|fail\|human)/`RejectTo`/`ExecutionScope`/`ArtifactOutputs`；非 DAG 拒绝（`workflow_graph.go:263`）、依赖成功分发（`node_execute.go:60-85`） | 本项目 013 WorkflowEngine 部分支持；九阶段闭环是**流程**骨架，SOP 引擎是**可执行编排**增量。of-inventory §7 明确列为空白 | 量级大，远期按 plugin-feasibility-study 立项；A4 gate 协议是其前置件 |
| C2 | 多宿主技能分发 | lf-frontend §⑤、⑦-5：闭环方法论编译为 `.claude/.codex/.cursor/...` 原生配置 + manifest | 本项目 `.agents/skills/`（13 技能）只活在自家宿主；`forge skills export --host claude\|codex\|dsh` 让外部 agent 也走铸己匣方法论 | 与 032 外部委派协同 |
| C3 | 知识库 Agent 写回 → 人审核流 | lf-frontend §④-C6；lf-backend ⑦ 次级（G1/G2 评审队列） | 027 Cordis 部分覆盖；写回需人审的队列语义可借鉴 | |
| C4 | @ 引用面板 + 统一引用搜索 API | lf-frontend 次级候选 B3 | 041 composer 上下文栏的引用来源扩展 | |
| C5 | 用量 costCny 人民币成本 + 环比对齐 | lf-frontend 次级 E3；**低置信**（N1 token 定价 USD→CNY 汇率 7 未复核，lf-backend §8） | 024 用量统计的成本展示扩展 | 引用前必须复核 N1，否则只借鉴「环比对齐」形式 |
| C6 | 欢迎页一键跑通首程 demo | lf-frontend 次级 B2 | 新用户首程引导 | 量级小，随版本节奏顺带 |
| C7 | 群聊约束式派活（一次最多 5 条 SOP） | lf-frontend 次级 A6 | 「约束式防滥用」设计 taste | 单机语境弱化，仅记 taste |

---

## 5. D 红线排除（NOT-to-do → 同步 `docs/07-decisions/not-taken-decisions.md` 059）

| # | 排除项 | 理由 |
|---|---|---|
| D1 | 团队协作 / SaaS / 企业化管理 / 强制账号体系 | 用户红线（of-inventory §7）：LoopForge 的「团队级」维度是定位冲突，非能力缺口 |
| D2 | Docker 并行运行时 / 多宿主 runner 舰队 / api\|worker 分离部署 | 运行经济学红线：宿主本机直执行；吸收隔离语义（B4 降级形态）而非运行时形态 |
| D3 | 通知机器人多用户维度（bot_user_bindings / bot_connections 多绑定） | 单用户工具定位；只吸收 B5 降级形态 |
| D4 | TAPD / 工蜂外部工单双向同步 | 外部系统耦合，与个人工具定位冲突 |
| D5 | 通用聊天盒（chatbot 通用对话壳）+ iframe postMessage 桥内嵌 | 本项目是 AI-Native 工作台非聊天产品；插件 web 体系原生加载，不需 iframe 桥 |
| D6 | 全量数组响应无分页的 API 风格 | lf-frontend 实测多处列表接口全量返回——反模式，不得借「对齐 LoopForge」之名吸收 |

---

## 6. E 已有能力对标（重叠映射表）

> LoopForge 功能 → 本项目承载 → 裁决。档案号索引：013 WorkflowEngine / 021 MAF / 022+034 MCP / 023 技能 / 024 用量 / 026 marketplace / 027 Cordis / 028a Sems 13 工具 / 030+100 认证+机密 / 031a 执行记录 / 031b IM 网关 / 032 AgentHub / 035 版本化（部分）/ 039 工具桥 / 040 manifest / 041 composer / 042 网络代理 / 043 智能终端 / 044 可审批会话。

| LoopForge 能力 | 本项目承载 | 裁决 |
|---|---|---|
| 九阶段闭环 + 三闸门 | AGENTS.md §11（强制流程） | **已有且更强**（LoopForge 无流程闭环，只有运行闸门） |
| 审批 / 打回 / 重试（基础） | 六闸门管线 + PersistentApprovalService + 043/044 可审批会话 + IM 审批闭环 | 已有（单机版）；**UI 三动作档位吸收 → A1** |
| 外部 Agent 委派（CodeBuddy/Codex/Claude Code 执行器） | 032 AgentHub：探测/登记/one-shot/事件流/审批台 | 已有；**版本托管台账借鉴 → B6** |
| 执行留痕复盘 | 031a AgentRun/StepRun/ToolCallsJson | 已有；**有界摘要吸收 → A5** |
| 暂停 / 恢复 / 取消 | 013 WorkflowEngine + P0 取消杀进程 | 已有；**租约+控制双保险吸收 → A3** |
| /resume 会话恢复 | 013 + P0 | 已有；检查点表范式补强 → A6 |
| MCP 工具接入 | 022/034 MCP + 028a Sems 13 工具 + 039 工具桥 | 已有且更强（能力接缝三层 L1 契约/L2 事件/L3 拦截管道，LoopForge 无对应概念）；**gate-mcp 最小范式吸收 → A4** |
| 技能体系（SKILL.md + 登记） | 023 + `.agents/skills/` 13 技能 + AGENTS §2.4 | 已有；多宿主分发借鉴 → C2 |
| 用量统计 | 024 | 已有基础；成本展示借鉴 → C5（低置信） |
| 版本化发布 + 显式切换 + 本地更新源 | 035（部分）+ 038 | 已有（部分实施，of-inventory 标注剩余边界 Inferred） |
| 知识库（含写回审核） | 027 Cordis | 已有（部分）；写回审核流借鉴 → C3 |
| 统一模型选择 | 各插件分散配置 | **空白 → B1** |
| 需求状态机 + 需求↔运行追溯 | `docs/ai/pilot/` 工件（非结构化） | **空白 → A7** |
| 可执行 SOP 编排 | 013（部分支持）+ SKILL.md 规范承载 | 部分空白 → C1（远期） |
| 工作区隔离执行 | 宿主本机直执行 + 审批管控 | 定位差异 → B4 降级吸收 |
| 多执行器并行编排（长期多 Agent 协作） | 032（one-shot 委派，非长期协作） | 空白但贴近 D2 红线 → 维持 one-shot，不立项 |

---

## 7. 优先级汇总

| 档 | 条目 | 一句话理由 |
|---|---|---|
| **P1** | A1 审批三动作 · A2 双游标 SSE · A3 租约心跳 · A4 gate 协议 · A5 有界摘要 · A6 检查点表 · B1 统一模型目录 · B3 基线守卫+互斥 | 机制平移、落点明确、量级可控、直接补强九阶段闭环的工程底座 |
| **P2** | A7 需求追溯 · B2 接管状态机 · B4 本地隔离降级 · B5 通知台账 · B6 CLI 托管（status 先行） | 需本地化改造或跨插件对表，收益明确但不紧急 |
| **P3** | C1 SOP 引擎 · C2 技能分发 · C3 知识写回审核 · C4 @ 引用 · C5 成本环比 · C6 首程 demo · C7 约束派活 | 远期立项，须过 plugin-feasibility-study，本文不承诺排期 |

---

## 8. 证据分级与边界

- **LoopForge 侧事实**：全部引自 005（其内已逐条分级，原始 `file:line` 在 005）；本文不重复给 LoopForge 原始行号，只给「005 §n + 成员报告节号」两级索引。
- **本项目侧现状**：引 of-inventory.md——其中 48 份档案头 + 5 份架构文档 + 目录实测为 Verified；五插件定位（BrowserUse/ClockBadge/CostScope/Home/Sems）、021 落点、035 剩余边界为 Inferred，涉这些的裁决已相应保守。
- **低置信项处理**：N1 token 定价（USD→CNY 汇率 7 未复核）→ C5 标注低置信，未进 P1/P2；015 迁移 summary 列类型、ingest 状态码清单 → 未进入任何吸收论证。
- **未读区**：`web/vscode-web/`、`web/extensions/`、httpapi/repo 内部实现、`local/` 内部逻辑——涉及其的判断（如 D5 对 vscode 嵌入的排除）基于产品定位而非实现细节，如后续立项 C1/B6 需先补读。
- **本文性质**：A/B/C/D 分档是 lead 裁决（组件团队汇总），非事实陈述；D 档已按 §10.4 同步 NOT-to-do 台账（059）。任何 A/B 条目动工前须走九阶段闭环闸门 1。
