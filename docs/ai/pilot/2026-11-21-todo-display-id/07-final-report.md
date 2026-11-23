# PILOT-085 · 07 Final Report

## 一、任务

用户输入10（2026-11-21）：「待办组件的展示没有展示编号，别的agent使用的时候，是#93 都不知道对应哪一条」→ 待办组件全部渲染面展示数字编号，与 agent 工具（list_todos/complete_todo）引用语义对齐。

## 二、根因与修法

- 根因：TodoDto.Id（数字自增）存在于数据/工具层，但 TodoTracker 四个渲染面（列表行/看板卡/专注列表/详情抽屉）只展示 `agentTaskKey` 截断串（`#run:xxxx`，委派 run 标识）或 Guid `taskKey`——numeric id 无一显示，「#93」无从对应。
- 修法：四处加 `#{{ task.id }}` 徽标（`.tt-tid`/`.td-tid`，token 化 pill：淡底+1px 边+次级字，与 P1 无边框 pill、`#run:` 委派串形成三层区分）；抽屉头 Guid 保留；`.td-key` title「任务编号：Guid」纠偏为「任务标识（GUID）」。

## 三、问题类归档

- PC 台账检索「待办/编号/todo」：**无同根因 PC 在档，本次不立新 PC**（首例；复发再立）。
- 同类扫查（跨插件同根因，**不属本批**）：`Plugins/AIAgent/web/src/agent-core/components/TodoEntry.vue`（弹层仅 title）、`Plugins/AIAgent/web/src/assistant/AgentWorkspace.vue`（待办面板仅 title）、`Plugins/Home/web/src/HomeView.vue`（最近待办面板）——已记 `TODO.md` P2（来源:输入10），待独立批次修（各自发版闭环，三插件批量超轻量裁剪面）。

## 四、重构提案

- 无强制重构。可选提案（P2 批次一并考虑）：给「详情头标识区」抽小组件（编号+Guid+阶段+优先级的展示逻辑在三处近似重复），当前规模不值得动。

## 五、悬空教训

1. 「接口字段有、渲染面漏显」＝假能力的一种：工具侧 agent 与人看的是两套标识，字段级对齐检查（渲染面出现 agent 引用的身份字段）应成为带工具插件的走查固定项——已在本插件 ui-design-brief 落「编号徽标」行；AIAgent/Home 待 P2 批次补齐。
2. 语义漂移：Guid taskKey 的 title 曾自称「任务编号」，与真正的数字编号冲突——展示「编号」字样的位置必须与工具引用语义对表，不能凭界面惯称。

## 六、验证结果（快档 · 均为实际执行）

| 项 | 结果 |
|---|---|
| TDD 红 | ✅ e2e 断言 `.tt-tid` 期望 `#1` not found（未修复 dist） |
| TDD 绿 | ✅ 1 passed (46.4s)，4 断言含身份锚定，既有链路未破 |
| 插件 `pnpm run build` | ✅ dist 27.40 kB style.css；bundle 含新类、vue 裸导入 |
| 宿主 `check` | ✅ 0 errors（165 warnings=存量） |
| 宿主 `test` | ✅ 1273 passed |
| `design:check` | ✅ 无新增裸色值/规范违反 |
| 起源码走查（六步③） | ✅ 四渲染面 + 亮(8.99:1)/暗(7.82:1) 实测 + 读图 + 清测试数据(204/残留0) + `-Stop` |
| 六步④发布 / ⑤运行实例复验 | ⏸ 待用户明示（见 §八） |

## 七、NOT-to-do（同步 `docs/07-decisions/not-taken-decisions.md`）

- 不动 AIAgent/Home 同根因渲染面（本批）——异插件各需发版闭环，记 TODO P2；
- 不立新问题类 PC（首例，无复发证据）；
- 不 bump plugin.json 版本（发布获批时随 package-plugin 流程 bump，避免版本-产物错位）；
- 不给专注列表加 e2e 断言（推到 Review 态成本高于冒烟从简原则的收益，走查已覆盖）。

## 八、状态与待用户事项

- **代码面完成**；插件六步闭环**待 ④⑤**：
  1. 批准发布：`release-local.ps1 -UpdateDir` 本地目录更新源投递（版本将 bump 1.2.1）；
  2. 你在运行实例页面点「自动更新」后，我可做⑤只读复验（版本徽标 + #编号主链路采证）。
- 未 commit（闸门3 需明示）；工作区改动：2 vue + 1 e2e spec + 1 brief + 本批工件 + TODO/日志。

## 九、代价与收益

- 代价：~35 行前端 diff（含样式与注释）+ 9 行 e2e 断言；e2e 两轮 + 守卫 + 走查 ≈ 25 分钟机器时。
- 收益：agent 指称（#93）与界面条目一一对应，消除「agent 说编号、人找无此条」的协作断点；顺带消除 Guid 冒称「编号」的语义混淆。
