# mini-task：新聊天页面（AIAgent 迁移组件）展示优化三项

- 日期：2026-10-15（跨零点承接 10-14）
- 类型：mini-task（宿主页面 + 迁移组件展示层微调，非新功能立项）
- 关联 pilot：`2026-10-14-aiagent-widget-migration`（本任务是其展示层收尾）
- 影响面：`Plugins/AIAgent/web/src/agent-core/**`（组件资产）+ `agentBridge.ts` 配置；**零后端改动**

---

## 1. 三条用户指令（原文）

| # | 原文 | 诉求 |
| --- | --- | --- |
| ① | 如图，这个提示放在输入框上面即可，不要居中。以上是当前在改的新的聊天页面的展示优化 | `.aiw-error` 状态提示不要浮在消息区中部，改贴输入框上方 |
| ② | 如图 关于右侧锚点显示用户消息，已经点击了最下面的航线，消息也导航到最后一个用户输入了，但是锚点的显示仍然在第一条那里，再稍微滚动那么一点点才能亮起第二条 | 刻度条高亮错位 bug |
| ③ | 关于消息的展示，ai的回复不需要展示用户头像了，请根据 `D:\src\my-proj\ai-assistant-widget-1.13.0` 里面的需求文档，看看折叠需求，避免展示过多信息。如果没有就参考行业通用方案来做 | 助手消息去头像 + 长回复折叠 |

## 2. 组件源需求文档调研结论（`ai-assistant-widget-1.13.0/docs/REQUIREMENTS.md`）

| 条目 | 原文要点 | 对本次的含义 |
| --- | --- | --- |
| FR-205 消息气泡左右分列 | 「**AI 左侧带头像**，用户右侧主题色；hover 显示时间」 | 去头像是**宿主显式裁剪**，不是组件默认值。故做成 `config.showBotAvatar`（默认 `true`），组件独立交付时仍满足 FR-205 |
| §2.2 下期候选 | 「消息级操作栏…、**消息级耗时与折叠**」——**未实现** | 组件没有可复用的消息级折叠，需自实现 |
| 已实现折叠 | 仅 `thinking` / `tool` / `group`(FR-410) / `html` 预览四类，**全部依赖 `message.blocks`** | AIAgent 迁移后消息只有 `content`(markdown) + `toolEvents`，**无 blocks 数据源**，这些折叠在本页落不了地 |

→ 按用户授权「没有就参考行业通用方案来做」：折叠形态对齐 ChatGPT / Claude / Copilot 的通行做法（助手正文超阈值截断 + 渐隐 + 「展开全文」）。

## 3. 三项改动

### 3.1 状态提示条搬出消息流（原文①）

- **改前**：`error` / `retrying` / `reconnecting` / `fellBackToMock` 四条渲染在 `.aiw-body__inner` **消息流末尾** → 被当成「一条消息」，随历史滚动走，视觉上浮在消息区中部空白处。
- **改后**：四条统一挂进新增的 `.aiw-status` 容器，位置在 `.aiw-quick` 与 `.aiw-footer` 之间 —— **贴输入框上方、不随消息滚动**。
- 细节：`.aiw-status + .aiw-footer { border-top: 0 }` 消掉双分隔线（相邻兄弟选择器，不用 JS）；`.aiw-status` 内四条 `margin: 0`；`readOnly`（无输入框）场景仍能显示（状态条在 footer 之外）。
- 保留类名 `.aiw-error` 等不变 → e2e 既有选择器与单测不受影响。

### 3.2 刻度条高亮错位修复（原文②）

- **根因**：`computeActiveTick()` 判「消息已越过视口顶」的容差**硬编码 8px**，而 `jumpToMessage()` 把目标消息顶边放在 `scrollTop + 12`（`JUMP_OFFSET`）。**12 > 8** → 跳完目标自己被判「还没越过」→ 高亮回落到倒数第二条，得再手滚一点点才亮。
- **修法**：
  1. 常量 `TICK_TOLERANCE = JUMP_OFFSET + 8`，注释锁死「容差必须大于跳转偏移」这条约束 —— 防止以后单独改其一再次踩坑；
  2. `jumpToMessage()` 点完**即刻**把 `activeTick` 置为目标索引（smooth 动画全程连发 scroll，否则中途经过的消息会抢亮再闪回）；
  3. +400ms 兜底重算（对齐 `jumpToBottom` 的既有兜底：部分环境 smooth 根本不跑，没有 scroll 事件来校准）。

### 3.3 展示精简：去头像 + 长回复折叠（原文③）

- `config.js` 新增 `showBotAvatar`（**默认 `true`**，保 FR-205 契约）；`agentBridge.ts` 置 `false`（宿主裁剪）。
- `MessageItem.vue`：
  - 头像 `v-if="!isUser && config.showBotAvatar !== false"`；
  - 长回复折叠：阈值 `COLLAPSE_MAX_HEIGHT = 240px`，超阈值默认折叠，带「展开全文 / 收起」与 `aria-expanded`；
  - **三条约束**：① 只折助手（用户自己敲的字永不折叠）；② 流式中不折（`streaming: true`，正在生成的文字被截断比啰嗦更糟）；③ 折叠是**视觉裁剪**（`max-height` + mask），DOM 里仍是全文 —— 复制 / Ctrl+F / 读屏不受影响（直接截断 DOM 会把这些一并废掉）。
- `widget.css`：`.aiw-msg__fold` / `.is-collapsed`（240px + 底部渐隐 mask）/ `.aiw-msg__toggle`；头像去掉后气泡上限 78% → **92%**（`is-noavatar`），并把左上圆角补回 14px。

## 4. 边界（本次不做）

- 不接 `message.blocks` 富内容块（步骤卡 / 工具卡 / thinking 折叠）—— 数据源不存在，属 `2026-10-14-aiagent-widget-migration` 已登记的功能缺口 TODO。
- 不改 `toolEvents` 渲染（当前 ChatWidget 路径不渲染工具轨迹，属既有设计）。
- 时间戳保持常驻显示（FR-205 原文是 hover 显示，改动范围外，已记 TODO）。
- 组件默认行为只新增一个默认 `true` 的开关，**未改任何既有默认值**。
