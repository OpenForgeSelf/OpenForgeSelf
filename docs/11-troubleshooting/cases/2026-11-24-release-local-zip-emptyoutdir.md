---
id: 2026-11-24-release-local-zip-emptyoutdir
title: 插件 web 构建 emptyOutDir:true 触发沙箱 safe-delete 批量删除守卫，本地发布链首插件即中断
pc: PC-047
severity: P1
evidence: Verified
---
# Case：2026-11-24 发布本地 zip 被 safe-delete 批量删除守卫拦截

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC，不复述。

## 1 现象

用户执行 `pwsh scripts/release/release-local.ps1` 打本地 zip 包失败。首次调用因工具默认 120s 超时中断（日志停在 AgentHub 构建中，无真实报错）；足量超时复现后拿到真错：`pnpm build (plugin-web/AgentHub) failed with exit code 1`，日志正文 `[safe-delete][SAFE_DELETE_BULK_CONFIRM_REQUIRED] {count:50, threshold:50, scope:turn, target:.../Plugins/AgentHub/web/dist/index.js}` at `vite emptyDir → prepareOutDir`。用户 11:29 外部启动的 `-Sign` 进程（PID 67036）同样卡在 pnpm install 19 分钟、CPU 恒 0.55s（死锁，属另一环境现象，未触碰）。

## 2 复现红灯

`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1`，输出重定向 `.temp/release-local-run2.log`：
- 时间 11:50:47 → 11:52:24，exit 1。
- `Exception: release-lib.ps1:26 … pnpm build (plugin-web/AgentHub) failed with exit code 1`。
- 日志 `error during build: [safe-delete][SAFE_DELETE_BULK_CONFIRM_REQUIRED] … target: .../dist/index.js` at `emptyDir → prepareOutDir`。

## 3 根因（5 Whys + 第 6 问）

1. 为什么发布链挂？→ AgentHub 的 `pnpm build` exit 1。
2. 为什么 build 挂？→ vite 构建前清空 dist（`emptyDir`）时 `fs.rmSync` 被拒。
3. 为什么被拒？→ 本机 WorkBuddy 沙箱注入 `node-safe-delete-shim`，单次 tool call 删除配额 49，超限即 `SAFE_DELETE_BULK_CONFIRM_REQUIRED`。
4. 为什么只有插件挂、宿主 web 过？→ 宿主 `ForgeSelf.Web/vite.config.ts:31` 已设 `emptyOutDir: false`，15 个插件全是 `true`。
5. 为什么插件没同步？→ 横切配置从初始模板就分叉，无类级守卫扫全部插件 config。
- **第 6 问**：为什么守卫没让我写对？→ 宿主 SOP（`doc-reverse-sync-sop.md:49`）只记「宿主已设 false」，未固化为「所有插件必须 false」的枚举守卫；新增插件复制模板时无人拦截。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置 |
|-----------------|----------|------|
| Plugins/AIAgent/web/vite.config.ts:23 | 是 | 已修 false |
| Plugins/AgentHub/web/vite.config.ts:21 | 是（首发） | 已修 false |
| Plugins/ClockBadge/web/vite.config.ts:19 | 是 | 已修 false |
| Plugins/CostScope/web/vite.config.ts:24 | 是 | 已修 false |
| Plugins/DbAdmin/web/vite.config.ts:9 | 是 | 已修 false |
| Plugins/DesignSystem/web/vite.config.ts:20 | 是 | 已修 false |
| Plugins/FileTools/web/vite.config.ts:22 | 是 | 已修 false |
| Plugins/Home/web/vite.config.ts:23 | 是 | 已修 false |
| Plugins/ImGateway/web/vite.config.ts:21 | 是 | 已修 false |
| Plugins/McpCenter/web/vite.config.ts:23 | 是 | 已修 false |
| Plugins/QuickLinks/web/vite.config.ts:22 | 是 | 已修 false |
| Plugins/Sems/web/vite.config.ts:15 | 是 | 已修 false |
| Plugins/SmartTerminal/web/vite.config.ts:19 | 是 | 已修 false |
| Plugins/TodoTracker/web/vite.config.ts:23 | 是 | 已修 false |
| Plugins/ToolBridge/web/vite.config.ts:18 | 是 | 已修 false |
| ForgeSelf.Web/vite.config.ts:31 | 否（已设 false，正例） | 无需改 |

## 5 修复与被否方案

canonical fix（PC-047 §3）：15 个插件 `emptyOutDir: true` → `false`，已 Verified（grep 0 命中、diff 15 行、G2 测试 Passed）。被否：全局关 shim / 调阈值（绕过安全守卫、非仓库根治）；改 shim 白名单（仓库外不可改）。

## 6 重构提案与结论

R0 不重构。理由：修法是 15 个同构配置行的统一值调整，无跨模块契约变更；G2 守卫已足够覆盖「新插件复制模板写错」的复发面。

## 7 守卫落地

- G2：`ForgeSelf.Api.Tests/ProblemClassEnumerationTests.Pc047_PluginViteConfigs_MustNotSetEmptyOutDirTrue`，遍历全部 `Plugins/*/web/vite.config.ts` 断言无 `emptyOutDir: true`，含阳性对照（config 数 ≥13）。`dotnet test --filter …Pc047…` = Passed（Verified，`.temp/pc047.trx`）。
- G3：PC-047 signature S1 grep + `scripts/check-problem-classes.ps1`。

## 8 逃逸分析（首见）

N/A 首次。宿主侧 `emptyOutDir:false` 是早先批次按 SOP 手工设的，未升级为跨插件类守卫——本次通过 PC-047 + G2 补齐该缺口。

## 9 基建改进

- PC-047 台账 + G2 枚举守卫，把「宿主 SOP 的手工约定」升级为「全插件自动校验」。
- 新增 blade：发布链排障须区分「vite 清空 dist（代码侧可修）」与「pnpm install 临时文件（沙箱配额，非仓库 bug）」——后者见 §遗留。

## 10 复用赋能

新建插件 web、迁移插件构建配置、发布链排障必须引用 PC-047。lens 三条已写入 PC front-matter，供后续 Spec Failure Modes 预填。

## 11 证据

| 结论 | 等级 | 出处 |
|------|------|------|
| 发布链真实报错 = safe-delete bulk guard 拦 vite 清空 dist | Verified | `.temp/release-local-run2.log:72-84` |
| 15 插件全改为 emptyOutDir:false，0 残留 | Verified | grep + `git diff --stat`（15 files, 15 +/15 -） |
| G2 守卫通过 | Verified | `dotnet test … --logger trx`，`Pc047… OUTCOME=Passed` |
| 修复后 AgentHub 构建通过（emptyOutDir 修复生效） | Verified | `.temp/release-local-fixed.log` 前段构建通过，失败点前移到 AIAgent pnpm install |
| 既有 PC-034 测试假红（Sequence contains more than one element，PC-034 双文件） | Verified | `.temp/pc047.trx`；与本任务无关，记 TODO |

## 遗留（非本类根因，沙箱环境约束）

修复 emptyOutDir 后，发布链在 AIAgent 的 `pnpm install` 清理 `_tmp_*` 临时文件时再次触发同一 bulk guard（`.temp/release-local-fixed.log:78`）。这是沙箱「单次 tool call 删除配额 49」的环境约束，15 插件链在一个 tool call 内必然累计超限，**不是仓库代码 bug**，不属 PC-047。已记 TODO，交用户拍板环境层解法（拆分多次工具调用 / 临时调高阈值 / 换无 shim 环境执行）。
