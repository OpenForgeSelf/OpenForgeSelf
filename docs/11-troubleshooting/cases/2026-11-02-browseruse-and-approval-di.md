---
id: 2026-11-02-browseruse-and-approval-di
title: 浏览器插件缺 Playwright，审批与成本控制器构造注入宿主契约
pc: PC-020
severity: P1
evidence: Verified
---
# Case：2026-11-02 浏览器插件与控制器构造注入

> 规则见 [PC-020](../problem-classes/PC-020-plugin-dependency-unresolved.md)。

## 1 现象

本地开发栈后端未就绪。日志是映射控制器时找不到 `Microsoft.Playwright, Version=1.62.0.0`。补上复制并重启后，`GET /api/ai-agent/approvals` 仍 500：`Unable to resolve service for type 'ForgeSelf.Abstractions.IApprovalService'`，激活 `ApprovalsController`。

## 2 复现红灯

NuGet：临时去掉 BrowserUse 的 `CopyLocalLockFileAssemblies`，`dotnet test --filter 插件独有第三方包必须复制到输出目录` 失败 1 / 通过 0。日志 `.temp/tmp/plugin-load-guard-red.log`。恢复该属性后同一过滤与 `ProblemClassRegistryTests` 一起 7 通过，日志 `.temp/tmp/plugin-load-guard-green.log`。

控制器：当天开发栈日志已有审批 500。守卫扫源码，在改 `ProjectController` 与 `CostController` 之前会点名这两处；改完后的 30 通过里包含该事实与 `CostControllerEndpointTests`，日志 `.temp/tmp/plugin-load-guard.log`。

## 3 根因（5 Whys + 第 6 问）

1. 后端为什么起不来？→ 插件目录没有 Playwright DLL。
2. 为什么没有？→ BrowserUse 是带新 NuGet 的类库，没开复制。
3. 审批列表为什么 500？→ 控制器构造参数要 `IApprovalService`，插件子容器没有。
4. 为什么写的人会这样写？→ 普通控制器都是构造注入；宿主契约清单虽然 seed 进了 `IContext`，并不进入插件 MS DI。
5. 为什么 10 月的事故没挡住 11 月？→ 只有 TODO，插件总入口技能当时没写，也没有测试。
- **第 6 问：** 开工读 `plugin-development`，不读半年前的 evidence。条文不在入口、没有红灯，就会再写一遍。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| ApprovalsController 构造注入 IApprovalService | 是 | 已改为 IContext + Get |
| ProjectController 构造注入 IApprovalService | 是 | 本回合改为用时 Get |
| CostController 构造注入 ITurnTelemetryQuery | 是 | 本回合改为 IContext + Get；单测改为 Mock IContext |
| 其余 Plugins/**/Controllers 构造参数 | 否 | 守卫扫过，不在宿主契约清单里 |
| AIWorkflowController 的 IServiceProvider | 否 | 容器自带，不是这条 500；拿到的是插件容器，归 PC-005 |

## 5 修复与被否方案

见 PC canonical fix。被否：在插件 Apply 里把宿主契约再 Add 进子容器。

## 6 重构提案与结论

R0。不改插件加载器。`PluginLoadContext` 的同目录回退留着当兜底，不代替复制开关。

## 7 守卫落地

G2 `PluginLoadDependencyGuardTests`。二次复发的逃逸是「待办代替测试」。
