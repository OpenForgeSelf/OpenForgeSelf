# Plan

> Task ID：`2026-11-19-todotracker-drawer-progressive`｜闸门1 批准前仅执行 Phase 0

## Phase 0（本回合 · 无业务代码）

1. 落盘 00–04 + 六段方案。  
2. 写 `Plugins/TodoTracker/docs/ui-design-brief.md` + HTML 原型。  
3. Chrome 截取列表全宽 / 抽屉渐进 / 下发缺项三态，存 artifacts。  
4. 交用户审截图与 Spec；**不改** `Plugins/TodoTracker/web` 业务实现。

## Phase 1（批准后 · Implement）

| # | file | reason |
| --- | --- | --- |
| 1 | `Plugins/TodoTracker/web/src/TodoView.vue` | 去掉 `aside.tt-detail-panel` 分栏；列表/看板全宽；挂抽屉壳；软提示文案 |
| 2 | `Plugins/TodoTracker/web/src/components/TaskDetail.vue` | 改分类折叠渐进披露；下发缺项反馈触发展开；保留失焦保存 |
| 3 | `Plugins/TodoTracker/web/src/components/TaskDrawer.vue`（新） | 抽屉壳（`el-drawer` append-to-body 或等价自绘），详情/编辑共用 |
| 4 | `Plugins/TodoTracker/web/src/actions.ts` / 列表文案 | 「还缺」→「下发前待补齐」展示口径 |
| 5 | `Plugins/TodoTracker/docs/ui-design-brief.md` | 与实现同步 |
| 6 | `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts` | 选择器迁到抽屉；冒烟覆盖开抽屉+缺项下发反馈 |
| 7 | `docs/02-features/005-todo-tracker.md` | 回写布局与校验时机真源 |

**不改**：`DispatchPayloadBuilder` 必填集合、Create API、DB、阶段机。

## Test Plan（批准后）

- 快档：插件 `pnpm run build`；宿主 `pnpm run design:check`；相关 vitest（若有）；1 条 todo-tracker 冒烟 e2e。  
- 后端：本批若只动 web，可不跑全量；若动文案映射测试则 filter TodoTracker。  
- 走查：`dev-stack` 起源码：创建仅标题 → 开抽屉 → 分类展开 → 缺项点下发见反馈 → 列表宽度不变。

## Verification

| 项 | 命令/动作 |
| --- | --- |
| Build | `cd Plugins/TodoTracker/web && pnpm run build` |
| Design | 根目录 `pnpm run design:check` |
| E2E | Playwright 定向 `e2e/plugins/todo-tracker` 一条 |
| UI | 截图读图：全宽列表 + 抽屉 + 折叠分类 |
