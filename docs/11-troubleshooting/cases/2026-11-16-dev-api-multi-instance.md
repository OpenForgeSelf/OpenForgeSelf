---
id: 2026-11-16-dev-api-multi-instance
title: 根 API 开发入口未隔离并行实例
pc: PC-007
severity: P1
evidence: Verified
---
# Case：2026-11-16 根开发入口复用整栈脚本

> Case 只讲这一次发生了什么；规则、守卫和 canonical fix 见 [PC-007](../problem-classes/PC-007-manual-dev-launch-silent-trap.md)。

（历史验证）早期 API-only 试验中，两实例并行构建、健康及退出隔离均通过；该启动器随后按用户最新入口要求移除，不作为当前 `pnpm dev` 的验收证据。

用户先要求将启动脚本接入根开发命令，之后指出仓库已有 `dev-stack.ps1`，并明确最终需求为 `pnpm dev` 启动前后端。检查确认根 `dev` 不应另设 API-only 生命周期；最终将它改为直接调用 `dev-stack.ps1`，删除不再使用的 `dev-api.ps1`，保留 `dev:web`。

## 2 复现红灯

初始 API-only 试验测试被 testhost 在断言执行前中止：`Win32Exception (5)`，栈位于 `ProcessManager.OpenProcess` / `DefaultEngineInvoker.SetParentProcessExitCallback`；全量 `dotnet test` 也遇到同一环境异常。切换为整栈入口后，本轮真实运行 `pnpm dev` 并分别验证后端健康端点和前端首页均 HTTP 200；更新后的 G2 用例仍在相同 testhost 启动阶段被阻塞，不能记为通过或断言失败。


## 3 根因（5 Whys + 第 6 问）

1. 为什么早期实现出现额外的 API-only launcher？用户最初要求将一个 PowerShell 脚本接到根开发命令。
2. 为什么继续维护了重复入口？实现时未先把根命令的预期与现有整栈脚本对齐。
3. 为什么需求会在过程中收敛？用户明确指出已有 `dev-stack.ps1`，并进一步确认根 `pnpm dev` 应启动前后端。
4. 为什么早期实现没有被守卫阻止？`RepositoryScriptTests` 尚未固定根 `dev` 必须调用 canonical 全栈入口。
5. 为什么最终纠偏可控？用户指令明确后，删除临时 API-only 脚本，将根命令接回 `dev-stack.ps1`，并把入口契约固化为 G2 守卫。
- **第 6 问：为什么真实行为验证仍不足以完成闭环？** 测试运行器在启动 testhost 时因 `OpenProcess` 权限异常退出；已将其与断言结果区分，并把重跑留作阻塞项。

## 4 同类扫查命中表

| 位置（文件） | 同根因？ | 处置 |
|-----------------|----------|-------------------------------|
| `package.json` 根 `scripts.dev` | 是 | 根 `dev` 改为调用现有 `dev-stack.ps1`；保留 `dev:web`；增加 G2 守卫 |
| `scripts/dev-stack.ps1` | 是，现有的全栈 canonical entry | 保持其单实例状态文件和停止行为；由根 `pnpm dev` 复用 |
| `scripts/dev-plugin.ps1` | 否，只构建插件并请求既有 dev 宿主热重载 | 保持原状 |
| `scripts/dev-plugin-web.ps1` | 否，只启动插件前端 HMR | 保持原状 |
| `scripts/dev-api.ps1` | 中途曾新增，最终按用户要求移除 | 不再维护第二套 API-only 生命周期 |

## 5 修复与被否方案

按用户最新指示将根 `pnpm dev` 接到既有 `scripts/dev-stack.ps1`，让默认命令启动宿主 API 与前端；保留 `dev:web` 单独启动前端，并移除重复的 `scripts/dev-api.ps1`。既有 dev-stack 的单实例状态文件与停止语义保持不变。

## 6 重构提案与结论

R0：不增加新的启动管理器或公共接口。根 package 接线和现有 PowerShell 启动器足以表达本任务需求；保留 `dev-stack.ps1` 的单实例状态生命周期。

## 7 守卫落地

将 `RepositoryScriptTests.RootDevCommand_MustUseFullStackLauncher` 作为 PC-007 G2，断言根 `dev` 路由到 `dev-stack.ps1`、`dev:web` 保留，并检查该脚本同时启动 dotnet 后端与 node 前端且保留状态文件管理。测试项目构建通过；testhost 权限问题导致用例未执行。全栈实际运行已验证：后端 `/api/health` 与前端首页均 HTTP 200。

逃逸原因：根 package 入口曾另行实现 API-only 语义，没有复用已存在的全栈入口，造成重复维护并引起用户预期偏差。本次把根命令路由纳入 G2 测试并回写单一入口说明；单测运行仍受本机 testhost 权限阻塞。

## 8 逃逸分析

前案：PC-007 已有整栈启动脚本，但根 package 接线未固定，早期方案还新增了 API-only 脚本。缺口是入口复用与预期未由 G2 固定。上浮动作：根命令 G2 守卫检查 `dev-stack.ps1`，更新 AGENTS 与文档索引；守卫测试等待 testhost 环境恢复后复验。

## 9 基建改进

根 `pnpm dev` 是完整开发栈入口；`pnpm run dev:web` 是前端-only 入口。API-only 多实例 launcher 已移除；若未来确有需求，应先明确独立命令及生命周期，不替换根默认入口。

## 10 复用赋能

后续修改根 package 开发命令时引用 PC-007 lens；守卫名为 `RootDevCommand_MustUseFullStackLauncher`。此前 API-only 双实例行为曾实测通过，但不代表最终全栈入口验收；本轮全栈运行与 testhost 恢复后的自动化守卫仍需验证。

## 11 证据

| 结论 | 等级 | 出处 |
|------|------|------|
| 根 `pnpm dev` 指向整栈脚本，`dev:web` 保留，API-only 脚本已删除 | Verified | `package.json`、`scripts/dev-stack.ps1`、本任务文件状态 |
| `pnpm dev` 启动前后端并成功探活 | Verified | `.temp/dev-stack.json` 地址/PID；后端 `/api/health` HTTP 200，前端首页 HTTP 200 |
| G2 xUnit 用例执行通过 | Unknown | testhost 在断言启动前因 `Win32Exception (5)` / `OpenProcess` 权限拒绝中止 |
| 测试项目编译 | Verified | `dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --no-restore --nologo`：28 projects、0 errors |
| 早期两个 API-only 实例健康与退出隔离 | Verified（历史，不作当前验收） | 早期试验记录：端口 52332 / 61346；最终实现已移除该 launcher |
| 全量后端 xUnit 通过 | Unknown | 当前环境未获得可用的全量测试结果；testhost 权限异常仍阻塞 |
