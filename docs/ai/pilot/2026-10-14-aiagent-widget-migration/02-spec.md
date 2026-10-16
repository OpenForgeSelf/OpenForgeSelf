# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：2026-10-14-aiagent-widget-migration

## Functional Requirements

### FR-1 agent-core 组件迁入（迁移保真，改造最小）

- 1.1 将 `D:\src\my-proj\ai-assistant-widget-1.13.0\src\` 的下列内容**复制**到 `Plugins/AIAgent/web/src/agent-core/`：
  - `components/`（ChatWidget.vue、MessageItem.vue、blocks/BlockBoundary.vue、blocks/BlockRenderer.vue）
  - `composables/`（useChat.js、useRemote.js）
  - `utils/`（auth.js、audit.js、charts.js、export.js、markdown.js、pairing.js、resume.js、schema.js、sessions.js）
  - `styles/`（widget.css、blocks.css）
  - `config.js`、`env.js`
  - **不迁**：`embed.js`（Shadow DOM 挂载入口，插件场景不走）、`main.js`（dev 预览）、`dist/`。
- 1.2 复制内容保留原文件（含修复：`useChat.js` return 已含 `exportSession, exportAllSessions`）；组件名保持原样（`ChatWidget.vue` 等），以便与上游 diff 对照。
- 1.3 agent-core 内**禁止** `import` 插件 `http.ts`/`types.ts` 或宿主 `@/` 别名；组件只通过 props/config 与外部交互（可测试、可复用）。

### FR-2 config 能力扩展（组件内小改，向后兼容）

- 2.1 `config.js` 新增配置键（默认值保证旧行为不变）：
  - `showHeader: true` —— `false` 时隐藏面板顶部标题栏（用户要求的「隐藏顶部在线客服标题栏」）。
  - `transport: null` —— 非空时 `useChat.send()` 改走 `transport(ctx)`，替代默认 OpenAI fetch 路径。
  - `sessionId: ''` —— 外部会话标识透传（AIAgent 会话 id），transport 上下文携带。
- 2.2 `ChatWidget.vue`：
  - header 加 `v-if="config.showHeader !== false"`（连同 aria 关联标题一起隐藏，避免读屏念不存在的标题）。
  - inline 模式（`config.inline`）隐藏 launcher 启动按钮（inline 下 `isOpen` 恒 true，launcher 为无效按钮；用 `v-if="!config.inline"` 包住 launcher，同时保证 `close()` 的 inline 拦截逻辑不变）。
  - 面板容器类保留 `aiw-root/aiw-panel` 等原样式体系（CSS 变量 `--aiw-*` 主题不变），inline 时由外部容器定高（组件自身 `height:100%`）。
- 2.3 `useChat.js`：
  - `send()` 分支：`if (config.transport) { await config.transport(ctx) } else { 原 requestStream/retry/reconnect 路径 }`。
  - transport ctx 契约：`{ text, sessionId, model, systemPrompt, onDelta(chunk), onDone(info), onError(err), onUsage(usage), signal }`。
  - 组件侧 `sending/streamingText/error/retrying/reconnecting` 状态语义不变（transport 异常抛给 `send` 的既有 catch 链）。
  - 新增 exposed `hydrate(list)`：把外部历史消息数组灌入 `messages`（重置 streaming 状态与未读计数），供 AIAgent 会话切换用。

### FR-3 AIAgent 页面集成（替换聊天区）

- 3.1 新增 `Plugins/AIAgent/web/src/agentBridge.ts`：构造 agent-core config + 实现 AIAgent transport。
  - config：`{ showHeader: false, inline: true, transport: aiAgentTransport, sessionId, persist: false, darkMode: false, storageKey: 'forgeself-agent-msg', title: 'AI Agent', subtitle: '就绪', placeholder: '输入消息，Enter 发送…', quickReplies: [], footerNote: '' }`（组件头部隐藏，欢迎语/快捷回复/页脚本批关闭）。
  - `aiAgentTransport(ctx)`：按当前激活 Agent 的 `executionMode` 分流：
    - `free` → `streamAgentChat({ sessionId, message, chatModelId, agentId, enabledToolNames, skillIds }, { onContent→onDelta, onUsage→onUsage, onDone→onDone+onTurnComplete, onError→onError }, ctx.signal)`。
    - `plan` → `streamAgentRun({ sessionId, agentId, taskInput, chatModelId }, buildRunHandlers 精简版, ctx.signal)`，其中 `onContent` 增量喂 `onDelta`、`onDone(content)` 喂 `onDelta` 收尾 + `onDone`（步骤卡 UI 本批不显示，记 TODO）。
  - 工具事件（tool_call/tool_result）本批**不渲染**（组件无工具卡片），transport 内忽略；usage 回调上抛（token 统计）。
  - 消息写入/会话存储由 AIAgent 后端（ISessionStore）负责，组件不落本地存储（persist:false）。
- 3.2 `AiAgentView.vue` 中栏改造：
  - 顶部工具条从 `ChatPanel` 提取到 `AiAgentView` 模板（保留类名 `.chat__bar` / `.chat__version` / `.chat__bar-right`、折叠左栏按钮、标题「AI Agent」+版本徽标、状态点、执行记录按钮、折叠右栏按钮），`StepProgressCard` 从工具条下移除（记 TODO）。
  - 工具条下方渲染 `agent-core` 组件（inline config），占满剩余高度（flex-1 + min-h-0 链保持）。
  - 会话切换 `selectSession(id)`：`loadHistory()` 后调组件 `hydrate(messages)`。
  - `sendMessage` 职责移交：组件内部 `send()` 走 transport；`AiAgentView` 保留 `sending` 透传（组件 exposed `isSending()`）与停止（组件 exposed `stop()`/`abort()`）。
  - `ChatPanel.vue` **保留文件、不再被 `AiAgentView` 引用**（作为后续功能迁移蓝本；移除引用后其 import 的 types/http 仍在，不产生编译破坏）。
  - 版本徽标数据源不变（`loadMeta` → `/api/plugin`）。
- 3.3 `index.ts` 不变（仍导出 `AiAgentView`）；本批 agent-core 模板不使用 EP 组件（保持自含），无需新增 EP 样式 import。

### FR-4 e2e 冒烟更新（铁律 24：每插件 1 条主链路）

- 4.1 `ai-agent.spec.ts`：
  - 用例 1（远程加载+三栏+版本徽标+诊断）：保留，补充断言新组件消息区（`.aiw-panel` inline 可见）与输入框（`.aiw-input textarea`）可见。
  - 用例 3（composer 工作台交互）：**替换**为「新组件聊天区主链路冒烟」——输入框输入 → 发送 → 断言无致命报错 + 无服务端 5xx + 用户消息气泡出现（`.aiw-msg--user`；真实后端无 provider 时返回 error 事件 → 组件错误条可见，不断言回复内容）。
  - 用例 6（计划驱动）：`.chat__textarea` → `.aiw-input textarea`；`.spc` 步骤卡断言**移除**（记 TODO 步骤卡迁入组件）；保留「runs POST 发出 + 执行记录面板 Run 可见」断言（`.chat__bar-right` 仍在工具条）。
  - 用例 2/4/5/7 不变（不依赖中栏聊天结构或直接 fetch 端点）。
- 4.2 冒烟主链路以用例 1 + 用例 3 改造后的发送链路为准，供 `--grep 冒烟` 快速回归。

## Input

- 迁移源：`D:\src\my-proj\ai-assistant-widget-1.13.0\src\`（组件源码 + 样式 + 配置 + utils；**已验证** gate 225/225 + mock 17/17）。
- AIAgent 现状：`Plugins/AIAgent/web/src/`（AiAgentView/ChatPanel/http.ts/types.ts）+ 后端聊天契约（`/api/ai-agent/chat/stream`、`/api/ai-agent/runs`、`/api/ai-agent/chat/sessions`、`/api/agents`）。
- 用户输入：迁移组件替换聊天区（澄清：重点组件定位 + 内嵌/悬浮双形态 + 属性控制 + 功能缺口先记录迭代补上）。

## Output

- `Plugins/AIAgent/web/src/agent-core/`（组件源码 + `types.d.ts` 类型声明 + `agent-core.test.ts` 单测）。
- `Plugins/AIAgent/web/src/agentBridge.ts`（config + transport 适配）。
- `Plugins/AIAgent/web/src/AiAgentView.vue`（中栏替换为组件 + 工具条保留）。
- `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts`（冒烟断言面更新）。
- `Plugins/AIAgent/plugin.json` Version 1.7.4 → 1.8.0；`web/package.json` 1.2.0 → 1.3.0。
- TODO.md 功能缺口清单。

## Business Rules

1. **组件自治**：agent-core 不 import 插件/宿主模块；一切外部依赖经 config/transport 注入。
2. **对话唯一通道**：聊天消息发送/历史加载只走 AIAgent 后端（`http.ts`）；组件内部 persist 关闭、history 关闭，杜绝与左栏 SessionPanel 双轨。
3. **版本徽标铁律**：插件根视图标题旁必须显示版本（保留在 AiAgentView 工具条）。
4. **plan/free 分流**：按 `AgentDefinition.executionMode` 分流发送通道（既有 `sendMessage` 语义，迁入 transport）。
5. **不删不改**：ChatPanel.vue 保留（不引用）、后端/数据模型不动、用户会话数据不动。
6. **构建产物契约**：入口 `web/dist/index.js`（导出 `AiAgentView`）+ `style.css`；external 清单不变。

## Boundary Conditions

- **本批不做**（记 TODO.md，标注「功能迁入组件后实现/恢复」）：
  - 目录/工具/技能/Agent/模型五个 composer 选择器（ChatPanel 现功能）→ 模型选择入口本批消失，`selectedModelId` 取 loadMeta 默认值。
  - 计划驱动步骤卡（`.spc`）、工具调用卡片渲染（`.chat__tool-card`）→ plan 流式文本照常展示。
  - 组件侧会话历史侧栏/导出/多标签同步/权限审计（role/roles/hideUnauthorized）/遥控（remote）/断点续传 UI/快捷回复/欢迎语/页脚。
  - 组件 Shadow DOM / launcher 悬浮球（inline 形态隐藏；悬浮客服形态后续场景再启用）。
  - agent-core 渐进 TS 化、与上游 widget 后续版本同步。
- **e2e 边界**：冒烟仅 1 条主链路（铁律 24）；原 composer 交互用例删除（功能迁入组件后恢复，记录 TODO）。

## Error Handling

1. **transport 抛错**：走组件既有 catch 链 → `error` 条 + `canRetry` 重试（transport 内部不重试；AIAgent 后端流中断的续传/重试记 TODO，本批 transport 抛错后组件提示错误、保留已生成内容语义由组件端 `streamingText` 保证）。
2. **后端 SSE error 事件**（`/api/ai-agent/chat/stream` 结构化 error）：transport 转 `onError` → 组件错误条展示（沿用现有 `.aiw-error`）。
3. **用户中止**（AbortError）：transport 捕获后不当作错误上抛（组件 `sending` 复位、保留已生成内容）。
4. **历史加载失败**：`loadHistory()` 现有 catch → 空消息 + 组件欢迎态（本批 quickReplies 关，空态即消息区空白 + 输入框可见）。
5. **组件 header 隐藏时无障碍**：隐藏 header 的同时隐藏其 aria 标签（FR-2.2），面板仍保留 `role="dialog"` + 面板级 aria-label。

## Compatibility

- **运行时**：仅 vue 依赖；插件 external 清单不变；宿主 import map 解析同一 vue 实例。
- **构建**：JS 源码由 vite/esbuild 转译（插件 web 无 vue-tsc 门禁 —— 现状如此，见 00 §测试方式）；新增 `types.d.ts` 供 TS 侧（agentBridge.ts/AiAgentView.vue）获得类型。
- **单测**：宿主 vitest include `../Plugins/*/web/src/**/*.test.{ts,tsx}` —— agent-core 单测必须写 `.test.ts`（JS 源码可被 TS 测试 import，esbuild 转译不查类型）。
- **样式**：去掉 Shadow DOM 后 `.aiw-*` 类名与宿主/插件样式冲突面小（前缀全局唯一）；保留 CSS 变量主题（`--aiw-*`），不引入裸色值（`design:check` 守卫范围在宿主 ForgeSelf.Web，插件产物不触发，但仍遵循 token 精神）。
- **e2e**：更新 ai-agent.spec.ts 断言面（FR-4）；`menu-route-consistency.spec.ts` 涉及路由未改名，无需同步（铁律 19③）。

## Non-functional Requirements

- 性能：组件单文件迁移，插件产物体积增量预估 ≤ 80KB gzip（原 dist 约 199KB raw，本批仅迁核心路径）；消息列表滚动沿用组件既有实现（无自研滚动条依赖）。
- 可访问性：组件自带 aria（role=log/aria-live、焦点管理、Esc 关闭）随迁保留；header 隐藏时 aria 同步隐藏。
- 可维护性：agent-core 与上游源目录保持文件级对应（便于 diff）；`types.d.ts` 集中声明 config/transport/暴露 API。
- 安全性：不新增任何密钥/令牌处理路径（AIAgent 请求沿用 `http.ts` 既有 token 逻辑）；组件不落 localStorage（persist:false）。

## Interaction Design（交互设计 · 必写节 · 2026-10-08 用户立）

> 审查用 `.agents/skills/ui-ux-design` 技能（CRAP + 配色 + 字体 + 交互规格模板）。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 中栏顶部工具条（AiAgentView 层） | 点击折叠左栏/右栏按钮 | 对应栏显隐切换，按钮图标随状态切换（Fold/Expand） | 展开/折叠 | 即时切换 | N/A | 三栏任一折叠时中栏撑满 |
| 版本徽标 | 加载完成 | 标题旁显示 `vX.Y.Z`（灰底圆角小徽标，不抢标题视觉） | 加载中=隐藏 | 显示 | 版本拉取失败=静默隐藏（现有降级） | 与 plugin.json Version 一致 |
| 状态点 | 发送/流式期间 | 绿点变「思考中」，结束后回「就绪」 | 就绪/思考中 | 点色 + 文案 | N/A | 无 |
| 执行记录入口 | 点击工具条按钮 | 打开 `RunRecordPanel`（既有面板） | 打开/关闭 | 面板滑出 | 无 Run=面板空态（既有） | 计划模式外也可查看历史 Run |
| 消息区（agent-core） | 会话加载 | 历史消息按时间渲染（用户右/助手左，Markdown 渲染） | 加载中/有消息/空 | 滚动条 + 自动贴底 | 空态=空白消息区 + 输入框可交互 | 长回复滚动不破版（flex min-h-0 链） |
| 输入框 | 聚焦 | 可输入，Enter 发送（Shift+Enter 换行），自动增高（≤108px） | 可输入/发送中禁用 | 输入即增高 | 禁用=发送中 textarea disabled（可见原因=发送中） | 中文输入法组合键不误发（isComposing 判定） |
| 发送/停止按钮 | 点击发送 | 发送中按钮变 ■ 停止；点击停止中断流式、保留已生成内容 | 待发送（有文本可点）/发送中/停止 | 按钮形态切换 | 无文本=禁用 | 发送中 textarea 禁用防重复 |
| 发送消息 | 输入后发送 | 用户气泡即时出现；后端流式回复逐 token 追加（打字指示…→正文）；完成后刷新左栏会话列表 | 发送中/完成/出错 | 流式打字机 + 状态点 | 无 provider 时后端 error 事件 → 错误条（.aiw-error） | Abort 保留已生成内容；transport 抛错 → 错误条 + 可重试 |
| 会话切换（左栏 SessionPanel） | 点击历史会话 | 中栏组件 hydrate 新会话历史消息；左栏高亮当前会话 | 加载中/已切换 | 消息区刷新 | 新会话无历史=空消息区 | 切换期间 sending 禁止（既有发送锁） |
| 错误提示 | 请求失败 | 消息区下方错误条（红字），可重试（若可重试） | 无错/有错 | 错误条出现 | N/A | 用户中止不显示错误 |

### 交互验收标准（并入 Acceptance Criteria，逐条可测）

- 每个交互点至少一条 AC：断言「触发后页面出现什么」，锚在真实 DOM/服务返回（见 Acceptance Criteria）。
- 空态分级：历史加载失败/无 provider 错误/空会话均有可测文案或状态（组件 error 条 / 空消息区）。
- 禁用态原因可见：发送中 textarea disabled + 按钮变停止图标（可见语义）。
- 破坏性操作：本批无新增破坏性操作（会话归档仍走左栏既有二次确认，不在本批改动范围）。

### 走查符合性（DoD 绑定）

- 走查（维护闭环 ③）按 `ui-ux-design`「走查 UI 符合性清单」逐项核对：组件消息气泡层级（用户右/助手左）、对齐（头像/气泡/时间）、间距、配色（浅色主题下 `--aiw-*` 与宿主 `--el-*` 不冲突）、字体继承；长回复滚动、窄屏不破版、发送/停止/错误/空态逐项点验 + 截图读图（存 `ForgeSelf.Web/screenshots/dev/ai-agent/`）。
- e2e 覆盖主链路（渲染 + 发送 + 无致命报错 + 无 5xx）；纯视觉用截图读图对照设计基准。

## Acceptance Criteria

- [ ] AC-1：`Plugins/AIAgent/web/src/agent-core/` 存在，含 `ChatWidget.vue/MessageItem.vue/useChat.js/config.js/styles/` 等迁移文件；`grep` 确认 agent-core 内无 `import '@/`、无 `import '../http'`、无 `import '../types'`（组件零插件依赖）。
- [ ] AC-2：`vite build` 产物 `web/dist/index.js` + `style.css` 生成（插件构建门禁绿）；产物 `index.js` 仍导出 `AiAgentView`。
- [ ] AC-3：宿主 `pnpm run test` 全绿（含 agent-core 新增 `.test.ts`：transport 契约分支、hydrate、showHeader/inline 配置分支）。
- [ ] AC-4：`pnpm run check` 与 `pnpm run design:check` 绿。
- [ ] AC-5：dev-stack 本地验收：`/ai-agent` 页面三栏可见；中栏为新组件聊天区（inline、无组件标题栏、无 launcher）；工具条版本徽标 = `v1.8.0`；输入「你好」发送 → 状态点「思考中」→ 回复流式出现（或后端 error 事件 → 错误条）；左栏会话列表在回复完成后刷新；切换会话 → 中栏 hydrate 对应历史。截图存 `ForgeSelf.Web/screenshots/dev/ai-agent/`。
- [ ] AC-6：冒烟 e2e（ai-agent.spec.ts 更新后）通过：渲染三栏 + 版本徽标 + `.aiw-panel`/`.aiw-input textarea` 可见 + 发送主链路无致命报错/无 5xx；原 composer 交互用例已按 FR-4 替换。
- [ ] AC-7：TODO.md 已登记功能缺口清单（composer 五选择器/步骤卡/工具卡片/组件侧历史导出/多标签/权限/遥控/TS 化/与上游同步），并注明恢复条件。
- [ ] AC-8：plugin.json `Version: 1.8.0`、web/package.json `1.3.0`；发布产物校验（zip + SHA256SUMS + L1/L2/L3 + `-Sign`）通过后交付用户更新。
- [ ] AC-9：走查（运行实例只读复验）完成：版本徽标核对、主链路中间态、无 5xx；文档（docs/02-features 或插件文档）与当天工作日记已更新。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| e2e 环境无 AI provider 时，`/api/ai-agent/chat/stream` 是否稳定返回结构化 error 事件（而非挂起/5xx） | 冒烟 e2e 发送用例的终态断言 | 保守假设：按既有 e2e 用例 2 实证（200 + SSE + "type" 字段存在），发送用例只断言「无致命报错 + 无 5xx + 用户气泡出现」，不断言回复内容；Implement 时实测修正 |
| `streamAgentChat` 的 SSE 事件中 usage 事件名/字段是否与本批 transport 解析一致 | token 统计显示 | 保守假设：沿用 AiAgentView 现有 onUsage 处理（http.ts 既有 AgentUsage 契约），transport 原样转发 |
| agent-core 迁入后与插件 style.css 的 CSS 合并体积/优先级 | 构建产物与视觉 | 保守假设：插件 vite cssCodeSplit:false 已合并全部 CSS；`--aiw-*` 前缀不冲突；Implement 后读构建产物与截图核对 |
| 组件 hydrate 与流式进行中并发调用（会话切换竞态） | 消息区内容错乱 | 保守假设：AiAgentView 发送锁（sending）在切换时生效（既有逻辑），hydrate 先 abort() 再灌入；Implement 时补单测 |
