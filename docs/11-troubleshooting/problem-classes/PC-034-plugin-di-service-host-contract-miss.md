---
id: PC-034
title: 插件 DI 构造服务经子容器解析宿主/跨插件契约恒 null
tags: [plugin-di, host-contract, ctx-get, smart-terminal]
invariant: 进插件子 DI 容器的服务构造函数只许注入 IContext；注入 IServiceProvider 会被 MS DI 自解析为子容器本体，对其 CreateHostScope()/GetService 解析宿主契约（ISecretEncryptionService/IChatCompletion 等）与跨插件契约恒 null。执行期宿主契约一律经 ctx.Get<T>()（共享表/宿主 seed）或 ctx.Get<IServiceProvider>() 回落宿主根
status: guarded
first_seen: 2026-11-14
recurrences: [2026-11-14-plugin-di-service-host-contract-miss]
cases: [2026-11-14-plugin-di-service-host-contract-miss]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StHostGatewayWiringTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StHostGatewayWiringTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs, status: active}
lens:
  - 新写/改动的插件服务（进子容器的 Service、控制器依赖）构造函数注入的是 IContext 还是 IServiceProvider？ISP = 子容器本体 = 宿主契约恒 null。
  - 宿主契约（ISecretEncryptionService/IChatCompletion/ILlmRuntime…）在插件里的执行期解析是否经 ctx.Get<T>()，而非对子容器 CreateHostScope()？
  - 工具函数（ToolExtensions.Add(new X(ctx))）持有的是 ctx 本体，与 DI 服务不同：形参可写 IServiceProvider，但实参必须是 ctx/宿主根（ctx.Get<IServiceProvider>()），绝不能传插件子 provider。
---
# PC-034 插件 DI 构造服务经子容器解析宿主/跨插件契约恒 null

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

每个插件挂载时拿到独立子 `ServiceCollection`（`ForgeSelf.Api/Plugins/PluginManager.cs:772-782`：`AddSingleton<IContext>(fiber.Context)` + 插件自注册），`BuildServiceProvider()` 后 MS DI 对构造参数 `IServiceProvider` 做**自解析 = 子容器本体**（`ForgeSelf.Api/Services/PluginServiceRegistry.cs` 头注释实证「真容器的 GetService(typeof(IServiceProvider)) 返回自身 scope」）。宿主契约不在子容器：`ISecretEncryptionService` 等由 `ProvideHostServices` seed 进**根 Context 共享表**（`PluginManager.HostProvidedServiceContracts`），`IChatCompletion` 由提供方插件 `ctx.Register` 进共享表——两条路都只对 `IContext` 通路可见。于是「构造注入 IServiceProvider + CreateHostScope()」这套宿主侧惯用法在插件里**恒拿到 null**，且 null 被上层按「模块不可用」降级，静默成永久 424。

**为什么会犯**：宿主服务的肌肉记忆是「构造存 IServiceProvider、用时 CreateHostScope」（宿主服务里这是对的）；PILOT-062 专门造了 `CreateHostScope` 扩展并写了「插件工具类构造函数禁止解析宿主服务」的规则（B4），但**规则只覆盖了构造期解析，没覆盖「把 IServiceProvider 存下来执行期解析」**，且没说清「注入进来的 IServiceProvider 在插件里根本不是宿主根」。同文件里工具函数拿 ctx 好用、DI 服务拿子容器坏用，形成「有时好有时坏」的误导性对照。

## 2 特征签名与扫描方法

- **G2 枚举式守卫**（`ProblemClassEnumerationTests.Pc034_DiConstructedPluginServices_MustInjectIContextNotServiceProvider`）：扫 `Plugins/**`（**单树**——DI 注册面全在插件 Apply；`ForgeSelf.Api/Plugins/` 树是宿主基建如 PluginManager，宿主 DI 生命周期，不属本 PC 语义面），收集 `AddSingleton/AddScoped/AddTransient<...>` 登记的实现类型，断言其类构造函数（含 C# 12 主构造函数）不含 `IServiceProvider` 参数。阳性对照：StHostGateway 注册点必须在扫描面内 + 注册实现 >20。配 `Pc034_Detector_*` 自证测试防检测器静默失效。
- **G1 接线级守卫**（`StHostGatewayWiringTests`）：按真实宿主装配（根 Context seed + AIAgent 式共享表注册 + 插件子容器）断言从子容器解析 `IStHostGateway` 后能拿到契约——mock 的是模型调用不是 DI 接线，专防「单测用显式 host rig 装配绕开真实接线」的 rig 假绿（2026-11-12 批次逃逸根因）。
- 已知盲区：`AddSingleton<T>(instance)` 实例注册形态不产生实现类型名（如 AIAgentChatCompletion 的自建 provider 注册——它自建 provider 是有意为之，非子容器注入）；反射拼类型名解析扫不到。这两类靠 lens 问题在 Review 人工核对。

## 3 Canonical fix

构造函数参数 `IServiceProvider` → `IContext`；执行期解析：宿主/跨插件契约 `ctx.Get<T>()`（共享表 + 宿主 seed，缺席返回 null 供上层降级）；确需宿主根 scope 时 `ctx.Get<IServiceProvider>()` 后再 `CreateHostScope()`（AgentDelegationProvider 模式，`Plugins/AgentHub/Services/AgentDelegationProvider.cs:37`）。参考实现：`Plugins/SmartTerminal/Services/StHostGateway.cs`（本次修复主角）、`Plugins/WorkflowEngine/Services/WorkflowExecutor.cs:13-25`（半迁移完成的收敛）、`Plugins/WorkflowEngine/Services/WorkflowExecutor.cs:564-567`。被否方案：把 IChatCompletion/ISecretEncryptionService 加进子容器或转发清单——把「消费侧拿错容器」变成「注册面蔓延」，每加一个契约都要记着转发，回归 PC-001 的老路。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `StHostGatewayWiringTests.cs`（真实双容器接线 3 例） | dotnet test（SmartTerminal 过滤集/全量） | 只锁 smart-terminal 网关实例与 424 降级契约 |
| G2 | `ProblemClassEnumerationTests.cs`（Pc034 两例） | dotnet test 全量 / CI | 实例注册形态、反射解析（见 §2）；allowlist 空账，新增违例即红 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| 2026-11-14-plugin-di-service-host-contract-miss | 2026-11-14 | 智能终端助手/凭据恒 424「宿主对话/凭据加密模块不可用」，StHostGateway 注入 IServiceProvider 拿到子容器 | 否（首见；PC-005/B4 守卫只扫构造期解析，未覆盖本形态） |
