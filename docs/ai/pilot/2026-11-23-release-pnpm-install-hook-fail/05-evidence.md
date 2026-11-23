# Evidence — 发布链 pnpm install (host-web) 恒败修复（输入5 2026-11-23）

> 全部为 Verified（亲自跑过、真实输出）；日志落 `.temp/`（不入库，结论摘录于此）。

## 红灯（修复前，主仓原位复现）
- 命令：`pnpm install --frozen-lockfile`（`ForgeSelf.Web`，主仓 checkout，与用户发布链同参数）→ **exit=1**。
- 实录（`.temp/repro-pnpm-install-hostweb.log` 摘）：`Lockfile is up to date` → 根 postinstall `node ../scripts/postinstall-install-hooks.cjs` → 2×警告「无法设置可执行位 …Unix file modes are not supported on this platform」→ `install-git-hooks.ps1:94 throw hook 安装失败（不可执行，Cursor/Git dispatcher 会跳过）: pre-commit → <git-common-dir>\hooks\pre-commit` → `ELIFECYCLE Command failed with exit code 1`。
- 根因链核实：`Test-IsExecutable` bash 分支（旧 :62）先于 `if ($IsWindows)`（旧 :67）；本机 `Get-Command bash` 命中（git-bash），MSYS 内建 `test -x` 收 `盘符:\` 形态路径恒 false。

## 绿灯（修复后）
- 主仓原位复跑同一命令 → **exit=0**，postinstall 输出「已安装 pre-commit/commit-msg → …（可执行）」「安装校验通过」「Done in 3.1s using pnpm v10.13.1」（`.temp/repro-pnpm-install-after-fix.log`）。
- G2 守卫：`RepositoryScriptTests.跨平台脚本平台短路必须先于POSIX探测_安装钩子`（顺序断言 `$IsWindows` index < `& bash -lc` index）随全量套件执行。
- 全量后端 `dotnet test ForgeSelf.Api.Tests`（中档，scripts/** 触发）：**3985 通过 / 37 失败 / 总计 4022 / 21 分钟**（`.temp/full-backend-tests-3.log`，`--logger console;verbosity=normal` 逐测试留痕）。37 失败对表：**3 处为本批自引入、当批修复并复跑转绿**（见下节），34 处为既有基线/环境红（与本批改动面无关，逐类归属记入 TODO）。
- 本批自引入 3 红与修复（全为守卫当场抓获，红→绿 Verified）：
  1. `RepositoryScriptTests.ScriptsWithNonAscii_MustHaveUtf8Bom(install-git-hooks.ps1)`：编辑工具落盘丢了 UTF-8 BOM（bytes[0]=0x3C）→ 两份拷贝（worktree + 主仓）补回 BOM（5529→5532 字节）→ 过滤复跑 40/41 绿。教训：PS1 中文脚本经程序化编辑后必查 BOM（守卫早已在位，本次正是它拦下的）。
  2. `ProblemClassRegistryTests.Ledgers_ListEveryPc_And_EveryCase`：新 case 漏登 `cases/README.md` 索引行 → 补行 → 绿。
  3. `ProblemClassRegistryTests.Cases_ResolveToExistingPc_And_PcCaseRefs_ResolveToFiles`：前批（输入4）在 PC-024 前置数据留下的悬空引用（`cases:`/`recurrences:` 各一处指向不存在的 `2026-11-23-sems-log-poll-converge`）→ 该复发已由既有 case `2026-11-23-sems-stop-no-feedback.md` §5 完整承载（同一事实一处承载），删悬空引用、recurrences 归一为 `["2026-11-05","2026-11-23"]` → 绿。
- 过滤复跑（修复后权威证据）：`dotnet test --no-build --filter "FullyQualifiedName~RepositoryScriptTests|FullyQualifiedName~ProblemClassRegistryTests"` → **40/41 通过**，唯一余红 = `RootDevCommand_MustUseFullStackLauncher`（既有基线红：守卫期望 `Start-Process -FilePath 'dotnet'`，脚本实际用 `Start-Process @beStart` 重建参数——脚本行为正常、守卫陈旧，记 TODO 不属本批）。
- `check-commit-hygiene.ps1 -Staged`（终态 37 文件）：**PASS files=37 lines=1360 hits=0**。
- 新 G2 守卫（跨平台短路顺序）在全量套件中**通过**（`已通过 …跨平台脚本平台短路必须先于POSIX探测_安装钩子 [1 ms]`）。

## 门禁备注（环境事实，非代码红）
- `check-problem-classes.ps1`：**OK，42 个 PC 全通过**（含新 PC-043）。
- `check-commit-hygiene.ps1 -Staged`（首轮 35 文件）：**PASS files=35 lines=1321 hits=0**（首轮 6 处 abs-drive 违规已改写为「盘符:\」通用表述后通过；终态 37 文件复跑 PASS 见上节）。
- 首次全量尝试 MSB3027：dev-stack 宿主（我方实例，PID 46676）锁 `ForgeSelf.dll` → `dev-stack.ps1 -Stop` 后重跑（非基线红，环境锁）。
- 主仓 `package.json` 存在他人未提交改动（3 条 check:theme 脚本行），与本缺陷无关，未触碰。

## 交付物
- worktree（将随既有 30 文件一起待用户授权提交）：`scripts/install-git-hooks.ps1`、`ForgeSelf.Api.Tests/RepositoryScriptTests.cs`、PC-043、case、cases/README 索引行、PC-024 前置数据去悬空、README 行、mini-task。
- 主仓 checkout：`scripts/install-git-hooks.ps1` 同 patch（未提交工作区改动，为当场解锁用户发布命令；用户可自行提交或待合流）。
