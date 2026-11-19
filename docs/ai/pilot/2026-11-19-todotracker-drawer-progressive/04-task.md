---
task_id: 2026-11-19-todotracker-drawer-progressive
feature: "005"
risk: { level: L2, triggers: ["Plugins/TodoTracker/web/src/", "ForgeSelf.Web/e2e/plugins/todo-tracker/", "scripts/dev-stack.ps1", "scripts/get-forge-token.cjs"], raised_by_agent: false }
gate1: { mode: user, at: 2026-11-19, ref: "six-section-plan.md §6 推荐项已批" }
gate2: { mode: reviewer, at: 2026-11-19, decision: "APPROVED" }
gate3: { mode: agent, at: 2026-11-19 }
expected_files:
  - Plugins/TodoTracker/web/src
  - Plugins/TodoTracker/docs/prototypes
  - ForgeSelf.Web/e2e/plugins/todo-tracker
  - ForgeSelf.Web/e2e/global-setup.ts
  - scripts/dev-stack.ps1
  - scripts/get-forge-token.cjs
rollback: "git revert <commit>"
writeback: { feature_doc: done, reason: "docs/02-features/005-todo-tracker.md 已回写抽屉/渐进披露" }
---

# Task

> Task ID：`2026-11-19-todotracker-drawer-progressive`

## Objective

把 TodoTracker 壳层改为「全宽列表 + 右侧抽屉详情/编辑 + 分类渐进披露」，并把硬必填校验留在下发前。

## Scope

### Allowed

- `Plugins/TodoTracker/web/src/**` 布局与 TaskDetail 披露、`TaskDrawer`
- `Plugins/TodoTracker/docs/**` 设计 brief + HTML 原型
- `ForgeSelf.Web/e2e/plugins/todo-tracker/**`、`e2e/global-setup.ts`（Linux HOST / machine-id 同源）
- `scripts/dev-stack.ps1`、`scripts/get-forge-token.cjs`（Linux 走查所需）
- `docs/02-features/005-todo-tracker.md` 与本 pilot 工件

### Forbidden

- 改 Create/Dispatch API 契约或扩大创建必填
- 改阶段机 / DB
- 停/启用户运行中宿主
- 改 SmartTerminal 或其他无关插件

## Acceptance Criteria

- [x] Spec 含 Interaction Design，路径可审
- [x] 原型展示：全宽列表、抽屉、分类折叠、下发前缺项反馈
- [x] AC-UI-1…6 + 快档门禁绿（见 05-evidence）

## Expected Files

见头部 `expected_files`。

## Verification Commands

```bash
cd Plugins/TodoTracker/web && pnpm run build
pnpm run design:check
# e2e 定向 todo-tracker 冒烟（环境按 AGENTS §5.0）
```
