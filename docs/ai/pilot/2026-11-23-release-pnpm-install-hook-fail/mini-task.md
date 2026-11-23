# Mini-Task — 发布链 pnpm install (host-web) 恒败：postinstall 装 hook 的平台判定在 Windows 抢跑 POSIX 分支（输入5 2026-11-23）

> 轻量工件（≤3 文件缺陷修复：Intent/Spec/Plan/Task 合并；规范 §11 裁剪档）。
> 来源：输入5 2026-11-23 19:04 原文「运行报错了 … release-lib.ps1:26 … pnpm install (host-web) failed with exit code 1 … [19:04:23] 进程已退出（PID 81308）」。

## Interaction Design（交互设计）

**无界面交互**——本批为仓库基建脚本（install-git-hooks.ps1）+ 后端守卫测试 + 归档文档，无任何用户可见 UI/交互面；唯一「使用者」是开发者终端（pnpm install 时的控制台输出，中文提示既有格式保持不变）。

## Intent（为什么/做什么/到什么程度）

用户在 sems 工作台运行发布命令（主仓 checkout，scripts/release/ 链），`build-frontend.ps1` 第一步 `pnpm install --frozen-lockfile`（ForgeSelf.Web）退出 1，发布链中断。

**根因（Verified，主仓原位复现 exit=1）**：`ForgeSelf.Web` 的 `pnpm install` 触发根 postinstall（`scripts/postinstall-install-hooks.cjs`，4a3f67f 落地）→ `install-git-hooks.ps1` 的 `Test-IsExecutable`（:61-75）**bash 分支在 `$IsWindows` 判定之前**——本机 bash 在 PATH（git-bash），但收到的是盘符:\... 形态的 Windows 路径，MSYS bash 内建 `test -x` 不识别反斜杠路径 → 恒 false → :94 throw「hook 安装失败（不可执行）」→ ELIFECYCLE 1。:67 的 `if ($IsWindows) { return $true }` 兜底在有 bash 的 Windows 机器上**永远不可达**。影响面：4a3f67f 之后本机（Windows + git-bash）一切 `pnpm install`（含 release 链 host-web 与全部 plugin-web 段）必挂；CI（linux）不受影响。

做到什么程度：Windows 上「可执行位」断言按 Windows 语义直接通过（NTFS 无 +x 概念，git 执行 hook 不看模式位；dispatcher +x 是 Linux 侧关切）；UnixFileMode 探测只在非 Windows 走；守卫落 RepositoryScriptTests（顺序断言）；发布链在本机恢复可跑。

## Spec（不变量）

- 不变量：**平台专属判定必须先于通用 POSIX 探测**——凡「Windows 无此概念」的检查（如可执行位），Windows 分支必须最先短路，不得让 POSIX 工具（bash/test/chmod）在 Windows 路径形态上裁决。
- postinstall 装 hook 失败仍保持响亮失败（不静默放行——2026-11-19「静默跳过仓库门禁」教训）；修的是误判，不是改成吞错。

## Plan（具体到真实文件）

1. `scripts/install-git-hooks.ps1`：`Test-IsExecutable` 把 `if ($IsWindows) { return $true }` 提到 bash 分支之前；`Set-ExecutableBit` 在 Windows 无 chmod 时静默跳过（去掉 UnixFileMode 警告噪声，NTFS 无该语义）。
2. `ForgeSelf.Api.Tests/RepositoryScriptTests.cs`：新增 G2 顺序守卫（红→绿：断言 `$IsWindows` 短路先于 `& bash -lc`）。
3. 主仓 checkout 同步同 patch（用户发布命令从主仓跑，必须当场解锁）+ 主仓原位复跑 `pnpm install --frozen-lockfile` 验证 exit=0。
4. 归档：PC-043 + case + problem-classes README 行。

## Task（Allowed/Forbidden + 门禁）

- Allowed：上列 4 处；Forbidden：不动 build-frontend/release-local 主流程、不动 postinstall 的响亮失败语义、不动 hook 内容。
- Gate1：auto-granted（§8.5 自动迭代 + 输入5 直接报障=修复指令 + never-ask 策略；留痕同前）。
- 门禁：**中档**（碰 scripts/** → 全量后端 dotnet test，与基线红对表）+ RepositoryScriptTests 定向 + 主仓原位 install 复跑。
- 交付：本批为脚本缺陷修复，无插件包；主仓 patch 即交付物。

## Evidence / Review

完成后回填：红灯（已留 repro 日志）、绿灯数字、全量后端与基线对表、主仓验证。Review 结论随汇报。
