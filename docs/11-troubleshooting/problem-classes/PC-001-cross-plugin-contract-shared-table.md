---
id: PC-001
title: 跨插件契约只进 IServiceCollection、未进共享服务表
tags: [plugin-contract, ctx-register, plugin-architecture]
invariant: 被其他插件（或宿主控制器经插件子容器）用 ctx.Get<T>() / 构造注入消费的契约，提供方必须在 Apply 期 ctx.Register<T>()（或宿主 seed 清单）；RegisterLocal/AddScoped 只对本上下文可见
status: guarded
first_seen: 2026-10-10
recurrences: [2026-10-10-workflow-engine-contract-not-registered]
cases: [2026-10-10-workflow-engine-contract-not-registered]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs}
allowlist:
  - {path: "Plugins/Scheduler/SchedulerPlugin.cs", reason: "守卫首跑真实命中（2026-10-17 Verified，G2 红）：StopScheduler 的 ctx.Get<ISchedulerHost>() 无任何注册/seed 路径恒 null，修复属插件六步闭环另起待办「修复：Scheduler 卸载不停止调度器」", expires: 2026-11-15}
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/WorkflowEngineTests/WorkflowEnginePluginRegistrationTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs, status: active}
lens:
  - 我新提供/消费的跨插件契约（Abstractions 里的接口），提供方是否 ctx.Register<T>() 进共享表，而只在 IServiceCollection AddScoped？
  - 消费方是否「每次用每次 ctx.Get<T>()」、禁止缓存为字段？接缝缺席时是否降级可解释（null → 503 + 文案）而非静默成功？
  - 插件控制器构造函数注入的宿主契约，是否同时在宿主 seed 清单（PluginManager.HostProvidedServiceContracts / ProvideHostServices）里？
---
# PC-001 跨插件契约只进 IServiceCollection、未进共享表

> 本文件是「这一类问题」的唯一真源。事件经过见 `../cases/2026-10-10-workflow-engine-contract-not-registered.md`。

## 1 不变量与诱因

`IContext`（`ForgeSelf.Core/IContext.cs:7-25`）语义：`Register<T>` 写 root 共享服务表（兄弟插件可见、提供即 effect）；`RegisterLocal<T>` 仅本上下文；`Get<T>` 解析序 = 本地值 → 共享表，缺席返回 **null 不抛错**。插件 DI 子容器只含插件自身服务 + IContext（architecture-design 铁律 4）。

**为什么会犯**：ASP.NET Core 肌肉记忆是 `services.AddScoped<TService, TImpl>()`；控制器/工具类构造注入在开发期单插件装配下经常"碰巧能解析"，一旦走插件子容器或兄弟插件 ctx.Get 就恒空/500。2026-10-10 首案即 WorkflowEngine 三契约只 AddScoped，AIAgent 侧表现为「工作流服务没激活」。

## 2 特征签名与扫描方法

- 主守卫为**枚举式测试**（BR4：能枚举就不 grep）：`ProblemClassEnumerationTests.ContractConsumedViaCtxGet_MustBeRegisteredSomewhere` 扫两棵插件树（`Plugins/**` + `ForgeSelf.Api/Plugins/**`）全部 `ctx.Get<T>()` 消费点（T 限 `ForgeSelf.Abstractions` 接口），断言每个 T 在插件/宿主源码出现过 `ctx.Register<T>(` / `ctx.Register<T,U>(` / `ctx.RegisterLocal<T>(`，或在宿主 seed 清单。
- 已知盲区：反射拼类型名、`GetService(typeof(T))` 动态调用扫不到——这类点靠 lens 问题 3 在 Review 时人工核对；消费点带 `?? throw`/null 降级文案的仍是红（消费了没供给就是缺陷）。

## 3 Canonical fix

提供方在 `Apply(IContext)` 内 eager 构造并 `ctx.Register<契约>(instance)`；消费方每次 `ctx.Get<T>()`，null 时降级 503 + 明确文案。参考实现：`ForgeSelf.Abstractions/AgentDelegationContracts.cs` 头注释（提供方/消费方/降级三段式契约注释模板）与 WorkflowEngine 修复后的 Apply。被否方案：让兄弟插件直接引用对方程序集（违反插件隔离，architecture-design 铁律 2/3）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `WorkflowEnginePluginRegistrationTests.cs` | dotnet test 全量 | 只锁首案三契约 |
| G2 | `ProblemClassEnumerationTests.cs` | dotnet test 全量 / CI | 反射动态解析（见 §2）；存量真缺陷按文件级 allowlist 豁免（带 TODO+到期日） |
| G4（提案） | `RegisterContract` 统一入口（消灭「AddScoped 忘 Register」的表达面） | 阶段四另批评估 | — |

**首跑验证（2026-10-17，Verified）**：G2 建立当天即逮到真实存量违例——`SchedulerPlugin.StopScheduler` 经 `ctx.Get<ISchedulerHost>()` 停调度器但无任何注册/seed 路径（宿主 seed 清单不含、插件只进自身 DI、`Context.Get` 无 DI 回落，源码实证 `Plugins/Scheduler/SchedulerPlugin.cs:183` + `ForgeSelf.Api/Plugins/PluginManager.cs:241-243`）；已入 TODO（P1）并 allowlist 豁免存量，修复时补正式 case。守卫有牙，非空转。

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-10-10-workflow-engine-contract-not-registered | 2026-10-10 | AIAgent 工作流工具报「服务没激活」，WorkflowEngine 三契约只进 IServiceCollection | 否（首见，当时无守卫） |
