# 027 · Cordis 内核（一切皆插件运行时）

> 状态：已实现/已闭环（ADR 001；内核、契约层、插件自注册、可变 MS DI、文件级热更新、动态端点移除、事件总线贯穿、会话/LLM 接缝接线、11 插件全部拆独立程序集、前端清单驱动动态挂载均已落地；剩余项见「已知问题 / 待办」）
> 最后更新：2026-08-18

## 概述

新增 `OpenForgeSelf.Core`（.NET 版 Cordis 内核）与 `OpenForgeSelf.Abstractions`（契约层），为 OpenForgeSelf 提供「一切皆插件」的底座：共享上下文（`IContext`，含宿主 MS DI 桥 `SetHostProvider`）、能力接缝、类型化事件总线（emit/waterfall/parallel/serial）、可逆副作用（`ctx.Effect`）、Fiber 插件生命周期。目标是消灭 `AppBuilder.cs` 中插件服务的硬编码 DI 注册，让每个功能以插件形式自注册、可替换、可热更新。当前插件自注册、可变 DI 容器（`PluginServiceRegistry`）、文件级热更新（版本目录 + ALC 卸载 + `FileSystemWatcher` 自动 reload）与前端清单驱动动态挂载均已落地。

## 关联文档

| 文档 | 位置 | 作用 |
|------|------|------|
| 调研依据 | [`06-research/001-deepseek-harness-plugin-architecture.md`](../06-research/001-deepseek-harness-plugin-architecture.md) | deepseek-harness 插件化设计的机制细节与源码证据 |
| 架构设计 | [`01-architecture/cordis-kernel.md`](../01-architecture/cordis-kernel.md) | 内核核心抽象、映射、接入方式 |
| 路线图 | [`15-roadmap/plugin-architecture.md`](../15-roadmap/plugin-architecture.md) | 分阶段改造步骤与验证门禁 |
| 关键决策 | [`07-decisions/001-cordis-kernel-architecture.md`](../07-decisions/001-cordis-kernel-architecture.md) | 契约/程序集/版本目录/前端/节奏五项 ADR |

## 代码落点

| 层 | 文件 |
|----|------|
| 内核项目 | `OpenForgeSelf.Core/`（`OpenForgeSelf.Core.csproj`，net10.0，零外部依赖） |
| 核心抽象 | `IContext.cs` / `Context.cs`（含 `SetHostProvider` 宿主 MS DI 桥）/ `IEventBus.cs` / `EventBus.cs` / `Disposable.cs` / `Service.cs` / `Fiber.cs` |
| 契约项目 | `OpenForgeSelf.Abstractions/`（`OpenForgeSelf.Abstractions.csproj`，net10.0） |
| 契约文件 | `IPlugin.cs` / `IEndpointRegistry.cs` / `IExtensionPoint.cs` / `IMenuExtension.cs` / `IToolFunctionExtension.cs` / `PluginMetadata.cs`（含 `FrontendContributes`/`Provides`/`Consumes`）/ `ApiResponse.cs` / 接缝（`ILlmRuntime`/`ISessionStore`/`IAgentLoop`/`IInbox`/`IConfigurationService`/`ILogService`/`IUsageStatsService`/`IWorkflowService`/`IWorkflowExecutor`/`IWorkflowAIAdvisor`/`IScriptTemplateService`）+ 共享 DTO（`UsageStatsModels.cs`/`WorkflowModels.cs`/`ScriptModels.cs`） |
| 后端引用 | `OpenForgeSelf.Backend/OpenForgeSelf.Backend.csproj`（`ProjectReference` Core + Abstractions + 11 插件 `ReferenceOutputAssembly=false`；`Compile Remove` 全部 11 个插件目录） |
| 插件装配 | `Plugins/PluginManager.cs`（发现/加载/Fiber 装配/热重载/拓扑排序/动态端点移除）、`Services/PluginServiceRegistry.cs`（可变 MS DI）、`Plugins/PluginVersionLayout.cs`（side-by-side 版本目录 + current 指针）、`Plugins/PluginAssemblyUnloader.cs`（ALC 回收 + 文件锁探测）、`Plugins/Services/PluginHotReloadWatcher.cs`（FileSystemWatcher 自动 reload）、`Plugins/ExtensionPointManager.cs`（菜单/工具扩展点）、`Services/MvcActionDescriptorChangeProvider.cs`（动态端点移除的 ActionDescriptor 刷新通知） |
| 平台装配 | `AppBuilder.cs`（插件自注册引导、Build 后 BuildAll + `SetServiceProvider` + `DiscoverAllExtensions` + ApplicationPartManager 注册插件程序集 + `ISessionStore`/`ILlmRuntime` 接缝接线 + `IEventBus` 注入 PluginManager） |
| 生命周期事件 | `Abstractions/PluginLifecycleEvent.cs`（`plugin/loaded` / `plugin/unloaded` 事件载荷） |
| 前端 | `src/stores/pluginManifest.ts`（清单 store）、`src/data/features.ts`（`mergeFeatureList` 内置 + 清单补充合并）、`src/router/dynamicPlugins.ts`（`/plugin-view` 命名空间动态 import 挂载 + router 守卫） |

## 核心抽象

- **`IContext`**：服务定位器 + 可逆副作用 + 事件总线 + 派生上下文。服务注册为「类型 → 实例」，`Get<T>()` 探测式获取（自身/父级字典 → 宿主 provider 回落），`Effect()` 注册卸载时逆序释放的清理函数。`Context.SetHostProvider(IServiceProvider)` 桥接宿主 MS DI，使 Fiber 派生上下文能解析宿主注册的服务。
- **`IEventBus`**：四种分发模式——`EmitAsync`（广播）、`WaterfallAsync`（环绕中间件/短路）、`ParallelAsync`（并发）、`SerialAsync`（按序短路）；注册句柄为 `On`（广播）、`OnSerial`（按序短路）、`OnWaterfall`（环绕中间件），均返回 `IDisposable` 供注销。平台级 `IEventBus` 单例由 `AppBuilder` 注册，`ToolRegistry` 已接线 `tools/pre-execute`（`SerialAsync` 拒绝门）/ `tools/execute` / `tools/post-execute`。
- **`Fiber`**：插件生命周期作用域（对标 Cordis Fiber），`Mount(apply)` 执行插件贡献函数，`Dispose` 逆序回滚副作用并注销服务，幂等。
- **`Service`**：服务基类（对标 Cordis Service），派生类用 `ctx.Register<T>(this)` 贡献服务，`static Inject` 声明依赖。
- **能力接缝（Abstractions 契约）**：`ctx.Get<T>()` 可解析的接缝接口已落地——`ILlmRuntime`、`ISessionStore`、`IAgentLoop`、`IInbox`、`IEndpointRegistry`、`IConfigurationService`、`ILogService`、`IUsageStatsService`、`IWorkflowService`/`IWorkflowExecutor`/`IWorkflowAIAdvisor`、`IScriptTemplateService`。消费者只依赖接口，替换 Provider 零改动。
- **插件自注册**：每个插件实现 `IPlugin.Apply(IContext)`，在 `Apply` 内经 `ctx.Get<IServiceCollection>()` 向该插件独立 `IServiceCollection` 注册服务、贡献 `IMenuExtension`/`IToolFunctionExtension` 扩展点、注册 `ctx.Effect` 副作用；`PluginManager.MountPlugin` 统一 Fiber 装配。

## 当前进度

- [x] 新增 `OpenForgeSelf.Core` 项目并编译通过（net10.0，零外部依赖）。
- [x] 挂载到 `OpenForgeSelf.Backend` 项目引用。
- [x] 新增 `OpenForgeSelf.Abstractions` 契约程序集（`IPlugin` + 扩展点 + 能力接缝 + 共享 DTO，见 ADR D2）。
- [x] `IPlugin` 改为单一 `Apply(IContext)`（见 ADR D1），11 个插件全部迁移到 `Apply` 自注册（`ctx.Get<IServiceCollection>()` + `ctx.Effect`）。
- [x] `AppBuilder.cs` 删除 11 个插件的硬编码 `AddScoped/AddSingleton`，改为插件自注册（临时 provider 引导 + `RegisterAllServices` + `AddSingleton(pluginManager)`）；仅保留启动路径对 `ITaskScheduler`（Scheduler 插件服务）的一次性 `GetRequiredService` 直引用。
- [x] 可变 MS DI 容器：`PluginServiceRegistry`（每插件子容器 + 宿主服务透传 + Transient 转发描述符 + `BuildAll` 延迟构建；卸载后 `GetRequiredService` 抛「插件服务已卸载」）。
- [x] 事件总线接线到 `ToolRegistry`：`tools/pre-execute`（`SerialAsync` 拒绝门）/ `tools/execute` / `tools/post-execute`。
- [x] `IAgentLoop` + `ISessionStore` + `ILlmRuntime` + `IInbox` 等接缝契约迁入 `OpenForgeSelf.Abstractions`；`ISessionStore`（`InMemorySessionStore` 单例）与 `ILlmRuntime`（`AIServiceLlmRuntime` Scoped）已注册进 DI，`ChatController` 经可选依赖使用 `ISessionStore` 追加 user/assistant 消息；`IAgentLoop`/`IInbox` 仅契约级测试，Provider 实现待后续。
- [x] 文件级热更新：`PluginVersionLayout`（side-by-side `versions/<semver>` + `current` 指针）、`PluginAssemblyUnloader`（`ForceCollect` + `FileShare.None` + 延迟删除）、`PluginHotReloadWatcher`（`FileSystemWatcher` + 300ms debounce，Testing 环境跳过）。
- [x] 插件程序集注册进 MVC：Build 后 `ApplicationPartManager` 添加插件程序集 `AssemblyPart`（MemorySystem 独立 dll 的控制器已被路由发现）。
- [x] 共享基础设施搬到宿主中性 `Services/`：`IToolRegistry`/`ToolRegistry`/`ToolCallContext`、`ICronParser`/`CronParser`、`IRuntimeDetector`/`RuntimeDetector`。
- [x] `ScriptExecutionHub`/`MonitorHub` 去静态化（Broadcaster 单例 + 构造注入）。
- [x] AIAgent 插件零 Workflow/Script 跨插件引用（契约迁 Abstractions）。
- [x] 前端清单驱动：`pluginManifest` store + `mergeFeatureList`（T9）+ `router/dynamicPlugins.ts` 动态 import 视图挂载（`/plugin-view` 命名空间 + router 守卫；试点 MemoryView/QuickLinksView/TodoView）。
- [x] 11 个插件全部拆独立程序集（批1 MemorySystem/TodoTracker/QuickLinks/TextTools → 批2 FileTools/DevTools/SystemMonitor → 批3 Scheduler/ScriptRunner/WorkflowEngine → 批4 AIAgent；每个插件 `Compile Remove` + `ReferenceOutputAssembly=false` + 专属 `Stage*Plugin` MSBuild 目标复制 DLL 到 `Plugins/<Name>/`）。
- [x] 动态端点移除：`MvcActionDescriptorChangeProvider`（自定义 `IActionDescriptorChangeProvider`，CTS 交换模式避免重订阅死循环）+ `PluginManager.DestroyPlugin` 调 `UnregisterApplicationPart` 摘除 `AssemblyPart` 并触发路由刷新（4 单测覆盖）。
- [x] 事件总线贯穿（P3）：`PluginLifecycleEvent` 载荷 + `PluginManager.EventBus` 注入 + `plugin/loaded` / `plugin/unloaded` 生命周期事件 + 集成测试。
- [x] 会话/LLM 接缝接线（P4）：`InMemorySessionStore` 单例 + `AIServiceLlmRuntime` Scoped 注册进 DI + `ChatController` 追加 user/assistant 到 `ISessionStore` + 测试。
- [x] `WorkflowHub` 去静态化（移除 `SetServiceProvider` 与无调用者 `Broadcast` 方法）。
- [x] 测试：`dotnet test` 966/966（Backend.Tests）、12/12（Core.Tests）、9/9（Abstractions.Tests）全绿；前端 `pnpm run test` 428/428（含 `dynamicPlugins.test` 6 例 + `pluginManifest.test` 4 例）。
- [x] QuickLinks SQLite 表随启动创建（早前 `no such table` 为旧启动遗留，已恢复）。
- [ ] 可变 MS DI 的 Scoped 语义是近似（宿主透传均映射为 Transient 转发，生命周期由子容器托管）。

## 使用要点

- 新增能力 = 实现一个 `Service`（或函数式 `apply(ctx)`）并通过 `ctx.Register<T>(this)` 贡献，无需改框架代码。
- 插件需要后台资源（定时器/连接）时用 `ctx.Effect(() => { ...; return disposer; })`，卸载自动清理。
- 插件间通信走 `ctx.Events`，拦截点用 `WaterfallAsync` / `SerialAsync` 实现「允许/拒绝」与「结果改写」。
- 每个贡献 registry 的插件必须配一条「卸载后断言清理成功」的测试（对标 dsh 的 dispose 测试门禁）。

## 已知问题 / 待办

### 1. 可变 MS DI 的 Scoped 语义是近似

**问题本质**：`PluginServiceRegistry` 对宿主服务的透传描述符统一用 `ServiceLifetime.Transient`，工厂委托 `hostProvider.GetService(serviceType)`。当宿主服务注册为 Scoped（如 `DbContext`）时，插件子容器每次解析都会调用**根 provider** 的 `GetService`，而 Scoped 服务从根 provider 解析等价于 Transient——每次返回新实例。

**影响范围**：插件内依赖宿主 Scoped 服务时，同一 HTTP 请求/Scope 内无法共享同一实例。典型场景：
- 插件依赖 `DbContext` → 每次解析得到新上下文，无法参与宿主的事务/变更追踪
- 插件依赖 `IHttpContextAccessor` → 可能拿到 null 或错误的 HttpContext

**当前规避**：现有 11 个插件均未依赖宿主 Scoped 服务（插件自身服务在子容器内注册，生命周期由子容器托管），因此运行时未暴露问题。

**建议动作**：
- **短期**：维持现状，插件设计时避免依赖宿主 Scoped 服务（文档已注明）
- **长期**：插件实现自己的服务提供器（Service Provider），与宿主的 ASP.NET Core 服务提供器隔绝。插件子容器不再透传宿主服务，而是通过契约接口（Abstractions）与宿主交互，彻底消除生命周期语义冲突

### 2. ~~AIAgent 未加入 .sln~~ → 已解决

AIAgent.csproj 已通过 `dotnet sln add` 加入解决方案，Visual Studio 解决方案资源管理器可见。

### 3. `IAgentLoop` / `IInbox` 无 Provider 实现

**问题本质**：这两个接缝契约已迁入 `OpenForgeSelf.Abstractions`，定义了 Agent 循环和消息收件箱的接口，并有契约级测试（`FakeAgentLoop`/`FakeInbox`），但 Backend 侧无真实实现类注册进 DI 容器。

**影响范围**：消费者（如 AI Agent 插件）无法通过 `ctx.Get<IAgentLoop>()` 或 `ctx.Get<IInbox>()` 获取真实服务，调用会返回 null 或抛异常。

**当前状态**：属于「契约先行、实现待补」的设计。接口定义已稳定，测试覆盖契约行为，但运行时功能不可用。

**建议动作**：
- **短期**：维持现状，标记为「待实现」，不阻塞其他插件开发
- **长期**：当 AI Agent 功能需要真实的 Agent 循环和消息队列时，实现 `InMemoryAgentLoop` 和 `InMemoryInbox`（或基于外部消息队列的实现）并注册进 DI

> 已解决（归档）：① 11 个插件全部拆独立程序集（批1-4，958/958）；② 动态端点移除（`MvcActionDescriptorChangeProvider` + `UnregisterApplicationPart`，958/958）；③ QuickLinks SQLite 表随启动创建恢复；④ `WorkflowHub` 去静态化。

> 内核与契约层测试：`OpenForgeSelf.Core.Tests`（`ContextHostBridgeTests`/`EventBusTests`/`FiberTests`）、`OpenForgeSelf.Abstractions.Tests`（`PluginContractTests`/`EndpointRegistryTests`/`SessionStoreContractTests`/`AgentLoopContractTests`/`InboxContractTests`/`LlmRuntimeContractTests`）、`MvcActionDescriptorChangeProviderTests`（动态端点刷新）、`PluginLifecycleEventTests`（加载/卸载事件）、`SessionStoreAndLlmRuntimeTests`（接缝真实实现）为契约与内核行为提供回归保护。
