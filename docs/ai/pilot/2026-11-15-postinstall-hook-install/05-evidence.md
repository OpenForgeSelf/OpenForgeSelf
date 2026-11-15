# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码或跨会话记录推断）/ Unknown（未验证）。
> 提交内容卫生（§B13）：本文所有路径均为仓库相对路径；探针输出中的机器绝对路径以 `<repoRoot>` 代指。

## Task

2026-11-15-postinstall-hook-install（轻量：mini-task.md + 05/06/07）

## Changed Files

### AC1 / AC2 清单事实（证据 = 文件本体与暂存 diff）

- AC1 `package.json`（新增：根清单，private + packageManager 同源 pnpm@10.13.1 + postinstall + 20 条透传脚本，经 `pnpm -C ForgeSelf.Web <script>` 转发）
- AC2 `ForgeSelf.Web/package.json`（修改：scripts 首位新增 `postinstall`，与根清单同一粘合层 `scripts/postinstall-install-hooks.cjs`）

### 其余变更

- `scripts/postinstall-install-hooks.cjs`（新增：postinstall 粘合层）
- `pnpm-lock.yaml`（`pnpm install` 自动生成；`.gitignore` 仅禁 `package-lock.json`，本文件入库）
- `scripts/README.md`（修改：自动安装说明 + 手动兜底行 + 两个脚本行）
- `AGENTS.md`（修改：§0【PILOT 工件链门禁】补自动安装与手动兜底）
- `docs/04-standards/agent-workflow/b-engineering-platform.md`（修改：§B13 补 2026-11-15 自动安装句）
- `docs/ai/pilot/2026-11-15-postinstall-hook-install/`（本目录工件）

## Build

不适用（本批无 C#/前端源码编译面改动；`scripts/**` 新增触发中档全量测试，见下）。

## Unit Test

### AC3 触发链实测（仓库根，常规 `pnpm install`）

Command:

```bash
# 预置：移走 .git/hooks/pre-commit 与 .git/hooks/commit-msg → 确认缺失 → 仓库根跑 pnpm install → 核对还原与内容一致
pnpm install
```

Result: PASS（来源等级：Verified）

```text
已安装 pre-commit -> <repoRoot>\.git\hooks\pre-commit
已安装 commit-msg -> <repoRoot>\.git\hooks\commit-msg
安装校验通过
```

还原后的 hook 内容与 `scripts/hooks/` 源一致（散列比对一致），移动走的 hook 被真实还原。

### AC4 触发链复测（ForgeSelf.Web 内，CI 形态 `pnpm install --frozen-lockfile`）

Command:

```bash
cd ForgeSelf.Web
pnpm install --frozen-lockfile
```

Result: PASS（来源等级：Verified）。清洁复验（全新进程）：commit-msg 还原散列一致 True、pre-commit 还原散列一致 True、两份临时备份（`.itbak`）均已清理 True。CI 侧同形态：`.github/flows/frontend-check.yml` 在 `ForgeSelf.Web` 目录执行 `pnpm install --frozen-lockfile`，Web 清单的 postinstall 将在 CI 触发（Inferred：CI 运行未在本机直接观测，机制与 AC4 本地实测同源）。

### AC5 失败模式运行时实证（临时副本探针，探针目录已清理、不进版本控制）

Result: PASS（来源等级：Verified，三态各有真实输出）

```text
探针A（无 .git 的临时目录内运行 shim 副本）:
  [postinstall] skip: not a git checkout, git hooks not installed   → exit 0
探针B（PATH 收窄至仅 node 目录，pwsh 不可解析）:
  [postinstall] skip: pwsh not found on PATH, git hooks not installed (manual fallback: pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1)   → exit 0
探针C（临时目录伪造 .git 但缺 install-git-hooks.ps1）:
  pwsh -File 缺文件退出码 64 → shim 原样透传 64（非零，还原显式失败）
```

记录探针B 首次执行失误：收窄 PATH 误写入普通变量未生效，实际跑成正常安装路径（幂等重装、无害）；已用新进程重测并以上述输出为准。

### AC6 透传实测（仓库根经根 package.json 透传跑前端门禁）

Command:

```bash
pnpm run design:check
pnpm run check
pnpm run test -- --run
```

Result: PASS（来源等级：Verified）

```text
design:check: exit 0（新增硬编码色值 1761 ≤ 棘轮基线 2501）
check:        exit 0（vue-tsc + eslint 零错误）
test:         exit 0 — 118 个测试文件 / 1223 用例，24.34s
```

### 中档全量 `dotnet test`（scripts/** 新增文件触发，§5.6）

Command:

```bash
pwsh scripts/dev-stack.ps1 之外的常规路径；dotnet test 全量，日志重定向 gate-logs/dotnet-test-full-2026-11-15-postinstall.log
```

Result: FAIL→基线对表判定 0 新增红（来源等级：Verified / 归属 Inferred）

```text
失败: 25，通过: 3756，总计: 3781，耗时约 14m33s（ForgeSelf.Api.Tests，net10.0）
基线对表（对照 .forgeself/memory/2026-11-14.md 最近一次全量 24 红，均在列）:
  ProjectWorkspaceDefaultRoot ×15、WorkflowPlanningIntegrationTests ×6、
  ScriptRunnerDi ×1、MafApprovalLoop ×1、JsonNonAsciiEscapingGuard ×1
  + DesignSystemTests.DesignAgentToolContractTests ×1
    （工作区存量：.forgeself/memory/2026-11-11.md 在列 Inferred；2026-11-13 日记佐证；历史归因 design_* 域并行互扰）
新增红: 0。本批改动面（2×package.json、shim、pnpm-lock、文档）不触 C# 代码。
诚实备注: ScriptRunnerTests.CancelAsync（11-13 期记账为红）本轮通过——该族历来抖动，本轮未红。
```

## Integration Test

不适用（无服务间集成面变更）。

## E2E

N/A（无 UI、无 e2e 触发面；§5.6 按改动面未达深档触发条件）。

## Static Analysis

### 提交内容卫生守卫（§5.6 快档固定腿 + pre-commit 第 4 道门）

Command:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Staged
```

Result: PASS（来源等级：Verified）

```text
check-commit-hygiene: PASS (files=7 lines=96 hits=0)
```

说明：files=7 lines=96 为 `-Staged` 单跑时的暂存子集（当时 pilot 05/06/07 尚未暂存）；最终提交时 pre-commit 对全量暂存集（含本目录工件）复跑本门禁，终值 PASS (files=11 lines=435 hits=0)（见提交日志），以终值为准。

## Screenshots

N/A（无 UI 改动）。

## 文档回写（AC7）

- `AGENTS.md` §0【PILOT 工件链门禁】：补「还原依赖时 hook 自动安装（根目录或 `ForgeSelf.Web/` 内 `pnpm install` 经根 postinstall 触发）+ 手动兜底命令」。Verified（见 commit 暂存 diff）。
- `docs/04-standards/agent-workflow/b-engineering-platform.md` §B13：补 2026-11-15 自动安装机制句。Verified。
- `scripts/README.md`：自动安装说明 + 手动兜底行 + 两个脚本行。Verified。
- 条件项（mini-task AC7 括注「如其提及手动安装为唯一途径」）：`.agents/skills/commit-hygiene-audit/SKILL.md` 全文 grep `install-git-hooks|hook|钩子|安装` 0 命中——技能未涉及 hook 安装、未声称手动安装为唯一途径，无需回写（N/A）。

## Known Limitations

- postinstall 覆盖面 = 在仓库根或 `ForgeSelf.Web/` 内执行 `pnpm install` 的还原（含 CI frontend-check 流水线）。**不在覆盖内**：`Plugins/*/web` 目录内安装、`pnpm install --ignore-scripts`、其他任意子目录安装——这些场景仍需手动兜底（AGENTS §0 已写明），不得宣称「全自动」。
- 根 package.json 透传脚本与 ForgeSelf.Web scripts 的同源性目前靠人工维护，无机械棘轮守卫（已记 TODO，P3）。
- 中档全量红为存量基线（25 = 24 已知族 + 1 已登记存量），非本批引入；`gate-logs/` 目录不入库（处置待拍板）。

## Unresolved Issues

无阻塞项。遗留见 Known Limitations 与 07 §8/§12（均已逐字落 TODO.md 或列为待拍板项）。
