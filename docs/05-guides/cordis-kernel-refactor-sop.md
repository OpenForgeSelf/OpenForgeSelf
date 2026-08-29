# Cordis 内核重构 SOP（TDD + 渐进式 + 子代理循环）

> 状态：稳定（随执行经验持续回写）
> 最后更新：2026-08-14
> 适用：把 OpenForgeSelf 重构为「一切皆插件」（ADR 001）。本文是执行层手册，设计见 [`../01-architecture/cordis-kernel.md`](../01-architecture/cordis-kernel.md)，路线见 [`../15-roadmap/plugin-architecture.md`](../15-roadmap/plugin-architecture.md)。

## 1. 目标与铁律

- 目标：P0→P5 逐条落地，每个任务「测试 → 实现 → 验证」闭环，不跳步。
- 铁律：
  1. **TDD 优先**：先写失败测试（红）→ 最小实现（绿）→ 回归全量测试。
  2. **渐进式**：一次只改一个最小可验证单元，改完立即 `dotnet build` + `dotnet test`，不做大爆炸。
  3. **子代理边界**：子代理只做「编译 + 单元测试」，不做运行时验证（重启后端/e2e）、不 git 提交、不改 TODO.md / `.forgeself/memory/*` / 本 SOP。
  4. **经验回馈**：每个子代理返回后，主代理提炼可复用规律 → 回写本 SOP「§4 经验」+ 必要时写 `.forgeself/memory/MEMORY.md`。

## 2. 单任务标准循环

1. 明确任务边界与验收标准（写入子代理 prompt）。
2. 子代理：写测试（红）→ 实现（绿）→ `dotnet build` + `dotnet test`。
3. 子代理返回：改动文件清单、测试结果、遇到的问题与解决。
4. 主代理：复核 diff + 测试绿 → TODO 勾选 → 提炼经验回写 SOP。
5. 失败处理：子代理连续 3 次仍未通过 → 升级给用户。

## 3. 子代理任务模板（prompt 必含）

- 背景：项目根路径、相关契约（ADR D1/D2）、已存在的内核文件与 API。
- 只做：明确文件/测试范围；编译 + 单元验证命令。
- 不做：运行时验证、git 提交、修改 TODO/MEMORY/SOP。
- 验收：具体断言 + 测试命令 + 预期输出（全绿）。

## 4. 经验（随执行回写）

- **T1（Fiber + Core.Tests）**：xUnit 包版本对齐 Backend.Tests（Microsoft.NET.Test.Sdk 17.12.0 / xunit 2.9.2 / xunit.runner.visualstudio 2.8.2 / coverlet 6.0.2）。`Fiber` 用 `new Context(parent as Context)` 建派生作用域、`Interlocked` 保证 `Dispose` 幂等。**关键坑**：`Context.Dispose` 必须清空 `_services`，否则卸载后 `Get<T>()` 仍返回旧实例（服务未真正注销）。
- **T2（Abstractions）**：`ForgeSelf.Abstractions` 保持纯 net10.0（不引 ASP.NET）；`IPlugin`/`IEndpointRegistry` 只需 `using ForgeSelf.Core;`。验证插件契约用 `Fiber.Mount(plugin.Apply)`，断言 Dispose 后服务注销。
- **T3（切换插件契约）**：`IContext : IServiceProvider` 是隐式桥——旧代码要 `IServiceProvider` 处直接传 `ctx`，无需改签名。PluginManager 用单例根 `Context` 作父级 + 每插件一个 `Fiber`；`Mount` 前把 `PluginMetadata` 注册进 `fiber.Context`，插件用 `ctx.Get<PluginMetadata>()?.Id` 拿自身 Id。可逆副作用（调度器启停/采样启停）用 `ctx.Effect`，一次性初始化并入 Apply，纯日志/清列表的 Start/Stop/Destroy 直接删。**注意**：`Plugins/SamplePlugin/` 只有 plugin.json 无实现类（实际 11 个插件类）；改契约要连带迁移测试工程里的 Fake 插件实现。
- **T4a（共享契约迁 Abstractions）**：迁 `IExtensionPoint`/`IMenuExtension`/`IToolFunctionExtension`/`PluginMetadata` 只改命名空间不改签名；Grep `using ForgeSelf.Api.Plugins.Abstractions` 逐文件更新（35 文件）。**遗留坑**：`PluginScaffolderService.cs` 内 raw string 模板仍生成旧 `IPlugin`(Initialize/Start/Stop/Destroy) 契约，非编译期引用不报错 → 需后续更新脚手架模板（记 TODO T4c）。
- **T4b-1（迁 IUsageStatsService）**：迁 `IUsageStatsService` + 12 个纯 DTO 到 Abstractions。**注意**：不要一刀切替换 using——混合使用者（还引用 `IWorkflowUsageService`/`IWorkflowRecommendationService`/`UsageStatsService` 实现）保留旧 using，纯 `IUsageStatsService` 使用者才走 Abstractions；`AppBuilder.cs` 因直接引用该接口，必须补一行 `using ForgeSelf.Abstractions;`（边界"不改 AppBuilder"与编译要求冲突时，做最小 using 级改动）。**偶发测试**：`HashServiceTests.AesDecryptAsync_WrongKey_ThrowsException` 全量跑偶发失败、单独跑 28/28 绿（顺序相关，非回归）。插件剩余外部 Backend 依赖：`ApiResponse`(Models.Plugins，最广)、`IConfigurationService`/`ILogService`、`IWorkflowUsageService`/`IWorkflowRecommendationService`、`AIConfig`(Models)——拆独立程序集前须先迁这些。
- **T4b-2a（迁 ApiResponse）**：迁 `ApiResponse`/`ApiResponse<T>`（24 文件引用更新）。**关键坑 CS0104**：共享 DTO 归并后，`PagedResult<T>` 同时存在于 Abstractions（`UsageStatsModels`）与各插件本地 `*.Models.PagedResult<T>` → 产生命名空间歧义；解决是插件本地 `PagedResult<T>` 用全限定名 `ForgeSelf.Api.Plugins.<X>.Models.PagedResult<T>`。**规律**：归并共享 DTO 时，同名类型在 Abstractions 与插件本地并存会歧义，须显式全限定其中一侧。
- **T4b-2b（迁剩余共享类型）**：迁 `IConfigurationService`(+AIConfig/AIProviderConfig/AIProviderType)/`ILogService`/`IWorkflowUsageService`/`IWorkflowRecommendationService`(+Workflow DTO 15 个)。**坑**：①插件自带重复 `AIConfig`（AIAgent ChatModels.cs）与 Abstractions 构成 CS0104 源，删插件副本统一用 Abstractions；②旧别名指向已迁类型要同步改（CoreAIConfig 删/CoreWorkflowExecutionStatus 改指向）；③PowerShell 批量脚本 Get-ChildItem 枚举会静默截断，幂等复跑补齐。迁完后 **12 插件代码零 Backend 依赖**（仅宿主 PluginManager/PluginVersionService 还用 4 个宿主 DTO，合法）。**顺序结论**：拆独立程序集前必须先做 T5（AppBuilder 去硬编码），否则 Backend 编译仍引用插件类型。
- **T5（服务注册接缝 + AppBuilder 去硬编码）**：①`ResolvePluginInstance`：程序集文件存在→PluginLoadContext 隔离加载，不存在（内嵌插件）→回退 `typeof(PluginManager).Assembly` 解析 EntryType（顺带解决 T032）。②`RegisterAllServices(IServiceCollection)`：发现→拓扑排序→逐插件建 Fiber→`Register(metadata)`+`Register(services)`→`Mount(Apply)`。③AppBuilder 用 `using(var bootstrap=builder.Services.BuildServiceProvider())` 拿 PluginManager 实例→RegisterAllServices→`builder.Services.AddSingleton(pluginManager)` 复用同一实例（无 double-build）。④**坑**：勿盲删所有插件 `.Services` using——平台级仍引用 `IToolRegistry/ToolRegistry`(AIAgent.Services)、`ICronParser/CronParser`(Scheduler.Services)、`IRuntimeDetector`(ScriptRunner.Services)，这 3 个 using 要保留。⑤TextTools 真实接口名 `IEncodingService/IHashService`（非别名 ITextEncoding/ITextHash）。⑥副作用提前到启动期，插件内须 try/catch 兜底。
- **T4c（脚手架模板）**：`PluginScaffolderService.GeneratePluginMainClass` raw 模板改 `Apply(IContext)`，删旧生命周期/元数据属性与 menuRegistration/toolRegistration 死片段。**注意**：`Provides/Consumes` 字段在 `PluginMetadata` 里从未实现（ADR/roadmap 设想过但代码没有）——文档与代码有偏差，后续要么补字段要么改文档。
- **T7（事件总线接线）**：ToolRegistry 注入 IEventBus，三个事件 `tools/pre-execute`(SerialAsync 短路拒绝)、`tools/execute`(Emit)、`tools/post-execute`(Emit)。**关键缺口**：原 IEventBus 只有 `On`(Func&lt;TEvent,Task&gt;) 注册入口，而 `SerialAsync` 只消费 `Func&lt;TEvent,Task&lt;TResult?&gt;&gt;`——serial 短路 handler 根本没有注册入口；补 `OnSerial&lt;TEvent,TResult&gt;` 才打通。事件上下文类型 ToolCallContext 放 ToolRegistry 同目录。
- **T8（会话/LLM 接缝）**：新增 `ISessionStore`/`IAgentLoop`/`IInbox`/`ILlmRuntime` + 轻量 `SessionEvent`/`Message`/`StreamChunk`/`TurnEvent`/`AgentRunRequest` 到 Abstractions；`DeriveMessages` 直接投影 `IReadOnlyList<Message>` 与 `ILlmRuntime` 词汇打通。命名无冲突（Grep 全仓确认）。契约级 Fake 测试 9/9。
- **T4b-2c（拆 MemorySystem 独立程序集）**：建 `ForgeSelf.sln`（`dotnet new sln --format sln`，默认 .slnx 要用 --format；同名 solution 文件夹冲突 MSB5004 手删）。插件 csproj `net10.0`+`FrameworkReference Microsoft.AspNetCore.App`+`AssemblyName`；Backend `Compile Remove`+`ProjectReference ReferenceOutputAssembly=false`+`AfterTargets=Build` 复制 DLL 到 `Plugins/<id>/`；Backend.Tests 加 ProjectReference。**关键坑**：①plugin.json 原用 `entryPoint`（PluginMetadata 无此字段）→ 解析空串 `GetType("")` 加载失败，改标准 `EntryAssembly/EntryType`；②NewLife.Core 传递依赖版本漂移（XCode 带旧版 vs 宿主新版）→ 显式加新版对齐；③嵌套 bin/obj 被 Web SDK 默认 glob 当 Content 复制 → 加 Content/None Remove。
- **T6（热更新闭环·文件级）**：新增 `PluginVersionLayout`（side-by-side `versions/<semver>/` + `current` 原子指针）、`PluginAssemblyUnloader`（`ForceCollect` + `FileShare.None` 破锁试探 + 延迟删除）、重写 `PluginVersionService.Update/Rollback`（StageVersion→DisablePlugin→ForceCollect→试探→切指针→Prune N=2）、`PluginHotReloadWatcher`（IHostedService 递归监听 + debounce 300ms + Testing 跳过）。`ResolvePluginInstance` 走 side-by-side→扁平→内嵌回退。真实 ALC 回收端到端未验证（环境无独立 DLL），用内嵌入口类型验证停用→重载序列；938 全绿。可变 MS DI/动态端点移除留后续。
- **T9（前端插件运行时）**：`FrontendContributes` 协议（PluginMetadata.Frontend）+ `/api/plugin/frontend-manifest` + 前端 store/features merge（按 path/route 去重，避免与内置项重复）。真实动态 import 视图渲染留后续。前端验证 `pnpm run check` + `pnpm run test`（418 用例）。
- **P1 优化**：①补 `OnWaterfall` 注册入口（原 WaterfallAsync 是死 API）；②AES 测试放宽断言（CBC+PKCS7 无认证，错误密钥约 1/256 概率解出合法 padding 不抛）；③PluginMetadata 补 `Provides/Consumes` 字段（消除文档-代码偏差）。**留后续（风险高未强行做）**：可变 MS DI 容器+动态端点移除、前端真实动态 import 视图渲染（跨插件耦合、静态 Hub、RecordUsageAsync 已清理）。
- **端到端验证（必守）**：拆独立程序集后，插件 `[Route]` 控制器**不会被 MVC 自动发现**（Backend 用 `ReferenceOutputAssembly=false`，AssemblyPart 不含插件）→ 表现为 API 404，且 build/test 覆盖不到。**修复**：PluginManager 存储独立程序集 `Assembly` + `GetLoadedPluginAssemblies()`；AppBuilder 在 `app.Build()` 后把插件程序集 `ApplicationPartManager.ApplicationParts.Add(new AssemblyPart(asm))`。验证方法：playwright/builtin_browser 控制浏览器逐插件路由导航，看 console 错误数 + API 状态码（404→修复→200）。另：`features.ts` 里 `path:null` 的功能（如「定时任务」）前端无独立页，「打开/配置」按钮 disabled，属前端配置而非后端故障。
- **可变 MS DI 容器（Option A 落地）**：每插件独立 `ServiceCollection`，`Mount` 只存集合不立即 Build；`BuildAll(hostServices, hostProvider)` 在 host Build 后构建。**关键坑**：插件服务「传递依赖」宿主服务时，仅顶层回落不够（子 provider 内部 call-site 会直接抛 `Unable to resolve ...`），必须把宿主服务描述符**透传进子 provider** 才能真正传递解析。转发描述符一律 `Transient`（避免宿主缓存首解），由子 provider 托管真实生命周期；`Unmount` Dispose 子 provider + 摘索引 → `GetRequiredService` 抛异常，「卸载即失效」成真。
- **动态端点移除**：`ApplicationPartManager.ApplicationParts` 是普通 `IList`，**无内置 ChangeToken**——移除 `AssemblyPart` 后 action-descriptor 不会自动刷新，必须自实现 `IActionDescriptorChangeProvider` + `NotifyChange`。`ChangeToken.OnChange` 回调内会重订阅：须「**先换新 CTS、再取消旧 CTS**」防递归重触发。`DestroyPlugin` 顺序：先摘 `AssemblyPart`（保证刷新重建时插件程序集仍可反射）→ 再摘 DI（registry.Unmount）→ Fiber.Dispose → ALC Unload。热重载路径也要在 ResolvePluginInstance 成功后重新注册 ApplicationPart。
