# mini-task：智能体对话列随页面变宽，消息从顶部往下排

- date: 2026-11-25
- task-id: `aiagent-transcript-column`
- feature: 021
- type: 轻量 UI 缺陷（对话列宽度与纵向锚点）｜裁剪依据：规范 §4 轻量。业务改动面是 `assistant.css` + 一条布局守卫；需求真源句子同步改写，避免下一次按旧文案改回去。Intent/Spec/Plan/Task 合并本文件。05/06/07 在验证后单独产出。
- 闸门1 处理：用户原文即验收口径（内容要居中、宽度随页面、首次输入从上往下且位置稳定）。无接口、契约、依赖变更。可 `git checkout` 回滚。

## 一、Intent（为什么 / 做什么 / 到什么程度）

- **Problem**：智能体整页的对话列和输入框一样，最宽 720px，而且对话列没有水平居中。短对话用 `margin-top: auto` 贴在输入框上方；右栏打开时又改成从顶部排。同一条首条消息会时而在下、时而在上。
- **Why**：用户要求内容居中、宽度跟着页面走，首次输入后内容从上往下，位置稳定。
- **Expected Outcome**：有消息时，对话列铺满中间栏可用宽度，随窗口变宽变窄；消息从对话区顶部往下排，右栏开或关都不改锚点。输入框仍较窄并在栏内居中。空会话仍是纵向居中的空态加输入框。
- **Constraints**：只动助手页布局。不改会话数据、发送、工具卡片结构、用户气泡右对齐、助手通栏正文。不改宿主、不发布、不启停用户实例。
- **Success Criteria**：守卫断言对话列宽 100%、文件中不再出现把短对话顶到底部的上外边距；输入框规则仍是 `min(720px, 100%)` 且水平居中；空态仍纵向居中。走查时首条消息贴对话区上沿，加宽窗口后对话列变宽、输入框仍居中且更窄。

## 二、Spec

### Functional Requirements

- FR-1 整页 `.aa-col` 宽度为中间栏内容区的 100%，不再使用 `min(720px, 100%)`。列本身 `margin-inline: auto`，在中间栏内居中铺满。
- FR-2 对话列不再使用 `margin-top: auto`。有消息时一律从 `.aa-thread` 顶部往下排。删除只在右栏打开时改锚点的 `.aa-stage.has-rail .aa-thread .aa-col` 规则。
- FR-3 `.aa-dock-inner` 保持 `width: min(720px, 100%); margin: 0 auto`。输入框不跟着对话列拉满。
- FR-4 `.aa-stage.is-empty` 仍 `justify-content: center`，空态输入框留在纵向中间。`.aa-col` 设 `flex-shrink: 0`，避免短内容被滚动容器压扁。
- FR-5 助手面板模式原本就把列宽写成 100%。本批去掉贴底后，面板里的短对话也从顶部往下排，与整页一致。

### Interaction Design

| 交互点 | 触发 | 结果 | 状态 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 空会话 | 打开智能体页或新建会话 | 印章和输入框在中间栏纵向居中 | `is-empty` | 不涉及 | 空态文案不变 | 输入框仍最多 720px 且居中 |
| 首次发送 | 输入后发送 | 空态消失；第一条消息出现在对话区顶部，其后的回复接在下面 | 有消息 | 发送中的既有状态不变 | 不涉及 | 右栏开或关，首条仍在顶部 |
| 窗口变宽 | 拖动页面宽度 | 对话列跟着中间栏变宽；输入框仍在栏内居中，宽不超过 720px | 静态布局 | 不涉及 | 不涉及 | 中间栏窄于 720px 时，输入框缩到栏宽，对话列仍是栏宽 |
| 用户气泡 | 有用户消息 | 气泡仍在对话列内右对齐，最宽仍是 `min(520px, 86%)` | 不变 | 不涉及 | 不涉及 | 列变宽后气泡仍贴列的右缘，不改成居中气泡 |

五态：空态保持居中；加载、出错、无权限沿用现有 `aa-error` 与会话错误行，本批不改文案。正常态消息顶对齐。

### Boundary / Compatibility / Failure Modes

- 兼容：用户消息右对齐、助手通栏、工具行、审批卡结构不变。只改列的宽和纵向起点。
- 旧规则作废：短对话贴输入框、右栏打开才顶对齐。该句写在 `Plugins/AIAgent/docs/chat-page-redesign/` 的需求、测试计划、组件方案和原型里，本批改成与 FR-1/FR-2 相同的句子。
- Failure Modes：无既有问题类命中。回归风险是有人按旧 I09 把 `margin-top: auto` 加回去。守卫锁住样式表里的这两条。
- Unknown：运行中的宿主是否已经从本仓库 `Plugins/AIAgent/web/dist` 加载样式。走查时以实际打开的页面量测为准。

## 三、Plan（真实文件）

| 文件 | 动作 | 理由 |
| --- | --- | --- |
| `Plugins/AIAgent/web/src/assistant/assistant.layout.test.ts` | 新增守卫：对话列宽 100%、无贴底上外边距、输入框仍限宽居中、空态仍纵向居中 | 锁住本批不变量 |
| `Plugins/AIAgent/web/src/assistant/assistant.css` | `.aa-col` 改为铺满并取消贴底；删 `.has-rail` 锚点例外 | 用户看到的布局 |
| `Plugins/AIAgent/web/src/assistant/AgentAssistant.vue` | 舞台不再挂 `has-rail` | 该类只服务于已删除的锚点开关 |
| `Plugins/AIAgent/web/src/assistant/useAgentSession.ts` | 删除只给该开关用的 `railOpen` | 避免留下无人读取的状态 |
| `Plugins/AIAgent/docs/chat-page-redesign/01-requirements.md` | 第 4 节与 I09 改成顶对齐 + 列宽随中间栏 | 需求真源，避免改回去 |
| `Plugins/AIAgent/docs/chat-page-redesign/02-test-plan.md` | I09 预期同步 | 走查口径 |
| `Plugins/AIAgent/docs/chat-page-redesign/03-frontend-component-scheme.md` | 整页模式那句同步 | 组件方案口径 |
| `Plugins/AIAgent/docs/chat-page-redesign/prototype.html` | `.thread .col` 与 `.col` 同步 | 原型与实现一致 |

- Test Plan：先跑新守卫确认旧样式为红，再改样式确认转绿。插件前端 `pnpm test` 跑该文件。`pnpm run build` 产出 `web/dist/style.css`。走查量测对话列与输入框宽度、首条消息距对话区顶部的距离。
- 守卫等级：G1 样式契约。不新增问题类编号。

## 四、Task

- **Scope Allowed**：上表文件、本目录工件、当天日记、`TODO.md` 这一条。
- **Scope Forbidden**：发送逻辑、会话接口、宿主壳层、插件版本号、发布、启停宿主、e2e 基建。
- **Acceptance Criteria**：
  - [x] AC-1 守卫先红后绿：对话列宽 100%，样式表无 `margin-top: auto`，输入框仍 `min(720px, 100%)` 且 `margin: 0 auto`，空态仍 `justify-content: center`
  - [x] AC-2 插件前端 `pnpm test` 中该守卫通过，`pnpm run build` 成功，产物样式含新规则
  - [x] AC-3 浏览器走查：首条消息贴对话区顶部；加宽后对话列变宽，输入框仍居中且不跟列等宽
- **Verification Commands**：
  - `pnpm exec vitest run src/assistant/assistant.layout.test.ts`（`Plugins/AIAgent/web`）
  - `pnpm test` 与 `pnpm run build`（同目录）
  - 浏览器打开智能体页，量测 `.aa-col` 与 `.aa-dock-inner` 的宽度和首条消息的 `offsetTop`

## 五、适用问题类

- 2026-11-25 按对话布局检索 `docs/11-troubleshooting/problem-classes/README.md`：无命中。这是产品锚点改口（旧 I09 规定贴底），不是已归档问题类复发。不新开 PC。
