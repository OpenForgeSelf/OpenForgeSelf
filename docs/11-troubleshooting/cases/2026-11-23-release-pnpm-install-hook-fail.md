---
id: 2026-11-23-release-pnpm-install-hook-fail
title: 发布链 pnpm install (host-web) 恒败——postinstall 装 hook 在 Windows+git-bash 机器误判 hook 不可执行
pc: PC-043
severity: P1
evidence: Verified
---

# case：发布链 pnpm install 恒败（2026-11-23，输入5）

## 现象
用户在 sems 工作台运行发布命令（主仓 checkout），19:04:23 进程退出。日志尾部：`Exception: scripts\release\release-lib.ps1:26 … pnpm install (host-web) failed with exit code 1`。

## 时间线
- 2026-11-23 19:04 用户报障（输入5），发布链在 build-frontend 第一步挂掉。
- 原位复现（主仓 ForgeSelf.Web，`pnpm install --frozen-lockfile`，exit=1 实录）：`Lockfile is up to date` → postinstall `node ../scripts/postinstall-install-hooks.cjs` → 两条「无法设置可执行位…Unix file modes are not supported」警告 → `install-git-hooks.ps1:94 throw hook 安装失败（不可执行，Cursor/Git dispatcher 会跳过）` → ELIFECYCLE exit 1。
- 根因链：`Test-IsExecutable` 的 bash 分支（:62）排在 `if ($IsWindows)`（:67）之前——本机 bash 在 PATH（git-bash），MSYS bash **内建** `test -x '盘符:\...'` 不识别反斜杠 Windows 路径 → 恒 false → 误判 hook 不可执行 → postinstall 恒败。自 4a3f67f（postinstall 自动装 hook）落地起，本机一切 `pnpm install` 必挂；发布链是第一个撞上的裸 install 场景。CI（linux）不受影响。
- 修复（红→绿）：`Test-IsExecutable` 把 `if ($IsWindows) { return $true }` 提到 bash 分支之前（Windows 执行 hook 不看模式位）；`Set-ExecutableBit` Windows 无 chmod 静默跳过。主仓原位复跑同一命令：**exit=0**，postinstall 输出「已安装 pre-commit/commit-msg → …（可执行）」「安装校验通过」。
- 守卫：`RepositoryScriptTests.跨平台脚本平台短路必须先于POSIX探测_安装钩子`（顺序断言，G2）；归档 PC-043。

## 特征签名
- `pnpm install` 失败且日志含「hook 安装失败（不可执行」；本机 `Get-Command bash` 成功（git-bash）。
- 全链报错只剩 wrapper 文案（release-lib.ps1:26），真异常在中间层——排查时必须看 postinstall 段输出全文。

## 修复
`scripts/install-git-hooks.ps1`（Test-IsExecutable 平台短路前置 + Set-ExecutableBit Windows 静默跳过）；守卫 `ForgeSelf.Api.Tests/RepositoryScriptTests.cs`；工件 `docs/ai/pilot/2026-11-23-release-pnpm-install-hook-fail/mini-task.md`；主仓 checkout 同 patch（用户发布命令当场解锁）。

## 守卫
- G2 顺序断言：`$IsWindows` 短路 index 必须 < `& bash -lc` index（RepositoryScriptTests，防回归）。
- 主仓原位复跑命令 = 行为级验证（exit=0 + hook 安装成功输出）。
