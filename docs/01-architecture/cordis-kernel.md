# 01-architecture — Cordis 内核（.NET 版）架构设计

> 功能编号：027
> 状态：已实现/已闭环（ADR 001；内核、契约层、插件自注册、可变 MS DI、文件级热更新、前端清单驱动均已落地；剩余项见功能档案「已知问题 / 待办」）
> 最后更新：2026-08-19
> 关联：调研见 [`06-research/001-deepseek-harness-plugin-architecture.md`](../06-research/001-deepseek-harness-plugin-architecture.md)，功能档案见 [`02-features/027-cordis-kernel.md`](../02-features/027-cordis-kernel.md)，路线图见 [`15-roadmap/plugin-architecture.md`](../15-roadmap/plugin-architecture.md)

本文描述新增项目 `ForgeSelf.Core`（.NET 版 Cordis 内核）的设计：它是什么、为什么这样设计、核心抽象是什么、如何渐进接入 `ForgeSelf.Api`。

---

## 架构全景图（一切皆插件）

> 下图展示新架构的四层分层、核心组件、依赖方向与数据流。箭头方向 = 依赖/调用方向。
> 完整 SVG 架构图见本文末尾「附录：架构图 SVG 源文件」。

```mermaid
flowchart TB
    subgraph FE["第1层 · 前端 Vue SPA（消费方）"]
        F1["features.ts（内置 fallback）"]
        F2["pluginManifest store（清单驱动）"]
        F3["mergeFeatureList / tabs 渲染"]
        F4["router/dynamicPlugins.ts（/plugin-view 动态 import 挂载）"]
        F1 --> F2
        F2 --> F3
        F2 --> F4
    end

    subgraph Host["第2层 · 宿主 ForgeSelf.Api"]
        A["AppBuilder（平台装配：临时 provider 引导自注册 + Build 后 BuildAll/SetServiceProvider/DiscoverAllExtensions/ApplicationPartManager）"]
        PM["PluginManager（发现/加载/Fiber 生命周期/拓扑排序/热重载）"]
        EP["ExtensionPointManager（菜单/工具扩展点注册）"]
        REG["PluginServiceRegistry（可变 MS DI：每插件子容器 + 宿主透传）"]
        HV["PluginVersionLayout / PluginAssemblyUnloader / PluginHotReloadWatcher（版本目录 + ALC 回收 + 自动 reload）"]
        PS["平台服务（认证/CORS/XCode/AI 网关/配置/日志）"]
    end

    subgraph Kernel["第3层 · 内核 ForgeSelf.Core + 契约 ForgeSelf.Abstractions"]
        K1["IContext / Context（服务定位 + Effect 可逆副作用 + SetHostProvider 宿主 MS DI 桥）"]
        K2["Fiber（每插件派生上下文 + 卸载逆序回滚）"]
        K3["IEventBus / EventBus（emit/waterfall/parallel/serial + On/OnSerial/OnWaterfall）"]
        C1["IPlugin.Apply(IContext)"]
        C2["IExtensionPoint / IMenuExtension / IToolFunctionExtension / IEndpointRegistry"]
        C3["PluginMetadata / FrontendContributes / Provides / Consumes / ApiResponse / 共享 DTO"]
        C4["能力接缝：ILlmRuntime/ISessionStore/IAgentLoop/IInbox/IConfigurationService/ILogService/IUsageStatsService/IWorkflowService/IWorkflowExecutor/IWorkflowAIAdvisor/IScriptTemplateService"]
    end

    subgraph Plugins["第4层 · 插件层 Plugins/*（12 个，MemorySystem 已拆独立程序集）"]
        PL["AIAgent / DevTools / FileTools / MemorySystem / ProxyCapture / QuickLinks / Scheduler / ScriptRunner / SystemMonitor / TextTools / TodoTracker / WorkflowEngine"]
        PL2["每个插件 Apply(ctx)：ctx.Get<IServiceCollection>() 自注册 DI + 菜单/工具扩展 + ctx.Effect 副作用"]
    end

    A -->|"Build 前：AddPluginManager + RegisterAllServices(builder.Services)"| PM
    A -->|"Build 后：BuildAll + SetServiceProvider(app.Services) + DiscoverAllExtensions"| PM
    A --> REG
    PM -->|"创建 Fiber（派生上下文）"| K2
    K2 --> K1
    PM --> EP
    PL -->|"Apply(ctx)"| K1
    PL -->|"贡献扩展点"| C2
    PL -->|"实现能力接缝契约"| C4
    PS -->|"宿主服务桥进 Context（SetHostProvider）"| K1
    K3 -->|"tools/pre-execute 拒绝门（经 ToolRegistry）"| PL
    EP -->|"菜单/工具清单"| FE
    FE -->|"GET /api/plugin/frontend-manifest"| A
```

### 分层职责（有什么）

| 层 | 组件 | 职责 | 与当前项目对应 |
|---|---|---|---|
| 宿主 | `AppBuilder` | 平台装配：认证/CORS/XCode/AI 网关等平台服务；Build 前用临时 provider 引导插件自注册，Build 后 `BuildAll` + 桥接 MS DI + `DiscoverAllExtensions` + ApplicationPartManager 注册插件程序集 | `ForgeSelf.Api/AppBuilder.cs` |
| 宿主 | `PluginManager` | 发现/加载/拓扑排序/Fiber 生命周期/热重载；独立 DLL 优先、内嵌插件主程序集回退 | `Plugins/PluginManager.cs` |
| 宿主 | `PluginServiceRegistry` | 可变 MS DI：每插件子容器 + 宿主服务透传 + Transient 转发描述符 + `BuildAll` 延迟构建；卸载后解析失败 | `Services/PluginServiceRegistry.cs` |
| 宿主 | `ExtensionPointManager` | 收集插件贡献的菜单/工具扩展 | `Plugins/ExtensionPointManager.cs` |
| 宿主 | `PluginVersionLayout` / `PluginAssemblyUnloader` / `PluginHotReloadWatcher` | side-by-side 版本目录 + `current` 指针；ALC 回收 + 文件锁探测；`FileSystemWatcher` 自动 reload | `Plugins/PluginVersionLayout.cs` / `Plugins/PluginAssemblyUnloader.cs` / `Plugins/Services/PluginHotReloadWatcher.cs` |
| 宿主 | 共享基础设施 | `IToolRegistry`/`ToolRegistry`/`ToolCallContext`、`ICronParser`/`CronParser`、`IRuntimeDetector`/`RuntimeDetector`（宿主中性，供插件复用） | `Services/` |
| 内核 | `IContext`/`Context` | 服务定位 + `Effect` 可逆副作用 + `SetHostProvider` 宿主 MS DI 桥 | `ForgeSelf.Core/IContext.cs`/`Context.cs` |
| 内核 | `Fiber` | 每插件一个派生上下文，`Mount` 装配 / 卸载逆序回滚，幂等 | `ForgeSelf.Core/Fiber.cs` |
| 内核 | `IEventBus`/`EventBus` | 类型化事件四模式 + `On`/`OnSerial`/`OnWaterfall` 注册句柄 | `ForgeSelf.Core/IEventBus.cs`/`EventBus.cs` |
| 契约 | `IPlugin` | 单一 `Apply(IContext)` 插件契约 | `ForgeSelf.Abstractions/IPlugin.cs` |
| 契约 | 扩展点 | `IMenuExtension`/`IToolFunctionExtension`/`IEndpointRegistry` | `ForgeSelf.Abstractions/` |
| 契约 | 能力接缝 | `ILlmRuntime`/`ISessionStore`/`IAgentLoop`/`IInbox`/`IConfigurationService`/`ILogService`/`IUsageStatsService`/`IWorkflowService`/`IWorkflowExecutor`/`IWorkflowAIAdvisor`/`IScriptTemplateService` 等可替换 Provider 契约 | `ForgeSelf.Abstractions/` |
| 插件 | 12 个插件 | 各自 `Apply` 自注册服务/菜单/工具/副作用 | `ForgeSelf.Api/Plugins/*`（MemorySystem 已拆独立程序集，其余 11 个内嵌主程序集） |
| 前端 | `pluginManifest store` + `features.ts` + `dynamicPlugins.ts` | 由后端清单驱动菜单/视图；动态 import 挂载 `/plugin-view` 路由 | `ForgeSelf.Web/src/` |

### 核心关系（怎么协作）

1. **启动装配**：`AppBuilder` 在 `Build` 前用临时 provider 拿 `PluginManager` → `RegisterAllServices(builder.Services)` → 每个插件 `Apply(ctx)` 经 `ctx.Get<IServiceCollection>()` 把服务 `AddScoped/AddSingleton` 进该插件独立 `IServiceCollection`、贡献菜单/工具扩展、注册 `ctx.Effect` 副作用。
2. **运行时桥接**：`Build` 后 `PluginServiceRegistry.BuildAll` 构建每插件子 provider（合并宿主服务透传），`PluginManager.SetServiceProvider(app.Services)` 把宿主 DI 桥进 `Context`，插件工具函数运行期经 `CreateScope()/GetService<T>()` 解析宿主服务。
3. **扩展点接线**：`DiscoverAllExtensions` 把插件贡献的菜单/工具注册到 `ExtensionPointManager`，供前端清单与 `ToolRegistry` 使用。
4. **工具执行拦截**：`ToolRegistry` 通过平台 `IEventBus`（AppBuilder 注册单例）发 `tools/pre-execute`（`SerialAsync` 拒绝门）→ `tools/execute`（`EmitAsync`）→ `tools/post-execute`（`EmitAsync`）。
5. **热更新**：`PluginVersionLayout` 用 side-by-side 版本目录 + `current` 指针，`PluginAssemblyUnloader` 做 `ForceCollect` + `FileShare.None` 探测 + 延迟删除，`PluginHotReloadWatcher`（`FileSystemWatcher` + 300ms debounce）自动 reload；卸载走 `registry.Unmount → Fiber.Dispose → ALC Unload`。
6. **前端驱动**：前端 `GET /api/plugin/frontend-manifest` → `pluginManifest store` → `mergeFeatureList` 合并菜单 → tabs 渲染；`router/dynamicPlugins.ts` 按清单把 `route + views` 映射为 `/plugin-view/*` 动态 import 懒加载路由（试点 MemoryView/QuickLinksView/TodoView），清单失败/为空时回退静态路由。

---

## 1. 定位与目标

`ForgeSelf.Core` 是 deepseek-harness / Cordis 架构在 .NET 上的**薄内核**，为「一切皆插件」提供底座。它不是 DI 容器（复用 Microsoft.Extensions.DependencyInjection），而是补齐 MS DI 缺失的三件事：

1. **可逆副作用**（`ctx.Effect`）：任何运行期注册都配对注销，插件卸载即逆序回滚。
2. **类型化事件总线**（`IEventBus`）：四种分发模式（emit / waterfall / parallel / serial），提供拦截点。
3. **能力接缝**（`IContext` 服务定位）：服务定义（接口）+ 服务提供者（实现）+ 消费者（`ctx.Get<T>()`），替换 Provider 零改动消费者。

**终态**：`AppBuilder.cs` 不再硬编码任何插件的 DI 注册；每个功能以插件形式通过 `IContext` 自注册。

---

## 2. 设计原则

1. **没有内核，只有插件**：系统只提供 `IContext`，不预设任何业务能力。
2. **可逆注册优先**：热更新稳不稳取决于「卸载即回滚」，而非文件锁技巧。
3. **复用 MS DI**：服务解析交给 ASP.NET Core 容器，`IContext` 只做「服务定位 + 副作用 + 事件」这一薄层。
4. **最佳设计优先**：本项目未上线，不留兼容层；`IPlugin` 一步到位改 `Apply(IContext)`，插件一步到位拆独立程序集（ADR D1/D2）。

---

## 3. 核心抽象

### 3.1 IContext（共享上下文）

```csharp
public interface IContext : IServiceProvider
{
    void Register<TService>(TService instance) where TService : class;
    void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new();
    TService? Get<TService>() where TService : class;
    IDisposable Effect(Func<IDisposable> sideEffect);
    IEventBus Events { get; }
    IContext Derive();
}
```

`Context` 实现为「类型 → 实例」本地字典 + 父级查找（`Derive()` 派生上下文用于会话/作用域隔离）。副作用按注册逆序释放，与 Cordis Fiber 的 dispose 语义一致。

### 3.2 IEventBus（类型化事件）

```csharp
public interface IEventBus
{
    Task EmitAsync<TEvent>(string name, TEvent payload);                                   // 广播
    Task<TResult> WaterfallAsync<TEvent, TResult>(string name, TEvent payload, Func<Task<TResult>> fallback); // 环绕/短路
    Task ParallelAsync<TEvent>(string name, TEvent payload);                                // 并发
    Task<TResult?> SerialAsync<TEvent, TResult>(string name, TEvent payload);               // 按序短路
    IDisposable On<TEvent>(string name, Func<TEvent, Task> handler);
}
```

事件名用「命名空间/动作」风格。规划中的事件族：

| 事件族 | 典型事件 | 用途 |
|--------|----------|------|
| `session/*` | `session/event` | 持久事实，追加到会话日志 |
| `agent/*` | `agent/pre-step`、`agent/request`、`agent/turn-stopping` | 观察/拦截 Agent 生命周期 |
| `tools/*` | `tools/pre-execute`、`tools/execute`、`tools/post-execute` | 工具执行管道（允许/拒绝门 + 结果检查） |
| `fs/*` / `telemetry/*` | 能力事件 | 给接缝附加策略，无需 import 循环 |

### 3.3 Service（服务基类，对标 Cordis Service）

```csharp
public abstract class Service : IDisposable
{
    public static IReadOnlyList<string> Inject { get; } = Array.Empty<string>();
    protected Service(IContext ctx, string name);
    public IContext Context { get; }
    public string Name { get; }
}
```

派生类在构造函数中调用 `Context.Register<ISomeService>(this)` 贡献服务，通过 `static Inject` 声明依赖的服务名（宿主据此拓扑排序，对标 Cordis 的 `inject`）。

### 3.4 插件契约（IPlugin，ADR D1 已决策）

```csharp
public interface IPlugin
{
    void Apply(IContext ctx);   // 一切贡献（服务/工具/端点/副作用/事件）都在此声明
}
```

插件只贡献、不管理生命周期；`Apply` 内注册的一切 effect 由 **Fiber** 在卸载时逆序回滚。元数据（Id/Name/Version/Provides/Consumes/contributes）单一真源在 `plugin.json`，插件代码不重复声明。

### 3.6 插件间服务互通（2026-08-19 设计修正，对标 Cordis ReflectService）

**背景**：初版移植时 `Context.Register` 只写自身字典、`Get` 仅父链查找——兄弟 Fiber 不互通，与 Cordis 原案背离（服务默认全局可见，见调研 §4.1/§6 偏差#1）。本节为修正设计，**决策来源 = 调研文档 `06-research/001` §5.6 权威裁决（A/D/E/F），不自作设计**。

**共享服务表**：root 上下文持有「服务类型 → 条目」共享表，条目 = `(实例, 提供者 Fiber)`：
- `Register<T>(instance)` 对标 Cordis `provide()`——**eager 单例实例**写入共享表并记录归属 Fiber；注册动作走 `ctx.Effect` → **Fiber 卸载逆序回滚时自动摘除**（可逆 effect，调研 §5.6 裁决 F）。
- **eager 单例**（非懒解析委托）：服务在 `Apply` 时构造，无 per-request 作用域（调研 §5.6 裁决 F）。需跨请求共享的单例依赖让它本身有状态安全。
- **fiber 私有状态**（`PluginMetadata`、`IServiceCollection` 等，`PluginManager.cs:578/581`）用**独立本地值 API**（如 `RegisterLocal<T>`），不复用 `Register`——本地值不参与共享表、`Get` 不到（调研 §5.6 裁决 A）。
- **解析顺序**：`Get<T>()` = 本地值 → 全局共享表（本地可遮蔽同名全局服务，调研 §5.6 裁决 D）。

**软依赖 / 硬依赖规则**（dsh 判例，调研 §5.5）：
- **软依赖（可选增强）= `ctx.Get<T>()` 探测**，不声明任何依赖；返回 null 即走降级分支。消费者**禁止缓存实例为字段**（每次用每次 Get），防提供者热重载后悬空。
- **硬依赖（必备能力）= 声明 `Consumes`**（机制待落地）：目标语义为 Cordis `inject`——未就绪则 PENDING 等待，提供者卸载/热重载时依赖者自动卸载、回归自动重启（`notify → _refresh → _setEpoch`）。首个强依赖接缝出现时实现，`PluginMetadata.Provides/Consumes` 字段已预留。

**首个落地案例**：`IWorkflowAIAdvisor`（AIAgent 提供 → WorkflowEngine 消费，软依赖）。**已实施完成（2026-08-19）**：AIAgentPlugin 补注册 `IAIWorkflowAssistant`/`IWorkflowAIAdvisor` 进子容器 + eager 构造 advisor 经 `ctx.Register<IWorkflowAIAdvisor>(实例)` 写入共享表；WorkflowExecutor 改 `_ctx.Get<IWorkflowAIAdvisor>()` 消费（null 降级默认重试）；`IWorkflowAIAdvisor` 不进 `PluginServiceRegistry.CollectForwardDescriptors`（裁决 E）。验证：`dotnet build` 0 错 + `dotnet test` 988/988 全绿（含 7 例 dispose 门禁）。

**事件传播**：Cordis 事件沿上下文树传播（子 emit、父默认收）；本项目当前每 `Context` 独立 `EventBus`，待修为派生链共享根总线（见功能档案待办第 5 条）。

### 3.5 能力接缝（Capability Seams）约定

> 设计稿中的接缝全集（`ctx.llm` / `ctx.tools` / `ctx.agents` / `ctx.agentLoop` / `ctx.fs` / `ctx.shell` / `ctx.sessions` / `ctx.sandbox`）为规划愿景。**当前已落地的契约**在 `ForgeSelf.Abstractions`：

| 接缝（`ctx.Get<T>()`） | 服务定义（已落地契约） | 可替换 Provider 示例 |
|------------------------|----------|----------------------|
| `ctx.llm` | `ILlmRuntime`（`StreamAsync(Message[]) → StreamChunk`） | OpenAI / Anthropic / Responses / AgentChat |
| `ctx.agentLoop` | `IAgentLoop`（`RunAsync(AgentRunRequest) → TurnEvent`） | 默认驱动器（可替换） |
| `ctx.sessions` | `ISessionStore`（`Append`/`Replay`/`DeriveMessages`，仅追加） | SQLite 事件日志 |
| `ctx.inbox` | `IInbox`（`Followup`/`Steer`/`Inject`） | 会话收件箱 |
| 端点 | `IEndpointRegistry`（`Map(pattern, handler) → IDisposable`） | 宿主桥接 HTTP 路由 |
| 配置/日志/统计 | `IConfigurationService` / `ILogService` / `IUsageStatsService` | 平台实现 |
| 工作流/脚本 | `IWorkflowService`/`IWorkflowExecutor`/`IWorkflowAIAdvisor` / `IScriptTemplateService` | 插件实现 |

> 未落地接缝（`IToolRuntime`/`IAgentRuntime`/`IFsRuntime`/`IShellRuntime`/`ISandboxRuntime`）仍属设计稿规划，代码暂无对应契约。消费者只依赖接口，通过 `ctx.Get<T>()` 获取，替换 Provider = 换注册实例，消费者零改动。

---

## 4. Cordis → .NET 映射

| Cordis (TS) | ForgeSelf.Core (.NET) |
|---|---|
| Context（`ctx`） | `IContext` / `Context` |
| Service（`super(ctx,'name')`） | `Service` 基类 + `ctx.Register<T>(this)` |
| ReflectService 全局 store（服务默认全局可见） | root 共享服务表（条目=eager 单例实例+归属 Fiber；本地值走 `RegisterLocal`，见 §3.6） |
| `inject: ['tools']`（PENDING 等待 + notify 自动重启） | `PluginMetadata.Consumes` 声明（机制待落地，见 §3.6） |
| `ctx.get(name)` 软依赖探测 | `ctx.Get<T>()` 返回 null 即降级（不缓存实例） |
| `ctx.isolate(name)` 服务隔离 | fiber 本地字典注册（框架内部对象默认本地化） |
| `ctx.effect()` | `ctx.Effect(Func<IDisposable>)` |
| typed events（emit/waterfall/parallel/serial + bail） | `IEventBus` 四方法 |
| 事件沿上下文树传播（filter/global） | 待修：派生上下文共享根 `EventBus`（功能档案待办 5） |
| capability seam | `ISeam` 三件套（接口定义 + Provider 实现 + 消费者） |
| Fiber 生命周期（PENDING/LOADING/ACTIVE/UNLOADING/DISPOSED/FAILED） | `Context.Dispose()` 逆序释放 + 现有 `PluginManager` 状态机 |
| declaration merging 类型安全 | C# 泛型接口（必要时 Source Generator，后置） |
| HMR（partialReload + 失败回滚 + fiber.update 原地更新） | ALC `Unload()` + `GC` 回收 + `FileSystemWatcher`（全量重建，无原地更新） |

---

## 5. 项目结构

```
ForgeSelf.Core/            # 内核（net10.0，零外部依赖，已实现）
  IContext.cs / Context.cs     # 共享上下文（含 SetHostProvider 宿主 MS DI 桥）
  IEventBus.cs / EventBus.cs   # 类型化事件总线（四模式 + On/OnSerial/OnWaterfall）
  Disposable.cs / Service.cs   # 辅助 + 服务基类
  Fiber.cs                     # 插件生命周期（Mount/Dispose 逆序回滚，已实现）

ForgeSelf.Abstractions/    # 契约程序集（已建立，ADR D2）
  IPlugin.cs                   # 单一 Apply(IContext) 契约
  IEndpointRegistry.cs         # 端点注册接缝（Map → IDisposable）
  IExtensionPoint.cs / IMenuExtension.cs / IToolFunctionExtension.cs  # 扩展点
  PluginMetadata.cs            # 元数据（FrontendContributes / Provides / Consumes）
  IAgentLoop.cs / ISessionStore.cs / IInbox.cs / ILlmRuntime.cs        # 能力接缝
  IConfigurationService.cs / ILogService.cs / IUsageStatsService.cs    # 平台服务契约
  IWorkflowService.cs(WorkflowServiceContracts.cs) / IScriptTemplateService.cs  # 工作流/脚本契约
  UsageStatsModels.cs / WorkflowModels.cs / ScriptModels.cs / ApiResponse.cs  # 共享 DTO

ForgeSelf.Core.Tests / ForgeSelf.Abstractions.Tests           # 内核/契约测试（12+9 全绿）
```

`ForgeSelf.Core`、`ForgeSelf.Abstractions` 均已实现并编译通过，`Fiber` 已补齐；`ForgeSelf.sln` 现含 7 个项目（Core/Abstractions/Backend/三个 Tests + MemorySystem 插件项目）。接缝 Provider 实现按需在各插件/宿主内落地。

---

## 6. 如何接入 Backend（已实现）

1. **P0 内核 + 契约（已完成）**：`ForgeSelf.Core` 已建立并挂载；`Fiber` 已实现；`ForgeSelf.Abstractions` 已建立；`IPlugin` 定为 `Apply(IContext)` 并全量迁移。
2. **P1 独立程序集 + 自注册（部分完成）**：12 个插件已全部迁移到 `Apply(IContext)` 自注册，`AppBuilder.cs` 硬编码已删除（仅保留 Scheduler `ITaskScheduler` 启动直引用）；独立程序集仅 MemorySystem 已拆（其余 11 个仍内嵌，见功能档案已知问题①）。
3. **P2 可逆注册 + 热更新闭环（部分完成）**：可变 DI（`PluginServiceRegistry`，Scoped 语义近似）、side-by-side 版本目录（N=2）、破文件锁（`PluginAssemblyUnloader`）、自动 reload（`PluginHotReloadWatcher`）均已实现；动态端点移除未做（见已知问题②）。
4. **P3 事件总线接线（已完成）**：`ToolRegistry` 执行管道接入 `tools/pre-execute` / `tools/execute` / `tools/post-execute`。
5. **P4 上层能力（契约已完成）**：`IAgentLoop` + `ISessionStore` + `ILlmRuntime` + `IInbox` 接缝契约已迁入 Abstractions；Backend 侧 Provider 实现与 Profile 配置层叠未接线。

详见 [`15-roadmap/plugin-architecture.md`](../15-roadmap/plugin-architecture.md)。

---

## 7. 边界与风险

- **不重写 DI 容器**：`IContext` 是薄层，服务已通过 `PluginServiceRegistry` 桥接到 MS DI（插件子容器 + 宿主透传）；宿主 Scoped 语义为近似（透传统一 Transient 转发）。
- **ALC 回收不保证**：残留根引用（事件订阅、静态字段、定时器）是热卸载失败的头号原因；每个插件强制 `IDisposable` 全量释放 + dispose 测试门禁；`WorkflowHub` 仍有静态 `SetServiceProvider`（无调用）技术债待清。
- **类型安全**：C# 泛型足够日常使用；强类型声明合并能力需 Source Generator，属可选项。

---

## 附录：架构图 SVG 源文件

> 以下 SVG 架构图展示四层分层、核心组件、依赖方向与插件间服务互通路径。可直接在浏览器打开或嵌入文档。

```svg
<svg viewBox="0 0 720 820" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <marker id="arrow" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto">
      <path d="M1 1 L7 4 L1 7 Z" fill="#52525B"/>
    </marker>
    <marker id="arrow-brand" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto">
      <path d="M1 1 L7 4 L1 7 Z" fill="#4B3FE3"/>
    </marker>
    <marker id="arrow-accent" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto">
      <path d="M1 1 L7 4 L1 7 Z" fill="#27D2BF"/>
    </marker>
  </defs>
  <rect x="0" y="0" width="720" height="820" fill="#F7F7F8" rx="12"/>
  <text x="360" y="30" text-anchor="middle" font-family="system-ui,sans-serif" font-size="16" font-weight="600" fill="#171717">OpenForgeSelf 一切皆插件架构全景</text>
  <text x="360" y="50" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">四层架构 · Cordis 内核 · 插件间服务互通</text>
  <!-- 第1层：前端 -->
  <rect x="40" y="70" width="640" height="100" rx="10" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)" stroke-dasharray="6 4"/>
  <text x="60" y="92" font-family="system-ui,sans-serif" font-size="14" font-weight="500" fill="#171717">第1层 · 前端 Vue 3 SPA</text>
  <text x="60" y="112" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">Vue 3.5 + Vite 6 + TypeScript 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia</text>
  <rect x="60" y="122" width="130" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="125" y="143" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">pluginManifest.ts</text>
  <rect x="200" y="122" width="130" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="265" y="143" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">dynamicPlugins.ts</text>
  <rect x="340" y="122" width="130" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="405" y="143" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">mergeFeatureList</text>
  <rect x="480" y="122" width="130" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="545" y="143" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">/plugin-view 路由</text>
  <path d="M360 170 L360 200" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#arrow)"/>
  <text x="370" y="190" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">HTTP / WebSocket</text>
  <!-- 第2层：宿主 -->
  <rect x="40" y="210" width="640" height="130" rx="10" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)" stroke-dasharray="6 4"/>
  <text x="60" y="232" font-family="system-ui,sans-serif" font-size="14" font-weight="500" fill="#171717">第2层 · 宿主 Backend（ASP.NET Core 10）</text>
  <text x="60" y="252" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">AppBuilder · PluginManager · PluginServiceRegistry · ExtensionPointManager</text>
  <rect x="60" y="262" width="145" height="34" rx="6" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="132" y="283" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">PluginManager</text>
  <rect x="215" y="262" width="145" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="287" y="283" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">PluginServiceRegistry</text>
  <rect x="370" y="262" width="145" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="442" y="283" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">ExtensionPointManager</text>
  <rect x="525" y="262" width="145" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="597" y="283" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">AppBuilder</text>
  <rect x="60" y="302" width="145" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="132" y="321" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">PluginVersionLayout</text>
  <rect x="215" y="302" width="145" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="287" y="321" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">PluginAssemblyUnloader</text>
  <rect x="370" y="302" width="145" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="442" y="321" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">HotReloadWatcher</text>
  <rect x="525" y="302" width="145" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="597" y="321" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">MvcActionDescriptor</text>
  <path d="M360 340 L360 370" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#arrow)"/>
  <text x="370" y="360" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">IContext 桥接</text>
  <!-- 第3层：内核 -->
  <rect x="40" y="380" width="640" height="130" rx="10" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="60" y="402" font-family="system-ui,sans-serif" font-size="14" font-weight="500" fill="#1A1759">第3层 · 内核 Core + 契约 Abstractions</text>
  <text x="60" y="422" font-family="system-ui,sans-serif" font-size="12" fill="#4B3FE3">ForgeSelf.Core（零外部依赖） + ForgeSelf.Abstractions（契约全集）</text>
  <rect x="60" y="432" width="110" height="34" rx="6" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="115" y="453" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">IContext</text>
  <rect x="180" y="432" width="110" height="34" rx="6" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="235" y="453" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">Context</text>
  <rect x="300" y="432" width="110" height="34" rx="6" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="355" y="453" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">Fiber</text>
  <rect x="420" y="432" width="110" height="34" rx="6" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="475" y="453" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">EventBus</text>
  <rect x="540" y="432" width="130" height="34" rx="6" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="605" y="453" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">Service 基类</text>
  <rect x="60" y="472" width="110" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="115" y="491" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">IPlugin</text>
  <rect x="180" y="472" width="110" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="235" y="491" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">PluginMetadata</text>
  <rect x="300" y="472" width="110" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="355" y="491" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">能力接缝 ×12</text>
  <rect x="420" y="472" width="110" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="475" y="491" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">IExtensionPoint</text>
  <rect x="540" y="472" width="130" height="30" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="605" y="491" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">PluginLifecycleEvent</text>
  <path d="M360 510 L360 540" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#arrow)"/>
  <text x="370" y="530" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">Fiber.Mount(Apply)</text>
  <!-- 第4层：插件 -->
  <rect x="40" y="550" width="640" height="140" rx="10" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)" stroke-dasharray="6 4"/>
  <text x="60" y="572" font-family="system-ui,sans-serif" font-size="14" font-weight="500" fill="#171717">第4层 · 插件层（12 个独立插件）</text>
  <text x="60" y="592" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">每个插件 = plugin.json + Apply(IContext) + 独立 ServiceCollection</text>
  <rect x="60" y="602" width="115" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="117" y="623" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">AIAgent</text>
  <rect x="185" y="602" width="115" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="242" y="623" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">WorkflowEngine</text>
  <rect x="310" y="602" width="115" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="367" y="623" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">Scheduler</text>
  <rect x="435" y="602" width="115" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="492" y="623" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">ScriptRunner</text>
  <rect x="560" y="602" width="110" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="615" y="623" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">ProxyCapture</text>
  <rect x="60" y="642" width="115" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="117" y="663" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">MemorySystem</text>
  <rect x="185" y="642" width="115" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="242" y="663" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">TodoTracker</text>
  <rect x="310" y="642" width="115" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="367" y="663" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">FileTools</text>
  <rect x="435" y="642" width="115" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="492" y="663" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">DevTools</text>
  <rect x="560" y="642" width="110" height="34" rx="6" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="615" y="663" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">SystemMonitor</text>
  <!-- 插件间服务互通 -->
  <path d="M117 636 C117 680, 242 680, 242 636" stroke="#27D2BF" stroke-width="2" fill="none" marker-end="url(#arrow-accent)"/>
  <rect x="130" y="678" width="110" height="22" rx="11" fill="#EAFBF8" stroke="#27D2BF"/>
  <text x="185" y="693" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#0F766E">ctx.Get&lt;T&gt;()</text>
  <!-- 共享服务表 -->
  <rect x="140" y="720" width="440" height="44" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="360" y="738" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">root 共享服务表（ConcurrentDictionary&lt;Type, (Instance, Provider)&gt;）</text>
  <text x="360" y="755" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#4B3FE3">Register = 全局共享 · RegisterLocal = 本地值 · Get = 本地→共享表</text>
  <path d="M360 720 L360 676" stroke="#52525B" stroke-width="1.5" stroke-dasharray="5 4" fill="none" marker-end="url(#arrow)"/>
  <path d="M242 720 L242 676" stroke="#52525B" stroke-width="1.5" stroke-dasharray="5 4" fill="none" marker-end="url(#arrow)"/>
  <path d="M492 720 L492 676" stroke="#52525B" stroke-width="1.5" stroke-dasharray="5 4" fill="none" marker-end="url(#arrow)"/>
  <!-- 图例 -->
  <rect x="40" y="778" width="12" height="12" rx="2" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="58" y="789" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">内核/核心组件</text>
  <rect x="170" y="778" width="12" height="12" rx="2" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="188" y="789" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">宿主/插件/契约</text>
  <line x1="310" y1="784" x2="340" y2="784" stroke="#27D2BF" stroke-width="2"/>
  <text x="346" y="789" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">插件间服务互通</text>
  <line x1="480" y1="784" x2="510" y2="784" stroke="#52525B" stroke-width="1.5" stroke-dasharray="5 4"/>
  <text x="516" y="789" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">共享表访问</text>
</svg>
```
