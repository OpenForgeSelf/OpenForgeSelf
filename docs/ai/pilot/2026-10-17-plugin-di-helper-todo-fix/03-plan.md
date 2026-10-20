# Plan

> 阶段：Stage 3 ｜ 任务：2026-10-17-plugin-di-helper-todo-fix ｜ Task ID：PILOT-062

## Files To Change

- file: `ForgeSelf.Abstractions/PluginContextDiExtensions.cs`
  reason: 新增——公共助手唯一入口（RequireHostProvider/CreateHostScope，执行期解析，未 seed 明确报错）
- file: `Plugins/TodoTracker/ToolExtensions.cs`
  reason: 删 `TodoToolProvider`；老三工具构造零解析、ExecuteAsync 内 `CreateHostScope()`；RecordUsage 传 scope provider
- file: `Plugins/TodoTracker/AgentToolFunctions.cs`
  reason: 基类 `TodoAgentToolBase` 构造零解析；`RunAsync` 执行期建 scope；头注释改写（不再「沿用 TodoToolProvider」）
- file: `Plugins/TodoTracker/TodoTrackerPlugin.cs`
  reason: `RegisterToolExtensions` 注释更新为「构造期零解析」约定（签名与调用不动）
- file: `ForgeSelf.Api.Tests/Plugins/TodoTracker/UniversalGatewayListTodosScopeReproTests.cs`
  reason: 翻写为成功回归（改名 `UniversalGatewayListTodosScopeRegressionTests.cs`，旧文件移入 `.trash/`）
- file: `ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoToolProviderTests.cs`
  reason: 被删（移入 `.trash/`）——语义迁移至新助手单测
- file: `ForgeSelf.Api.Tests/Plugins/TodoTracker/PluginContextDiExtensionsTests.cs`
  reason: 新增——S-1 三分支 + 「seed 前构造/seed 后调用成功」时序免疫用例
- file: `ForgeSelf.Api.Tests/Architecture/PluginToolConstructorDiGuardTests.cs`
  reason: 新增——S-4 架构守卫（静态扫描 + 剥注释 + 变异探针验证）
- file: `docs/ai/pilot/2026-10-17-plugin-di-helper-todo-fix/05~07`
  reason: Evidence / Review / Final Report（验证后产出）
- file: `docs/02-features/034-mcp-center.md`（网关故障处置段回写）+ `docs/04-standards/agent-workflow/README.md`（新工程规则「插件工具构造期禁解析服务，统一走 PluginContextDiExtensions」）+ TODO + 日记
  reason: §0 出口清单文档同步

## Implementation Steps

1. 新建 `PluginContextDiExtensions.cs`（先写公共助手，插件侧才有替换目标）。
2. 新建 `PluginContextDiExtensionsTests.cs`（三分支 + 时序免疫；此时旧类未删，可独立跑）。
3. 改 `ToolExtensions.cs`：删 TodoToolProvider、老三工具惰性化。
4. 改 `AgentToolFunctions.cs`：基类惰性化（5 子类构造体不动，仅 base 语义变）。
5. 改 `TodoTrackerPlugin.cs` 注释。
6. 删除迁移：`TodoToolProviderTests.cs` → 移 `.trash/`（语义已并入步骤 2 用例）。
7. 翻转复现测试 → `UniversalGatewayListTodosScopeRegressionTests.cs`（fake ITodoService 经 Moq；旧文件移 `.trash/`）。
8. 新建守卫测试 `PluginToolConstructorDiGuardTests.cs`，首跑核其他插件命中情况；做变异探针（临时塞 eager 写法→翻红→还原）。
9. 门禁与闭环：`dotnet build` → 定向测试 → 相邻回归 → todo-tracker 冒烟 e2e → dev-stack 本地验收（经网关真调 list_todos）→ 文档回写 → 05/06/07 → 汇报（发布④⑤待拍板）。

## Test Plan

1. `PluginContextDiExtensionsTests`：seed 命中分支 / 直传真容器分支 / 未 seed 明确文案（含三要素子串）/ null 输入；Context 直构工具 + seed 后调用成功（时序免疫）。
2. `UniversalGatewayListTodosScopeRegressionTests`：真实时序（工具构造早于 seed），网关 `{"parameters":"{}","tool":"list_todos"}` → `success:true` + fake 数据回传。
3. `PluginToolConstructorDiGuardTests`：全 Plugins 扫描绿 + 变异探针翻红记录（探针不进最终提交）。
4. 相邻回归：`--filter "FullyQualifiedName~TodoTracker|FullyQualifiedName~UniversalToolForwarder"` 除 2 条在册基线红（盘符）外全绿。
5. 冒烟 e2e：`e2e/plugins/todo-tracker` 既有 1 条冒烟跑绿（铁律 24，不新铺）。

## Verification

### Build

```powershell
dotnet build ForgeSelf.Api
```

### Unit Test

```powershell
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginContextDi|FullyQualifiedName~UniversalGatewayListTodos|FullyQualifiedName~PluginToolConstructorDiGuard"
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoTracker|FullyQualifiedName~UniversalToolForwarder"
```

### Integration Test

同相邻回归过滤集（本仓插件集成测试在同一 TestProject 内，无独立命令）。

### E2E

```powershell
pwsh scripts/dev-stack.ps1
# todo-tracker 冒烟 e2e（e2e-testing 技能姿势）+ 经网关 API 真调 list_todos 验收
```

### Other Checks

- 变异探针：守卫翻红实测记录进 05-evidence。
- `dotnet build Plugins/TodoTracker` 单编不算数（铁律 12b），判据取宿主 build + 测试。

## Plan 偏差记录

> 实现中若与 Plan 不一致，在此登记原因，不回改 Plan 上文。

| 时间 | 偏差 | 原 Plan | 原因是 |
| --- | --- | --- | --- |
| （待填） | | | |
