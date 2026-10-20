# Repository Understanding

> 阶段：Stage 0 ｜ 任务：2026-10-17-plugin-di-helper-todo-fix ｜ 依据：规范 `docs/04-standards/ai-native-engineering-workflow.md` §2

## 项目结构

- `ForgeSelf.Abstractions/` — 宿主与插件共享契约层（引用 ForgeSelf.Core + MS DI.Abstractions，见 csproj 实测）；已有 `ContextExtensions.cs`（IContext 扩展、未 seed 时抛明确错的同款姿势 L16-17）与 `ToolFunctionUsageReportingExtensions.cs`（RecordUsageAsync，内部 CreateScope + 全吞）。
- `ForgeSelf.Core/` — Cordis 式 `Context`（`IContext : IServiceProvider`，IContext.cs:7）；`GetService` 只解析「本地值 + root 共享表」，**不含宿主 MS DI**。
- `ForgeSelf.Api/Plugins/PluginManager.cs` — `ProvideHostServices`（L180）在 `builder.Build()` 之后把宿主根 `IServiceProvider` seed 进根 Context（契约清单 L270 `typeof(IServiceProvider)`）；`AppBuilder.cs` L364 `RegisterAllServices`（立即触发插件 Apply）早于 L402 seed——**时序缺口是本仓既有事实**，宿主注释（AppBuilder.cs:360-362、PluginManager.cs:217-226）已明文。
- `Plugins/TodoTracker/` — 8 个工具：老三工具（`ToolExtensions.cs`，构造期 eager `TodoToolProvider.Resolve`）+ PILOT-054 五工具（`AgentToolFunctions.cs` 基类 `TodoAgentToolBase` L32 构造期**同样 eager**）。
- `ForgeSelf.Api.Tests/` — xUnit + FluentAssertions + Moq；静态扫描守卫先例 `XCodeConfigTests.有XCode实体库的插件连接名_必须在PluginDbs登记`（L184-208：扫 `Plugins/**/*.cs`、排除 bin/obj/_前缀、`PluginsRootOfRepository()` 上溯定位）。

## 技术栈

| 层 | 技术 | 证据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10 + MS DI + NewLife.XCode | ForgeSelf.Api.csproj |
| 插件上下文 | ForgeSelf.Core.Context（Cordis） | IContext.cs:7 |
| 测试 | xUnit + FluentAssertions + Moq | ForgeSelf.Api.Tests.csproj |

## 架构特点

- 工具函数在插件 `Apply(IContext)` 期构造并注册（`TodoTrackerPlugin.RegisterToolExtensions` L57 把 ctx 当 `IServiceProvider` 直传）；执行期才可能拿到已 seed 的宿主容器——**正确范式=运行期惰性解析**，先例 `Plugins/AgentHub/Services/AgentHubDi.cs`（ResolveHost 每次调用时解析，类注释 L13-17 说明 seed 语义）。
- `ToolRegistry.InvokeToolBodyAsync`（ToolRegistry.cs:535-538）把工具返回 JSON 原样以 Success=true 透传——工具内部错误以 `{success:false,error}` 封套面客（复现批已证，`docs/ai/pilot/2026-10-17-gateway-list-todos-scope-repro/`）。

## 测试方式

- 单测/集成：`dotnet test ForgeSelf.Api.Tests --filter …`；复现批基线：`UniversalGatewayListTodosScopeReproTests` 1 绿（断言当前 buggy 封套，修复后必翻红，文件头注释预告翻写为成功回归）。
- 既有相邻域：`TodoTracker`+`UniversalToolForwarder` 过滤集 160 绿 / 2 既有基线红（盘符大小写，TODO P2 在册，非本批）。
- 插件维护闭环：`plugin-development` §四 六步（门禁→冒烟 e2e→dev-stack 验收→发布→运行实例复验→文档）。

## 构建与验证

```powershell
dotnet build ForgeSelf.Api
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoTracker|FullyQualifiedName~UniversalToolForwarder|FullyQualifiedName~PluginContextDi|FullyQualifiedName~PluginToolConstructorDiGuard"
pwsh scripts/dev-stack.ps1   # ③ 源码本地验收；-Stop 收尾
```

## 重要目录职责

| 目录 | 职责 |
| --- | --- |
| ForgeSelf.Abstractions | 宿主↔插件共享契约与扩展（新公共助手落位处） |
| Plugins/TodoTracker | 缺陷归属侧（eager 解析工具） |
| ForgeSelf.Api.Tests/Architecture | 架构守卫测试（静态扫描类先例所在） |

## 既有工程规范

- AGENTS.md §0 预飞 / §11 九阶段；plugin-development 铁律 24（1 冒烟 e2e）/25（源码先行验收）/§七B（静态守卫先剥注释、上溯循环推进指针、探针必验翻红）。
- §4.2 后端分层与 async/await 规范；`ContextExtensions.GetPluginDataDirectory` 的「未注册即抛明确 InvalidOperationException」为本助手错误语义的同文件先例。

## 候选方案与风险

- 方案 A（采纳）：Abstractions 增公共助手 + TodoTracker 全工具改惰性 + 架构守卫测试。影响面=宿主共享层 1 新文件 + 插件 3 文件 + 测试 3 文件；无契约/DB/UI 变更。
- 方案 B（否）：只改 TodoTracker 内部惰性化，不上升公共助手——同类缺陷（sems/AgentHub 都踩过）仍会在其他插件复发，用户已拍板「公共方法杜绝」。
- 风险：guard 静态扫描可能命中其他插件既有 eager 构造写法 → 首跑即暴露，如实登记（命中即真缺陷，同批整改或记 TODO 视规模定，范围控制 §1.3）。
