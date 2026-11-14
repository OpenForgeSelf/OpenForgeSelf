# 调研 005：LoopForge 功能需求文档（源码功能提炼）

> **本文定位**：对腾讯开源 LoopForge（MIT，源码为本地临时下载副本，仓库地址见 LoopForge 官方发布页，路径不入档）做只读分析后提炼的**功能需求文档**——它有哪些功能、每个功能解决什么问题、关键机制如何实现。可吸收清单与本项目（OpenForgeSelf）现状的逐条对照见 [006-loopforge-absorption-map.md](006-loopforge-absorption-map.md)。
> **分析方式**：Agent 团队分工（lf-backend = Go 后端与核心引擎；lf-frontend = 前端与产品形态；of-inventory = 本项目现状盘点；lead = 合成裁决）。成员原始报告于 `.forgeself/tmp-loopforge-analysis/`（gitignored 中间产物）。
> **证据分级**：✅Verified = 亲自读码/读文档（标 `相对路径:行号` 或成员报告节号）；🔶Inferred = 目录/文件名/文档推断。未读不写。
> **日期**：2026-11-14

---

## 1. 产品定位与总体形态（结论先行）

LoopForge = **可自主部署的团队级 AI 编码交付工作台**：「与 Main Bot 对话」和「运行 SOP 流程」两种方式，把需求一路驱动到 PR/MR/直推交付，全程留痕、可审批、可回溯（`README.zh-CN.md:1-30`，✅）。

- **一句话**：它不是「又一个聊天框」，而是**给 AI 编码代理配的「生产流水线」**——需求建模 → SOP 编排 → 隔离工作区执行 → 人工关口 → PR 交付 → 运行留痕与成本核算。
- **两种工作方式**（`README.zh-CN.md`，✅；lf-frontend §②）：
  1. **对话**：普通对话本地进程；首次需改仓库/跑命令时由 Bot 调 `workbench_workspace_open` 开 Docker 对话工作区，改完 `workbench_workspace_submit_mr` 提 PR/MR 或直推共享分支；
  2. **SOP**：节点 = Agent/脚本/人工关口/子 SOP；四条内置 SOP（后端/前端/缺陷修复/产品方案）；独立沙箱、审批/打回/重试/暂停恢复。
- **角色分离**：方案/实现/评审/测试各有独立提示词、执行器、模型、规则、Skills、MCP 工具（`README.zh-CN.md`，✅）。
- **执行器多品牌**：codebuddy/codex/claude-code/opencode/cursor/agy(gemini)/kimi/pi/dsh(deepseek)（lf-frontend §② 引 `web/orchestrate.js` 品牌表，✅）。**口径修正**（lf-backend §6 ✅）：公开 API `GET /api/executors` 只读返回 **3 个内置执行器**（CodeBuddy/Codex/Claude Code，API Key/BaseURL 取需求 Owner、回退项目 Owner）；迁移 025 注释**预留** OpenCode/Cursor/Antigravity/Kimi/Pi/DeepSeek Harness 凭据层——品牌表是前端展示面、凭据层是后端预留，两者不是一回事。
- **边界**：团队协作/SaaS/企业管理属其「平台版」维度；本项目（个人机）吸收时须剥离（详见 006）。

## 2. 功能需求清单（按功能域）

### 2.1 对话工作台（Main Bot）

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 会话式主入口 | 用自然语言探索代码、做快速修复 | 普通对话本地进程；改仓库时升级为工作区（§2.2） | `README.zh-CN.md` ✅ |
| 每会话独立配置 | 不同任务用不同执行器/模型/审批档 | 执行器、模型、确认模式（confirm/assisted/auto）按会话保存 | `README.zh-CN.md:148-162` ✅；lf-frontend §④B5 |
| @ 引用 | 把角色/需求/SOP/运行/仓库直接带进上下文 | 引用五类；统一搜索 `GET /api/assistant/references?projectId=&scope=&type=&q=&offset=`；180ms debounce | lf-frontend §④B3 ✅ |
| 排队托盘 + 引导打断（steer） | Agent 跑着时用户想到新指令 | running 时发送→`stopTurn()`（`POST /api/assistant/sessions/{id}/generation/cancel?generationId=`）→400ms 轮询至 8s→`{steered:true,toAi:true}` 重发原话 | lf-frontend §④B4（`web/workbench.js`）✅ |
| 草稿卡工具确认 | Agent 提议的工具调用先过人 | 工具调用渲染草稿卡→afConfirm「将通过平台接口执行 {tool}」→`POST /mcp` JSON-RPC `tools/call` | lf-frontend §④B6 ✅ |
| 斜杠命令 | 高频动作快捷化 | `/建需求`、`/跑`、`/sop` | lf-frontend §④B7 ✅ |
| 会话管理 | 置顶/重命名/归档/删除/未读 | 未读圆点 = `assistant_session_reads` 表 + `POST /api/assistant/sessions/{id}/read` | `design.md`（未读圆点节）✅ |
| 双 UI 并存 | 重客户端聊天 vs 页内轻工作台 | React 19+Vite `chatbot/` 构建产物 `web/native-chat/`（846KB JS + 288KB CSS）以 iframe 挂载走 postMessage 协议；另有原生 JS `web/workbench.js`（789 行）页内工作台重实现 | lf-frontend §②/B1/B2 ✅ |
| postMessage 桥契约 | 宿主与 iframe 聊天解耦通信 | 宿主→iframe 发项目上下文；iframe→宿主 6 种消息（chat-ready/chat-session(白名单 `/^[a-zA-Z0-9_-]{1,64}$/`)/chat-project/projects-changed/welcome-demo/quick-access-completed） | lf-frontend §④B2（`web/app.js:1490-1549`）✅ |
| 富渲染与导出 | 对话内看图/出 PPT | mermaid 11 / pptxgenjs 4 / react-markdown / remark-gfm / dompurify（XSS 防护），浏览器端 PPT 导出 | lf-frontend §④B1（`chatbot/package.json`）✅ |
| 产物抽屉 | 集中查看运行产物 | 独立抽屉面板，宽 440px | `design.md`（布局节）✅ |

### 2.2 对话工作区（Docker 沙箱 + 按轮次租约）

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 工作区唯一入口 | 防绕过：所有落盘改动必须经工作区 | `workbench_workspace_open` 工具；`issueId`/`workspaceRunId`/`new` 三参互斥 | `docs/conversation-workspaces.md` ✅ |
| 按轮次租约 | 工作区不长期独占 | 每轮租约 + 续租；快照只在轮末或切换前保存；释放的工作区先落 fallback 快照再回收 | `docs/conversation-workspaces.md` ✅ |
| 检查点快照落库 | 崩溃/回收后可恢复 | `run_workspace_checkpoints`：快照 **MEDIUMBLOB + sha256 校验 + lease 列**；恢复态 `restore_status`/`restore_started_at`/`restore_error`（迁移 013/018） | lf-backend §C2（迁移 SQL）✅ |
| 分支模式 | 改动落到哪条分支可控 | `auto`/`continue`/`create`/`readonly` + `baseBranch` + 按仓库 `repositoryBranches`；`missingRepositories` 错误驱动新开工作区 | `docs/conversation-workspaces.md` ✅ |
| PR/MR 提交 | 改完即交付 | `workbench_workspace_submit_mr`；`mode=direct` 直推共享分支 | `docs/conversation-workspaces.md` ✅ |
| SOP 工作区控制 | 人可介入 SOP 执行中的工作区 | `workbench_workspace_sop` pause/resume/takeover；执行中只读暂停后可写 | `docs/conversation-workspaces.md` ✅ |
| 角色权限隔离 | 子角色不得另开工作区 | 角色子 Agent `disallowedTools: [workbench_workspace_open]` | `docs/conversation-workspaces.md` ✅ |
| 需求会话可见性 | 前端知道需求有无对话/分支 | `GET /api/issues/{id}/requirement` → `conversation {sessionId, available, branches}` | `docs/conversation-workspaces.md` ✅ |
| 对话交付态 | 对话式开发也有交付状态 | 对话专有状态：对话开发中/MR 待合入/已直推等 | `docs/conversation-workspaces.md` ✅ |

### 2.3 SOP 流程引擎（核心）

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 四类节点 | 编排 Agent/脚本/人审/命令 | `WorkflowNode` 类型 `agent`/`script`/`approval`/`command` | `internal/engine/core_cases_test.go:33-93` ✅ |
| 节点校验字段 | 流程定义非法即拒 | `MaxAttempts`/`RetryStrategy`/`OnFailure`(`reject`/`fail`/`human`，空串→`fail`)/`RejectTo`/`ExecutionScope`/`ScriptLanguage`(python\|shell，ruby 非法)/`RunOnWorkspaceRebuild` | `internal/engine/core_cases_test.go:33-93` ✅ |
| 产物声明 | 节点产出可追溯、防路径逃逸 | `ArtifactOutputs`(Name/Path)；拒绝 `../` 路径与重名 | `internal/engine/core_cases_test.go:33-93` ✅ |
| 失败打回 | 失败不整条报废，退回指定上游 | `OnFailure=reject` + `RejectTo` 指向节点；gate 失败归一化（`resolveGateOnFailure`，大小写不敏感） | `internal/engine/core_cases_test.go:33-93,239-246` ✅ |
| 条件边 | 分支执行 | `WorkflowEdge.When: "success"`；环检测（a→b→a 拒绝） | `internal/engine`（边/环用例）✅ |
| 调度与租约 | 分布式下不重跑不饿死 | 非 DAG 拒绝（`workflow_graph.go:263`）；依赖全成功即调度（`node_execute.go:60-85`，非按波次）；MySQL 行租约 TTL 30s（`AGENTFLOW_RUN_LEASE_TTL`）、心跳=TTL/3（`engine.go:33-35`） | lf-backend §A2-A4（实测行号）✅ |
| gate 结果文件协议 | 门禁结论结构化、跨进程可信 | 执行器写 `.agentflow/gate-results/<nodeID>/attempt-<N>.json`：`Decision`(pass\|rework\|fail\|human)+`Reason` 必填+`ActionRequired`(human 必填)+`Summary`+`Findings[]`（每条 ≤64 字）；字段上限 4000/50/500/64/4000 字符；**文件缺失不视为失败** | lf-backend §A5（gate_submit.go:44-100）✅ |
| rework 预算与停靠 | 打回不无限循环 | `MaxReworks`/`ReworkCount`；rework 无 `RejectTo` 目标 ⇒ `fail`；预算耗尽停靠人工关口 | lf-backend §A6 ✅ |
| 中断恢复 | 进程重启不丢运行 | `Attempt>0` 且无 Error 且 Activity 含「等待恢复」⇒ 恢复该节点；script 节点清 `ResumeSessionID`、agent 节点保留 | lf-backend §A7（lifecycle.go）✅ |
| 人工关口 | 关键节点必须人拍板 | approval 节点三动作：**通过/补充上下文重跑/退回上游**；门禁人话说明三端同文案（契约测试钉死） | lf-frontend §④D3（`web/app/approvals.js` 538 行）✅ |
| SOP 版本历史与发布 | 流程改动可回溯、发布才生效 | 模板版本历史 + 发布机制 | `README.zh-CN.md:148-162` ✅ |
| 四条内置 SOP | 开箱即用 | 后端/前端/缺陷修复/产品方案 | `README.zh-CN.md` ✅ |
| 运行控制工具组 | 人可查/暂停/完成/重试 | `runs_control_get`、`runs_group_sop {nodeId}`、`runs_group_complete {reason}`、`runs_group_sop_status`/`retry`；**工具返回=受理而非完成**；验收凭据存 `workflowSnapshot.groupCompletion` | `docs/bot-sop-controls.md` ✅ |
| 无人值守纪律 | 定时任务不得自批 | 计划任务（无人值守）禁止调用控制写工具 | `docs/bot-sop-controls.md` ✅ |
| 三个独立事实 | 防「跑=通过」混淆 | 节点执行结束 / 验证结果 / 用户验收 三者独立记录 | `docs/bot-sop-controls.md` ✅ |
| AI 编排器 | 自然语言生成流程 | `startAIBuild(raw, opts)`（orchestrate.js:499）：自然语言→可放置 SOP/画布；edit 模式失败文案「这轮没有改动画布，当前流程还在」 | lf-frontend §④C2 ✅ |
| SOP 画布 | 可视化编排 | 节点/边编辑、flow-palette/flow-viewport/flow-history 模块（flows.js 1146 行） | lf-frontend §④C1 ✅ |
| SOP 预览站点 | 发布前无需起服务即可走查 | 预览模式（不经发布链直接走查流程形态） | lf-frontend §④ ✅ |
| 免 API Key SOP | 新手零配置起步 | 预置执行器配置即可跑内置 SOP | `README.zh-CN.md` ✅ |

### 2.4 角色分离（方案/实现/评审/测试）

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 角色定义 | 不同环节不同「人格」与工具面 | 每角色独立提示词/执行器/模型/规则/Skills/MCP 工具 | `README.zh-CN.md:148-162` ✅ |
| 角色视图 | 界面化管理角色 | `agents` 视图（index.html data-view） | lf-frontend §③ ✅ |
| 子角色工具限制 | 职责越权防护 | 子 Agent 禁 `workbench_workspace_open`（§2.2） | `docs/conversation-workspaces.md` ✅ |

### 2.5 需求到交付（需求空间 + PR/MR）

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 需求 8 状态机 | 需求生命周期标准化 | backlog/todo/in_progress/in_review/done/closed/blocked/cancelled | lf-frontend §④A2（`web/app/issue-model.js`）✅ |
| 需求向导三步 | 把模糊想法变成可执行需求 | 描述→AI 澄清循环（chatClarifySession/JobId/AbortController、问题逐条作答、进度徽章）→确认；仓库多选变更→重新澄清；composer.js 1143 行 | lf-frontend §④A1 ✅ |
| 需求↔SOP 泳道 | 需求与运行按泳道组织 | lane{产品/前端/后端/设计/修复/其他}；按 `run.executionContext?.issueId && !parentRunId` 归组 | lf-frontend §④A3 ✅ |
| 需求健康度 | 一眼看哪些需求要人管 | attention/active/not_started/closed + rank；attention 强制表格视图 | lf-frontend §④A4 ✅ |
| playbook 多 SOP 绑定 | 一个需求按项目自动挂多条 SOP | projectSOPBindings/materializePlaybook/effectiveIssueSOPBindings | lf-frontend §④A5 ✅ |
| PR/MR 交付与合入检测 | 交付状态自动回填 | PR/MR 状态、合入检测、共享分支直推；交付状态（§2.2） | `README.zh-CN.md:148-162` ✅ |
| 群聊派活 | 多人群聊里 @Bot 干活 | @ 引用；一次最多挂 5 条 SOP（超限 toast）；SOP 目录=通用或项目自有/共享 published | lf-frontend §④A6（`web/rooms.js`）✅ |

### 2.6 运行与审批

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 运行列表/详情 | 全局看所有执行 | 无列头一体化面板、滑动标签筛选、SOP 下拉、搜索 | `design.md`（运行记录列表节）✅ |
| 实时流双游标 | 运行中增量刷新不重不漏 | SSE `after={telemetrySequence}&logsAfter={logSeq}` 两独立游标 + telemetryDelta/logDelta 增量合并 + workflowSnapshot 快照 | lf-frontend §④D1 ✅ |
| 状态徽标 8 态 | 运行状态一眼可辨 | 进行中(呼吸灯)/排队中/待确认/已暂停/已完成/失败/已取消/自由协作·待指令；SSE 打补丁整体替换徽标 | `design.md`（状态徽标表）✅ |
| 节点轨迹/日志产物 | 出问题可钻 | 详情页节点轨迹 + 日志 + 产物 | lf-frontend §③ ✅ |
| 重跑与分支恢复 | 失败可局部重跑 | 取消后重跑单分支，恢复被连坐取消的兄弟分支 | `docs/architecture.md` ✅ |
| 控制中断双保险 | 暂停/取消不误伤进行中的活 | 控制位**先落库再通知** worker；worker 收 `X-Agentflow-Control-Token` 校验后才生效（`run_control.go:18-110`） | lf-backend §A4（run_control.go:18-110）✅ |
| 并发准入 | 项目级限流防跑飞 | `run_admission_locks`：项目并发上限 10（迁移 010），超限排队等待 | lf-backend §A9（迁移 010）✅ |
| 审批队列 | 待我确认集中处理 | approvals 视图 + 三动作（§2.3）；正常三键/轮次用尽双键（正则 `/max turns|轮次用尽|是否重试/i`）；gateResult 渲染待确认项与证据（title+evidence） | lf-frontend §③/§④D3 ✅ |

### 2.7 知识中心

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 知识接入 | 团队文档可被 Agent 检索 | 连接配置 + 来源管理（平台默认/项目自定义、`status=ready`、`doc_count` 占比条） | `design.md`（知识中心节）✅ |
| 角色开启/用量 | 按角色控制知识可见性 | 已开启角色数 + 待审核数即按钮，点击切标签；`usage.activity` 记最近检索 | `design.md` ✅ |
| Agent 写回→人审核 | Agent 产出知识不直写 | 写回候选→审核（待处理/已写回/已驳回）→写回前校验来源与 `.md` 路径并二次确认；未开「审核后写回」时写回按钮禁用 | `design.md` ✅；lf-frontend §④C6 |
| 检索留痕 | 检索可回溯引用 | 检索框 + 结果保留标题/小节/两行摘要/引用原文（淡琥珀高亮抽屉）；「Agent 最近的检索」 | `design.md` ✅ |
| kb_* MCP 工具 | 知识能力以 MCP 工具暴露 | `kb_*` 工具族 | lf-frontend §③（knowledge.js）✅ |

### 2.8 通知机器人（IM 双向）

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| IM 机器人接入 | 运行事件推 IM | 三步向导配置（机器人管理页）+ 健康徽章 + 发送记录 | lf-frontend §③（bots.js）✅ |
| 双向对话 | IM 里直接跟 Bot 说话 | 双向对话连接 | lf-frontend §③ ✅ |
| 通知引擎 | 服务端统一投递 | `notification_bots`+`user_bot_credentials`（密文列）+`notification_deliveries`（迁移 004）；Provider=CLI 子进程 `Send(ctx,bot,targetID,content)`；投递失败健康码 **850003/853004 → 持久化 `awaiting_authorization`**；过期通知回执「节点已无需人工处理」；AI 日报 `report_*` 字段（迁移 007） | lf-backend §I1-I2（notification/service.go:17-56）✅ |
| IM 用户绑定与会话队列 | 群里 @Bot 找对人、事件不丢 | `bot_user_bindings`+绑定挑战（code_hash/attempts/expires_at）、`bot_connections`(lease_until/state)、`bot_conversations`/`bot_interactions` 队列表（迁移 007/008） | lf-backend §I3（迁移 SQL）✅ |

### 2.9 MCP 三件套

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| MCP 服务端 | 平台能力以 MCP 暴露 | `/mcp` stateless Streamable HTTP（JSON-RPC `tools/call`）；body ≤4MiB；协议版本 2024-11-05/2025-03-26/2025-06-18；资源模板 `agentflow://runs/{runId}/artifacts/{artifactId}` 与 `.../logs`；GET/DELETE → 405；`/api/capabilities` 声明 `readOnly:true` | `docs/api/README.md` ✅；lf-backend §M1（mcpserver 2/723）✅ |
| MCP 客户端 | 平台调用外部 MCP 工具 | `internal/mcpclient`（1 文件/355 行，极薄）；角色可挂 MCP 工具（§2.4） | lf-backend §M2 ✅ |
| gate-mcp / 代理网关 | 门禁提交与 stdio 分发独立成二进制 | `cmd/agentflow-gate-mcp`（21 行 main：经 stdio 写 `AGENTFLOW_GATE_RESULT_PATH` 结果文件，§2.3）；`cmd/agentflow-mcp`（26 行 stdio 代理）；Makefile:63-72 四平台静态二进制 | lf-backend §M3（gate_submit.go:44-100；Makefile）✅ |
| MCP 工具接入视图 | 界面化管理接入 | `mcp` 视图 | lf-frontend §③ ✅ |

### 2.10 用量统计与成本

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 用量分析 | token/成本/完成率可视化 | 双 tab（用量/趋势）、scope 切换、环比；成本 **CNY** 展示 | lf-frontend §④E3（observability.js）✅ |
| Token 公开标准价 | 成本算法透明可复算 | 公开标准单价（⚠️ 定价数字细节 lf-backend 报告为低置信，引用须标「低置信」） | `README.zh-CN.md:148-162` ✅；lf-backend N1 🔶 |
| 静态导出 | 报告可离线归档 | 静态 HTML/SVG 导出 | `README.zh-CN.md` ✅ |

### 2.11 运行评测与可观测

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 运行评测（runeval） | SOP/Agent 跑得好不好可量化 | `internal/runeval`（3 文件/601 行）；BuildFacts 生成有界事实摘要：maxDigestEvents=400/maxDigestMessage=200/maxFactInput=800/maxErrorText=160/maxToolRepeats=12（`facts.go:12-55`） | lf-backend §R1（facts.go:12-55）✅ |
| 可观测 | 平台自身运行状态可查 | `internal/observability`（2 文件/763 行）；事件表 STORED 生成列 + `JSON_EXTRACT` 索引（迁移 001，全库唯一非幂等）+ SQLite JSON 函数兜底；obsPending/obsKick/StartMaintenance 维护机制；指标仅 OTLP 回环自收（迁移 001 内建 receiver） | lf-backend §O1-O3（迁移 SQL）✅；`design.md`（运行记录列表节）✅ |
| 长命令看护（procwatch） | 工作区准备不被慢命令拖死 | 监控**工作区准备期长外部命令**（git clone、docker build）：流式读取输出、Observer 进度回调、停止打印即杀、总超时（`DurationEnv`，"0"=禁用）、错误仅保留尾部日志行；Unix 杀进程组/Windows 杀子进程（共 4 文件：procwatch.go:1-5,81-163 + group_unix/group_windows） | lf-backend §P1（procwatch.go 实读）✅ |

### 2.12 执行环境（Docker/Runner）

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| Docker 对话工作区 | AI 改代码不污染宿主 | 每工作区 Docker 沙箱 + repo checkout（§2.2） | `README.zh-CN.md` ✅ |
| Runner 管理 | 执行机器可注册可观测 | `cmd/agentflow-runner`（runner 18 文件/5061 行）：bootstrap token 注册、工作区 `state.json`+心跳 30s+长轮询 35s、标签选择、`-reuse-run-sandbox`/`-auto-update`、仅出站 HTTPS | lf-backend §B1-B2（runner/）✅；lf-frontend §③ ✅ |
| 多 CLI 托管 | 本机执行器版本不漂移 | `managedcli`（14 文件/1259 行）：`-cli-action status\|update\|rollback`；版本锁定 CodeBuddy 必装、Codex ≥0.162.1、Claude Code ≥2.1.300 | lf-backend §B3 ✅ |
| 沙箱档案 | 执行隔离可分级 | `SandboxProfile{local\|native\|docker}` + 预构建镜像清单 | lf-backend §B4 ✅ |
| 环境资产 | Dockerfile/环境/沙箱模板化 | assets 视图（子视图归并：规则/技能/Dockerfile/环境/沙箱） | lf-frontend §③ ✅ |

### 2.13 团队协作/认证/平台

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 登录与鉴权 | 团队多用户安全 | `AGENTFLOW_AUTH_REQUIRED=true`；HttpOnly Cookie 登录；Bearer `afp_<token>`（token 只能经 Cookie 签发）；401 + `WWW-Authenticate: Bearer realm="AgentFlow"` | `docs/api/README.md` ✅ |
| API 纪律 | 契约稳定可测 | 2 MiB JSON 上限、RFC 3339、gzip、**全量数组响应（无分页）**、同源 CORS、engine 错误→400 | `docs/api/README.md` ✅ |
| 平台元接口 | 客户端自描述引导 | `/api/health`、`/api/capabilities`、`/api/bootstrap` | `docs/api/README.md` ✅ |
| GitHub 设备码授权 | 免手动 token | 设备码授权流程 | `README.zh-CN.md` ✅ |
| 页面反馈系统 | 用户在页面内报问题 | 悬浮反馈按钮/卡片/元素拾取；`page_feedbacks`、`page_feedback_images` 表 + 对应 API | `design.md:454` ✅ |
| 业务领域/平台管理 | 多团队多项目治理 | businessDomains/platform 视图 | lf-frontend §③ ✅ |

### 2.14 AI 定时任务

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| AI 计划任务 | 周期性自动跑 SOP/对话 | aiSchedules 视图 + `internal/schedule`（1 文件/399 行）+ 自研 `internal/cronexpr` cron 解析；`ErrAlreadyRunning` 单实例互斥 | lf-backend §S1 ✅；lf-frontend §③ ✅ |
| 无人值守纪律 | 定时任务不得自批 | 禁调控制写工具（§2.3） | `docs/bot-sop-controls.md` ✅ |
| 共享计划 | 计划可跨域共享 | `docs/api/shared-schedules.md`；已发布 base 不可变（再次发布 baseVersion 冲突 409）+ 项目订阅共享计划；执行身份=目标项目 Owner；参数 ≤20 个/每值 ≤4000 字节；BaselineGuard 拒越基线新文件 | `docs/api/shared-schedules.md` ✅；lf-backend §S2 ✅ |

### 2.15 前端产品形态

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 单页多视图 | 23 个视图 + 2 旧名 alias | hash 路由 `switchView()`（app.js:1911-2021）；56px 图标栏 + 304px 二级侧栏 + 440px 产物抽屉 | lf-frontend §③ ✅；`design.md` ✅ |
| 无框架主体 | 零构建依赖、嵌入即用 | 原生 JS SPA：`index.html`（25 data-view）+ `app.js`（全局 state）+ `app/` 组件库 41+ 模块 | lf-frontend §② ✅ |
| UI 上游 | 站在成熟设计上 | Grasp+Harness UI 上游 `da2e4768`（MIT）；主题 `agentflow.theme.v3` | `design.md` ✅ |
| 暗色主题三段开关 | 人可用性 | 浅色/深色/跟随系统（radiogroup，偏好写 `agentflow.theme.v3`，含 legacy 键迁移）；切换 ~280ms 渐变；`prefers-reduced-motion` 时直接切换 | lf-frontend §④F1 ✅ |
| i18n | 双语界面 | `web/i18n-en.js` 词典（亦作功能全景） | lf-frontend §③ ✅ |
| 全局命令面板 | 键盘党快速操作 | 「搜索或执行操作…」顶栏 | lf-frontend §③ ✅ |
| 契约测试防漂移 | 前后端文案/契约钉死 | `*_test.js` ↔ `*_test.go` 双端同断言 + node 假 DOM（`hui_fake_dom.js`） | `design.md:463-465` ✅ |
| 嵌入式部署 | 单二进制分发 | 服务端 embed 前端资源；只改源码不重建嵌入资源不生效 | `design.md:468` ✅ |
| 可访问性/动效纪律 | 人可用 | `prefers-reduced-motion`；e2e `#pageTitle` 锚点 | `design.md` ✅ |

### 2.16 本地形态（local/ CLI + 多宿主技能分发）

| 功能 | 解决什么问题 | 关键机制 | 证据 |
|---|---|---|---|
| 本地 CLI | 不起平台也能用方法论 | Python(`devflow_cli`)+Node(`bin/loopforge.mjs`) 混合 CLI | lf-frontend §② ✅ |
| 多宿主技能分发 | 平台方法论编译到各 agent 宿主 | `.claude/.codex/.cursor/.codebuddy/.agents` 五套宿主配置（agents/skills/commands/rules/workflows/checklists/runtime）+ manifest | lf-frontend §② ✅ |
| dsh/hermes 插件 | DeepSeek Harness/Hermes 用户直装 | 仓内 dsh-plugin、hermes-plugin | lf-frontend §② ✅ |
| 一键安装 | `npx loopforge-cli install claude` | 安装后获得 `/start-devflow` | `README.zh-CN.md` ✅ |
| 平台版 vs Local | 按团队规模选形态 | README 对比表 | `README.zh-CN.md` ✅ |

## 3. 非功能需求

- **可自主部署**：自托管，MySQL + Go 单服务 + 嵌入式前端（`make build`；`design.md:468`）。
- **容器化部署**：compose 样例含 mysql:8.4 + `ghcr.io/tencent/loopforge:0.3.4`（镜像内含全部 5 个二进制）| lf-backend §② ✅。
- **安全**：Cookie/Bearer 双鉴权、2 MiB 上限、同源 CORS、iframe 消息白名单（`docs/api/README.md`；`web/app.js:1490-1549`）。
- **可测试**：Go 单测 + `webtest`（node 假 DOM）+ `e2e` 目录 + 双端契约测试（`design.md:456-466`）。
- **浏览器验收**：桌面/窄屏、明暗主题、减少动效、键盘、软键盘浮层、动画期布局稳定（`design.md:470`）。
- **验收语义**：受理≠完成、三个独立事实、历史设计记录标注「已移除」（`docs/bot-sop-controls.md`；`docs/conversation-workspaces.md`）。

## 4. 数据模型概览

MySQL migrations 001–026（30 个 SQL 文件；lf-backend §D 已核 ✅）：仅 007/016/018/024 为增量对（带 guard 条件可重跑），**001 非幂等**（一次性全量建表，重跑即报错，系全库唯一）；GORM AutoMigrate 与手写迁移共存（AutoMigrate 只加列）。已点名表：`run_workspace_checkpoints`（013/018）、`run_admission_locks`（010，项目并发上限 10）、`sop_handoff_jobs`（015/017）、编辑器窗口 `workspace_editor_id`/`edit_until`（011）、`requirement_spaces` 五表（018）、`runs.kind='workspace'` 与 `submit_mr`（020）、`auth_integrations`（022）、`notification_bots`/`user_bot_credentials`/`notification_deliveries`（004）、`bot_user_bindings`/`bot_connections`/`bot_conversations`/`bot_interactions`（007/008）、`assistant_session_reads`（未读圆点，`design.md`）、`page_feedbacks`/`page_feedback_images`（页面反馈，`design.md:454`）；`workflowSnapshot.groupCompletion`（验收凭据，运行时快照非表，`docs/bot-sop-controls.md`）。

## 5. 技术栈与架构

- **后端**：Go ≥1.26.10；MySQL 8.4（utf8mb4）唯一数据源（`-mysql-dsn`）；GORM AutoMigrate + 手写 SQL 迁移共存；`-role api|worker|all` 分角色部署；运行需 `AGENTFLOW_SESSION_SECRET`；运行租约 TTL 30s 可调（`AGENTFLOW_RUN_LEASE_TTL`）。`cmd/` 5 入口：agentflow / agentflow-runner / agentflow-mcp / agentflow-gate-mcp / loopforge-account（⚠️ design.md §8 写 `cmd/agent-runner` 与实际不符，以目录为准）。实测 21 个包（文件/行）：httpapi 131/39979、repo 78/17849、runtime 52/13184、engine 49/12704、domain 50/7424、runner 18/5061、managedcli 14/1259、tracker 9/3321、preview 9/2120、gitwork 8/1847、botinteraction 5/1093、knowledge 4/880、auth 3/577、runeval 3/601、scriptview 3/509、notification 3/531、observability 2/763、mcpserver 2/723、gatemcp 1/249、mcpclient 1/355、schedule 1/399（procwatch 未测量）（lf-backend §①-③ ✅）。
- **前端**：无框架原生 JS SPA（web/）+ React 19 聊天（chatbot/→iframe 挂载 web/native-chat/）；VS Code Web 内嵌任务 IDE（`web/vscode.js`，🔶 内容未读）。
- **数据**：MySQL migrations；运行时快照（workflowSnapshot）。
- **本地**：Python+Node CLI + 五套宿主配置。
- **架构原则**：engine 就绪分支并行跑；一域一仓；一域一 handler（`docs/architecture.md` ✅）。

## 6. 与本项目的关系（速览）

本项目的吸收裁决（哪些吸收/怎么吸收/哪些不吸收）见 **[006-loopforge-absorption-map.md](006-loopforge-absorption-map.md)**。速览：重叠——九阶段闭环、审批/打回/重试、外部 Agent 委派、执行留痕、暂停恢复；空白——可执行 SOP 编排引擎、隔离沙箱工作区、多宿主技能分发、需求↔运行追溯、统一模型目录；红线外——团队协作/SaaS 维度。

## 7. 证据分级说明

- **✅Verified**：lead 亲读 `README.zh-CN.md`（全文）、`design.md`（全文 472 行）、`docs/conversation-workspaces.md`、`docs/bot-sop-controls.md`、`docs/architecture.md`、`docs/api/README.md:1-80`、`internal/engine` 关键用例（grep 定位）；lf-frontend 亲读 web/ 25 文件（清单见其 §①）；of-inventory 亲读本项目 48 档案与 5 份架构文档。
- **🔶Inferred / 低置信**：lf-backend 报告已合并（原「目录级 🔶（lf-backend 补强中）」占位行已全部升级为 ✅ 或明确低置信）。**低置信项**（引用时必须标注「低置信」）：token 公开定价细节、迁移 015 摘要列类型、ingest 状态码列表。**仍未读不写**：`web/vscode-web/`、`chatbot/src` 内部、`docs/validation.md`。
- **边界**：本文只描述 LoopForge「有什么」，不含吸收建议的论证（论证在 006）。
