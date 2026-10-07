# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：PILOT-033（统一 LLM 可观测性，已并入 PILOT-032 CostScope）｜ 日期：2026-10-03 ｜ 2026-10-05 整合 032

## Problem

本项目已经具备**业界领先的 LLM 请求/响应落库能力**（`ChatTurn` 逐字段落库请求体/响应体/头/状态/模型/token/延迟/错误），但对「这些数据意味着什么」的分析能力几乎为零：

1. **看不见花了多少钱**。`ChatTurn` 有 `PromptTokens`/`CompletionTokens`/`TotalTokens`，但**没有 `Cost` 列**，全仓无价目表、无成本换算。用户无法回答「今天花了多少」「哪个模型最贵」。
2. **看不见一次 Agent 运行的完整轨迹**。`AgentRun`/`AgentStepRun` 记录了多步执行，`ChatTurn` 记录了每次 LLM 调用，但**两者之间没有外键**（`ChatTurn` 只有 `RequestId`，无 `TraceId`/`AgentRunId`）。一次 Agent 运行内部产生多少次模型调用、每步耗时多少、哪一步最贵——**无法回溯**。
3. **看不见分布**。`ChatRecordDetail.vue` 只展示单轮的 token 与耗时；`UsageStatsController` 是**工具调用维度**（`UsageRecord` 的 `PluginId`/`ToolId`/`ActionType`），**不区分模型、不涉及 token 成本**。没有「按模型分布」「延迟 P50/P95」「错误率」这类时间序列视图。
4. **主聊天链路用量数据永久丢失**（本回合实测）。`ChatController.cs:131` 与 `:216` 两处 `Usage: null` 硬编码——即使做了成本聚合，**app 主聊天与 agent 会话的消耗全部不可见**，聚合结果会系统性偏低。
5. **已建好的可视化面板是假数据**。`dsh-ui-bundle/my-dsh-activity-timeline-client/src/panel.tsx` 的 `MOCK_ENTRIES`（:53-58）当前是写死的 4 条演示数据，footer 文案自陈「占位置 · 接入 ctx.sessions.<...> 后显示实时数据」。

## Why

**为什么现在做**：

- **数据已经在库里了，缺的是解读**。请求/响应/token/延迟全部落库，**不需要新建采集链路**，只需聚合与呈现——投入产出比最高的时机。
- **成本是决策依据，不是锦上添花**。多供应商（OpenAI/Anthropic/本地模型）接入后，无成本可见性就无法判断该用哪个模型、是否该降级、预算是否超支。
- **trace 是多步 Agent 的排障前提**。本项目核心理念是「以器铸己，日积寸进」的 AI-agent 系统，Agent 会做多步规划与工具调用。**不能回放一次运行，就无法定位「哪一步错了、哪一步最贵」**。
- **与行业标杆存在明确差距**。行业四层（trace / 成本归因 / 质量评分 / 可视化）中，本项目**只有「原始数据落库」这一层达标**，其余三层空白（详见 `00-repository-understanding.md` 缺口表）。
- **已有的假数据面板在误导**。`MOCK_ENTRIES` 让用户以为有活动面板，实际是演示数据。

**为什么不直接引入外部工具**（Langfuse/LangSmith/Helicone 等）：

- 本项目是**本地单机、自托管**的 AI agent 系统，核心资产（对话、工具、配置）全在本地 SQLite。引入外部 SaaS 会**把对话内容送出本机**，与项目的数据自控定位冲突。
- 本项目已有自建 OpenAI 兼容网关（`OpenAICompatibleProvider`），**请求响应天然经过本机**，在宿主内补齐分析能力**零埋点、零锁定**。
- 前置研究（`llm-observability-research-2026.md`）结论：**自托管且数据自控 → Langfuse**；而本项目地基（落库 + 实时通道）已比 Langfuse 的起点更完整，**缺的只是聚合与呈现**，自建成本远低于引入+改造。

## Expected Outcome

完成后，用户**不查数据库、不看后台**，能在本项目界面内回答：

1. **今天/本月花了多少钱？哪个模型最贵？哪个供应商占比最高？**（成本归因，按模型/供应商/日/风格维度）
2. **一次 Agent 运行内部发生了什么？**（以 AgentRun 为根的调用瀑布：每一步的工具调用、每次 LLM 调用的模型/token/耗时，某步最慢/最贵一目了然）
3. **响应速度分布如何？错误率多少？**（延迟 P50/P95/P99、错误率时间序列）
4. **主聊天与 agent 会话的消耗是否可见？**（U6 修复后 usage 真实透传，聚合不再系统性偏低）
5. **会话活动面板显示真实数据而非 mock？**（dsh activity-timeline 接真实数据源）

**程度界定（做到哪一步）**：

- 本轮做到 **阶段0（成本）+ 阶段1（trace 关联）+ 阶段2（可视化 dashboard）+ U6 修复**，三者统一在本 pilot 内闭环。
- **阶段0（成本）已并入**：032 CostScope 于 2026-10-05 并入本 pilot，成本引擎、单价目录 CRUD、惰性物化日汇总、预算规则、模型→供应商解析均纳入本 pilot 范围（详细 API 14 端点 / 4 表 / 任务书见 `archive/032-cost-scope/design.md`；本工件 02-spec FR-3 / 03-plan / 04-task 已整合其口径）。**不重建与 032 重复的设计**，只做统一整合 + 修正 U6 错误。
- **阶段3（质量评估 / LLM-as-judge）本轮不做** —— 需引入评估框架与人工标注流程，属独立立项范畴，仅在 Unknown 登记。

## Constraints

对照规范 §1 硬性约束逐条：

| # | 约束 | 本任务适用情况 |
| --- | --- | --- |
| 1 | 不修改生产环境 | ✅ 只改仓库代码，不动 `:51888` 等运行实例；禁停启宿主（AGENTS.md 发布规范铁律 1） |
| 2 | **不修改数据库结构（迁移即高风险，须升级审批）** | ⚠️ **本任务触及**：`ChatTurn` 需增 `AgentRunId`/`TraceId` 列 → **列为闸门1 裁决点**，未批不得实施 |
| 3 | 不改鉴权/权限/支付/安全核心逻辑 | ✅ 新增端点必须类级 `[Authorize]`（铁律 17）；不改既有鉴权链路 |
| 4 | 不新增大规模依赖 | ✅ 复用 XCode + 现有前端栈（Element Plus）；不引入 Langfuse/OpenTelemetry SDK 等外部依赖 |
| 5 | 不进行无关重构 | ✅ 严格 Allowed/Forbidden 清单；不顺手改 `UsageStatsController`（其缺 `[Authorize]` 属独立 TODO，见 032 D9） |
| 6 | 不修改与本任务无关的文件 | ✅ 见 `04-task.md` Allowed/Forbidden |
| 7 | 不为展示 Agent 能力扩大范围 | ✅ 阶段3 质量评估明确不做 |
| 8 | 最终必须能跑实际测试/构建验证 | ✅ Build + Unit + Integration + E2E 四层，命令见 `03-plan.md` |
| 9 | 结论必须基于真实仓库内容 | ✅ Stage 0 全部实测，已标 Verified 与本回合亲自跑的命令 |
| 10 | 验证失败不伪造成功 | ✅ Evidence 按 Verified/Inferred/Unknown 三级标注，禁止混用 |

**项目额外约束**：
- 前端 **0 自定义 token**（仅 `--el-*` + `color-mix()`），见 user-level skill `frontend-zero-custom-token`。
- e2e **禁 mock、真实登录**；禁手写一次性 `temp/*.cjs`（AGENTS.md 红线）。
- e2e 跑长批须显式指定 `E2E_FRONTEND_PORT`/`E2E_BACKEND_PORT`（本回合日记已记：并行会话共用机器时端口争抢会导致 ERR_CONNECTION_REFUSED）。
- **禁止自动 git commit/push**（须用户显式授权）。
- `features.ts` 作 SSOT，新增视图须登记 + `scripts/check-features.mjs` 双向校验。

## Success Criteria

**须可被实际验证判定，禁止主观描述**：

| # | 判据 | 验证方式 |
| --- | --- | --- |
| SC1 | `ChatController.cs` 两处 `Usage: null` 改为透传真实 `UsageInfo`，主聊天链路 usage 可见 | 单测断言：经 ChatController 的会话事件带非空 `Usage`；集成测试断言聚合端点能取到主聊天 token |
| SC2 | 成本聚合端点返回按模型/供应商/日/风格的 token 与金额，且**未配单价的模型显式列出而非默默计 0** | 单测：未配单价模型进 `UnpricedModels` 且不计入总额；配价模型金额与纯函数手算逐字相等 |
| SC3 | 成本计算是纯函数，单位换算（每 1M token）被单测锁死 | 单测：1M token 基准、小数精度、0 tokens、改单价后历史重算 |
| SC4 | trace 聚合端点能以 AgentRun 为根返回其下 LLM 调用与工具步骤的时序数据 | 单测：给定含 2 次 LLM 调用的 AgentRun，返回的 waterfall 含 2 个 LLM 节点 + N 个 tool 节点，耗时非负且按时间排序 |
| SC5 | 延迟统计正确返回 P50/P95/P99 | 单测：构造已知延迟序列，断言分位数与手算一致 |
| SC6 | dsh activity-timeline 显示真实数据，`MOCK_ENTRIES` 不再是唯一数据源 | e2e：真实会话下打开面板，条目数与该会话实际事件数一致（反例探针：切到空会话必须显示空态而非 mock 4 条） |
| SC7 | 前端 dashboard 呈现成本/延迟/错误率/模型分布，数据与后端端点逐项一致 | e2e + 组件测试：后端返回值与页面展示逐项比对（禁 mock） |
| SC8 | 插件/端点鉴权生效，无 token 裸请求得 401 | 反射断言 + 集成测试无 token 调用返回 401 |
| SC9 | 后端 `dotnet build` + `dotnet test` 通过；前端 `pnpm run check` + `pnpm run test` 通过且**无新增错误** | 真实命令输出，记录 PASS/FAIL 与计数 |
| SC10 | `features.ts` 登记新视图，`scripts/check-features.mjs` 双向校验通过 | 脚本退出码 0 |

**范围纪律**：SC1–SC10 之外的功能**不算完成**；若实施中遇到 Plan 与仓库实际不符，**先记偏差再改 Plan**，不得绕过（规范 §2 Stage 5 第 7 条）。
