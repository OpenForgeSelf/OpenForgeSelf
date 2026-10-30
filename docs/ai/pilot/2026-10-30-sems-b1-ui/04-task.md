---
task_id: 2026-10-30-sems-b1-ui
feature: none
risk: { level: L1, triggers: ["Plugins/Sems/web/src/SpecPanel.vue"], raised_by_agent: false }
gate1: { mode: user-order, at: 2026-10-30, ref: "输入4：完善 Sems 功能，测试没问题后界面也实现一下" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/Sems/web/src/types.ts
  - Plugins/Sems/web/src/specOps.ts
  - Plugins/Sems/web/src/specOps.spec.ts
  - Plugins/Sems/web/src/specApi.ts
  - Plugins/Sems/web/src/SpecPanel.vue
  - Plugins/Sems/web/src/ProjectCard.vue
  - Plugins/Sems/web/src/SemsView.vue
  - ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts
  - Plugins/Sems/docs/requirements.md
rollback: "git checkout -- Plugins/Sems/web/src ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts Plugins/Sems/docs/requirements.md；未提交，无 commit 可 revert"
writeback: { feature_doc: pending, reason: "界面落地后回写 Plugins/Sems/docs/requirements.md 的 B1 现状一句" }
---

# Agent Task

> 阶段：Stage 4｜Task ID：2026-10-30-sems-b1-ui
> 闸门1：用户输入4已经要求实现界面。本目录仍补齐 00–04，不把该要求当成免工件。

## Task ID

2026-10-30-sems-b1-ui

## Objective

Sems 插件页能对一个已登记项目完成规格的创建、读版本、追加、关联、反查、登记用例和覆盖展示，并且对应单测与一条 e2e 通过。

## Scope

### Allowed

只改 03-plan 列出的文件，外加本目录 00–07。允许在需求文档 B1 现状补一句界面事实。

### Forbidden

不改 `SpecsController`、`SpecService`、`plugin.json` 版本与工具数。不新增依赖。不发布，不推送，不停启 `:51888` 或任何用户宿主。不实现 B3 沙箱。不把 13 个旧工具的 e2e 名单改成 22。不提交，除非用户另行同意。

## Acceptance Criteria

- [ ] AC1 至 AC10 与 02-spec 一致，证据写入 05
- [ ] `Plugins/Sems/web` 的 `pnpm test` 与 `pnpm build` 有真实日志
- [ ] 规格主链路 e2e 有真实日志；跑不了就在 05 标 Unknown，不写成通过

## Expected Files

- `Plugins/Sems/web/src/SpecPanel.vue`
- `Plugins/Sems/web/src/specOps.ts`
- `Plugins/Sems/web/src/specApi.ts`
- `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`

## Verification Commands

```bash
cd Plugins/Sems/web
pnpm test
pnpm build
cd ../../ForgeSelf.Web
pnpm exec playwright test e2e/plugins/sems/sems.spec.ts -g "规格主链路"
```
