# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。

## Task

PILOT-062（2026-10-17-plugin-di-helper-todo-fix · 用户指令「全部都做」批）

## Changed Files

- 新增 `ForgeSelf.Abstractions/PluginContextDiExtensions.cs`（RequireHostProvider/CreateHostScope 公共助手）
- 修改 `Plugins/TodoTracker/ToolExtensions.cs`（删 TodoToolProvider；老三工具构造期零解析、执行期 CreateHostScope；成功分支用量上报改传 scope.ServiceProvider）
- 修改 `Plugins/TodoTracker/AgentToolFunctions.cs`（TodoAgentToolBase 惰性化，5 子类随之时序免疫）
- 修改 `Plugins/TodoTracker/TodoTrackerPlugin.cs`（RegisterToolExtensions 注释登记 DI 约定，签名/调用不动）
- 新增 `ForgeSelf.Api.Tests/PluginContextDiExtensionsTests.cs`（5 Fact）
- 新增 `ForgeSelf.Api.Tests/Plugins/TodoTracker/UniversalGatewayListTodosScopeRegressionTests.cs`（复现翻转为成功回归）
- 新增 `ForgeSelf.Api.Tests/Architecture/PluginToolConstructorDiGuardTests.cs`（构造期解析架构守卫）
- 移入 `.trash/`：`TodoToolProviderTests.cs`、`UniversalGatewayListTodosScopeReproTests.cs`（工作区原件已删，副本留 .trash 可取回）

## Build

Command:

```powershell
dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\
```

Result: **PASS**（来源等级：Verified）

```text
1356 个警告（全部存量 CS86xx/xUnit 分析器）
0 个错误
```

> **绕行标注**：不带 `-p:OutputPath` 的 `dotnet build ForgeSelf.Api` 命中 MSB3027/MSB3021——
> `ForgeSelf.Core.dll`/`ForgeSelf.Abstractions.dll` 无法拷入 `ForgeSelf.Api\bin\Debug\net10.0-windows\`，
> 被用户运行中的 dev 宿主（当时的 .NET Host PID 78616）锁定；输出无任何代码编译错误。
> 绕锁姿势沿用 2026-10-16-settings-proxy-mode-bypass 先例；后续 `dotnet test` 一律带同一绕锁属性。

## Unit Test

Command:

```powershell
dotnet test ForgeSelf.Api.Tests -p:OutputPath=bin\BuildCheck\ --filter "FullyQualifiedName~PluginContextDi|FullyQualifiedName~UniversalGatewayListTodos|FullyQualifiedName~PluginToolConstructorDiGuard"
```

Result: **PASS**（Verified）

```text
已通过! - 失败: 0，通过: 7，已跳过: 0，总计: 7（助手 5 + 网关成功回归 1 + 守卫 1）
```

修复过程中的真实失败与纠正（如实记录）：
- 首跑编译红 2 条：`Match` 与 `Moq.Match` 二义（CS0104）→ 守卫测试内改全限定名；`PagedResult<>` 与 `ForgeSelf.Abstractions.PagedResult<T>` 二义 → 回归测试改全限定 Models 形状。
- 次跑 1 条红：`RequireHostProvider_Null`——FluentAssertions `Invoking` 拒收 null subject（ArgumentNullException 'subject'）→ 改 `Action` 包装直调扩展方法后绿。

Command:

```powershell
dotnet test ForgeSelf.Api.Tests -p:OutputPath=bin\BuildCheck\ --no-build --filter "FullyQualifiedName~TodoTracker|FullyQualifiedName~UniversalToolForwarder"
```

Result: **PASS**（Verified）

```text
已通过! - 失败: 0，通过: 158，已跳过: 0，总计: 158
```

### AC-3 变异探针（守卫翻红实证）

- 临时在 `ListTodosToolFunction` 构造体注入 `_ = serviceProvider.GetService(typeof(object));`（MUTATION-PROBE 行）。
- 守卫**运行时读源码**，无需重编，直接跑 `--filter ~PluginToolConstructorDiGuard` → **翻红**（Verified）：

```text
失败! - 失败: 1
Expected offenders to be empty … 违规明细：TodoTracker\ToolExtensions.cs:126 → 构造期出现「.GetService(」。整改：…
```

- 探针行还原后再跑 → **翻绿**（通过: 1）；`git grep MUTATION-PROBE -- Plugins` 无残留，探针不进提交 ✓。

## Integration Test

Result: **N/A**（本批无独立集成测试工程改动；网关链路已由单测内五步时序复刻 + 现场宿主双重覆盖，见 E2E/现场节）

## E2E

Command:

```powershell
cd ForgeSelf.Web; pnpm exec playwright test --config=playwright.config.ts e2e/plugins/todo-tracker -g "A1" --output=../.pw-out-todotracker-smoke
```

Result: **PASS**（Verified）

```text
[1/1] [chromium] todo-tracker.spec.ts:109 › A1 清单 entry 生效：/todo 由插件产物渲染，入口 200，版本徽标与 plugin.json 一致
1 passed (43.5s)
```

globalSetup 全新宿主发布至 `.temp\e2e\wt-b26d4625\publish`（不触用户 Debug 锁）；二进制内含本批修复（`ForgeSelf.Abstractions.dll` 含 `PluginContextDiExtensions`、`Plugins\TodoTracker\TodoTracker.dll` 含 `CreateHostScope`，时间戳 2026-10-17 11:55，均为本次构建产物——二进制串检索 Verified）。

## 现场验收（AC-5 · dev-stack 等效替代）

> **偏差如实记录**：`pwsh scripts/dev-stack.ps1` 内部固定 `dotnet build ForgeSelf.Api -c Debug`（scripts/dev-stack.ps1:137），
> 必撞上述用户宿主锁（且 `-SkipBuild` 只会用陈旧的 bin\Debug 产物，验收无效）。
> 改用 **e2e globalSetup 刚编译的同一份最新产物**（`.temp\e2e\wt-b26d4625\publish\ForgeSelf.exe`）按 globalSetup 同款姿势拉起真实宿主进程
> （`--console` 首参 + `FORGESELF_DATA_ROOT` 隔离 + `FORGESELF_PORT=7103` 空闲口 + `FORGESELF_MCP_GATEWAY_PORT=18897` + `FORGESELF_NO_TRAY=1` + 独立 `FORGESelf_INSTANCE_ID`），
> 与 dev-stack 的差异仅为"跳过 Debug 重编译"，进程真实性与代码新度等价。

拉起过程实录（Verified）：
- 第一次以 7102/18895 拉起后发现 18895 的监听者是**用户 dev 宿主**（dotnet PID 102168，命令行 `bin\Debug\net10.0-windows\ForgeSelf.dll --console --plugins-dir …\Plugins`）——先前打到 18895 的 initialize/tools/call 是用户实例的**有状态网关**（-32001 Session not found），不属本批证据，弃用；只停掉了**我们自己拉起的**实例（PID 2576），未触用户进程。
- 换 7103/18897 重拉，`/api/health`=healthy，18897 监听归属本实例 PID 123720。

网关真调（MCP `tools/call` → `universal_tool`，用户原始缺陷入参 `{tool:"list_todos", parameters:{}}`）响应原文：

```text
data: {"result":{"content":[{"type":"text","text":"{\"success\":true,\"data\":{\"Items\":[],\"Total\":0,\"Page\":1,\"PageSize\":20}}"}],"isError":false}
```

写/读路径补强存证（隔离数据目录）：
- `create_todo {"title":"PILOT-062 现场存证"}` → `success:true`，Id=1，TaskKey=32e17f31f275445899373c59062de0c4；
- `list_todos {}` → `success:true`，Total=1，Items[0].Title 回读一致；
- 清理：REST `DELETE /api/todos/1`（200 空体）→ 停止本实例 → 18897/7103 无残留监听 ✓。

**结论：缺陷错误串「No service for type 'IServiceScopeFactory' has been registered.」在真实宿主进程·真实网关链路上不再出现，返回成功封套。**（AC-1 单测级 + AC-5 进程级双证）

## Static Analysis

Result: **N/A**（本批零前端改动；`pnpm run check/test` 不适用——无 ForgeSelf.Web 文件变更）

## Screenshots

N/A（无 UI 改动；e2e A1 截图产物在 `ForgeSelf.Web/screenshots/e2e/todo-tracker/`，非本批新增断言依据）

## AC 覆盖对照（02 §Acceptance ↔ 本文证据节；编号原文作 `AC-n`，此处按门禁 token 对齐）

| AC | 判据（01-intent / 02-spec） | 证据位置与结果 |
| --- | --- | --- |
| AC1 | `dotnet test` 回归转绿（`success:true`，fake ITodoService 数据回读） | 本文「Unit Test」节：新回归 7/7 + 既有 TodoTracker/UniversalToolForwarder 158/158，均 Verified |
| AC2 | 助手三态：seed 前清晰错文 + seed 后解析成功 + 未 seed 报错文案含 ProvideHostServices 指引 | 「Unit Test」节 `PluginContextDi` 助手 5 测试（含 `RequireHostProvider_Null` 纠正记录） |
| AC3 | 守卫变异探针：临时 eager 写法 → 守卫翻红 → 还原翻绿 | 「### AC-3 变异探针」节：翻红/翻绿均 Verified，`git grep MUTATION-PROBE` 无残留 |
| AC4 | 存量规则不回归（TodoTracker+UniversalToolForwarder 回归全绿）、`dotnet build` 0 error | 「Build」节 0 错误 + 「Unit Test」节 158 通过 |
| AC5 | dev-stack 源码本地验收：真实实例 `list_todos` 返回成功封套 | 「现场验收」节：7103/18897 隔离实例 tools/call 原文 `{"success":true,…}`，写/读/清三步存证（偏差点已如实披露） |

## Known Limitations

- 成功路径用量上报此前对 Context 建 scope 被 `RecordUsageAsync` 内部吞异常（永不生效）——本批成功分支改传 `scope.ServiceProvider` 顺带治愈；**catch 分支仍传 `_serviceProvider`**（失败时用量记录尽力而为，未 seed 场景建 scope 会再抛并被内部吞掉，属可接受静默，登记不扩改）。
- 现场验收以 e2e publish 产物等效替代 dev-stack（原因见上节），Debug 目录产物本批未更新——待用户 dev 宿主空闲后自然重建。

## Unresolved Issues

无阻塞项。守卫首跑全 Plugins 目录零违规（含 McpCenter/AgentHub/sems 等——AgentHub 早已惰性范式，McpCenter 的 UniversalToolForwarder 构造只存 ctx 合规）。
