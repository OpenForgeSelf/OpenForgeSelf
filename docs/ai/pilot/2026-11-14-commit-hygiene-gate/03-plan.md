# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**，禁止只写「修改 Service、增加测试」。
> Task ID：2026-11-14-commit-hygiene-gate

## Files To Change

- file: `scripts/check-commit-hygiene.ps1`（新建，UTF-8 BOM）
  reason: 守卫本体（FR1-FR4）：四模式 + L1/L2 + 文本/JSON 报告 + 三档退出码。
- file: `scripts/hooks/pre-commit`（编辑，第 4 道门）
  reason: FR5——有暂存内容即跑 -Staged，非零 FAIL 聚合拒绝；日志重定向文件。
- file: `.forgeself/commit-hygiene.denylist.txt`（新建，目录已 gitignore）
  reason: FR2——本地拒绝列表种子（群内昵称 + 机器账号片段），永不入库。
- file: `.agents/skills/commit-hygiene-audit/SKILL.md`（新建）
  reason: FR7——流程沉淀（何时用/怎么跑/怎么修/拒绝列表维护），登记进 AGENTS §2.4。
- file: `AGENTS.md`（编辑 5 处）
  reason: FR7——§2.3 速记一条、§2.4 技能表一行、§5.6 快档入口、:33/:326/:333 就地清理。
- file: `DESIGN.md`（编辑 2 处）
  reason: FR7——:4 与 :98 机器前缀路径清理（保留项目目录名）。
- file: `docs/04-standards/agent-workflow/b-engineering-platform.md`（编辑，末尾新节 §B13）
  reason: FR7——规则真源 §B13：文字化规则表 + 守卫用法 + 拒绝列表格式 + 棘轮与清理策略（真源归 Part B 平台账；daily 另补 §B2 构建踩坑一条）。
- file: `ForgeSelf.Api.Tests/CommitHygieneGuardTests.cs`（新建）
  reason: FR6——G3 守卫测试（子进程模式，§B2:95-99）。
- file: `docs/07-decisions/not-taken-decisions.md`（编辑，末尾追加 060）
  reason: FR7——三不做与重审触发条件。

## Implementation Steps

1. 写守卫脚本 `scripts/check-commit-hygiene.ps1`（参数/解析器/规则/报告），写后字节级验证 UTF-8 BOM（编辑工具会剥 BOM，需 WriteAllText + UTF8Encoding($true) 恢复）。
2. 写本地拒绝列表种子 `.forgeself/commit-hygiene.denylist.txt`。
3. 编辑 `scripts/hooks/pre-commit` 加第 4 道门（`> 日志 2>&1`，重定向后回显，FAIL 聚合）。
4. 跑 `scripts/install-git-hooks.ps1` 重装 hook。
5. 就地清理 AGENTS.md :33/:326/:333 与 DESIGN.md :4/:98（去机器前缀/改通用词，≤5 行/文件）。
6. 写 Part B §B13 真源节 + AGENTS.md §2.3/§2.4/§5.6 登记。
7. 写技能 `.agents/skills/commit-hygiene-audit/SKILL.md`。
8. 写 `CommitHygieneGuardTests.cs`（fixture 字符串全部运行期拼接，源码零违规样本）。
9. 追加 not-taken-decisions.md §060（编辑前重读文件尾，防并行会话漂移）。
10. 写 pilot 05-07；日记补全；TODO 移除。

## Test Plan

1. 守卫自测（-Files 阳性/阴性 + -DiffFile 行号 + 拒绝列表 + 缺失拒绝列表跳过）→ 判据看日志正文。
2. `dotnet test --filter CommitHygieneGuard` 快档全绿。
3. 全量中档 `dotnet test`（TEMP 重定向 .temp/tmp，先 build-server shutdown）→ 比对基线，仅认新增红。
4. `pnpm run design:check`（未触前端源码，跑守卫确认零误伤）。
5. `verify-pilot-artifacts.ps1 -TaskId 2026-11-14-commit-hygiene-gate -EnforceHeader -EnforceAcCoverage -EnforceInteraction`。
6. 守卫对本批新增文件自扫（AC9）。
7. hook 重装后 -Staged 空暂存/带暂存演练（不真实 commit，§8.5）。

## Verification

### Build

```bash
pwsh -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile('scripts/check-commit-hygiene.ps1', [ref]$null, [ref]$e); $e.Count"
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter CommitHygieneGuard
```

### Integration Test

```bash
# N/A：本批无服务集成面；守卫与 hook 的集成由 Test Plan 7 的演练 + pre-commit 语义覆盖
```

### E2E

```bash
# N/A：无 UI 改动（02-spec 交互设计节已声明无界面交互）
```

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run design:check
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-14-commit-hygiene-gate -EnforceHeader -EnforceAcCoverage -EnforceInteraction
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1
# 中档：全量 dotnet test（TEMP → 仓库 .temp/tmp）
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-11-14 12:58 | 规则真源节落点 | FR7/Files 清单：`b-engineering-daily.md` 末尾新节（原拟 §B7） | 真源落 `b-engineering-platform.md` 末尾新节 §B13（平台不变规则归 Part B 平台账；daily 仅补 §B2 PILOT-076 构建踩坑一条） |
| 2026-11-14 12:58 | Test Plan 7 演练形态 | hook 重装后 `-Staged` 空暂存/带暂存演练，不真实 commit | 以真实提交演练替代：B3/B2 经 hook 全链提交；B3 首提交（005:3 绝对盘符路径）被第 4 道门活拦截（`FAIL hits=1`）→ 脱敏改写 → 重试 `PASS hits=0`，证据强于空转演练 |
| 2026-11-14 12:58 | Test Plan 3 执行 | 一次中档全量 | 首跑 11:26 卡死废弃（非证据）；重跑 12:10→12:25:04 有效：26 红 = 25 基线 + 1 已知 flaky（ForgeConfigTests，TODO L17/L185 台账在案，全量红/隔离绿，本批代码零触及） |
