---
id: PC-007
title: 手敲命令起前后端的静默陷阱（范例 PC：G4 结构消除——脚本化使错误不可表达）
tags: [dev-stack, scripts, local-run]
invariant: 本地起宿主/前端只允许 `pwsh -NoProfile -File scripts/dev-stack.ps1`（停止 `-Stop`）；手写 `dotnet <dll>` / `npx vite` 存在两个静默陷阱（--console 首参缺失 → exit 0 无报错；esbuild 写 deps_temp 沙箱 Access denied → 崩）
status: structural
first_seen: 2026-10-08
recurrences: []
cases: []
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs}
allowlist: []
guards:
  - {level: G4, path: scripts/dev-stack.ps1, status: active}
lens:
  - 要起的进程是否已有脚本入口（dev-stack / dev-plugin-web）？没有的话先脚本化再干活（AGENTS §0 核心原则 4）
  - 端口是否走动态认领（e2e/helpers/e2e-env.ts / FORGESELF_PORT），有没有硬编码 7102/7002/51888？
---
# PC-007 手敲命令起前后端的静默陷阱（范例归档）

> **本条是「做对的范例」之二**：解法不是文档提醒，而是 `dev-stack.ps1` 把两个陷阱内置规避（--console 恒首参 + probe-esbuild-write 探测后按需关预打包 + 端口占用自动顺延），规则本体真源 = `AGENTS.md` §2.3 关键约定 + `plugin-development` 铁律 20。此处登记类与 lens。

## 1 不变量与诱因

失败模式=「无声成功」：命令返回 0 / 端口没起 / 页面空白，排查者误以为环境问题。诱因：快捷手敲比读脚本门槛低。

## 2 特征签名与扫描方法

G4 后不设 grep（禁令已由流程入口承接）；登记完整性由 `ProblemClassRegistryTests`（守卫的守卫）保证——签名 S1=台账自检本身，防本条腐烂。

## 3 Canonical fix

用 `scripts/dev-stack.ps1`（实际端口/令牌/PID → `.temp/dev-stack.json`）；插件前端真 HMR 用 `scripts/dev-plugin-web.ps1`。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G4 | `scripts/dev-stack.ps1`（错误表达面消除） | 每次本地起栈 | Agent 无视规则手敲——靠 AGENTS §2.3 指针 + 本 lens |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| （历史散在 AGENTS §2.3，脚本落地即封口，不逐条补档） | 2026-10-08 | 手敲 dotnet 起宿主 exit 0 无端口 | — |
