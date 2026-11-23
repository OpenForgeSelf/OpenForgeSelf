---
id: PC-043
title: 跨平台脚本平台短路缺失——Windows 上 POSIX 探测抢跑（bash test -x 收 Windows 路径恒 false）
tags: [scripts, powershell, cross-platform, postinstall, release-chain]
invariant: 跨平台脚本里「平台无此概念」的检查（如 NTFS 无可执行位）必须把平台专属短路放在最前：Windows 分支先于任何 POSIX 工具探测（bash/test/chmod/UnixFileMode）。禁止让 POSIX 工具在 Windows 路径形态（盘符:\ 反斜杠）上裁决——MSYS bash 内建 test 不识别反斜杠路径，判据恒 false，错误被上游当作「校验失败」放大成整链失败。
status: guarded
first_seen: 2026-11-23
recurrences: []
cases: [2026-11-23-release-pnpm-install-hook-fail]
allowlist: []
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/RepositoryScriptTests.cs"}
guards:
  - {level: G2, path: "ForgeSelf.Api.Tests/RepositoryScriptTests.cs", status: active}
lens:
  - 写跨平台 .ps1 时逐个函数问：「这个判据在 Windows 上有概念吗？」没有 → `if ($IsWindows) { 短路/直通 }` 放函数第一行，再写 POSIX 分支。
  - 调 bash/POSIX 工具前必须确认传入路径形态：Windows 路径先经 `cygpath -u` 转换，或干脆平台短路不进该分支。
  - 环境探测（Get-Command bash/chmod）不能代替平台判定：git-bash 机器上 bash 存在但收的是 Windows 路径——「工具有」≠「判据对」。
  - 上游链条（postinstall→pnpm install→release）的失败要回看第一次 throw 的原始异常全文，wrapper 的 exit code 只说明「挂了」，不说明「为什么挂」。
---

# PC-043 跨平台脚本平台短路缺失（POSIX 探测在 Windows 抢跑）

## 1 不变量与诱因

跨平台 .ps1 常按「有 bash 就走 POSIX 判据」写分支，但**环境里有 bash ≠ POSIX 判据可用**：git-bash 机器上 `bash` 在 PATH，而脚本持有的是盘符:\... 形态的 Windows 路径——MSYS bash **内建** `test` 不做路径转换（自动转换只发生在 msys 程序的 exec 边界，内建命令拿原样字符串），`test -x '盘符:\src\...'` 恒 false。诱因三：① 平台判定（`$IsWindows`）写成兜底而非前置短路；② 误以为「bash 可用 = POSIX 语义可用」；③ 上游（postinstall → pnpm install → release 链）把一次误判放大成整链 exit 1，报错只留下 wrapper 的「failed with exit code 1」，真异常埋在中间层输出里。

## 2 特征签名与扫描方法

- 签名：`pnpm install` 在含 postinstall 的包上失败，日志尾部 `hook 安装失败（不可执行…）`；且本机 `Get-Command bash` 成功。全链报错 = `release-lib.ps1:26 throw "$What failed with exit code $LASTEXITCODE"`。
- 扫描：grep scripts/**/*.ps1 中 `bash -lc`/`test -x`/`chmod` 的调用点，逐个核对「$IsWindows 短路 vs POSIX 分支」的先后顺序（守卫已机械化为顺序断言）。

## 3 修复与守卫

修复（2026-11-23 输入5）：`scripts/install-git-hooks.ps1` —— `Test-IsExecutable` 把 `if ($IsWindows) { return $true }` 提到 bash 分支之前（Windows 上 hook 执行不看模式位，直通语义正确）；`Set-ExecutableBit` Windows 无 chmod 时静默跳过（UnixFileMode 在 NTFS 必抛警告噪声）。守卫 G2：`RepositoryScriptTests.跨平台脚本平台短路必须先于POSIX探测_安装钩子`（顺序断言：`$IsWindows` 短路 index < `& bash -lc` index，红→绿已验证：修复前该文件 bash 分支在 :62、IsWindows 在 :67）。

## 4 同类扫查结论

scripts/** 全量 grep `test -x|bash -lc`：仅 install-git-hooks.ps1 一处 POSIX 探测（本处已修）。其余跨平台脚本的平台分支（pwsh vs 5.1、UnixFileMode）无「工具存在性抢跑平台判定」形态。

## 5 复发记录

（首见即守卫，暂无复发）

## 6 逃逸分析

非本类复发，属**潜伏缺陷首次暴露**：4a3f67f（2026-11-19 前后，postinstall 自动装 hook）落地时，验证路径都在有 POSIX 语义的环境（CI linux / 无 bash 的纯 Windows 走 IsWindows 兜底），只有「Windows + git-bash 在 PATH」这台机器恒挂；用户首个触发点是发布链（第一个裸 `pnpm install` 场景）。教训入 lens：环境探测不能代替平台判定。
