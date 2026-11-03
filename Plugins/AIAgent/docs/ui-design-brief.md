# AI Agent · 交互设计说明（ui-design-brief）

> 面向设计师与走查人员（不读代码）。插件路由：`/ai-agent`；技术形态：宿主壳 + 三栏布局 + 中栏 **agent-core**（`ChatWidget`，无 Shadow DOM 的 SFC 集成版）。
>
> **真源链**：仓库根 [`DESIGN.md`](../../../../DESIGN.md)（色板 / 组件 / 三禁红线）→ 本 brief（插件级落地）→ 各 PILOT 的 Spec「Interaction Design」节引用本文件。
>
> **编写方法**：2026-11-03 存量优化——先产品事实与问题盘点，再给**单一推荐重设计方向**（非多稿对比）；实现须遵守 `DESIGN.md` 与 `--el-*` / `--aiw-*` token，不引入仓库外独立色板/字体体系。

---

## 〇、设计北极星（方向摘要）

| 项 | 内容 |
| --- | --- |
| **用户** | 在本机用智能体写代码、跑工具、盯 Run 的开发者 |
| **品牌任务** | 「看见 AI 在做什么、能打断、能续跑、能验收结果」——不是聊天玩具，是**执行工作台** |
| **首屏主角** | 中栏**消息流**（助手过程 + 工具块 + 正文） |
| **唯一主动作** | **发送**（composer 主按钮）；次主动作降级为图标：**执行记录**、侧栏折叠 |
| **调性（五刻度）** | 安静↔中等能量（偏安静）；精致对齐；中等密度；分量中等；偏庄重（少装饰、多信息） |
| **品类基线** | ChatGPT/Claude 三栏 + Cursor 右上下文；保留用户已学会的「左会话 / 中对话 / 右工程上下文」 |
| **让人记住的一刻** | 工具调用展开后 **I/O 与正文同一竖对齐线**，扫一眼能跟完一条 Run 的因果链 |

---

## 一、页面结构（会话视图 · 默认）

```
┌──────────────┬──────────────────────────────────────┬──────────────┐
│ SessionPanel │  chat__bar：折叠 | 智能体 v* | 状态   │ ContextPanel │
│ 会话/Agent   │  [执行记录] [折叠右栏]                  │ 项目/MCP/…   │
│              ├──────────────────────────────────────┤              │
│              │  ChatWidget：消息区（滚动）              │              │
│              │  · 气泡 / 富块（思考·工具·计划…）        │              │
│              ├──────────────────────────────────────┤              │
│              │  ContextChips（有选中才出现）            │              │
│              │  输入框 + ContextBar（恒显入口行）       │              │
└──────────────┴──────────────────────────────────────┴──────────────┘

叠加层：RunRecordPanel（ElDialog 820px）、AgentEditDialog、DirectoryPickerDialog、FileEditor（中栏顶）
```

入口：宿主菜单 **AI Agent** → 上布局。左/右栏可折叠；折叠态仅中栏 + 顶栏图标恢复。

---

## 二、组件清单与职责

| # | 面 | 主要源码 | 职责 |
| --- | --- | --- | --- |
| 1 | 聊天消息流 | `agent-core/components/MessageItem.vue`、`widget.css` | 用户/助手气泡、时间戳、状态矩阵、长文折叠 |
| 2 | 富内容块 | `agent-core/components/blocks/BlockRenderer.vue`、`blocks.css` | 思考、工具、计划、图表等；权限门禁 |
| 3 | 计划/步骤（流内） | `components/StepProgressCard.vue`（**宿主页未挂载**，见问题 P1） | 步骤 started→completed、卡住态 |
| 4 | 执行记录 | `components/RunRecordPanel.vue` | Run 列表/详情、目标模式、收据、规划过程、介入按钮 |
| 5 | Composer | `ChatWidget.vue` + `ContextBar.vue` + `ContextChips.vue` | 输入、上下文入口、权限胶囊 |
| 6 | 左栏 | `SessionPanel.vue` | 会话统计、历史、Agent 列表与切换 |
| 7 | 右栏 | `ContextPanel.vue` | 项目目录、打开文件、MCP/技能/记忆等摘要 |

---

## 三、用户旅程（主路径）

1. **进入页面** → 加载当前 Agent/会话；顶栏「就绪 / 思考中」点状态随 `sending` 变化（`AiAgentView.vue` chat__status）。
2. **发消息** → 输入框 Enter/发送；流式增量写入消息区；工具块默认**展开**（`BlockRenderer`：`open = block.open !== false && !block.collapsed`）。
3. **看工具** → 折叠行：`图标槽 · 工具调用 · 名 · 行为 · 状态`；点开 → 唯一带边框卡片，输入/输出 `--aiw-bg-soft` 区，`max-height: 170px` 内滚动（`blocks.css:158-165`）。
4. **计划驱动 Run** → 顶栏「执行记录」→ Dialog：左侧新建任务/列表，右侧步骤时间线 + **结果收据**（B2）+ 规划过程折叠区 + 继续/重开/跳过等（`RunRecordPanel.vue`）。
5. **选上下文** → 输入框**下方** ContextBar 点项目/Agent/模型/… → 弹出面板（`ContextBar.vue` 注释：入口必须在下方恒显）。
6. **改 Agent** → 左栏编辑 → `AgentEditDialog`；选项目目录 → `DirectoryPickerDialog`（**当前后端缺失，见 P5**）。

---

## 四、分组件说明（功能 · 交互 · 五态 · 视觉基准）

### 4.1 聊天消息流

- **功能**：展示轮次对话；助手支持纯 Markdown 或 `blocks[]` 富渲染。
- **交互**：用户消息右对齐、助手左对齐；**同一浅底深字**，靠位置区分（`widget.css:864-872`，禁止回到主色底白字）。助手长文 >240px 高 → 渐隐 +「展开全文」（`MessageItem.vue` COLLAPSE_MAX_HEIGHT=240；`widget.css:824-831`）。
- **五态**：`sending` / `streaming` / `done` / `failed` / `stopped` + 低置信度 / `denied`（`MessageItem.vue` 状态矩阵）；失败带原因 + 重试链。
- **视觉基准**：气泡 padding `9px 12px`（`--aiw-space-5`）；无头像助手列宽 `max-width: 92%`（`widget.css:848`）；时间戳 hover 显示（`widget.css:915-918`，色 `#9aa1ae` **待 token 化**）。

### 4.2 工具调用块

- **功能**：展示一次工具的名称、参数、结果、耗时、审批态。
- **交互（实装 · 2026-11-03 起）**：
  - **默认是否收起**：**是 —— 成功态 tool 默认折叠**（仅折叠行；I/O 在 `v-show="open"` 内）。`BlockRenderer.vue`：`status===running|error` 或 `open:true` 时默认展开；`collapsed:true` / `open:false` 强制收起。
  - **折叠行语义**：折叠态一行摘要；成功态不写「完成」；**执行中/失败**在折叠行仍可见状态词。
  - **与正文的先后顺序**：**按事件时间交错**。`agentBridge.ts` 自由循环：`onContent` 更新 `type:'text'` 块（`seg-text-live` / `seg-text-N`），`onToolCall` 前先 `finalizeTextSegment`；`MessageItem.vue` 按 `blocks[]` 顺序渲染，`text` 块用 `BlockRenderer` 内联气泡；含 `text` 块时不再重复渲染底部 `content` 气泡（legacy 无 text 块时仍为「工具块 + content」）。
  - **块数组内顺序**：`push` / 带 `id` 替换（`useChat.js`）；工具结果更新后 `onBlock` 再推一次以刷新 UI。
  - **计划模式**：`executionMode === 'plan'` 仍 **不走** 工具块时间线（步骤见 Run 面板）。
- **五态**：`running` 脉冲图标；`error` 名/状态变红（`blocks.css:135-137`）；`awaiting` 等待审批；`denied` 已拒绝。
- **视觉基准**：行首 **22×22 图标槽**（`blocks.css:14-20`）；展开卡片 **唯一**保留 1px 边框（2026-10-26 拍板）；I/O `max-height: 170px`（`blocks.css:165`）。
- **后续（§7 其余项）**：Run 状态条、Dialog→Drawer、写操作默认展开策略等仍待实现。

### 4.3 思考 / 计划 / 其他块

- **思考**：无边框，浅底；折叠单行摘要 `THINK_PEEK_MAX=88`（`BlockRenderer.vue:51`）；展开体 `max-height: 190px`（`blocks.css:102`）。
- **计划**：带边框列表，勾选/进行中/错误 mark（`blocks.css:169-186`，部分 mark 色为硬编码 hex，**待 token 化**）。
- **五态**：流式思考 `done:false` → 「思考中…」；空 args 显示 `aiw-tool__empty`。

### 4.4 执行记录面板

- **功能**：会话级 Run CRUD 视图 + 目标模式 + MAF 收据与 Verify 徽章。
- **交互**：Dialog 打开；左选 Run 右看详情；步骤列表 `flex-shrink: 0` 防压扁（`RunRecordPanel.vue` 注释 PILOT-077 FR-4）；规划过程默认折叠，可切原始 JSON。
- **五态**：列表空 → 引导 copy；详情 loading/error 保留旧数据 + 细提示；stuck 条警示色。
- **视觉基准**：Dialog `820px` / `top: 6vh` / 内容 `max-height: 70vh`（`RunRecordPanel.vue` 模板+样式）；步骤区多段 `max-height: 140–200px` + `overflow`（同类 FR-4 修复）。

### 4.5 Composer（输入 + 上下文）

- **功能**：草稿、发送、中止、Context 入口、权限模式胶囊。
- **交互**：Chips 在上（有值才显）；Bar 在下恒显（`ContextBar.vue`）；面板单开、可 flip 向上（空间 <320px）。
- **五态**：`sending` 禁发；`error` 条在 ChatWidget；未绑定模型 → 后端 404（宿主/插件 API，非本组件文案）。
- **视觉基准**：与参考图一致——**图标行与输入框同一视觉整体**（`ChatWidget.vue` 注释 ~1186）。

### 4.6 左栏 SessionPanel

- **功能**：会话切换、归档、Agent 列表、统计 token/工具次数。
- **交互**：点会话切换 `ChatWidget` seed；新建/归档/激活 Agent。
- **五态**：无 Agent → 引导新建；列表空 → EmptyHint。
- **视觉基准**：头栏 `min-height: 44px` 对齐中栏 `chat__bar`（`ContextPanel.vue:373` 同类注释）。

### 4.7 右栏 ContextPanel

- **功能**：项目根、文件树摘要、打开文件到中栏 `FileEditor`。
- **交互**：点文件 → `open-file`；选目录 → 目录弹层（**P5**）。
- **五态**：未选项目 → 空态 + 选目录按钮。
- **视觉基准**：与中栏同高滚动；分组标题 `flex-shrink: 0`。

---

## 五、五态总则（插件级）

| 状态 | 消息区 | 执行记录 | 侧栏 |
| --- | --- | --- | --- |
| 加载 | 历史灌入前可空白/骨架 | 列表 spinner | 统计延迟 |
| 空 | 欢迎/提示发首条 | 「暂无执行记录」 | 无项目/无 Agent 引导 |
| 出错 | 消息级 failed + 重试 | 保留列表 + 刷新 | API 失败细条 |
| 无权限 | 块级 gate / denied 文案 | 401 由宿主处理 | — |
| 正常 | 见第三节 | 见第三节 | 见第三节 |

---

## 六、当前 UI 问题盘点（2026-11-03 · 代码+规范对照）

> 严重程度：**P0** 阻断 / **P1** 体验债 / **P2** 一致性 / **P3** 美化

| ID | 级 | 问题 | 证据 | 影响 |
| --- | --- | --- | --- | --- |
| P1 | P1 | **流内步骤卡未接入** | `AiAgentView.vue:79-80` 注释「计划步骤卡暂不渲染」；`StepProgressCard.vue` 存在但未挂载 | 计划驱动执行时用户只能在 Dialog 里看步骤，**聊天主路径断裂** |
| P2 | P1 | **执行重心挤在 Modal** | `RunRecordPanel` 仅 ElDialog；composer 与 Run 并行任务切换成本高 | 与「主角=消息流、次动作降级」不一致——Run 介入应更贴流 |
| P3 | P2 | **裸 hex / 非 token 色残留** | 例：`blocks.css:132,157,175` `#9aa1ae`；`widget.css:859` `#fff` 气泡底；plan mark `#10b981` 等 | 亮/暗主题切换不一致；触犯 `DESIGN.md` 新增裸色守卫精神 |
| P4 | P2 | **信息密度双轨** | 流内工具「一行折叠」 vs Dialog 内「多段 140px 滚动区」 | 同一 Run 在 two places 形态差异大，学习成本高 |
| P5 | P0 | **选目录弹层调不存在 API** | `Plugins/AIAgent/TODO.md`：`DirectoryPickerDialog` → `/api/project/browse-directories` 无实现 | 右栏选目录路径 404，**功能假入口** |
| P6 | P2 | **三栏默认全展** | 窄屏无「自动收侧栏」策略（仅手动折叠） | 中栏消息列 `<78%/`92%` 仍偏窄 |
| P7 | P3 | **版本徽标挤标题** | `AiAgentView.vue:46-50` 标题后 v{{ version }} | 非设计稿元素，CRAP 上略抢「智能体」主标题 |
| P8 | P2 | **块级错误色不统一** | tool error 用 `#b42318`；消息 failed 用 `--aiw-text-danger` | 同一语义两套红 |
| ~~P9~~ | — | ~~工具默认展开 + 全文在上~~ | **2026-11-03 已修**（pilot `2026-11-03-aiagent-tool-timeline`） | — |

**已修复（基准写入 brief，防回归）**：工具 I/O 黑底（PILOT-077 FR-3）；**默认折叠 + 时间序 text/tool 交错**（2026-11-03）；flex 步骤压扁；左列图标槽 hover 不换列（2026-10-26）。

---

## 七、推荐重设计（单一方向 · 待实现）

> 目标：**不推翻三栏品类基线**，在 `DESIGN.md` 内做层级与动线重排。下列为设计规格；**本 brief 不自动改代码**。

### 7.1 原则

1. **一条对齐母线**：消息正文、折叠行图标槽、工具 I/O 左缘共用「母线 A」（当前约 42px 思考缩进 + 12px 内边距体系，统一命名 `--aiw-align-rail` 便于后续实现）。
2. **执行态贴流**：Run 的「当前步骤 + 卡住 + 继续」在消息区底部或 composer 上方做 **Run 状态条**（单行可展开）；Dialog 降为「历史与深度介入」。
3. **删减并列入口**：顶栏仅保留「执行记录（历史）」；进行中的 Run 不以 Dialog 为唯一反馈面。
4. **token 收敛一轮**：P3/P8 硬编码色 → `--aiw-*` / `--el-*` 映射表写进 `blocks.css` 注释区单表维护。

### 7.2 布局（目标态）

```
┌─ Session ─┬─ 消息流（主角）──────────────────┬─ Context ─┐
│ 可折叠     │ [可选 Run 条：步骤 2/5 · 继续]      │ 可折叠     │
│            │  …消息 + 块…                       │            │
│            │  chips?                            │            │
│            │  [ composer 一体块 ]                 │            │
└────────────┴────────────────────────────────────┴────────────┘
```

- **视口 <1280px**：默认收右栏；**<1024px**：默认收左栏，只留中栏 + 汉堡恢复（与宿主 `DESIGN.md` 断点对齐时再定数值）。
- **RunRecordPanel**：改为 **右侧 Drawer（840px）或全屏 Sheet**，与 Context 互斥展开，避免 Dialog 遮罩打断阅读流。

### 7.3 组件级改动意向（实现批 backlog）

| 项 | 动作 | 优先级 |
| --- | --- | --- |
| 流内步骤 | `AiAgentView` / `agentBridge` 重新挂载 `StepProgressCard`（或 agent-core 内等价块） | P1 |
| Run 条 | **已实装（2026-11-03）**：`ChatWidget` `.aiw-runstrip` + `AiAgentView` `listRuns` 5s 轮询与 `runCard` SSE 合并写 `agentConfig.runStrip`；详情经 `RunRecordPanel.selectRun` | P1 ✅ |
| 目录选择 | 实现 browse API 或临时隐藏 DirectoryPicker 入口 | P0 |
| 色票 | blocks/widget 硬编码 hex 扫一遍 | P2 |
| Dialog→Drawer | RunRecordPanel 容器替换，保留内部双栏 | P2 |

### 7.4 动效（轻量）

- 工具 `running`：保留现有 pulse（`blocks.css:134`），不新增全屏动画。
- Run 条出现/消失：150ms height + opacity（轻量**状态反馈**，不抢消息流注意力）。
- 禁止：蓝紫渐变、玻璃拟态过度（`DESIGN.md` 三禁）。

### 7.5 验收标准（重设计落地后）

- [ ] 主路径「发送 → 看见工具块 → 看见步骤进展」**无需打开 Dialog**。
- [ ] 抽查 10 条视觉声明仍满足第四节追溯（文件:行或 PILOT-077 实测）。
- [ ] `pnpm run design:check` 无新增裸色；插件 `pnpm run check` 绿。
- [ ] 走查：独立读截图（或 dev-stack 现场），主角/主动作 3 秒内可指认（对齐 `ui-ux-design` 走查清单）。

---

## 八、变更记录

| 日期 | 版本 | 变更 |
| --- | --- | --- |
| 2026-11-03 | （文档） | 首版：7 面说明 + 问题 P1–P8 + 单一重设计方向；零代码 |
| 2026-11-03 | UI | 工具默认折叠 + text/tool 交错；Run 条（`runStrip` + 轮询）实装；§7.3 更新 |

---

## 九、追溯索引（抽查用）

| 声明 | 出处 |
| --- | --- |
| 工具 I/O max-height 170 | `agent-core/styles/blocks.css:165` |
| 图标槽 22px 不换列 | `agent-core/styles/blocks.css:21-25` |
| 气泡统一浅底 | `agent-core/styles/widget.css:864-872` |
| 长文折叠 240px | `agent-core/components/MessageItem.vue:68` |
| ContextBar 在输入框下 | `agent-core/components/ContextBar.vue:2-8` |
| 执行记录 Dialog 820px | `components/RunRecordPanel.vue:6-9` |
| 步骤未挂载 | `AiAgentView.vue:79-80` |
| 目录 API 缺失 | `Plugins/AIAgent/TODO.md` |
