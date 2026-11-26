---
id: PC-048
title: 发布链 pnpm install 依赖 TTY 确认，无 TTY（智能终端/管道）下清 modules 提示直接 abort 中断发布
tags: [发布链, pnpm, 非TTY, 交互确认, 跨平台]
invariant: 发布链 pnpm install 不得依赖 TTY 交互确认——pnpm 检测到 node_modules 需重建时弹 Proceed?(Y/n)，无 TTY 环境直接 ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY；必须显式 --config.confirmModulesPurge=false（等价人工按 Y，且保留 --frozen-lockfile）
status: guarded
first_seen: 2026-11-24
recurrences: []
cases: [2026-11-24-release-local-pnpm-nontty-purge-confirm]
signature:
  - {id: S1, kind: grep, glob: "scripts/release/build-frontend.ps1", regex: "\\$pnpm install --frozen-lockfile(?!.*--config\\.confirmModulesPurge=false)", expect: no-match}
  - {id: S2, kind: test, path: "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs", status: active}
lens:
  - 任何发布/构建脚本里调用 pnpm install 时，若执行环境可能是 CI / 智能终端 / 管道（无 TTY），必须显式关闭需要交互的确认（confirmModulesPurge=false），不得假设有人值守按 Y。
  - 排查「pnpm install 在无 TTY 挂掉」时先区分三类根因：本类（清 modules 交互确认）、PC-043（postinstall 平台短路）、PC-047 邻域（safe-delete 批量删除守卫）；报错文案与触发点不同。
  - 关闭 purge 确认不等于放弃依赖一致性：--frozen-lockfile 仍必须保留，清目录重装后依赖版本由 lockfile 锁死。
---
# PC-048 发布链 pnpm install 无 TTY 清 modules 确认中断

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

**不变量**：脚本化的 `pnpm install` 不得依赖 TTY 交互确认。pnpm 在 node_modules 被判定需要重建时弹 `The modules directory at "..." will be removed and reinstalled from scratch. Proceed? (Y/n)`；有 TTY 时人工按 Y 即可，无 TTY（智能终端、CI 未设 `CI=true`、管道）时直接 `ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY` 退出，发布链 `release-local.ps1` 中断、zip 不产出。

**为什么会犯**：
- `build-frontend.ps1` 头部自称 "Usable locally and in CI"，但 CI（GitHub Actions 默认 `CI=true`）会静默自动确认，开发者手动终端有 TTY 可按 Y，唯独「无 TTY 且无 CI 变量」的智能终端场景没有覆盖——这是脚本契约的隐形第三态。
- pnpm 的确认提示只在 node_modules 状态异常时出现，平时测试跑不到；不是每次发布都触发，所以长期潜伏。

## 2 特征签名与扫描方法

- **S1**：grep `scripts/release/build-frontend.ps1` 中不带 `--config.confirmModulesPurge=false` 的 `$pnpm install --frozen-lockfile` 调用即红。
- **S2**：G2 枚举测试 `ProblemClassEnumerationTests.Pc048_BuildFrontendPnpmInstall_MustDisablePurgeConfirm`，断言 install 调用同时含 `--frozen-lockfile` 与 `--config.confirmModulesPurge=false`（含阳性对照，防空转假绿）。
- **盲区**：只扫 build-frontend.ps1 这一条发布链调用；其它脚本（`publish-plugin-full.ps1:50` 只在 node_modules 缺失时裸 install，无 purge 提示路径；CI workflow 自带 `CI=true`）当前不受影响，若将来改成发布链入口须同步纳入签名。

## 3 Canonical fix

`scripts/release/build-frontend.ps1` 的 `Invoke-PnpmBuild` 内：

```
& $pnpm install --frozen-lockfile --config.confirmModulesPurge=false
```

中文注释说明非 TTY 理由。依据：pnpm 官方报错文案明确给出 `confirmModulesPurge=false` 作为非交互解法；该开关只关这一个确认，`--frozen-lockfile` 的锁文件约束不变。

**被否方案**：
- `CI=true` 全局设置 → 影响面大（frozen-lockfile 默认、其它读 CI 的工具行为），且污染调用方环境；不如单参数精确。
- `.npmrc` / pnpm config 全局关 → 改的是所有项目的用户级配置，越界。
- 调用方命令临时加环境变量 → 靠人记，非根因，下次别的调用方照样挂。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（Pc048…） | `dotnet test` | 只覆盖 build-frontend.ps1 调用点 |
| G3 | `scripts/check-problem-classes.ps1` S1 签名 | pre-commit / 归档校验 | 正则对同一行多参数敏感，换行写法需复核 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| [2026-11-24-release-local-pnpm-nontty-purge-confirm](../cases/2026-11-24-release-local-pnpm-nontty-purge-confirm.md) | 2026-11-24 | 智能终端跑 `release-local.ps1 -Sign` 遇 pnpm 清 modules 确认，无 TTY 直接 abort | 否（首次） |
