# Evidence

> Task ID：`2026-11-19-todotracker-drawer-progressive`｜日期：2026-11-19

## Task

TodoTracker 全宽列表 + 右侧抽屉详情/编辑 + 分类渐进披露（闸门1 已批推荐项）。

## Changed Files

- `Plugins/TodoTracker/web/src/components/TaskDrawer.vue`（新，Teleport 到 body）
- `Plugins/TodoTracker/web/src/components/TaskDetail.vue`（三页签 → 分类折叠）
- `Plugins/TodoTracker/web/src/TodoView.vue`（去分栏、软提示、挂抽屉）
- `Plugins/TodoTracker/web/src/store.ts`（`expandWorkUnitTick` / `requestExpandWorkUnit`）
- `Plugins/TodoTracker/docs/ui-design-brief.md`
- `docs/02-features/005-todo-tracker.md`
- `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`
- `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.live.spec.ts`（新）
- `ForgeSelf.Web/e2e/global-setup.ts`（Linux 宿主入口 `ForgeSelf`）
- `scripts/dev-stack.ps1`（Linux 跳过 `-WindowStyle`）
- `scripts/get-forge-token.cjs`（Linux `/etc/machine-id` 回落）
- `docs/ai/pilot/2026-11-19-todotracker-drawer-progressive/05`–`07`

## Build

Command:

```bash
cd Plugins/TodoTracker/web && pnpm run build
```

Result: PASS（来源：Verified）

```text
✓ 23 modules transformed.
dist/style.css   27.45 kB │ gzip:  5.13 kB
dist/index.js   102.51 kB │ gzip: 25.68 kB
✓ built in 487ms
```

## Design Check

Command:

```bash
pnpm run design:check
```

Result: PASS（来源：Verified）

```text
[style-tokens] PASS：无新增硬编码色值
[design-compliance] PASS：无新增设计规范违反
```

## Unit Test

Result: N/A（本批未改 vitest 覆盖的纯函数契约；插件 web 无独立 `pnpm test` 脚本）

## Integration Test

Result: N/A（未改后端 / Create / Dispatch API）

## E2E

### 隔离冒烟

Command:

```bash
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/todo-tracker/todo-tracker.spec.ts --reporter=list
```

Result: PASS（来源：Verified）

```text
✓ 真实宿主加载、流水线筛选、看板流转与抽屉渐进披露 (2.2s)
1 passed (14.8s)
```

### 起源码 live 走查

Command:

```bash
# dev-stack :7301/:7399 + E2E_API_TOKEN
pnpm exec playwright test --config=playwright.live.config.ts e2e/plugins/todo-tracker/todo-tracker.live.spec.ts
```

Result: PASS（来源：Verified）

```text
✓ AC-UI：创建仅标题、自动开抽屉、全宽列表、渐进披露、缺项反馈、Esc 关闭 (4.8s)
1 passed (5.4s)
```

## Screenshots

| AC | 路径 |
| --- | --- |
| 列表全宽基线 | `/opt/cursor/artifacts/tt-ac1-list-fullwidth-before.png` |
| 创建后抽屉概要+分类收起 | `/opt/cursor/artifacts/tt-ac2-create-drawer-summary-collapsed.png` |
| 开抽屉列表宽度 | `/opt/cursor/artifacts/tt-ac3-list-width-with-drawer.png` |
| 去补齐展开工作单元 | `/opt/cursor/artifacts/tt-ac4-expand-workunit-from-hint.png` |
| 分类展开 | `/opt/cursor/artifacts/tt-ac5-sections-expanded.png` |
| 下发缺项反馈 | `/opt/cursor/artifacts/tt-ac5b-dispatch-missing-feedback.png` |
| Escape 关闭 | `/opt/cursor/artifacts/tt-ac6-escape-closed.png` |
| 看板开抽屉 | `/opt/cursor/artifacts/tt-ac7-board-opens-drawer.png` |
| 冒烟主链路 | `/opt/cursor/artifacts/todo-tracker-main-flow.png` |

读图核对（Verified）：软提示为 warning 色「下发前待补齐」非危险「还缺」；抽屉 overlay；分类折叠头可见；工作单元展开含四栏；Esc 后抽屉消失。

## Known Limitations

- 抽屉壳为 Teleport 自绘（宿主 `exposeSharedDeps` 未暴露 `ElDrawer`）；行为对齐 Spec（append-to-body / 480 / 窄屏 100% / Esc·遮罩·×）。
- 插件本地目录发布 / `:51888` 运行实例只读复验：本环境无用户常驻 `:51888` 实例 → **Unknown / 跳过**（未停杀任何用户宿主）。
- 隔离 e2e 依赖 Linux 宿主 apphost `ForgeSelf`（本批已修 global-setup）。

## Unresolved Issues

无。
