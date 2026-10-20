# mini-task：复现「通用网关调 list_todos 报 IServiceScopeFactory 未注册」（输入下午 · 2026-10-17）

> 级别：轻量（新增 1 个测试文件）。Intent/Spec/Plan/Task 合并本文件；Evidence/Review 单独产出。
> 闸门1：用户指令即明确范围（「先复现一下问题，增加一条单元测试」），本批**只复现不修复**。

## Intent
- **Problem**：经 McpCenter 通用网关（MCP `universal_tool`）调用 `list_todos`，入参 `{parameters:"{}", tool:"list_todos"}`，返回 `{success:false, error:"No service for type 'Microsoft.Extensions.DependencyInjection.IServiceScopeFactory' has been registered."}`。
- **Why**：缺陷无自动化证据，修复前必须先把「网关→注册表→TodoTracker 工具」这条链路的确切失败封套锁成可重复测试（红线：验证必须走可重复测试，不用临时脚本）。
- **Expected Outcome**：一条 xUnit 单元测试，按宿主真实时序复刻该错误，跑红→断言当前 buggy 行为（复现成功即测试通过，修复后此测试翻红提醒更新）。
- **Constraints**：不改任何业务代码（TodoTracker/McpCenter/宿主均不动）；修复另立批次。
- **Success Criteria**：`dotnet test`（过滤本测试类）PASS，且断言的错误文本与用户观察字符串逐字一致。

## 根因初判（读码结论，待修复批次实证）
- `TodoTrackerPlugin.Apply` → `RegisterToolExtensions(pluginId, ctx)` 把**插件上下文**（`ForgeSelf.Core.Context`）当 `IServiceProvider` 传给 `ListTodosToolFunction`；构造函数内 `TodoToolProvider.Resolve` **eager** 解析：Apply 发生在 `builder.Build()` 之前（AppBuilder.cs:364 RegisterAllServices），宿主根 provider 要到 `ProvideHostServices(app.Services)`（AppBuilder.cs:402，Build 之后）才 seed 进根 Context——构造期 `GetService(typeof(IServiceProvider))` 恒 null，回落捕获了 Context 本身。
- 请求到来时 `_serviceProvider.CreateScope()` 在 Context 上解析 `IServiceScopeFactory`（本地值/共享表均无）→ 抛 `InvalidOperationException`，被 `ListTodosToolFunction.ExecuteAsync` catch 成 `{success:false, error:ex.Message}`；宿主 `ToolRegistry.InvokeToolBodyAsync`（ToolRegistry.cs:535-538）把该 JSON 原样作为成功结果透传，网关再原样回传——即用户看到的封套。
- 同工程其他插件（AgentHub/ScriptRunner/AIAgent）均为**运行期惰性解析**并注明了同一时序坑；TodoTracker 的老三工具（create/list/complete_todo）是唯一构造期 eager 的写法（AgentToolFunctions.cs 头注释亦承认此为历史欠账）。

## Spec（复现测试规格）
- **Input**：`universal_tool` 入参原文 `{"parameters":"{}","tool":"list_todos"}`（parameters 为字符串形态，走转发器 L74-76 兼容分支）。
- **时序复刻**（与 AppBuilder 一致）：① 建 root `Context` + 派生插件子上下文；② 以子上下文构造真实 `ListTodosToolFunction`（= Apply 期）；③ 之后才把宿主 `ServiceProvider` 以 `ctx.Register(typeof(IServiceProvider), host)` seed 进 root（= ProvideHostServices）；④ 真实宿主 `ToolRegistry` 注册该工具并 seed 进 Context（= 工具发现接线）；⑤ 真实 `UniversalToolForwarder.ForwardAsync` 执行（= MCP tools/call）。
- **Output（断言）**：返回 Text 为 JSON，`success == false`，`error` 逐字等于 `No service for type 'Microsoft.Extensions.DependencyInjection.IServiceScopeFactory' has been registered.`；`ITodoService` 刻意**不注册**——证明失败发生在建 scope 之前，与业务服务无关。
- **Error Handling / Boundary**：`RecordUsageAsync` 自带 try/catch（ToolFunctionUsageReportingExtensions.cs:41-45），不得让复现测试因统计上报二次异常。
- **Acceptance Criteria**：见 Intent Success Criteria。

## Plan
1. 新建 `ForgeSelf.Api.Tests/Plugins/TodoTracker/UniversalGatewayListTodosScopeReproTests.cs`：一个 `[Fact]`，按上节五步时序组装（真实 `Context`/`ToolRegistry`/`UniversalToolForwarder`；`McpClientManager` 用临时目录真实实例，同 UniversalToolForwarderTests 现例）。
2. 不碰：`Plugins/TodoTracker/**`、`Plugins/McpCenter/**`、`ForgeSelf.Api/**` 任何业务代码。

## Task
- **Objective**：一条可重复的单测复现网关→list_todos 的 IServiceScopeFactory 错误封套。
- **Scope Allowed**：新增上述 1 个测试文件；本 pilot 目录工件。
- **Scope Forbidden**：修复缺陷；改插件/宿主代码；顺带清理 eager 写法；git 提交（待闸门2）。
- **Acceptance Criteria**：
  - [ ] 新测试文件编译通过（注意 2026-10-16 登记的 ModelProxy.cs 编译阻断是否仍存在，若存在须上报而非自修）
  - [ ] `dotnet test --filter UniversalGatewayListTodosScopeReproTests` PASS（断言 buggy 封套）
  - [ ] 错误字符串与用户观察逐字一致
- **Verification Commands**：
  - `dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`
  - `dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~UniversalGatewayListTodosScopeReproTests"`
