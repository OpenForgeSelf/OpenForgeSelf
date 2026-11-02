---
task_id: PILOT-B4-UI
feature: campaign/2026-11-02-maf-runtime-hardening-b4-permission-ui
risk: { level: L2, triggers: ["ForgeSelf.Web/src/views/SettingsView.vue", "ForgeSelf.Web/src/data/features.ts"], raised_by_agent: true }
gate1: { mode: team-lead, at: 2026-11-02T22:38, ref: "seq6 开工令（契约先行 + 范围/门禁已定）" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: lead, at: null }
expected_files:
  - ForgeSelf.Web/src/services/permissionPolicyApi.ts
  - ForgeSelf.Web/src/services/__tests__/permissionPolicyApi.test.ts
  - ForgeSelf.Web/src/components/settings/PermissionPolicyPanel.vue
  - ForgeSelf.Web/src/components/settings/PermissionAuditView.vue
  - ForgeSelf.Web/src/views/SettingsView.vue
  - ForgeSelf.Web/src/data/features.ts
rollback: "git checkout -- ForgeSelf.Web/src/services/permissionPolicyApi.ts ForgeSelf.Web/src/services/__tests__/permissionPolicyApi.test.ts ForgeSelf.Web/src/components/settings/PermissionPolicyPanel.vue ForgeSelf.Web/src/components/settings/PermissionAuditView.vue ForgeSelf.Web/src/views/SettingsView.vue ForgeSelf.Web/src/data/features.ts"
writeback: { feature_doc: pending, reason: "B4 UI 链交付后由 lead 归档战役 feature 文档" }
---

# Agent Task

> 阶段：Stage 4｜前序工件 00/01/02/03 齐备（闸门1 = lead 开工令 seq6 + 契约冻结知会）。

## Task ID

PILOT-B4-UI

## Objective

设置页「权限策略」面板全功能可用（策略 CRUD + 规则矩阵 + 白名单 + 审计双视图 + 深链），快档三门禁绿，真实走查实证 AC1..AC10。

## Scope

### Allowed

- 03-plan「Files To Change」所列 6 文件（4 新 2 改）。
- 新增 vitest 用例（`src/services/__tests__/permissionPolicyApi.test.ts`）。
- 走查截图写入 `ForgeSelf.Web/screenshots/dev/b4-permission-20261102/`。

### Forbidden

- 后端任何文件（`ForgeSelf.Api/**`、`ForgeSelf.Abstractions/**`——契约只读）。
- 既有设置面板行为变更（本批仅纯增量注册）。
- 新增 npm 依赖 / vite 配置变更 / 路由表变更。
- e2e 基建文件（global-setup、playwright configs）。
- dev-stack 停/启只经 `scripts/dev-stack.ps1`；禁碰 :51888 运行实例。

## Acceptance Criteria

- [ ] AC1..AC10 全部满足（定义见 02-spec.md「Acceptance Criteria」节，逐字对应至 05-evidence.md）。
- [ ] 快档三门禁：check 0 错误 / test 全绿 / design:check 棘轮不涨。
- [ ] 走查截图 ≥5 张落 `screenshots/dev/b4-permission-20261102/` 并读图核对。

## Expected Files

- 见 front-matter `expected_files`（6 项）+ 本目录 05/06/07 工件。

## Verification Commands

```bash
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
```
