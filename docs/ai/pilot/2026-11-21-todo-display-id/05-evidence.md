# PILOT-085 · 05 Evidence（只记实际发生 · Verified/Inferred/Unknown 分级）

> 任务：待办组件展示自身编号（#93 对不上条目）｜ 日期：2026-11-21 ｜ 环境：Windows / pwsh 7.6.6 / 本仓主 worktree

## 1. TDD 红（Verified）

- 命令：`pnpm exec playwright test --config=playwright.config.ts e2e/plugins/todo-tracker --output=../.pw-out-todo-tracker --reporter=list`（ForgeSelf.Web 内，§5.0 环境前置已设：NO_PROXY + TEMP/TMP→`.temp\tmp`）
- 前置：以**未修复源码**重建插件 dist（23 modules，style.css 27.00 kB）——保证红灯只因缺徽标，非陈旧产物
- 结果：**1 failed**，失败点=新增断言：
  ```
  Locator: locator('.tt-item').filter({ hasText: '冒烟-看板闭环-1795223359306' }).locator('.tt-tid')
  Expected: "#1"   Error: element(s) not found
  ```
  判读：REST 回读 id=1，界面无 `.tt-tid` —— 缺陷复现成立；同用例其余链路断言（宿主加载/流水线筛选/软提示）全部通过 → 红灯归因单一。
- 日志：`.temp/e2e-todo-red.log`

## 2. 实现（Verified）

| 文件 | 变更 |
|---|---|
| `Plugins/TodoTracker/web/src/TodoView.vue` | 列表行 `.tt-item-top`（P1 徽标与标题间）+ 看板卡 `<small>` 信息行 + 专注列表按钮前缀，三处 `#{{ task.id }}`；新增 `.tt-tid` 样式（全 `--el-*` token：fill-color-light 底 / border-color-lighter 边 / text-color-regular 字 / 11px） |
| `Plugins/TodoTracker/web/src/components/TaskDetail.vue` | 抽屉头 P1 与 Guid 间加 `#{{ task.id }}`（`data-test="td-tid"`）；`.td-key` 的 title 由「任务编号：Guid」纠偏为「任务标识（GUID）：…」（消除与数字编号的语义冲突） |

- 重建 dist：style.css 27.40 kB；bundle 内确认 `tt-tid` / `td-tid` 类与 `data-test` 均存在，`import … from "vue"` 保持裸导入（宿主 import map 解析，插件铁律 4 自检）。

## 3. TDD 绿（Verified）

- 同一用例复跑：**1 passed (46.4s)**，测试体 2.4s；四条身份锚定断言通过：
  1. 列表行 `listRow.locator('.tt-tid')` toHaveText `#<REST回读id>` ✅
  2. 看板卡 `card.locator('.tt-tid')` toHaveText `#<id>` ✅
  3. 抽屉 `drawer.locator('[data-test="td-tid"]')` toHaveText `#<id>` ✅
  4. 既有链路（版本徽标 v1.2.0、流水线计数、流转落库回读、抽屉四分类、下发缺项点名、时间线）未破 ✅
- 日志：`.temp/e2e-todo-green.log`（另有一次误从仓库根启动的失败运行，属命令路径错误，已终止并以正确目录重跑，不涉被测代码）

## 4. 宿主守卫（快档 · Verified）

| 门禁 | 结果 | 证据 |
|---|---|---|
| `pnpm run check` | exit 0；**0 errors**（165 warnings=存量基线，非本批新增） | `.temp/gate-check.log` |
| `pnpm run test` | **1273 passed (1273)** | `.temp/gate-test.log` |
| `pnpm run design:check` | PASS：硬编码色值 1766（基线 2501，无新增）；token 族/胶囊/玻璃/渐变四计数与基线持平 | `.temp/gate-design.log` |
| 后端 `dotnet build/test` | 未跑——零后端改动（判据：改动仅 2 个 `.vue` + 1 个 e2e spec；Inferred：不受影响） | — |
| commit-hygiene `-Staged` | 未跑（未暂存未提交；新增行为中文注释+token 样式+e2e 断言，无绝对路径/人名/账号字样，人工过目） | — |

## 5. 起源码走查（六步③ · Verified）

- 栈：`pwsh scripts/dev-stack.ps1` → 后端 `:7301` / 前端 `:7399`（端口自动顺延），pluginsDir 指源码 `Plugins/`，token 由 `.temp/dev-stack.json` 取得注入 `forge_api_token`。
- 数据：自建「走查-编号展示-0914」→ 得 **#12**（挂载即现列表行；创建后自动开抽屉即现抽屉头）。
- 四渲染面目测（读图核验）：
  1. 列表行：`待验收 | P1 | #12 | 走查-编号展示-0914` ✅（既有真实任务 #11、#10 同样正常显示 `#<id>`）
  2. 看板卡：e2e 暗色截图（`screenshots/e2e/todo-tracker/todo-tracker-main-flow.png`）卡内 `#1 P1 · 未关联项目` ✅
  3. 专注列表：#12 流转草稿→就绪→已下发→执行中→待验收（服务端 allowedTargets 逐级）后，专注项出现 `#12 · 走查-编号展示-0914 · 待验收 · P1` ✅
  4. 抽屉头：`待验收 | P1 | #12 | 2d6a7cfa02c8…`（编号徽标 + Guid 并排）✅
- 对比度实测（getComputedStyle，WCAG 相对亮度公式）：
  - 亮色：字 rgb(74,68,60) / 徽标底 rgb(251,247,238) → **8.99:1**（≥4.5 ✅；徽标底与卡底同值，靠 1px 边框成形——预期弱化设计）
  - 暗色（显式 `html.dark`）：字 rgb(181,171,159) / 底 rgb(27,24,21) → **7.82:1**（≥4.5 ✅）
- 截图：`ForgeSelf.Web/screenshots/dev/todo-tracker/walkthrough-light-list.png`、`walkthrough-light-focus.png`、`walkthrough-dark-list.png`（已读图）
- 清理：DELETE `/api/todos/12` → 204；复查列表「走查-编号展示*」残留 **0**；dev-stack `-Stop` 两进程清理；走查未触碰用户真实数据（#10/#11 只读未动）与 :51888 运行实例。

## 6. Unknown / 未验证项

- ⑤运行实例只读复验：未做（发布④待用户批准后才有新版本可更新——见 07 §风险）。
- 版本号：plugin.json 保持 1.2.0 未 bump（发布获批时随 package-plugin 流程 bump 至 1.2.1，避免版本-产物错位）。
- 全量 e2e / 中深档：未触发（改动面不满足 §5.6 触发条件，快档判据内）。
