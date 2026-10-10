---
task_id: 2026-10-10-aboutpanel-real-version
feature: none
risk: { level: L1, triggers: ["ForgeSelf.Web/src/components/settings/AboutPanel.vue"], raised_by_agent: false }
gate1: { mode: auto, at: 2026-10-10T00:00, ref: "用户会话委托·闸门1视为已通过" }
gate2: { mode: reviewer, at: 2026-10-10T00:00, decision: "同会话代理审查（06-review.md）" }
gate3: { mode: agent, at: 2026-10-10T00:00 }
expected_files:
  - ForgeSelf.Web/src/components/settings/AboutPanel.vue
  - ForgeSelf.Web/src/components/settings/__tests__/AboutPanel.test.ts
rollback: "无 git 操作（用户禁止）；恢复=手动将 AboutPanel.vue script 清空并还原 v0.1.0 行"
writeback: { feature_doc: none, reason: 展示位修正，无特性文档承载 }
---

# Agent Task

> 阶段：Stage 4。前置工件 00-03 已产出，闸门1 经用户委托视为通过。

## Task ID
PILOT-2026-1010-ABOUTVER

## Objective
AboutPanel.vue 的版本行由硬编码 v0.1.0 改为运行实例真实版本（updateApi.getStatus().currentVersion），三态（加载/成功/失败）行为符合 02-spec，门禁全绿。

## Scope

### Allowed
- 修改 `ForgeSelf.Web/src/components/settings/AboutPanel.vue`
- 新增 `ForgeSelf.Web/src/components/settings/__tests__/AboutPanel.test.ts`
- 项目任务工件：TODO.md、.forgeself/memory/、docs/ai/pilot/2026-10-10-aboutpanel-real-version/

### Forbidden
- 其他任何业务文件；publish/ 与 .trash/；git 历史操作；启停杀进程；改 updateApi.ts/UpdatePanel.vue/布局样式类

## Acceptance Criteria
- [x] 模板不再含 v0.1.0，格式 `v{{ version }}`
- [x] 挂载取数 + 加载占位 v… + 失败 v未知（try/catch 吞错）
- [x] 新单测三态通过（4/4：占位/成功/拒绝/空值）
- [x] pnpm run check 全绿（vue-tsc 0 error + eslint 0 error，warning 基线持平 76）
- [x] vitest run 全量无回归（769/769，含新 4 例）
- [x] 仅 Allowed 清单内文件变更（git diff --stat 核对：AboutPanel.vue + 新测试文件）

## Expected Files
- ForgeSelf.Web/src/components/settings/AboutPanel.vue
- ForgeSelf.Web/src/components/settings/__tests__/AboutPanel.test.ts

## Verification Commands
```bash
cd ForgeSelf.Web
pnpm run check
pnpm vitest run
```
