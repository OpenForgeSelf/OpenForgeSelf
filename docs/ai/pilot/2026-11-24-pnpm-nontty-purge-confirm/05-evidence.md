# 05 Evidence — 发布链 pnpm install 非 TTY 清 modules 确认中断（输入34/35）

## Changed Files

| 文件 | 变更 |
|------|------|
| `scripts/release/build-frontend.ps1` | `:39` install 加 `--config.confirmModulesPurge=false` + 中文注释（4 行） |
| `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs` | 末尾新增 `Pc048_BuildFrontendPnpmInstall_MustDisablePurgeConfirm` G2 守卫 |
| `docs/ai/pilot/2026-11-24-pnpm-nontty-purge-confirm/mini-task.md` | 轻量工件（Intent/Spec/Plan/Task） |
| `docs/11-troubleshooting/problem-classes/PC-048-pnpm-install-nontty-purge-confirm.md` | 新问题类真源 |
| `docs/11-troubleshooting/cases/2026-11-24-release-local-pnpm-nontty-purge-confirm.md` | 事件 case |
| `docs/11-troubleshooting/problem-classes/README.md` | tags 行 + PC 台账行 |
| `docs/11-troubleshooting/cases/README.md` | case 索引行 |
| `docs/11-troubleshooting/index.md` | 现象速查行 |
| `.forgeself/memory/2026-11-24.md` / `TODO.md` | 预飞日志 + 待办（完成即移除队列） |

## Validation（Verified / Inferred / Unknown 分级）

| 项 | Command | Result | 等级 |
|----|---------|--------|------|
| 红灯（用户现场） | `pwsh … release-local.ps1 -Sign`（智能终端） | `ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY` + `(Y/n)»true` 提示，发布中断 | Verified（输入34 原文） |
| 环境无 TTY | `test -t 0` / `echo $CI` | `no` / `unset` | Verified |
| 参数接受度 | `.temp/pnpm-nontty-repro`：`pnpm install --frozen-lockfile --config.confirmModulesPurge=false` | `Done in 3.4s`，exit=0；fixture 内 pnpm 10.33.1 接受参数 | Verified |
| 发布链单插件真跑 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/build-frontend.ps1 -PluginsOnly -Plugin SmartTerminal` | `Already up to date` → `vite build … ✓ built in 560ms` → `frontend: plugin webs ok (27.4s)` → `all done`，**exit=0**；产物 `dist/style.css 17.82 kB` + `dist/index.js 84.58 kB` | Verified |
| G2 守卫绿灯 | `dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~ProblemClassEnumerationTests.Pc048" -v q --nologo` | **通过: 1，失败: 0**，exit=0（编译 warning xUnit1031 为存量噪声，非本批） | Verified |
| PC 机器校验 | `pwsh -File scripts/check-problem-classes.ps1` | `check-problem-classes OK：48 个 PC 签名/allowlist/guards 全部通过`，含 PC-048；exit=0（输出中文乱码为控制台码页噪声，不影响判定） | Verified |

## 红灯→绿灯说明

用户现场红灯为真实发生（非本会话构造）。最小 fixture 尝试 3 种方式触发 pnpm purge 提示未成（pnpm 10.33.1 判定更严），按试错熔断停止；红灯证据以用户现场为准，不强造。绿灯由「参数接受度实测 + 发布链单插件真跑 + G2 守卫」三重证据闭合。

## Known Limitations

- 未跑完整 `release-local.ps1 -Sign`（涉及签名 + 15 插件链，本机沙箱 safe-delete 配额另有 TODO P1；且 `-Sign` 曾有待处理的外部死锁进程）——本批只验证到 install 不再因 TTY 中断 + 单插件构建成功。
- 未构造出可重复的 purge 提示红灯（见上）；G2 守卫用「参数存在性断言」而非行为级复现，行为级证据由用户现场 + 单插件真跑补位。

## Unresolved Issues

- 完整发布链签名批未跑（环境约束，非本批引入）→ 已在 TODO「safe-delete 配额发布链」项跟踪。
