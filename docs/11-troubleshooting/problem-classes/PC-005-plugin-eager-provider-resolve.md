---
id: PC-005
title: 插件工具/服务在构造期 eager 解析宿主 provider（或解析到错误容器），执行期恒空/异常
tags: [plugin-di, ctx-register, service-provider-scope]
invariant: 插件侧任何经 IContext / 宿主 provider 的服务解析必须发生在执行期（每次调用内建 scope 或 ctx.Get）；构造期 eager 解析早于宿主 ProvideHostServices seed、或解析到插件子容器 ⇒ 恒空 / IServiceScopeFactory 未注册
status: guarded
first_seen: 2026-10-08
recurrences: [2026-10-17-gateway-list-todos-scope]
cases: [2026-10-17-gateway-list-todos-scope]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Plugins/TodoTracker/UniversalGatewayListTodosScopeRegressionTests.cs}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/Architecture/PluginToolConstructorDiGuardTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/TodoTracker/UniversalGatewayListTodosScopeRegressionTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/Architecture/PluginToolConstructorDiGuardTests.cs, status: active}
lens:
  - 工具/服务类的构造函数里有没有 `provider.GetService` / `scope.ServiceProvider` 取宿主服务的动作（应改为执行期取）？
  - 单例 `Initialize`/seed 时序：被调用顺序在宿主 `ProvideHostServices` 之后吗？解析用的是哪个 provider（插件子容器 vs root）？
  - 热重载后缓存的实例字段是否失效（禁缓存，每次 Get）？
---
# PC-005 插件执行体构造期 eager 解析宿主 provider

> 事件经过见 `../cases/2026-10-17-gateway-list-todos-scope.md`。本条与 PC-001 相邻但**不变量不同**：PC-001 管「供给方没注册进共享表」，本条管「消费方解析时机/容器错误」。

## 1 不变量与诱因

既有同族教训（本会话记忆库「插件工具类服务解析铁律：禁止构造期缓存，统一执行期 scope」）+ 两起实证：
- 2026-10-08 AgentDelegationProvider/工具基类经插件 IContext 解析宿主服务恒空 → `AgentHubDi.ResolveHost`（Inferred，出自 10-08 evidence 偏差记录，未单独归档 case）；
- 2026-10-17 TodoTracker `list_todos` 工具在 Apply 期 eager 解析宿主 provider，早于 `ProvideHostServices` seed → `No service for type 'IServiceScopeFactory' has been registered.`（Verified，复现测试已锚定错误串）。

诱因：C# 构造注入习惯 + 「dev 单插件装配能跑通」的假象；seed 时序只在宿主启动尾段成立，工具注册表在 Apply 期就固化了错误的 provider 引用。

## 2 特征签名与扫描方法

现守卫 = G1 成功回归（复现批特征化测试由同日修复批 PILOT-062 翻转而来，`UniversalGatewayListTodosScopeRegressionTests`：仍在 seed 之前构造工具，断言成功封套）+ G2 架构守卫（`PluginToolConstructorDiGuardTests`：枚举插件工具类拦截构造期 eager 解析写法本身，扫描集合非空由阳性对照保证）。

## 3 Canonical fix

执行期解析：工具 `ExecuteAsync` 内 `using var scope = hostProvider.CreateScope()` 或 `ctx.Get<T>()` 每次取；参考实现：PILOT-062 修复批的 `ForgeSelf.Abstractions/PluginContextDiExtensions`（RequireHostProvider/CreateHostScope，未 seed 明确报错）与 `AgentHubDi.ResolveHost`（10-08 修复）。被否方案：把 seed 提前到 Apply 前（与「提供即 effect」的生命周期冲突，未见调研支持）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `UniversalGatewayListTodosScopeRegressionTests.cs` | dotnet test 全量 / CI | 只锁 list_todos 一条时序 |
| G2 | `PluginToolConstructorDiGuardTests.cs`（架构守卫：拦构造期 eager 解析写法） | dotnet test 全量 / CI | 反射动态取用的写法（靠 lens 问题 1 人工核对） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-10-17-gateway-list-todos-scope | 2026-10-17 | 通用网关 list_todos 报 IServiceScopeFactory 未注册 | 是（10-08 同族缺陷已修过一次，当时未做类级扫查/未建 PC——本 PC 即该逃逸的补账） |
