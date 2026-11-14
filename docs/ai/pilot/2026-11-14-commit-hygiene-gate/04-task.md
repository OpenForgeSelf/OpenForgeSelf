---
task_id: 2026-11-14-commit-hygiene-gate
feature: none                  # 流程/工程规则基建，无对应 docs/02-features 编号
risk: { level: L1, triggers: ["scripts/check-commit-hygiene.ps1", "scripts/hooks/pre-commit", "AGENTS.md", "DESIGN.md", "docs/04-standards/agent-workflow/b-engineering-daily.md"], raised_by_agent: false }
gate1: { mode: auto, at: 2026-11-14T08:59+08:00, ref: "用户输入1 原文即任务指令（.forgeself/memory/2026-11-14.md 输入1）" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - scripts/check-commit-hygiene.ps1
  - scripts/hooks/pre-commit
  - .agents/skills/commit-hygiene-audit/SKILL.md
  - AGENTS.md
  - DESIGN.md
  - docs/04-standards/agent-workflow/b-engineering-daily.md
  - ForgeSelf.Api.Tests/CommitHygieneGuardTests.cs
  - docs/07-decisions/not-taken-decisions.md
rollback: "git checkout HEAD -- <触碰文件>；删除新增文件 scripts/check-commit-hygiene.ps1 与 .agents/skills/commit-hygiene-audit/；重跑 install-git-hooks.ps1"
writeback: { feature_doc: none, reason: "流程基建无 feature 文档；规则真源写 b-engineering-platform §B13" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。
> 闸门1 依据：用户输入1 为直接指令且明确「增加一条」，按风险策略 L1 自动放行（原文存日记）。

## Task ID

2026-11-14-commit-hygiene-gate

## Objective

提交口新增第 4 道固定审计门「提交内容卫生」：暂存区新增行命中「工作区/工作区外绝对地址、人名/角色名/成员名、本地拒绝列表词」即拒绝提交；规则真源、技能、盘点口径与存量就地清理同步落位。

## Scope

### Allowed

- `scripts/check-commit-hygiene.ps1`（新建）
- `scripts/hooks/pre-commit`（追加第 4 道门）
- `.forgeself/commit-hygiene.denylist.txt`（新建；该目录不入库）
- `.agents/skills/commit-hygiene-audit/SKILL.md`（新建）
- `AGENTS.md`（§2.3/§2.4/§5.6 登记 + :33/:326/:333 清理，合计 ≤5 处编辑）
- `DESIGN.md`（:4/:98 清理）
- `docs/04-standards/agent-workflow/b-engineering-daily.md`（末尾新增 §B7）
- `ForgeSelf.Api.Tests/CommitHygieneGuardTests.cs`（新建）
- `docs/07-decisions/not-taken-decisions.md`（末尾追加 060）
- `docs/ai/pilot/2026-11-14-commit-hygiene-gate/`（工件 00-07）
- `.forgeself/memory/2026-11-14.md`、`TODO.md`（流程文件）

### Forbidden

- 触碰 `ForgeSelf.Api/**` 宿主源码、`ForgeSelf.Web/src/**` 前端源码、`Plugins/**`。
- 触碰 L4 工件链脚本 `scripts/verify-pilot-artifacts.ps1`、`-pilot-art.ps1`。
- 修改 `ForgeSelf.Api.csproj` 版本行；安装新依赖；改构建配置。
- 修复 pre-commit 5.1 回退偏差（已登记 TODO，超出本批）。
- 触碰其他会话在飞文件语义：`.gitignore`、`docs/README.md`、`docs/02-features/044-db-admin.md`、`Plugins/DbAdmin/docs/03-架构与接口.md`（not-taken 仅追加）。
- 任何入库文件包含违规样本字符串（路径/称呼/账号原文）。
- 执行 `git commit` / `git push`（须用户明示同意）。

## Acceptance Criteria

- [ ] AC1 四类 L1 阳性各命中对应规则 ID（-Files，exit 1）
- [ ] AC2 阴性集 0 命中（-Files，exit 0）
- [ ] AC3 -DiffFile 命中行号与 diff 新侧行号一致
- [ ] AC4 拒绝列表命中 + 缺失文件时 L2 跳过有注明
- [ ] AC5 pre-commit 第 4 道门接线 + FAIL 聚合拒绝 + hook 重装
- [ ] AC6 过滤测试全绿 + 全量中档仅基线红
- [ ] AC7 -All 盘点本批文件零新增违规
- [ ] AC8 五处登记齐备（Part B §B7 / AGENTS ×3 / 技能 / not-taken 060 / 清理行）
- [ ] AC9 本批入库文件自扫零违规

## Expected Files

- scripts/check-commit-hygiene.ps1
- scripts/hooks/pre-commit（修改）
- .agents/skills/commit-hygiene-audit/SKILL.md
- ForgeSelf.Api.Tests/CommitHygieneGuardTests.cs
- AGENTS.md / DESIGN.md / docs/04-standards/agent-workflow/b-engineering-daily.md / docs/07-decisions/not-taken-decisions.md（修改）
- docs/ai/pilot/2026-11-14-commit-hygiene-gate/00-07

## Verification Commands

```bash
# 守卫自测（阳性/阴性/行号/拒绝列表）——判据看日志正文
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Files <自测文件>
# 过滤测试
cd ForgeSelf.Api.Tests && dotnet test --filter CommitHygieneGuard
# 设计守卫（未触前端源码）
cd ForgeSelf.Web && pnpm run design:check
# 工件链
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-14-commit-hygiene-gate -EnforceHeader -EnforceAcCoverage -EnforceInteraction
# hook 重装
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1
# 中档全量（TEMP → 仓库 .temp/tmp，先 build-server shutdown）
cd ForgeSelf.Api.Tests && dotnet test
```
