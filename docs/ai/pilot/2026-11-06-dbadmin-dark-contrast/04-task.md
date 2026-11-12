---
task_id: 2026-11-06-dbadmin-dark-contrast
task_kind: defect
feature: 044
risk: { level: L2, triggers: ["ForgeSelf.Web/src/styles/themes/workshop-forge.css", "ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts", "Plugins/AIAgent/web/src/components/RunRecordPanel.vue"], raised_by_agent: true }
gate1: { mode: user, at: "2026-11-06T00:00:00+08:00", decision: "用户输入1 明确要求「先检查规范与走查验收标准 → 完善规范流程 → 修复该问题」，范围以本文件与 03-plan 为准，视为闸门1 通过" }
gate2: { mode: reviewer, at: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Web/src/styles/themes/workshop-forge.css
  - ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts
  - ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts
  - Plugins/AIAgent/web/src/components/RunRecordPanel.vue
  - Plugins/DbAdmin/docs/ui-design-brief.md
  - Plugins/DbAdmin/docs/06-UI交互评审稿.md
  - AGENTS.md
  - DESIGN.md
  - .agents/skills/ui-ux-design/SKILL.md
  - docs/04-standards/agent-workflow/b-engineering-daily.md
  - docs/18-templates/ai-pilot/02-spec.tpl.md
  - docs/11-troubleshooting/problem-classes/PC-025-dark-theme-token-contrast.md
  - docs/11-troubleshooting/problem-classes/README.md
  - docs/11-troubleshooting/cases/2026-11-06-dbadmin-dark-connection-contrast.md
  - docs/11-troubleshooting/cases/README.md
  - docs/11-troubleshooting/index.md
  - docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/00-repository-understanding.md
  - docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/01-intent.md
  - docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/02-spec.md
  - docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/03-plan.md
  - docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/04-task.md
  - docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/05-evidence.md
  - docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/06-review.md
  - docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/07-final-report.md
  - TODO.md
  - .forgeself/memory/2026-11-06.md
rollback: "git checkout -- ForgeSelf.Web/src/styles/themes/workshop-forge.css Plugins/AIAgent/web/src/components/RunRecordPanel.vue ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts；删除新增 spec/PC/case/pilot 文件；本轮不提交 git，工作区可整体丢弃"
writeback: { feature_doc: pending, reason: "视觉 token 语义修复，功能契约未变；同步 UI brief 与评审稿" }
---

# Agent Task

> 阶段：Stage 4｜任务变成可直接执行的工作单元
> 前序工件：00 / 01 / 02 / 03 齐备；用户输入1 视为闸门1 通过。

## Task ID

`2026-11-06-dbadmin-dark-contrast`

## Objective

把暗色主题 primary light-N 的明暗语义修复为设计稿真源，消除 DbAdmin 选中连接名不可见及同类表面；同时补强规范/走查/模板与机器守卫，使该类缺陷下次在快档或 e2e 即被拦截。

## Scope

### Allowed

- 修改 `workshop-forge.css` 暗色 primary ramp 五值。
- 修改 `RunRecordPanel.vue` 中 `light-7` 文字色这一同类命中。
- 新增 `workshop-forge.contrast.spec.ts`；在 DbAdmin 冒烟 e2e 内新增亮/暗对比度断言。
- 更新 `DESIGN.md`、`ui-ux-design` 清单、`b-engineering-daily` UI 批规则、`02-spec.tpl.md` 主题矩阵条目。
- 更新 DbAdmin 的 ui-design-brief 与 06 评审稿（验收项）。
- 新建 PC-025/case 并更新三处索引；补 00-07 工件、TODO、日记。

### Forbidden

- 不改后端代码/接口/数据库/鉴权；不改其它语义色 ramp；不改插件交互结构与布局。
- 不新增依赖；不跑发布；不停/启/杀用户运行实例；不 git commit/push。
- 不修改本清单以外的业务文件；不顺手重构。

## Acceptance Criteria

- [ ] AC1 类级守卫先红后绿（暗色 light-8 vs text-primary 1.10 → ≥4.5；light-8/9 亮度低于主色）。
- [ ] AC2 DbAdmin e2e 选中行 light/dark 对比度均 ≥4.5，实际数值入证据。
- [ ] AC3 `pnpm run check` + `pnpm run test` + `pnpm run design:check` 绿（记录档位与范围）。
- [ ] AC4 `Plugins/DbAdmin/web` 构建通过，`dist/index.js` 存在。
- [ ] AC5 dev-stack 亮/暗两态走查截图 + 可读性确认，05 记数值。
- [ ] AC6 四份规范/模板补强落地（DESIGN / ui-ux / b-doc / 02-spec tpl）。
- [ ] AC7 同类扫查命中表齐全；同根因已修，异根因 TODO 逐字可检索。
- [ ] AC8 PC-025 + case + 三处索引齐；`check-problem-classes.ps1` 绿；07 三节齐。

## Expected Files

见 front-matter `expected_files`（与 03-plan Files To Change 一致）。

## Verification Commands

```bash
cd ForgeSelf.Web && pnpm exec vitest run src/styles/themes/workshop-forge.contrast.spec.ts
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
```
