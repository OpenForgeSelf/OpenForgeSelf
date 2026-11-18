---
id: PC-007
title: 手敲命令起前后端的静默陷阱（范例 PC：G4 结构消除——脚本化使错误不可表达）
tags: [dev-stack, scripts, local-run]
invariant: 根 `pnpm dev` 使用 `scripts/dev-stack.ps1` 启动本地整栈，`pnpm run dev:web` 单独启动前端；禁止绕过脚本手动启动宿主/前端
status: structural
first_seen: 2026-10-08
recurrences: []
cases:
  - 2026-11-16-dev-api-multi-instance
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/RepositoryScriptTests.cs}
allowlist: []
guards:
  - {level: G4, path: scripts/dev-stack.ps1, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/RepositoryScriptTests.cs, status: active}
lens:
  - 根 `pnpm dev` 是否复用 `dev-stack.ps1` 整栈入口？若单独启前端是否使用 `dev:web` / `dev-plugin-web`？
  - 端口是否走动态认领（e2e/helpers/e2e-env.ts / FORGESELF_PORT），有没有硬编码 7102/7002/51888？
  - 根 `package.json` 的 `dev` 是否路由到整栈 `dev-stack.ps1`，前端-only 是否使用 `dev:web`？
---
# PC-007 手敲命令起前后端的静默陷阱（范例归档）

> 本条记录本地启动入口绕过脚本的同类不变量：根 `pnpm dev` 复用 `dev-stack.ps1` 启动整栈，`dev:web` 单独启动宿主前端；规则本体真源 = `AGENTS.md` §2.3 + `plugin-development` 铁律 20。此处登记类与 lens。

## 1 不变量与诱因

失败模式=「无声成功」：命令返回 0 / 端口没起 / 页面空白，排查者误以为环境问题。诱因：快捷手敲比读脚本门槛低。

## 2 特征签名与扫描方法

G4 后不设 grep（禁令已由流程入口承接）；登记完整性由 `ProblemClassRegistryTests`（守卫的守卫）保证——签名 S1=台账自检本身，防本条腐烂。

## 3 Canonical fix

根 `pnpm dev` 用 `scripts/dev-stack.ps1` 启动整栈（实际端口/令牌/PID → `.temp/dev-stack.json`）；前端单独开发用根 `pnpm run dev:web`；插件前端真 HMR 用 `scripts/dev-plugin-web.ps1`。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G4 | `scripts/dev-stack.ps1`（错误表达面消除） | 每次本地起栈 | Agent 无视规则手敲——靠 AGENTS §2.3 指针 + 本 lens |
| G2 | `RepositoryScriptTests.RootDevCommand_MustUseFullStackLauncher` | 根 package 开发入口回归 | testhost 未能启动时测试无法执行；脚本静态检查不能替代运行时验证 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| （历史散在 AGENTS §2.3，脚本落地即封口，不逐条补档） | 2026-10-08 | 手敲 dotnet 起宿主 exit 0 无端口 | — |
| [2026-11-16-dev-api-multi-instance](../cases/2026-11-16-dev-api-multi-instance.md) | 2026-11-16 | 根 `dev` 最终入口调整为复用整栈脚本，消除入口混淆 | 是，补 G2 入口守卫 |
