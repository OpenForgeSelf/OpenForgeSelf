# 铸己匣「一切皆插件」架构改造方案（含热更新）

> 本文是**唯一执行手册**：整合了 deepseek-harness 调研结论与原路线图落地步骤，并补齐「一切皆插件」缺失的架构支柱（共享 Context、能力接缝、类型化事件、可逆副作用、会话日志、Profile 配置层叠）。
> 调研详情与机制细节见 `../06-research/001-deepseek-harness-plugin-architecture.md`，本文不再重复。
> 定位：总路线图与执行手册，不替代各阶段 spec/tasks。
> **状态（2026-08-19 更新）**：P0 已完成；P1 自注册/可变 DI/文件级热更新已实现（独立程序集仅 MemorySystem 已拆，动态端点移除未做）；P2 已完成；P3/P4 契约已迁入 Abstractions（Backend 侧 Provider 实现与 Profile 配置层叠未接线）；P5 前端动态 import 试点已落地（MemoryView/QuickLinksView/TodoView）；**P6 插件间服务互通（Cordis 共享服务表）待实施**。测试：后端 949+12+9、前端 424 全绿。剩余项见功能档案 [`../02-features/027-cordis-kernel.md`](../02-features/027-cordis-kernel.md)「已知问题 / 待办」。

---

## 0. 目标与范围

**终态目标**
- **一切皆插件**：功能模块（含现有内置插件 FileTools / DevTools / Memory / Scheduler / ScriptRunner / WorkflowEngine / TodoTracker / QuickLinks / AIAgent / TextTools / SystemMonitor）以独立、可替换、可热更新的形式存在。
- **热更新 / 部分更新**：不重启主进程即可启用/停用/升级单个插件，且升级时不断开其他插件。

**关键澄清**：热更新不是独立目标，而是「一切皆插件」可逆注册带来的**自然结果**。先补可逆注册语义，文件锁问题会退化为「等 ALC 回收」的工程细节。

**明确不做**
- 不引入 Cordis（TS/Node 框架，无法 1:1 移植 .NET），只借鉴其**模式**。
- 不做插件市场 / 远程拉取（本期仅本地文件级热更新）。
- 不做 DB 迁移 / 主依赖升级（不在本方案范围）。

---

## 1. 现状盘点（改造前基线，2026-08-14）

> 本节是改造启动时的基线盘点，多数缺口已被后续实现修复（P0-P5 完成度见第 4 节与功能档案）；保留作历史对照与「改造解决了什么」的溯源。

### 1.1 工程层三缺口（热更新做不下去的直接原因）

| # | 缺口 | 代码证据 | 后果 |
|---|---|---|---|
| **1** | **文件锁** | `PluginVersionService.cs:174/245/297` 三处 `File.Copy(file, destFile, true)` 覆盖正在 ALC 加载的 DLL | ALC `Unload()` 异步，GC 回收前 DLL 仍被锁 → 升级 `IOException` |
| **2** | **运行时无 DI / 路由** | `AppBuilder.cs:88 AddControllers()` + `:351 MapControllers()` 仅启动注册一次；插件程序集不在 `ApplicationPartManager`；`csproj` 仅 `None Update="Plugins\**\plugin.json"`，**未 `Compile Remove`** → `Plugins/**` 编入主 exe；`IPlugin.Initialize(IServiceProvider)` 只拿已构建 provider | 插件自带 `[Route]` 控制器实际未被路由；插件无法运行期注册自身 DI 服务 |
| **3** | **前端未插件化** | 前端静态视图 + `features.ts` SSOT，无动态 `import()` | 前端功能无法按插件动态装载 |

### 1.2 架构层四缺失（「一切皆插件」不成立的根本原因）

| # | 缺失 | 现状证据 | 后果 |
|---|---|---|---|
| **4** | **DI 硬编码** | `AppBuilder.cs` 中 12 个插件的服务全部 `builder.Services.AddScoped<...>` 手工注册，并用 `using ForgeSelf.Api.Plugins.*.Services` 显式引入各插件命名空间 | 新增插件必须改框架代码 `AppBuilder.cs`，插件无法自注册 |
| **5** | **无能力接缝** | `IAIService`/`AIService` 单一实现直连，Agent 层直接依赖具体类；无 Service Definition / Provider / Consumer 三件套 | 替换 LLM/文件系统/Shell 等能力需改代码，无法「运行期换实现」 |
| **6** | **无类型化事件总线 + 可逆副作用** | 插件间要么 DI 直接引用，要么 `IServiceProvider` 强转；`IPlugin.Destroy()` 只清两个 List，无 `ctx.Effect()` 统一清理 | 无 `tools/pre-execute` 等拦截点；热卸载后残留定时器/连接/事件订阅 |
| **7** | **无会话日志 / Profile 配置** | 聊天记录有 `IChatTurnService`/`IChatSessionService`，但无「仅追加事件流 + 模型历史投影」；`plugin.json` 无 `Provides/Consumes/Profile/Patch` | 无法满足「模型可见 = 已记录」；无法声明式组合能力方案 |

> 现有地基仍可用：`PluginLoadContext : AssemblyLoadContext(isCollectible:true)`（`PluginLoadContext.cs:26`）、`PluginManager` 生命周期（`Enable/Disable/Destroy→Unload()`）、`IToolRegistry`/`ToolRegistry`（register/validate/execute/timeout）、`AIProviderRegistry`、Agent 系列服务（`IAgentRegistryService`/`IAgentExecutorService`/`IAgentCoordinatorService`）。

---

## 2. 设计蓝本与核心借鉴（精简）

DeepSeek Harness 的「一切皆插件」= **没有内核，只有插件**。五个支柱：共享 Context（服务容器）、能力接缝（定义/提供者/消费者）、类型化事件（emit/waterfall/parallel/serial）、可逆副作用（卸载即逆序回滚）、Profile/Bundle（配置即组装）。机制细节与 dsh 源码证据见调研文档 `../06-research/001-deepseek-harness-plugin-architecture.md`。

映射到 .NET：

| Cordis (TS) | C# / .NET 等价 |
|---|---|
| Context（`ctx`） | `IContext`：服务定位器 + 副作用回收器 |
| Service（`super(ctx,'name')`） | 接口 + `IContext.Register<TService,TImpl>()` |
| `inject: ['tools']` | 构造注入 / `IContext.Get<T>()` |
| `ctx.effect()` | `IDisposable`/`IAsyncDisposable` disposer 集合，卸载逆序释放 |
| typed events（4 模式） | `IEventBus`：`EmitAsync`/`WaterfallAsync`/`ParallelAsync`/`SerialAsync` |
| capability seam | `ISeam<T>`：定义 + 默认 Provider + 消费者 `ctx.Get<T>()` |
| Bundle/Profile | `plugin.json` 的 `Provides/Consumes/Profile/Patch` |
| SessionEvent append-only | `ISessionStore`（事件溯源，append-only） |
| AgentLoop React | `IAgentLoop` + `IInbox` |

> **一句话根因**：dsh 热更新稳，不是因为它解决了文件锁，而是「卸载即回滚」让文件锁退化为工程细节。本项目应先补可逆注册，再谈破锁。

---

## 3. 目标架构核心抽象（POC 骨架）

### 3.1 IContext / IEventBus

```csharp
public interface IContext : IServiceProvider
{
    void Register<TService>(TService instance) where TService : class;
    void Register<TService, TImpl>() where TImpl : class, TService;
    TService? Get<TService>() where TService : class;
    IDisposable Effect(Func<IDisposable> sideEffect);   // 可逆副作用
    IEventBus Events { get; }
    IContext Derive();                                   // 会话级派生上下文
}

public interface IEventBus
{
    Task EmitAsync<TEvent>(string name, TEvent payload);                       // 广播
    Task<R> WaterfallAsync<TEvent, R>(string name, TEvent payload, Func<Task<R>> fallback); // 环绕/短路
    Task ParallelAsync<TEvent>(string name, TEvent payload);                   // 并发
    Task<R?> SerialAsync<TEvent, R>(string name, TEvent payload);              // 按序短路
    IDisposable On<TEvent>(string name, Func<TEvent, Task> handler);
}
```

### 3.2 能力接缝示例

```csharp
public interface ILlmRuntime   // 服务定义（接缝）
{
    IAsyncEnumerable<StreamChunk> StreamAsync(IReadOnlyList<Message> messages, CancellationToken ct);
}
// Provider 通过 IContext 注册，消费者通过 ctx.Get<ILlmRuntime>() 获取。
// 替换 Provider = 换注册实例，消费者零改动。
```

### 3.3 插件自注册示例（对标 Cordis Service）

```csharp
public class AIAgentPlugin : IPlugin
{
    public void Apply(IContext ctx)
    {
        ctx.Register<IToolRuntime>(new ToolRuntime(ctx));     // 贡献服务
        ctx.Get<IToolRuntime>()?.RegisterTool(new GetCurrentTimeToolFunction("aiagent.plugin")); // 注册工具（Id 来自 plugin.json）
        ctx.Effect(() => {                                    // 可逆副作用：卸载自动清理
            var timer = new Timer(_ => Heartbeat(), null, 0, 5000);
            return timer.Dispose;
        });
    }
}
```

### 3.4 插件清单（plugin.json 扩展）

```json
{
  "id": "aiagent.plugin",
  "name": "AI代理插件",
  "version": "1.0.0",
  "entry": "ForgeSelf.Plugins.AIAgent.dll",
  "provides": ["ctx.agents", "ctx.tools", "ctx.agentLoop"],
  "consumes": ["ctx.llm", "ctx.sessions", "ctx.fs"],
  "profile": ["default"],
  "contributes": {
    "services": ["IAIAgentService"],
    "endpoints": ["/api/plugins/aiagent"],
    "frontend": { "views": ["AIChatView"], "menu": "AI聊天" }
  }
}
```

---

## 4. 分阶段改造路线（优化后，向前演进，不做 big-bang）

> 每阶段独立可验证；遵守「向前改，不回滚」（用新代码替换，不用 git revert/stash/reset）。
> 原 roadmap 阶段 A-E 已重组为 P0-P5，映射关系在每阶段标注。

### P0 — 内核底座 + 统一契约（已完成）

**目标**：落地 `ForgeSelf.Core` 内核与 `ForgeSelf.Abstractions` 契约程序集，确立 `IPlugin.Apply(IContext)` 单一契约与 Fiber 生命周期。

**动作**
- `ForgeSelf.Core`：`IContext`/`Context`、`IEventBus`/`EventBus`、`Service`、`Fiber`（全部已落地，`Context.SetHostProvider` 桥接宿主 MS DI）。
- 新增 `ForgeSelf.Abstractions`：业务能力接缝（`ILlmRuntime`/`ISessionStore`/`IAgentLoop`/`IInbox`/`IEndpointRegistry`/`IConfigurationService`/`ILogService`/`IUsageStatsService`/`IWorkflowService`/`IWorkflowExecutor`/`IWorkflowAIAdvisor`/`IScriptTemplateService`）+ `IPlugin` + 共享 DTO（已建立）。
- `IPlugin` 直接改为单一 `Apply(IContext)`，删除旧 `Initialize(IServiceProvider)`/`Start`/`Stop`/`Destroy`（已全量迁移，见 ADR D1）。
- `plugin.json` 增加 `provides`/`consumes`，`PluginManager` 按 `consumes` 做服务级拓扑排序（**未落地**：`PluginMetadata` 类已支持 `Provides`/`Consumes` 字段，但 12 个 `plugin.json` 均未填写；`PluginManager` 的 `TopologicalSort` 按 `Dependencies` 排序）。

**验证**：`dotnet build` + 内核单测（Fiber 卸载逆序回滚，`Core.Tests` 12/12 全绿）。✅
**风险**：中（契约一次性切换）。**回滚点**：契约先落到 Abstractions，再逐插件迁移。✅ 已完成

### P1 — 插件独立程序集化 + 可逆注册 + 热更新闭环（部分完成）

**目标**：12 个插件拆为独立程序集，各自 `Apply(IContext)` 自注册；运行期可注销 DI 服务与 HTTP 端点；升级不再触发文件锁。

**动作**
- 每个 `Plugins/<Name>/` 建独立类库项目（引用 Core + Abstractions），输出独立 DLL（见 ADR D2）。**部分完成**：仅 `MemorySystem` 已拆独立 csproj（仅引用 Core + Abstractions，其控制器经 ApplicationPartManager 被路由发现）；其余 11 个仍编译进主程序集（`Backend.csproj` `Compile Remove` 仅排除 MemorySystem）。
- 各插件 `Apply(IContext)` 内注册服务/工具/端点/副作用；卸载时由 Fiber 逆序回滚。✅ 12 个插件全部迁移到 `Apply` 自注册（`ctx.Get<IServiceCollection>()` + `ctx.Effect`）。
- `AppBuilder.cs` 删除 12 个插件的 `AddScoped/AddSingleton` 与 `using`，只保留平台级注册。✅ 已删除；仅保留启动路径对 `ITaskScheduler`（Scheduler 插件）的一次性 `GetRequiredService` 直引用。
- 可变 DI 容器：`ServiceCollection` + 自定义 `IServiceProvider` 工厂，卸载按注册逆序 `Remove`。✅ `PluginServiceRegistry`（每插件子容器 + 宿主透传 + Transient 转发描述符 + `BuildAll` 延迟构建；卸载后 `GetRequiredService` 抛「插件服务已卸载」）；宿主 Scoped 语义为近似（透传统一 Transient 转发）。
- 端点：`app.Map*` 返回句柄存入 `EndpointDataSource`，卸载时移除；控制器型插件用 `ApplicationPartManager` 增删 part。**部分完成**：启动时已把插件程序集 `AssemblyPart` 加入 `ApplicationPartManager`（MemorySystem 控制器被发现）；卸载时移除 `AssemblyPart` 未做（见功能档案已知问题②）。
- side-by-side 版本目录：`Plugins/<id>/versions/<semver>/<id>.dll`，`current` 指针指当前版本，保留 N=2（见 ADR D3）。✅ `PluginVersionLayout` 已实现（原子切换指针）。
- `FileSystemWatcher` 监听 `Plugins/<id>/`，debounce 300ms 自动 reload；单插件失败不影响宿主并回退上一可用版本。✅ `PluginHotReloadWatcher`（Testing 环境跳过）+ `PluginAssemblyUnloader.ForceCollect`/`FileShare.None` 探测/延迟删除。

**验证**：xUnit「启用→端点可达→停用→端点消失」；「升级→旧 DLL 可删→新版本加载」；手动一次真实热升级无 `IOException`。✅ 后端测试 949/949 全绿（含插件生命周期/热更新相关）。
**风险**：中高（独立程序集 + 可变 DI + ALC 回收）。**回滚点**：插件逐目录迁移，每迁移一个即验证。

### P2 — 类型化事件总线 + 可逆副作用（已完成）

**目标**：插件间通信统一走事件；工具执行有拦截点；热卸载真正可靠。

**动作**
- `IEventBus` 落地四种分发模式（emit/waterfall/parallel/serial），单测覆盖。✅ `EventBusTests` 全绿。
- `ToolRegistry` 执行管道接入 `tools/pre-execute`（允许/拒绝门）、`tools/execute`（调度包装）、`tools/post-execute`（检查/替换结果）。✅ 已接线：`tools/pre-execute` 用 `SerialAsync` 拒绝门，`tools/execute`/`tools/post-execute` 用 `EmitAsync`。
- `AgentExecutorService` 接入 `agent/request`、`llm/stream`、`turn/start`、`turn/end` 事件。**未落地**（事件族仍为规划）。
- `IContext.Effect()` 落地：`PluginManager` 为每插件维护 disposer 列表，`Destroy()` 逆序执行。✅ `Context.Effect` + `Fiber.Dispose` 逆序回滚。

**验证**：单测四种分发模式；`tools/pre-execute` 能拒绝/放行；插件卸载后定时器/订阅被清理（写一个带 `ctx.Effect` 定时器的测试插件验证）。✅ 单测已覆盖四模式与 Fiber 逆序回滚。
**风险**：中（改造 ToolRegistry 执行路径）。**回滚点**：保留旧同步执行路径 fallback。

### P3 — Agent Loop 标准化 + Session 日志（契约已完成，实现未接线）

**目标**：散落的 Agent 服务收敛到 `IAgentLoop`；建立「仅追加事件流 + 模型历史投影」会话模型。

**动作**
- `ISessionStore` 实现（基于现有 SQLite + 追加事件表 `SessionEvents`）。**契约已建**（`ISessionStore`：`Append`/`Replay`/`DeriveMessages` + `SessionEvent`）；Backend 侧实现未接线。
- `IAgentExecutorService`/`IAgentCoordinatorService` 重构为实现 `IAgentLoop`（Claim→Assemble→Request→Execute→Repeat）。**契约已建**（`IAgentLoop.RunAsync → TurnEvent`）；重构未做。
- `IInbox` 实现（followup/steer/inject 三种输入语义）。**契约已建**（`IInbox.Followup/Steer/Inject`）；实现未接线。
- 每次 LLM 请求前执行「模型可见 = 已记录」校验（`Replay()` 能重建全部模型可见内容）。未落地。

**验证**：`dotnet test`（会话重放重建完整模型历史，Resume/fork 可用）；运行时聊天流式响应正常，历史可追溯。**契约单测已绿**（`Abstractions.Tests` 9/9 含 `SessionStoreContractTests`/`AgentLoopContractTests`/`InboxContractTests`）；运行时验证待接线后执行。
**风险**：中高（核心 Agent 流程）。**回滚点**：新 `IAgentLoop` 与旧服务并行，配置切换后删除旧路径。

### P4 — LLM 接缝 + Profile/Bundle 配置层叠（契约已完成，实现未接线）

**目标**：`IAIService`/`AIProviderRegistry` 收敛为 `ILlmRuntime` 接缝；实现声明式组合与配置层叠。

**动作**
- `ILlmRuntime` 定义 `Message`/`StreamChunk` 内部词汇；每个 Provider（OpenAI/Anthropic/Responses/Models/AgentChat）实现为 `ILlmRuntime` Provider，通过 `IContext` 注册（对标 `ctx.llm`）。**契约已建**（`ILlmRuntime.StreamAsync` + `Message`/`StreamChunk`）；Provider 适配未接线（`IAIService` 仍为直连实现）。
- Agent Loop 通过 `ctx.Get<ILlmRuntime>()` 调用，不再直连 `IAIService`。未落地。
- `plugin.json` 完整落地 `provides/consumes/profile/patch`；`PluginManager.DumpEffectiveConfig()` 输出合并配置树。**未落地**：`PluginMetadata` 类已支持 `Provides`/`Consumes`，但 12 个 `plugin.json` 均未填写（仅 3 个有 `frontend` 声明：MemorySystem/QuickLinks/TodoTracker）。
- 前端菜单/工具由后端能力清单下发（`PluginController` 已有 `PluginMenuItemDto`/`PluginToolFunctionDto` 雏形）。✅ `PluginController` + `frontend-manifest` 已落地（见 P5）。

**验证**：切换 Provider 仅换注册/配置不改 Agent 代码；不同 Profile 得到不同能力组合；`--patch` 覆盖生效。待接线后验证。
**风险**：中高（AI 网关核心路径）。**回滚点**：`IAIService` 作为 `ILlmRuntime` 适配层过渡。

### P5 — 前端插件运行时（试点已完成）

**目标**：前端功能按插件动态装载，而非硬编码视图。

**动作**
- 扩展点协议 `contributes.views/menus/routes`；后端「已装载插件清单」API 返回 `contributes`。✅ `PluginMetadata.FrontendContributes`（Views/Menu/Route/Icon）+ `GET /api/plugin/frontend-manifest`。
- 视图用 `defineAsyncComponent(() => import(/* @vite-ignore */ url))` 动态挂载；`features.ts` 退化为「运行期清单」。✅ `router/dynamicPlugins.ts`：`/plugin-view` 命名空间 + 真实 `import()` 懒加载 + `registerManifestRoutes`/`clearManifestRoutes`；`features.ts` 的 `mergeFeatureList` 以内置为 fallback 合并清单菜单。
- 先试点 `MemoryView` 改造为首个前端插件，验证协议闭环。✅ 试点覆盖 MemoryView/QuickLinksView/TodoView（与 3 个含 `frontend` 声明的 plugin.json 对应）。

**验证**：vitest 组件测试（动态挂载+卸载）；手动启停插件断言视图增减。✅ 前端 424/424 全绿（含 `dynamicPlugins.test.ts`/`pluginManifest.test.ts`）。
**风险**：中（动态 import 需 `@vite-ignore` + 运行期 URL）。**回滚点**：前端保留本地菜单 fallback。✅ 清单失败/为空时回退静态路由。

### P6 — 插件间服务互通（Cordis 共享服务表，2026-08-19 新增 · 服务互通部分已实施 ✅）

**目标**：补 Cordis 原案「服务默认全局可见」语义——插件 A 提供的服务可被插件 B 经 `ctx.Get<T>()` 消费（当前兄弟 Fiber 上下文互不可见，`IWorkflowAIAdvisor` 因此解析恒为 null）。依赖调研 §4（ReflectService 全局 store）+ §5.5（软依赖判例）+ §5.6（服务可见性/生命周期权威裁决 A/D/E/F）+ §6（偏差对照表，重点偏差#1/#2/#4）。**决策来源 = 调研文档 `06-research/001`，不自作设计**。

**动作**
- **Core 层（`Context`）**：引入 root 持有的共享服务表，条目 = `(eager 单例实例, 提供者 Fiber)`；`Register<T>()` 对标 Cordis `provide()`——写共享表、走 `ctx.Effect` → Fiber 逆序回滚自动摘除（调研 §5.6 裁决 F）。fiber 私有状态（`PluginMetadata`/`IServiceCollection`）用独立本地值 API（`RegisterLocal`），不复用 `Register`（调研 §5.6 裁决 A）。`Get<T>()` 解析顺序 = 本地值 → 共享表（裁决 D）。
- **软依赖规则**：可选能力 = `ctx.Get<T>()` 探测，null 走降级，**消费者禁止缓存实例为字段**（每次用每次 Get，防热重载悬空）。硬依赖（`Consumes` + PENDING 自动重启）机制缓建，首个强依赖接缝出现时落地（见待办）。
- **AIAgent 插件**：`Apply` 补注册 `IAIWorkflowAssistant`/`IWorkflowAIAdvisor` 进子容器 + **eager 构造** `AIWorkflowAdvisor` 并 `ctx.Register<IWorkflowAIAdvisor>(实例)` 提供到共享表（调研 §5.6 裁决 F，非懒解析委托）。
- **WorkflowEngine 插件**：`WorkflowExecutor` 构造注入 `IContext`，`GetAIAdviceAsync` 把 `_serviceProvider.GetService(typeof(IWorkflowAIAdvisor))` 改 `_ctx.Get<IWorkflowAIAdvisor>()`；null → 默认重试（保留现有降级语义）。
- **PluginServiceRegistry**：`IWorkflowAIAdvisor` 等「插件→插件」契约**不进 `CollectForwardDescriptors`**（调研 §5.6 裁决 E），仅走 `ctx.Get`。
- **事件总线传播**（配套修正，可同批或独立）：派生上下文共享根 `EventBus`（`Derive` 传递），详见功能档案待办 5。

**验证（dispose 门禁）**：xUnit——AIAgent 提供后可 `ctx.Get<IWorkflowAIAdvisor>()` 解析 → 卸载 AIAgent 后解析为 null 且 WorkflowExecutor 走默认重试 → 重挂载后恢复；`dotnet build` + `dotnet test` 全量回归（现状 949+12+9 不得回退）。✅ **已实施（2026-08-19，来源:输入7 /spec）**：Core 共享服务表 + `RegisterLocal` + 解析顺序 + AIAgent eager 提供 + Executor 改 `ctx.Get` + 移除冗余转发全部落地；新增 `KernelServiceInteropTests` 7 例 dispose 门禁；`dotnet build` 0 错 + `dotnet test` 988/988 全绿（基线 981 无回退）。**事件传播修正（P6 配套）未实施**，独立排期（027 待办5）。
**风险**：中（内核 `Context` 语义变更，影响全部插件；改动集中、测试可覆盖）。**回滚点**：`Context` 语义修改先落单测，再逐插件切消费端。

---

## 5. 统一插件契约（已决策：单一 Apply + Fiber）

```csharp
public interface IPlugin
{
    void Apply(IContext ctx);   // 一切贡献（服务/工具/端点/副作用/事件）都在此声明
}
```

- **元数据单一真源**：Id/Name/Version/Provides/Consumes/contributes 全部在 `plugin.json`，插件代码不再重复声明。
- **生命周期由 Fiber 统一**：`Discovered → Loaded(ALC) → Applied → Disposed`；`Apply` 内注册的一切 effect 在 Disposed 时逆序自动回滚。
- **端点注册**：通过宿主接缝 `ctx.Get<IEndpointRegistry>()`，卸载时由 effect 移除句柄。

**生命周期状态机（Fiber）**

```
Discovered → Loaded(ALC) → Applied(Apply 完成)
                                  │
                                  └── Disposed（逆序执行 effect 清理 + ALC Unload）
```

**HMR 安全测试要求（写入插件契约）**
- 每个插件一条 xUnit：`Dispose` 后断言 `IsCollectible==true` 且 `File.Open(...,FileShare.None)` 可删 DLL。
- 每条 `contributes.endpoints` 一条「注册→可达→注销→不可达」测试。
- 每个贡献 registry 的插件一条「dispose 掉 fiber 后断言清理成功」测试（对标 dsh 每 registry 一条 dispose 测试）。

---

## 6. 验证门禁（每阶段强制）

| 层 | 命令 | 门禁 |
|---|---|---|
| 后端构建 | `dotnet build` | 0 错误 |
| 后端测试 | `dotnet test`（`ForgeSelf.Api.Tests`） | 全绿，含新增 dispose 释放测试 |
| 前端类型/规范 | `pnpm run check` | 0 错误 |
| 前端测试 | `pnpm run test` | 全绿 |
| 手动 | 跨目录启动 + 一次真实热升级 | SPA 正常 + 升级无 IoException |

---

## 7. 风险与回滚

- **资源泄漏是头号风险**：ALC 不回收的主因是残留根引用（事件订阅、静态字段、定时器）。每个 Fiber 卸载必须逆序 `Dispose` 全部 effect 并清空静态引用。
- **向前改不回滚**：用新代码替换旧路径，不用 `git revert/stash/reset`；每阶段 git 独立提交便于 diff 溯源。
- **高风险升级点**（契约破坏性变更、跨插件接口改名、主依赖升级）按 AGENTS.md 升级规则先确认再动。
- **分步迁移**：插件逐目录迁移为独立程序集，每迁移一个即验证，避免一次性外置引发大面积构建失败。

---

## 8. 里程碑与执行顺序

```
P0 接缝抽象+Context+自注册 ──► P1 可逆注册+可变DI+动态端点+解耦+破锁+自动reload
        （架构地基，先做）              （后端热更新闭环，业务价值最高）
                                           │
                                           ▼
                              P2 事件总线+可逆副作用（让热插拔真正可靠）
                                           │
                                           ▼
                              P3 Agent Loop+会话日志 ──► P4 LLM接缝+Profile ──► P5 前端插件
                                           │
                                           ▼
                              P6 插件间服务互通（Cordis 共享服务表 + 事件传播修正）
```

**完成度（2026-08-19）**：P0 ✅ / P1 🟡（自注册+可变 DI+版本目录+破锁+reload 已完成；独立程序集仅 MemorySystem，动态端点移除未做）/ P2 ✅ / P3-P4 🟡（契约已迁 Abstractions，Backend 侧实现未接线）/ P5 ✅（试点）/ **P6 🟡（服务互通 ✅ 已实施 2026-08-19，988/988 全绿；事件传播修正待排期）**。

**建议**：先 P0→P1 打通「后端热更新闭环」（地基 + 高价值），再做 P2 让热插拔可靠，P3-P5 依次补齐架构支柱与前端。P5 可独立排期。→ 该路径已走通；剩余项（11 插件拆独立程序集、动态端点移除、P3/P4 实现接线、QuickLinks 表初始化、WorkflowHub 静态债、**P6 事件传播修正**）见功能档案「已知问题 / 待办」。

---

## 9. 已决策（ADR）

> 本项目未上线，决策**不考虑向后兼容**，以最佳架构为唯一标准。五项决策已拍板，完整理由见 [`../07-decisions/001-cordis-kernel-architecture.md`](../07-decisions/001-cordis-kernel-architecture.md)。

| # | 决策 | 一句话 |
|---|------|--------|
| D1 | 契约 | 统一为 `IPlugin.Apply(IContext)`，生命周期由 Fiber 统一，不留兼容层 |
| D2 | 程序集 | 12 插件一步到位拆独立程序集 + 新增 `ForgeSelf.Abstractions` 契约程序集 |
| D3 | 版本目录 | `Plugins/<id>/versions/<semver>/` + `current`，保留 N=2 |
| D4 | 前端 | 轻量动态 `import()` + `contributes`，不上 module-federation |
| D5 | 节奏 | 按 P0→P5 逐阶段出 spec→plan→tasks 分批实现 |

据此拆解为 `specs/NNN-*/` 的 spec → plan → tasks 正式落地。
