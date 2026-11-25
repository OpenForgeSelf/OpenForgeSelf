---
id: 2026-11-24-release-local-pnpm-nontty-purge-confirm
title: 智能终端跑 release-local.ps1 -Sign 遇 pnpm 清 modules 交互确认，无 TTY 直接 abort
pc: PC-048
severity: P1
evidence: Verified
---
# Case：2026-11-24 发布链 pnpm install 无 TTY 清 modules 确认中断

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC，不复述。

## 1 现象

用户在智能终端执行 `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\release\release-local.ps1 -Sign`，发布链跑到 `build-frontend.ps1` 的插件 web install 段报：

```
--- plugin-web/AIAgent ...
 ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY  Aborted removal of modules directory due to no TTY
If you are running pnpm in CI, set the CI environment variable to "true", or set "confirmModulesPurge" to "false".
--- plugin-web/SmartTerminal ...
 ? The modules directory at "...\Plugins\SmartTerminal\web\node_modules" will be removed and reinstalled from scratch. Proceed? (Y/n) » true
```

非 TTY 直接 abort，zip 不产出。出处：输入34 用户原文（Verified）。

## 2 复现红灯

- 用户现场错误（Verified，输入34）：非 TTY 下 `ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY`。
- 本机环境核验（Verified）：`pnpm --version`=10.13.1（shell）/10.33.1（fixture 实际），`CI=unset`，`test -t 0`=no（确无 TTY）。
- 可重复最小 fixture 构造「modules 需重建」提示：3 次尝试（删 `.modules.yaml`、加 npm `package-lock.json` 标记、篡改 `layoutVersion`）均未触发 pnpm 的 purge 提示（pnpm 10.33.1 判定逻辑更严），按熔断停止；红灯以用户现场输出为 Verified 证据，不强造。

## 3 根因（5 Whys + 第 6 问）

1. 为什么发布中断？→ pnpm install 退出非 0，`Assert-ExitCode` 抛错。
2. 为什么 install 退出？→ pnpm 要清 node_modules 重装，弹交互确认。
3. 为什么确认会 abort？→ 智能终端无 TTY，pnpm 无法读取 Y/n，按设计直接 abort。
4. 为什么 CI 没这问题？→ GitHub Actions 默认 `CI=true`，pnpm 自动确认；脚本头部声称 "Usable locally and in CI"，但只覆盖了这两端，漏了「无 TTY 且非 CI」第三态。
5. 为什么不早发现？→ 提示只在 node_modules 状态异常时出现，平时 install 不触发。
- **第 6 问**：为什么脚本一开始没写对？→ Spec/Plan 阶段没有「非交互执行路径不得依赖 TTY 确认」这条不变量，也没有守卫扫 install 参数；靠 prose 自称 "Usable in CI" 而无机器校验。→ 本批固化 PC-048 + G2 守卫 + lens。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置 |
|-----------------|----------|------|
| `scripts/release/build-frontend.ps1:39` | 是（发布链唯一走 `--frozen-lockfile` 的 install 调用） | 已修（加 `--config.confirmModulesPurge=false`） |
| `.github/workflows/frontend-check.yml`（2 处 install） | 否（CI 自带 `CI=true`，pnpm 自动确认；且非本地智能终端路径） | 不修，纳入 PC-048 lens 提示 |
| `scripts/publish-plugin-full.ps1:50` | 否（仅 node_modules 缺失时裸 install，无「已存在需 purge」路径） | 不修，PC-048 盲区已记载 |
| pilot 文档 / `.agents/skills/**` 中的 `pnpm install` 示例 | 否（非执行路径，示例命令由人按环境决定） | 不修 |

同根因命中 1 处（即本批所修），无扩散。

## 5 修复与被否方案

最终修法：`build-frontend.ps1:39` 加 `--config.confirmModulesPurge=false` + 中文注释，详见 PC-048 §3。

被否：① `CI=true` 全局设置（影响面大、污染调用方）；② `.npmrc`/pnpm 全局 config（越界改用户配置）；③ 只在调用命令临时加环境变量（靠人记、非根因）。三案对比见输入34 汇报。

## 6 重构提案与结论

R0 不重构。理由：单点参数修复 + G2 守卫即可覆盖，无跨模块契约变更；`release-local.ps1` 主流程不动。

## 7 守卫落地

- G2：`ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs` 新增 `Pc048_BuildFrontendPnpmInstall_MustDisablePurgeConfirm`，断言 install 同时含 `--frozen-lockfile` 与 `--config.confirmModulesPurge=false`（含阳性对照，防文件缺失假绿）。
- G3：`check-problem-classes.ps1` 由 PC-048 front-matter `signature.S1` 驱动，pre-commit 扫描。

## 8 逃逸分析（首见）

N/A 首次。PC-043/047 是邻域（同为发布链 install 失败族），但根因不同（平台短路 / safe-delete），本批非复发。

## 9 基建改进

新增 PC-048 问题类 + lens（脚本化 install 不得依赖 TTY 确认），后续新发布脚本/新调用点在设计期即被问到。

## 10 复用赋能

任何新增/改造发布脚本、CI workflow、插件构建脚本中调用 `pnpm install` 时，必须引用 PC-048 lens 自检非交互路径；与 PC-043/047 lens 合为「发布链 install 三问」。

## 11 证据

| 结论 | 等级 | 出处 |
|------|------|------|
| 非 TTY 下 pnpm purge 确认 abort | Verified | 输入34 用户原文报错 |
| 本机确无 TTY、CI 未设 | Verified | `test -t 0`=no；`CI=unset` |
| 参数被 pnpm 接受、install exit=0 | Verified | `.temp/pnpm-nontty-repro` fixture 实测 `pnpm install --frozen-lockfile --config.confirmModulesPurge=false` |
| 发布链单插件真跑通过 | Verified | `build-frontend.ps1 -PluginsOnly -Plugin SmartTerminal` exit=0，SmartTerminal dist 产出（05-evidence） |
| PC-048 G2 守卫通过 | Verified | `dotnet test --filter Pc048` （05-evidence） |
