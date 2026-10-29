# Mini Task：AIAgent 消息区左列对齐（图标槽）+ 边框只留工具调用

> Task ID：2026-10-26-aiagent-block-align-border｜task_kind: refactor（UI 形态收口）｜日期：2026-10-26｜轻量（本文件合并 Intent / Spec / Plan / Task）
> 闸门1：用户输入1 原文「先记录并完善需求，再调整」= 明确要求「记录 → 调整」同一轮推进；两处口径歧义已由用户当面拍板（见 §Spec-Q）。
> 对象：`Plugins/AIAgent` 插件前端 `agent-core`（宿主侧无产品代码改动，仅在宿主 CT 画廊补一条视觉守卫）。

## Repository Understanding

| 事实 | 位置（改前实测） |
| --- | --- |
| 消息容器：`.aiw-msg__col` → `.aiw-blocks`（富内容块）+ `.aiw-msg__bubble`（正文） | `MessageItem.vue` |
| 三种可折叠行的行首结构 | `BlockRenderer.vue`：思考 `chev+spark(7px)+label`／工具 `chev+icon(22px)+label+名+行为`／分组 `chev+badge(20px)+title` |
| 折叠箭头 | 独立元素 `.aiw-chev`（固定 15px 宽、`opacity:0`，hover/focus 提到 0.6，展开 0.55）→ `blocks.css:23-42` |
| 正文气泡 | `.aiw-msg__bubble`：`border:1px solid var(--aiw-line)` + `box-shadow` + `padding:9px 13px`（`widget.css:828-838`） |
| 其它带框的内容块 | 思考 `.aiw-think` 虚线框；步骤分组 `.aiw-grp` 实线框；图表/表格/KPI/引用/HTML/代码块/审批各有框 |
| 测试体系 | 插件单测 vitest（`BlockRenderer.test.ts`、`widget-contract.test.ts`）；宿主 CT 像素回归（`src/components/AIAgentMessage.ct.spec.ts` + 画廊 `ct/`） |

## Intent

用户在真实会话里看到两处不一致：①悬浮时行首**多出一个**下拉箭头，图标整体右移，「左边看起来不齐」；②正文也被套了边框，一屏里除了工具调用还有一堆框，读起来像表单。

期望：左列图标永远在同一个位置（悬浮时**它自己**变成箭头），且**只有工具调用**带边框。

## Spec

- **FR-1 图标槽（核心）**：思考 / 工具调用 / 步骤分组三种行的行首统一为一个 `.aiw-ico` 槽（固定 22×22）。槽内两层：块类型图标（glyph）+ 折叠箭头（chev），二者**同位叠放**、按状态交叉淡入淡出。
  - 默认：显示 glyph（工具图标 / 思考脉冲点 / 步骤计数）
  - 悬浮 · 键盘聚焦 · 已展开：同一位置换成箭头（展开态箭头旋转 180° 常驻，提示可收回）
  - 结构上**不再有独立的箭头元素**参与 flex 排版 ⇒ 悬浮不产生任何横向位移。
- **FR-2 左缘对齐**：三种行首的左右内边距与 `.aiw-msg__bubble` 的左右内边距取同一值（`--aiw-space-5` = 12px），使图标左缘、正文左缘落在同一条竖线上；`.aiw-think__body` 左内边距对齐到行首文字列（12 + 22 + 8 = 42px）。
- **FR-3 边框收口**：正文气泡去边框 + 去阴影；思考块去虚线框；步骤分组去实线框。**保留**：工具调用展开体 `.aiw-tool__body`（唯一保留框的内容块）、危险操作审批（风险色语义）、以及数据卡片类（图表 / 表格 / KPI / 引用 / HTML 预览 / 代码块）。折叠态工具行仍无框（用户拍板：折叠无框、展开有框）。
- **FR-4 触摸屏补偿**：`@media (hover:none)` 下箭头常驻（glyph 让位），保持「这里可展开」的可发现性 —— 与改前行为一致，不引入新回归。
- **非目标**：不改后端、不改消息数据契约、不改主题色板、不重排富内容块顺序、不动宿主产品组件。

### Spec-Q（用户 2026-10-26 拍板）

| 问题 | 结论 |
| --- | --- |
| 「其他内容无边框」的范围 | **只去过程/正文的框**；数据卡片（图表/表格/KPI/引用/HTML/代码块）与危险审批保留边框 |
| 工具行边框形态 | **折叠无框、展开有框**（维持与参考形态一致，长对话不变成一堆盒子） |

### Interaction Design（交互设计）

| 序号 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| I-1 | 鼠标移入任意折叠行 | 行内图标原位淡出、箭头原位淡入；行底色变浅 | 默认 / hover | 160ms 交叉淡入（`--aiw-dur-fast`） | 无 | 指针位置不因结构变化而漂移 |
| I-2 | 点击折叠行 | 内容展开，箭头旋转 180° 常驻 | 折叠 / 展开 | 行底色保留 | 展开后无可读输入输出时给「这次调用没有留下可展开的输入或输出」 | 已展开时图标不再显示（避免与箭头抢同一格） |
| I-3 | 键盘 Tab 聚焦行 | 与 I-1 同效（focus-visible） | 焦点态 | 焦点环（通用兜底） | 无 | 不吞焦点环 |
| I-4 | 触摸屏（无 hover） | 箭头常驻（glyph 让位） | 静态 | 无 | 无 | 与改前一致 |
| I-5 | 阅读正文 | 正文无边框无阴影，靠底色与 12px 内边距与其它块对齐 | 静态 | 无 | 无 | 折叠渐隐（mask）仍生效 |

### Acceptance Criteria

- AC1 三种可折叠行的 `button` 首个元素都是 `.aiw-ico`，且 `.aiw-ico` 内同时存在 glyph 与 chev；模板里不再出现独立参与排版的 `.aiw-chev`。
- AC2 同一消息内两条工具行的图标左缘 x 完全相等；对其中一条 `hover()` 前后 `.aiw-ico` 的 boundingBox 不变（不产生位移）。
- AC3 hover 后 glyph `opacity: 0`、chev `opacity > 0`；展开（`is-open`）后 chev 常驻且 `svg` 旋转 180°。
- AC4 正文气泡 computed `border-width: 0px`、`box-shadow: none`；`.aiw-think` / `.aiw-grp` 无边框；`.aiw-tool__body` 边框仍在。
- AC5 插件单测 + 宿主 `pnpm run check/test/test:ct/design:check` 全绿；CT 基线更新后读图确认（左列齐、只有工具调用带框）。

## Plan

1. `BlockRenderer.vue`：思考 / 工具 / 分组三处行首改为 `.aiw-ico` 槽（glyph + chev），删除独立 `.aiw-chev`；分组计数徽标在槽内改为 22×22。
2. `blocks.css`：删 `.aiw-chev` 规则组，新增 `.aiw-ico` / `.aiw-ico__glyph` / `.aiw-ico__chev`（含 hover / focus-visible / is-open / `hover:none` / 动效 token）；行首内边距 11px → `var(--aiw-space-5)`；`.aiw-think` 去虚线框、`.aiw-grp` 去实线框（`--running` 靠徽标脉冲表达）；`.aiw-think__body` 左内边距 25px → 42px。
3. `widget.css`：`.aiw-msg__bubble` 去 `border` + `box-shadow`，左右内边距 13px → `var(--aiw-space-5)`。
4. `BlockRenderer.test.ts`：替换「箭头放最左」用例为「图标槽」用例（首元素/唯一 chev/is-open/无 `.aiw-chev`）；补 AC4 的 CSS 断言（读 blocks.css 与 widget.css 文本，防「把框加回来」）。
5. `AIAgentMessage.story.ts`：新增 `AIAgent/MessagesToolRows` 变体（同一消息两条工具行，一条已展开），并修正 `MessageToolAndText` 里过期的「完成」状态断言对应数据。
6. `AIAgentMessage.ct.spec.ts`：修过期断言（成功态不再显示状态词）；新增 AC2/AC3 几何与状态守卫 + 像素基线。

## Files To Change (planned)

- `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.vue`
- `Plugins/AIAgent/web/src/agent-core/styles/blocks.css`
- `Plugins/AIAgent/web/src/agent-core/styles/widget.css`
- `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.test.ts`
- `ForgeSelf.Web/src/components/AIAgentMessage.story.ts`
- `ForgeSelf.Web/src/components/AIAgentMessage.ct.spec.ts`
- 本工件目录（mini-task / 05 / 06 / 07）+ 当天日记 / TODO

## Allowed / Forbidden

- Allowed：上述 6 个源文件（全部在 AIAgent 插件前端与宿主 CT 测试内）+ 工件与日记。
- Forbidden：改后端 / 数据库 / 鉴权；改消息数据契约（`types.d.ts`、`toolEvents.ts`、`agentBridge.ts`）；改其它插件或宿主产品页面；改主题 token 值；git 提交（闸门3 待用户）；发布与重启宿主（未获授权）。

## Verification Plan

```powershell
cd Plugins/AIAgent/web; pnpm run build; npx vitest run            # 插件前端构建 + 单测
cd ForgeSelf.Web; pnpm run check; pnpm run test                   # 宿主类型/lint/规范守卫 + vitest
$env:CT_FRESH='1'; pnpm run test:ct:update; pnpm run test:ct      # 基线更新 + 回归（换 5193，避开画廊 dev server）
pnpm run design:check                                             # 设计 token 棘轮
pwsh scripts/dev-stack.ps1                                        # 起源码本地前后端走查 + 截图读图
```

判据：以上全绿；CT 截图读图确认左列图标同一竖线、悬浮无位移、仅工具调用带框。

## Rollback

回退上述 6 个文件即可；无语义/数据/依赖副作用（纯样式与模板结构）。

## 实施偏差（实施后回填，如实）

| 计划项 | 实际 | 原因 |
| --- | --- | --- |
| Plan 4：CSS 断言写进 `BlockRenderer.test.ts` | 写进 `widget-contract.test.ts`（新增「边框归属」describe，并读 `blocks.css`） | 后者本就是「读 CSS 文本、锁规范」的契约守卫；前者是 jsdom 结构测试，放进去职责错位 |
| Files To Change 6 个文件 | 实际 9 个 | 新增：`widget-contract.test.ts`（AC4 守卫）、`AIAgentMessageStoryHost.vue`（CT 宿主底色改 `--aiw-bg-soft`，否则去框后基线看不出气泡边界）、`e2e/plugins/ai-agent/ai-agent.live.spec.ts`（外批 lint 红灯，1 行 `let sawRunning: boolean`，不修 `pnpm run check` 过不了） |
| Plan 6：CT 像素基线 | 已重锁 | 上一会话遗留的基线是旧形态（箭头在右、有框），本批形态变了必须重锁，否则相位回归第一轮就红 |
| Files To Change 外附带入 1 个清单文件 | `Plugins/AIAgent/plugin.json` 1.9.5 → 1.9.6（Description 追加 v1.9.6 形态说明） | 本批改了 `web/`，不 bump 就会重现「包内版本徽标与内容对不上」的 P2；本批只能解这一半，机制层守卫（发布脚本拒发）仍留 TODO |

未做（如实）：

- 第④⑤步（本地目录发布 + `:51888` 只读复验）：用户 2026-10-26 拍板「**先不发布**」，本批不产出新 zip、不动运行实例；任务状态记 PARTIALLY_COMPLETED，挂账 TODO（“AIAgent 消息区批第④⑤步待发起”）。
- 第③步起源码 dev-stack 手工走查：以**源码构建的冒烟 e2e + 现场截图**作等价证据；dev-stack 数据根隔离为空且模型密钥 401，开页面看不到本批关键形态。
- 真实会话工具卡的一手截图：本机模型密钥 401，`流式聊天端点` 用例虽然 passed，但流里回的是 `401 (Unauthorized)`，拿不到工具卡数据；工具行对齐证据改为 CT 几何断言（真浏览器 + 真 CSS）。

