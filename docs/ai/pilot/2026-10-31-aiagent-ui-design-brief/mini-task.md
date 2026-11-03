# Mini-Task：AIAgent 插件 ui-design-brief.md 首篇回填（UI 交互设计说明 · 面向设计师）

> 日期：2026-10-31｜级别：轻量（1 个文档文件，Intent/Spec/Plan/Task 合并单文件）
> 闸门1：用户 2026-10-31 18:17「按你推荐的来」= 批准「AIAgent ui-design-brief 插队下一批」（输入10 拍板项①）；本文件为该批的实施方案，待闸门1 追认后实施。
> 背景：2026-10-30 用户立「所有插件必备交互设计说明」；PILOT-077 复判确认 AIAgent（UI 最活跃插件）无 brief 是工具块四缺陷逃逸的结构性缺口之一，优先序第一（输入6）。

## Intent（为什么/做什么/到什么程度）

- **Problem**：`Plugins/AIAgent/docs/ui-design-brief.md` 不存在（输入10 glob 实证）。AIAgent 是插件中 UI 面最大、改动最频的（agent-core 聊天组件 + 执行记录面板 + 左右栏），却没有一份面向设计师的交互说明——设计师不读代码，四缺陷（黑底/残行/默认收起/不对齐）暴露前无人有「应该长什么样」的基准。
- **Why**：① 2026-10-30 规则（所有插件必备，18 个待回填）；② PILOT-077 复判 + im-gateway 批实证：有 brief 的插件（Sems/ImGateway）在 UI 审计里可核对，无 brief 的（AIAgent）只能靠代码反推；③ 本批把 PILOT-077 已实测的视觉事实（286 对齐线、rgb 主题值、flexShrink=0、默认展开、170 max-height）沉淀为设计师可读基准，防再逃逸。
- **Expected Outcome**：AIAgent 全部用户可见面各有一节「功能描述 + 旅程 + 界面交互行为（点什么出现什么）+ 五态 + 视觉基准」，每条声明可追溯到组件文件或实测值；后续 AIAgent UI 批的 Spec/走查以它为基准文件。
- **Constraints**：只写文档不改代码；不改变 DESIGN.md/im-design 真源关系（DESIGN.md 仍是全站真源，brief 是插件级落地说明）；**不做** Sems/其余 17 插件（排队按优先序）。

## Spec（要点）

## Interaction Design

本批为设计师可读 brief：逐面写「点什么出现什么」与五态；实装交互以 §FR-2 七面为准（聊天流、工具块、步骤、执行记录、composer、左右栏）。

- **FR-1 文件与骨架**：新建 `Plugins/AIAgent/docs/ui-design-brief.md`，结构对齐 Sems 样例（参照 `Plugins/Sems/docs/ui-design-brief.md`）：概览（页面入口/版本/技术形态 agent-core Shadow DOM）→ 组件清单 → 逐组件节 → 五态总则 → 验收基准。
- **FR-2 覆盖面（全部用户可见组件，逐组件一节）**：
  1. **聊天消息流**（bubbles：用户/助手气泡、左右对齐、时间戳、气泡内 markdown/代码块）；
  2. **工具调用块**（BlockRenderer tool：默认展开、I/O 卡片主题基准、折叠交互、写操作徽标、执行中→成败状态流转、max-height 170 内滚动）；
  3. **计划/步骤时间线**（步骤进度卡：步骤 started→completed 流转、run-stuck 呈现、目标评估事件行）；
  4. **执行记录面板**（RunRecordPanel：列表/详情双栏、Run 状态徽标、干预按钮组 继续/重开/跳过/补位/取消、步骤全高不压缩、规划过程折叠）；
  5. **composer 输入区**（输入框/发送/上下文图标行 项目/Agent/模型/工具/技能/标准/待办/权限胶囊）；
  6. **左栏会话面板**（当前会话统计、历史会话折叠、能力画像、Agent 列表）；
  7. **右栏 AI 上下文**（项目目录/MCP 工具/技能/提示指令/记忆计数）。
- **FR-3 每节必含**：一句话功能描述；用户旅程（从哪进、点哪出现什么）；五态（加载/空/出错/无权限/正常）在该组件的表现；**视觉基准**（对齐线 286 教义、间距 token `--aiw-space-*`、主题矩阵 light/dark 逐面 token 引用、关键几何值）。
- **FR-4 可追溯性**：每条视觉基准标注出处 = 组件文件:行（`Plugins/AIAgent/web/src/agent-core/...`）或「PILOT-077 实测值」（05-evidence 背书）；无实测依据的目标值标 `目标态（未实测）`，禁止编造。
- **FR-5 开头声明真源链**：UI/UX 规范唯一真源 = 仓库根 `DESIGN.md` → 本 brief = 插件级落地说明 → 02-spec 视觉规格节引用本 brief（对齐 im-gateway 批 2026-10-31 修正惯例）。

## Plan

1. 读齐素材：`Plugins/AIAgent/web/src/agent-core/`（blocks.css/widget.css/bubbles.css/BlockRenderer.vue 等）、`web/src/components/RunRecordPanel.vue`、`web/src/views/AiAgentView.vue`、Sems brief 样例、PILOT-077 05-evidence 实测值。
2. 起草 `Plugins/AIAgent/docs/ui-design-brief.md`（FR-1~5）。
3. 交叉核对：逐条声明对源码/实测值；发现「brief 声明 ≠ 实装」的偏差当场列清单（只记录不修码，进 05 的 Problems Found，代码偏差归 TODO）。
4. 回写：TODO 回填条目标注 AIAgent 完成；PILOT-077 07 报告不动（独立批）。

## Task

- **Allowed**：`Plugins/AIAgent/docs/ui-design-brief.md`（新建）+ `docs/ai/pilot/2026-10-31-aiagent-ui-design-brief/`（mini-task/05/06）+ TODO/日记。
- **Forbidden**：`Plugins/AIAgent/**` 其余任何文件（纯文档批，零代码）；DESIGN.md/02-spec.tpl（真源已齐）；git 提交；发布。
- **验收标准（AC）**：
  - [ ] brief 存在且含 FR-1 骨架七节，无空节。
  - [ ] 覆盖 FR-2 全部 7 组件面，每节含功能/旅程/五态/视觉基准四要素。
  - [ ] 抽查 10 条视觉声明：每条可回溯到 `文件:行` 或 PILOT-077 实测值，0 条编造。
  - [ ] 偏差清单（brief 声明 vs 实装）落 05；代码侧偏差逐条进 TODO。
  - [ ] 真源链声明在文件头（DESIGN.md → brief → spec 引用）。

## Test Plan

- 文档批无 e2e/单测；验证 = 上述 AC 抽查 + 06-review 八问（含「声明可追溯性」专项核对）。
- 门禁：不触代码 → 无构建/test 要求；`check-problem-classes` 不受影响（可复跑确认绿）。
- 悬空教训检查：AC 发现的实装偏差必须落 PC/TODO/07 三节，不许悬空（pre-commit L4 校验 07 三节）。
