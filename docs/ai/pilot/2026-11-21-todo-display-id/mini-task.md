# PILOT-085｜待办组件展示自身编号（#93 对不上条目）——渲染面缺 numeric id（mini-task）

> 任务协调人：MainAgent ｜ 裁剪裁定：**轻量**（≤3 代码文件：TodoView.vue + TaskDetail.vue + 插件冒烟 e2e 断言；Intent/Spec/Plan/Task 合并本文件，Evidence/Review 单独产出 05/06/07）
> 状态：**闸门1 已按 §8.5 自动迭代自主通过**（2026-11-21；会话审批策略=never-ask 自动迭代授权，决策留痕本节；发布动作仍待用户明示）
> 证据：`.forgeself/memory/2026-11-21.md` 输入10 段

## 0. 对外方案（六段）

### 1) 目标
用户输入10（2026-11-21）：「待办组件的展示没有展示编号，别的agent使用的时候，是#93 都不知道对应哪一条」。
根因已证：agent 侧 `list_todos` / `complete_todo` 全部引用**数字 Id**（TodoDto.Id），但 TodoTracker 所有渲染面（列表行 / 看板卡 / 专注列表 / 详情抽屉）只展示 `agentTaskKey` 截断串（`#run:xxxx…`）或 Guid `taskKey`，**numeric id 一个都没显示**——「#93」在界面上无从对应。
成功判据：四个渲染面均出现 `#<id>` 徽标，e2e 断言「界面上的 #N == REST 回读的 TodoDto.id」相等（身份字段锚定，非仅可见）。

### 2) 改动（≤5 条）
| 文件:行 | 动作 |
|---|---|
| `Plugins/TodoTracker/web/src/TodoView.vue` | 列表行 meta、看板卡、专注列表三处加 `#{{ task.id }}` 徽标（新类 `tt-tid`，token 化样式 + title 提示） |
| `Plugins/TodoTracker/web/src/components/TaskDetail.vue` | 抽屉头 taskKey Guid 前加 `#{{ task.id }}` |
| `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts` | TDD 断言：列表行/看板卡/抽屉内 `#<id>` 文本与 REST 回读 id 相等（先红后绿） |
| `Plugins/TodoTracker/docs/ui-design-brief.md` | 交互说明补「编号展示」行（六步⑥） |

### 3) 不改
- **AIAgent / Home 的同根因渲染面**（`TodoEntry.vue` 弹层、`AgentWorkspace.vue` 待办面板、`HomeView.vue` 最近待办）——同类扫查已命中，跨 3 插件超出轻量裁剪面，记 TODO.md P2 另批（各自发版闭环）；
- 后端零改动：`TodoDto.Id` / `list_todos` / `complete_todo` 已有数字 Id，不动契约；
- 不动 `agentTaskKey`（`#run:xxx`）与 `taskKey`（Guid）的既有展示语义——委派溯源信息保留，仅**并排补**数字编号；
- 不 commit / 不发布（闸门3 需用户明示）。

### 4) 验证（§5.6 快档）
- TDD：e2e 断言先红（当前 dist 无 id 徽标）→ 实现后绿；
- `cd Plugins/TodoTracker/web && pnpm run build`（dist 更新）；
- 插件冒烟 e2e 定向：`playwright test e2e/plugins/todo-tracker`（快档，非全量）；
- 截图读图（e2e 产物 + dev-stack 走查亮/暗两态，插件专项 E 条）；
- 宿主守卫：`pnpm run check` + `pnpm run test` + `pnpm run design:check`（新增 CSS 必须全 token）；
- 后端/全量 e2e 不跑（零后端改动、未触 e2e 基建——快档判据内）。

### 5) 代价与风险
- 纯前端展示 +1 徽标 ×4 处，风险低；回滚点 = 2 个 vue 文件 + 1 spec revert；
- 风险①：`#run:xxxx`（委派 run 标识）与 `#93`（待办编号）同为 `#` 前缀可能混淆 → 缓解：数字编号用**独立徽标样式**（tt-tid pill + title「待办编号（agent 工具引用）」），run 串保持弱化文本；
- 风险②：看板小卡加徽标可能拥挤/换行破版 → e2e 截图读图 + 溢出核对兜底。

### 6) 待拍板（默认按自动迭代执行，发布除外）
- Q1 修复方案（闸门1）：**默认按上述执行**（§8.5 授权已自主通过，留痕）。
- Q2 完成后是否本地目录发布（`release-local.ps1 -UpdateDir` 投递 + 你在页面点更新）：**默认待你明示**——本批交付止于源码 + dist + 文档 + e2e 绿，④⑤两步（发布、运行实例复验）挂起待批。

---

## 1. Intent
其他 agent 通过 `list_todos` 拿到的条目以 `#<数字Id>` 指称（`complete_todo` 亦收数字 id），但 TodoTracker 界面任何位置都不显示这个数字——用户看到 agent 说「#93 完成」时无法在界面上定位该条。目标：让 numeric id 在全部四个渲染面可见，且 e2e 以「界面 #N == REST 回读 id」锚定身份。

## 2. Spec（要点）
- S1 数据面：`TodoItem.id: number`（types.ts）← TodoDto.Id（camelCase 序列化）← Todo 自增主键；链路已有，缺的只是渲染。
- S2 渲染面（四处）：
  - 列表行：`.tt-item-top` 行内加 `#<id>` 徽标（class `tt-tid`），title=「待办编号（agent 工具引用）」；
  - 看板卡：卡内 meta 区加 `#<id>` 小徽标；
  - 专注列表：条目 meta 加 `#<id>`；
  - 详情抽屉：头部 Guid `taskKey` 前加 `#<id>`（保留 Guid 展示）。
- S3 样式：全 token（`--el-*`），禁止裸色值（design:check 棘轮）；徽标用弱化 pill 形态（对齐 ui-ux-design J 条：淡底+1px 边+次级字，非主色实心）。
- S4 亮/暗两态：新徽标对比度两态各自可读（走查时 `getComputedStyle` 实测留痕）。

### Interaction Design（交互规格 · ui-ux-design 模板）
| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
|---|---|---|---|---|---|---|
| 列表行编号徽标 | 挂载即渲染（随行） | 行 meta 区出现 `#<id>` pill；hover 显 title「待办编号（agent 工具引用）」 | 只读静态，无点击行为 | 无（纯信息） | 列表空则无行 | id 恒有值（自增主键）；不渲染条件：数据缺失时不输出 `#undefined` |
| 看板卡编号 | 挂载即渲染 | 卡内 meta 加 `#<id>` | 只读静态 | 无 | 无卡片无此元素 | 卡宽最小态不换行破版（截图读图核） |
| 专注列表编号 | 挂载即渲染 | 条目 meta 加 `#<id>` | 只读静态 | 无 | 空列表无此元素 | 同上 |
| 详情抽屉编号 | 打开抽屉 | 头部出现 `#<id>` + 原 Guid | 只读静态 | 无 | 无数据不开抽屉 | Guid 保留不删 |

### 适用问题类（problem-class-closure 同类扫查 · 2026-11-21）
- PC 台账（`docs/11-troubleshooting/problem-classes/README.md`）检索「待办/编号/todo」：**无同根因 PC 在档**（仅 PC-023 沾 filesChanged 字样，无关）→ 不立新 PC（本缺陷为「接口字段有、渲染面漏显」首例，若复发再立类）；
- **同类扫查命中（跨插件同根因）**：`Plugins/AIAgent/web/src/agent-core/components/TodoEntry.vue`（弹层仅 title）、`Plugins/AIAgent/web/src/assistant/AgentWorkspace.vue`（待办面板仅 title）、`Plugins/Home/web/src/HomeView.vue`（最近待办面板）——同为「渲染面不显示 todo id」。**异插件 ≠ 本批**：改 3 插件超轻量裁剪面且各自需发版闭环 → 已记 `TODO.md` P2（来源:输入10），批次另立。

## 3. Plan
1. **TDD 红**：读 `todo-tracker.spec.ts` 现状 → 加断言（列表行 `#<id>` 文本 == REST 回读 id；看板卡含 `#<id>`；抽屉含 `#<id>`）→ §5.0 环境前置 → 当前 dist 跑定向 e2e **确认红**；
2. **实现**：TodoView.vue 三处 + TaskDetail.vue 一处（token 化样式）→ 插件 `pnpm run build` → 同一 e2e **转绿** → 读截图（含身份锚定：断言用回读值）；
3. **门禁收尾**：宿主 `pnpm run check` / `pnpm run test` / `pnpm run design:check`；
4. **走查（六步③）**：`pwsh scripts/dev-stack.ps1` 起源码 → 建 1 条测试待办 → 核四处徽标 + 亮/暗两态截图（`ForgeSelf.Web/screenshots/dev/todo-tracker/`）→ 读图 → 清测试数据 → `-Stop`；
5. **文档（六步⑥）**：ui-design-brief 补编号行 → 05/06/07 工件 → 日志（逐回合原文）→ TODO P1 移除（P2 保留）；
6. **挂起待批**：④本地目录发布 + ⑤运行实例只读复验（需用户明示，见 §0-Q2）。

## 4. Task
**Allowed**：改 `TodoView.vue`、`TaskDetail.vue`、`todo-tracker.spec.ts`、`ui-design-brief.md`、pilot 工件、日记/TODO；跑 e2e/build/check 脚本。
**Forbidden**：不动 AIAgent/Home 渲染面（P2 另批）；不动后端 DTO/工具/实体；不 commit / 不 push / 不发布；不触碰用户运行实例（:51888 等）；不动 e2e 基建（global-setup/config）。
**验证命令**：
- 插件：`cd Plugins/TodoTracker/web && pnpm run build`
- e2e（快档）：`cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/todo-tracker --output=../.pw-out-todo-tracker --reporter=list`
- 宿主守卫：`pnpm run check`、`pnpm run test`、`pnpm run design:check`
- 走查：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1`（-Stop 收尾）
