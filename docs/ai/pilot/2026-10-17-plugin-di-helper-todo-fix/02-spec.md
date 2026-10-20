# Spec

> 阶段：Stage 2 ｜ 任务：2026-10-17-plugin-di-helper-todo-fix ｜ 功能档案：none（缺陷修复 + 架构门禁，不改对外功能；主题文档回写=034-mcp-center.md 网关故障段 + agent-workflow/README.md 新工程规则）

## Summary

把「插件工具解析宿主 DI」这一动作收敛为 ForgeSelf.Abstractions 的单一公共入口（仅执行期可用、未就绪报明确错），TodoTracker 8 个工具全量改为构造存原始上下文/执行期解析；并加一条架构守卫测试扫描 `Plugins/**`，把「构造期解析服务」变成 CI 判据。

## Behavioral Spec

### S-1 公共助手（`ForgeSelf.Abstractions/PluginContextDiExtensions.cs`）

- `RequireHostProvider(this IServiceProvider? services)`：
  1. `services.GetService(typeof(IServiceProvider))` 命中（宿主 seed 进根 Context 的根 provider，PluginManager 契约清单在册）→ 返回之；
  2. 未命中但 `services.GetService(typeof(IServiceScopeFactory))` 命中（直传真 MS 容器，测试/宿主直传场景）→ 返回 `services`；
  3. 都不命中 → 抛 `InvalidOperationException`，文案**必须**含三要素：宿主根 provider 尚未 seed、seed 发生在 `builder.Build()` 后的 `ProvideHostServices`、只能在 `ExecuteAsync` 调用本方法而非构造期。（对照旧文案「No service for type 'IServiceScopeFactory' has been registered.」——不再出现。）
- `CreateHostScope(this IServiceProvider? services)`：`RequireHostProvider(...).CreateScope()`，每次调用即时解析，**不缓存结果**（时序免疫的根）。
- `services == null` → 同抛明确错（「服务提供者未初始化」语义并入统一文案）。
- 依赖约束：只允许 `ForgeSelf.Core` + `Microsoft.Extensions.DependencyInjection.Abstractions` 类型，禁止 NewLife/XTrace（同 `ToolFunctionUsageReportingExtensions` L43 注释约束）。

### S-2 TodoTracker 工具惰性化

- 老三工具（CreateTodo/ListTodos/CompleteTodo ToolFunction）：构造函数只存原始 `services`（插件 ctx），**零解析**；`ExecuteAsync` 内 `using var scope = _services.CreateHostScope();`（在 try 内，异常照旧转 `{success:false,error}` 封套，封套形状不变）；`RecordUsageAsync` 改传 scope 内 provider（用量上报自此真正可用——旧写法下对 Context 建 scope 被 RecordUsageAsync 内部吞掉，用量静默丢失，本批顺带治愈，记 Evidence）。
- `TodoAgentToolBase.RunAsync`：同改——`_services` 存原始 ctx，scope 在 try 内 `CreateHostScope()` 创建；删除 `Fail("服务提供者未初始化")` 预检（语义并入异常封套，文案更明确）。
- `TodoToolProvider` 类删除（被 S-1 取代；其单测迁移为 S-1 的单测）。
- `TodoTrackerPlugin.RegisterToolExtensions`：签名不变（继续直传 ctx），注释更新为「构造期零解析」约定。

### S-3 复现测试翻转（成功回归）

- `UniversalGatewayListTodosScopeReproTests` 改写为 `UniversalGatewayListTodosScopeRegressionTests`：同一时序复刻（**工具在 seed 之前构造**，seed 在构造后、调用前完成），host 容器注册 fake `ITodoService`；断言网关返回 `success:true` 且 data 回传假数据；不再断言任何错误串。
- 头注释记录：本文件由复现批特征化测试翻转而来（指向 `2026-10-17-gateway-list-todos-scope-repro`）。

### S-4 架构守卫测试（`ForgeSelf.Api.Tests/Architecture/PluginToolConstructorDiGuardTests.cs`）

- 扫描仓库 `Plugins/**/*.cs`（排除 `bin/ obj/ _*`，定位方式照抄 `XCodeConfigTests.PluginsRootOfRepository`，while 循环推进指针）；
- 剥注释（`//`、`/* */`）后，匹配「构造/主构造函数签名含 `IServiceProvider` 或 `IContext`」的函数体，体内出现 `GetService(`/`GetRequiredService(`/`CreateScope(`/`Resolve(`/`ResolveHost` 即 fail，失败信息给 `文件:行` 与整改指引（改存原始 ctx、执行期走 PluginContextDiExtensions）；
- 变异探针（AC-3）：临时把某工具构造改回 eager 写法 → 守卫必翻红 → 还原；探针结果记 05-evidence。

## Interfaces

- 新增 public API：`PluginContextDiExtensions.RequireHostProvider / CreateHostScope`（扩展方法，静态类，可被全部插件与宿主使用）。
- 删除 public API：`TodoToolProvider`（仅 TodoTracker 与两个测试文件引用，全仓检索确认无其他消费方后删）。
- MCP 对外契约：零变化（工具名/入参 schema/封套形状不动）。

## Error Handling

- 未 seed 场景：执行期抛 `InvalidOperationException`（明确文案）→ 被工具 try/catch 转成 `{success:false, error:"<明确文案>"}` 封套面客；注册表/网关透传行为不变。
- 守卫测试命中：报出文件与行号，不静默豁免（豁免表初始为空；若首跑命中其他插件既有写法，属真缺陷——按规模决定本批整改或记 TODO，禁止塞豁免了事）。

## Compatibility

- 宿主直传真 provider 的既有调用方（如 McpCenter 测试组装姿势）经分支 2 兼容；`AgentHubDi.ResolveHost` 不动（其惰性语义本就正确；收敛到公共助手列为后续批次，避免波及 AgentHub 测试域）。

## Non-functional Requirements

- 无热路径开销新增（执行期一次 GetService 探测）；不引入新包依赖。

## Interaction Design

不适用——本批无用户可见 UI/交互改动（后端工具与测试）。

## Acceptance Criteria

同 01-intent AC-1 ~ AC-5（可测判据在 04-task 逐条绑定验证命令）。

## Unknown

| 不确切处 | 影响 | 处置 |
| --- | --- | --- |
| 其他插件是否还有构造期 eager 写法（守卫首跑才知道） | 守卫初版可能红 | 首跑如实登记；命中即真缺陷，不塞豁免 |
| 线上 `:51888` 实例当前 TodoTracker 版本 | 运行实例复验（⑤）需在用户更新后 | 发布待拍板，复验随发布批 |
