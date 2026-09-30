# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。

## Task ID

PILOT-051（2026-09-30-sign-default-off）

## Objective

让发布/CI 签名语义变为「默认不签名、不自签，显式传 `-Sign` 才签名」，并完成提交 → 推送 → 重打 tag → CI 复跑验证，解除 CI 卡死在自签证书生成的问题。

## Scope

### Allowed

- 编辑 `.github/workflows/release.yml`（去 `-Sign` + 注释同步）
- 编辑 `docs/04-standards/packaging-upgrade-backup.md`（§1.1 签名行 + §5 变更记录）
- 编辑 `AGENTS.md`（§2.3 发布规范句）
- 编辑 `.agents/skills/plugin-publish-verify/SKILL.md`（第 40 行参数说明）
- 创建 `docs/ai/pilot/2026-09-30-sign-default-off/` 00-07 八件工件
- 本地打包验证（`release-local.ps1 -Version 0.0.0-local -SkipFrontend`，不带 `-Sign`）
- git：选择性暂存上述 4 文件 + pilot 目录 → commit → push github main → 重打 tag `v2.2.2026.0930` force push → 确认旧 run cancel → CI 复跑验证

### Forbidden

- 改 `sign-publish.ps1` 实现（签名能力保留）
- 改 `release-local.ps1` / `package-release.ps1` / `build.ps1` 实现
- 改 `docs/09-operations/code-signing.md`
- 改/提交并行会话 PILOT-050 的任何改动（AppBuilder.cs / StartupPortResolver.cs / ForgeSelf.Web/e2e/* / playwright.*.config.ts / vite.config.ts / docs/04-standards/*.md / docs/18-templates/ai-pilot/README.md / 历史 pilot 01-intent 等 ~29 文件）
- 未经用户明确指令执行 git 之外的对外操作
- 停/启/杀用户运行中的宿主进程

## Acceptance Criteria

- [ ] release.yml 无 `-Sign`
- [ ] 真源 §1.1 签名行 + §5 变更记录已更新（输入42 行标注废止 + 2026-09-30 新增行）
- [ ] AGENTS.md §2.3 已更新（默认不签 + 真源引用）
- [ ] plugin-publish-verify SKILL.md 已更新（-Sign 可选 + CI 不传）
- [ ] 本地 `release-local.ps1 -Version 0.0.0-local -SkipFrontend` exit=0、无签名步骤、出 zip
- [ ] git commit 通过 pre-commit hook（PILOT 工件链 00-07 PASS）
- [ ] push github main 成功
- [ ] 重打 tag `v2.2.2026.0930` 推送成功
- [ ] CI 重跑走完（Release 产出，不再卡在签名）

## Expected Files

- `.github/workflows/release.yml`（M）
- `docs/04-standards/packaging-upgrade-backup.md`（M）
- `AGENTS.md`（M，仅 §2.3 一行）
- `.agents/skills/plugin-publish-verify/SKILL.md`（M）
- `docs/ai/pilot/2026-09-30-sign-default-off/00-07`（新增八件）

## Verification Commands

```powershell
# 本地打包（默认不签）：
powershell -File scripts/release/release-local.ps1 -Version 0.0.0-local -SkipFrontend
# 判据：exit=0；日志检索 "sign|签名|Authenticode" 无签名步骤；artifacts/release/ 出 zip

# PILOT 工件链门禁：
powershell -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-09-30-sign-default-off
# 判据：PASS: 全部 PILOT 工件链齐全

# 暂存清单核对：
git diff --cached --name-only
# 判据：仅 .github/workflows/release.yml / docs/04-standards/packaging-upgrade-backup.md / AGENTS.md / .agents/skills/plugin-publish-verify/SKILL.md / docs/ai/pilot/2026-09-30-sign-default-off/（00-07）

# 推送与 tag：
git push github main
git tag -f v2.2.2026.0930 <新commit> && git push github v2.2.2026.0930 --force

# CI 复跑验证：
gh run list
gh release view v2.2.2026.0930
```
