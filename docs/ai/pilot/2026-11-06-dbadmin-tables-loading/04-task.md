---
task_id: 2026-11-06-dbadmin-tables-loading
task_kind: feature
feature: 044
risk: { level: L1, triggers: ["Plugins/DbAdmin/web/src/DbAdminView.vue", "ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts"], raised_by_agent: true }
gate1: { mode: user, at: "2026-11-06T10:30:00+08:00", decision: "用户输入2 明确要求加 loading、补交互规范、CT 增测试并输出报告，视为闸门1 通过" }
gate2: { mode: reviewer, at: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/DbAdmin/web/src/DbAdminView.vue
  - Plugins/DbAdmin/web/src/types.ts
  - ForgeSelf.Web/src/components/DbAdmin.story.ts
  - ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts
  - AGENTS.md
  - DESIGN.md
  - .agents/skills/ui-ux-design/SKILL.md
  - .agents/skills/plugin-development/SKILL.md
  - docs/18-templates/ai-pilot/02-spec.tpl.md
  - Plugins/DbAdmin/docs/ui-design-brief.md
  - Plugins/DbAdmin/docs/06-UI交互评审稿.md
  - docs/ai/pilot/2026-11-06-dbadmin-tables-loading/00-repository-understanding.md
  - docs/ai/pilot/2026-11-06-dbadmin-tables-loading/01-intent.md
  - docs/ai/pilot/2026-11-06-dbadmin-tables-loading/02-spec.md
  - docs/ai/pilot/2026-11-06-dbadmin-tables-loading/03-plan.md
  - docs/ai/pilot/2026-11-06-dbadmin-tables-loading/04-task.md
  - docs/ai/pilot/2026-11-06-dbadmin-tables-loading/05-evidence.md
  - docs/ai/pilot/2026-11-06-dbadmin-tables-loading/06-review.md
  - docs/ai/pilot/2026-11-06-dbadmin-tables-loading/07-final-report.md
  - TODO.md
  - .forgeself/memory/2026-11-06.md
rollback: "git checkout -- Plugins/DbAdmin/web/src/DbAdminView.vue Plugins/DbAdmin/web/src/types.ts ForgeSelf.Web/src/components/DbAdmin.story.ts ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts；删除新增 CT 基线；文档回滚；本轮不提交 git"
writeback: { feature_doc: pending, reason: "交互状态补强，功能契约未变；同步 ui-design-brief/06 评审稿" }
---

# Agent Task

## Task ID

`2026-11-06-dbadmin-tables-loading`

## Objective

点击 DbAdmin 连接加载表时出现局部 loading（不抢先空态、去重/防竞态）；补交互规范与 CT 状态测试并验证，输出报告。

## Scope

### Allowed

- `DbAdminView.vue` 增加 loading 状态/渲染/CSS；`types.ts` 增 fixture 字段；story/CT 新增 loading 用例。
- 规范 5 处 + DbAdmin 2 处文档补异步 loading/CT 规则。
- pilot 工件、TODO、日记。

### Forbidden

- 不改后端/接口/数据库；不改其它插件行为；不整页遮罩；不引入依赖；不提交/发布；不触碰运行实例。

## Acceptance Criteria

- [ ] AC1 loading 状态成功/失败/竞态均正确结束。
- [ ] AC2 加载中显示 `tables-loading-<id>` + spinner，不显示「（无表）」。
- [ ] AC3 CT `DbAdmin/TablesLoading` 行为断言 + 基线；`test:ct` 绿。
- [ ] AC4 插件 build 通过。
- [ ] AC5 规范 5 处补强落地。
- [ ] AC6 DbAdmin 文档补 loading 验收项。
- [ ] AC7 pilot 00-07 齐，05 真实结果，07 报告。

## Verification Commands

```bash
cd ForgeSelf.Web && CT_FRESH=1 pnpm run test:ct:update
cd ForgeSelf.Web && CT_FRESH=1 pnpm run test:ct
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && pnpm run test && pnpm run design:check
cd ForgeSelf.Web && CI=1 E2E_CLEAN=1 pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```
