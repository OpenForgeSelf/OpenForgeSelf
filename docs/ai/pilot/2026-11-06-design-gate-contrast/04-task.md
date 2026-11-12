---
task_id: 2026-11-06-design-gate-contrast
feature: none                  # 设计门禁基建，不对应单一功能编号
risk: { level: L2, triggers: ["scripts/check-design-compliance.mjs", "scripts/hooks/pre-commit", ".github/workflows/frontend-check.yml"], raised_by_agent: false }
gate1: { mode: user, at: null, ref: "AGENTS §11 闸门1" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - scripts/check-design-compliance.mjs
  - scripts/check-theme-contrast.mjs
  - scripts/design-compliance-baseline.json
  - ForgeSelf.Web/package.json
  - scripts/hooks/pre-commit
  - .github/workflows/frontend-check.yml
  - AGENTS.md
  - DESIGN.md
rollback: "git revert <commit>（纯守卫/文档，无数据与契约影响）"
writeback: { feature_doc: pending, reason: "设计门禁基建，入 DESIGN.md 门禁化说明而非功能文档" }
---

# Agent Task

## Task ID

PILOT-design-gate-contrast（目录 `docs/ai/pilot/2026-11-06-design-gate-contrast/`）

## Objective

让「焦点/背景配对不可读」和「背景级 token 当文字色」在**提交时与 CI 上**必然失败，不再依赖有人手动跑 vitest；同时把 AGENTS §8 的细节收回到 DESIGN.md / 技能清单。

## Scope

### Allowed

- `scripts/check-design-compliance.mjs`（新增规则 E 与 baseline `e` 列）
- 新建 `scripts/check-theme-contrast.mjs`
- `scripts/design-compliance-baseline.json`（仅 `--update` 生成）
- `ForgeSelf.Web/package.json`（`design:check` 链）
- `scripts/hooks/pre-commit`（design 段单入口化）
- `.github/workflows/frontend-check.yml`（新增一步）
- `AGENTS.md` §8、`DESIGN.md` §门禁化说明
- `docs/ai/pilot/2026-11-06-design-gate-contrast/05-07.md`

### Forbidden

- 任何业务 UI / 主题取值改动（token ramp 已在上一批修复，不得再调）
- 新增 npm 依赖 / 改构建工具链
- 把全量 vitest 或 CT 塞进 pre-commit
- 抬高任何棘轮基线数值
- 改动 `Plugins/**` 业务代码

## Acceptance Criteria

- [ ] 现状 `pnpm run design:check` PASS，含 `[theme-contrast]` 输出，耗时 <2s
- [ ] 人造违规 E（`color: var(--el-color-primary-light-7)`）→ 退出码 1，日志含文件名与规则名
- [ ] 人造违规 F（暗色 `light-8` 改回 `#FDE68A`）→ 退出码 1，日志含实测对比度
- [ ] `pre-commit` 与 `frontend-check.yml` 均调用 `design:check`（grep 可证）
- [ ] `pnpm run check` / `pnpm run test` 不回归
- [ ] AGENTS §8 三步门禁 ≤3 行/步且全部指向真源文件

## Expected Files

- scripts/check-design-compliance.mjs
- scripts/check-theme-contrast.mjs
- scripts/design-compliance-baseline.json
- ForgeSelf.Web/package.json
- scripts/hooks/pre-commit
- .github/workflows/frontend-check.yml
- AGENTS.md
- DESIGN.md

## Verification Commands

```bash
cd ForgeSelf.Web && pnpm run design:check
cd ForgeSelf.Web && pnpm run check
cd ForgeSelf.Web && pnpm run test
cd ForgeSelf.Api.Tests && dotnet test        # §5.6 中档：本批碰 scripts/**
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-design-gate-contrast -EnforceHeader -EnforceAcCoverage -EnforceInteraction
```
