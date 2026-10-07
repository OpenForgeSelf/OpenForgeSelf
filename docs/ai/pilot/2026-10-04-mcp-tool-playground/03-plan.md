# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**。
> Task ID：2026-10-04-mcp-tool-playground｜工作树：`wt-mcp-playground`（分支 `feat/mcp-tool-playground`）

## Files To Change

### 后端（`Plugins/McpCenter/`）

- file: `Plugins/McpCenter/Models/McpToolInvoke.cs`（**新增**）
  reason: 承载 invoke 契约，避免把新 DTO 塞进已 300 行的 `McpExternalServerConfig.cs`：
    - `McpToolInvokeRequest { Tool, ArgumentsJson }`
    - `McpToolCallOutcome { IsError, Text, RawJson }`（会话层结果）
    - `McpToolInvokeResult { ServerId, Tool, Ok, IsError, Text, RawJson, ElapsedMs }`（HTTP 响应）

- file: `Plugins/McpCenter/Models/McpExternalServerConfig.cs`
  reason: `McpExternalToolDto` 增加 `ServerId`（前端 `types/external.ts` 已声明该字段却从未赋值）；其余字段一字不动，保证 `GET {id}/tools` 向后兼容。

- file: `Plugins/McpCenter/Services/McpClient/McpClientSession.cs`
  reason: 新增 `CallToolDetailedAsync(name, argumentsJson, ct)`，把现有 `CallToolAsync` 里的结果提取逻辑抽成私有 `ExtractOutcome(JsonElement)`，让两个方法共用。`CallToolAsync` 改为 `var o = await CallToolDetailedAsync(...)` 后按**现有语义**返回（`isError` → `{"success":false,"error":...}`；文本 → 文本；无 content → raw），确保 4 条既有用例原样通过。同时在 `RefreshToolsAsync` 里给新 DTO 填 `ServerId`。

- file: `Plugins/McpCenter/Services/McpClientManager.cs`
  reason: 新增 `InvokeToolAsync(serverId, toolName, argumentsJson, ct)`：未连接抛 `McpClientException`（文案与 `CallExternalAsync` 一致）；命中会话后调 `CallToolDetailedAsync`；返回 outcome。另在 `GetTools` 之外无需改动，`GetStates` 不动。

- file: `Plugins/McpCenter/Controllers/McpExternalController.cs`
  reason: 新增 `POST {id}/tools/invoke`。复用既有 catch 结构（`McpClientException` → 400；其他 → 500），返回 `ApiResponse<McpToolInvokeResult>`。**类级 `[Authorize("ApiKeyPolicy")]` 已存在，新增动作自动继承**，不改鉴权。

- file: `Plugins/McpCenter/plugin.json`
  reason: Version `2.2.0 → 2.3.0`，Description 补一句「工具测试台：按 inputSchema 动态生成参数表单并发起调用」。

### 前端（`Plugins/McpCenter/web/`）

- file: `Plugins/McpCenter/web/src/types/external.ts`（改）
  reason: 修正字段名为后端真名 `inputSchemaJson`；新增 `McpToolInvokeRequest` / `McpToolInvokeResult`。

- file: `Plugins/McpCenter/web/src/api/external.ts`（改）
  reason: 新增 `invokeExternalTool(serverId, tool, argumentsJson)`，走既有 http 封装（`localStorage['forge_api_token']`）。

- file: `Plugins/McpCenter/web/src/playground/schemaForm.ts`（**新增**）
  reason: 纯函数、零 element-plus 依赖，供 vitest 直接锁定：
    - `parseInputSchema(json: string): JsonSchemaObject | null`
    - `resolvePropertyType(prop): SchemaFieldType`（含 `anyOf` → 首个带 `type` 的分支）
    - `buildFieldDescriptors(schema): FieldDescriptor[]`（name/type/required/description/default/enumValues）
    - `buildDefaultArguments(fields): Record<string, unknown>`
    - `serializeArguments(fields, values): string`（空可选值丢弃；number/boolean/array/object 按类型解析）

- file: `Plugins/McpCenter/web/src/playground/schemaForm.test.ts`（**新增**）
  reason: AC5 的单测载体。

- file: `Plugins/McpCenter/web/src/playground/ToolPlayground.vue`（**新增**）
  reason: 独立组件承载「工具列表 + 参数表单 + JSON 模式 + 调用结果面板」，避免把已 1698 行的 `McpCenterView.vue` 继续撑大。只使用 `McpCenterView.vue` 已 import 过的 EP 组件集合。

- file: `Plugins/McpCenter/web/src/McpCenterView.vue`（改，最小面）
  reason: ① 把「外部服务器工具」弹窗（约 904–919 行）里的纯文本 `v-for` 换成 `<ToolPlayground>`；② 在「新增/编辑外部服务器」表单加一个「填入 DeepWiki 预设」按钮（AC8）。其余不动。

- file: `Plugins/McpCenter/web/package.json`（改）
  reason: 照抄 `Plugins/DesignSystem/web/package.json` 补 `check`（宿主 vue-tsc）与 `test`（宿主 vitest）脚本，让 AC5 有正规入口、也让插件前端具备 `plugin-development` 门禁第一步。

### 测试

- file: `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpToolInvokeTests.cs`（**新增**）
  reason: 覆盖 AC1/AC3/AC4：会话层 `CallToolDetailedAsync`（text / isError / 结构化 content / 无 content）+ 控制器 invoke（成功、未连接、参数非 JSON、工具不存在）。构造方式照抄 `McpClientIntegrationTests.cs:160`：`new ExternalServersStore(TempDataDir())` + `new McpClientManager(store)` + `new McpExternalController(store, manager)`，传输层用 `Mock<IMcpClientTransport>`。

- file: `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（**可能改**）
  reason: 若该文件断言了旧工具弹窗文本 DOM，则同步更新；实施前先读，判据只增不减。

### 文档

- file: `docs/02-features/034-mcp-center.md`
  reason: 记录新端点与测试台用法（`plugin-development` §四.5 要求：功能/契约有实质变化必须同步文档）。

## Implementation Steps

1. **先读两个既有文件确认落点**：`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（是否断言旧弹窗 DOM）、`Plugins/McpCenter/Services/McpCenterRuntime.cs`（是否有并发心跳与会话竞争）。发现与 Plan 不符 → 记入「Plan 偏差记录」再修正。
2. 后端：新增 `Models/McpToolInvoke.cs` → 改 `McpExternalServerConfig.cs`（加 `ServerId`）→ 改 `McpClientSession.cs`（抽 `ExtractOutcome` + 新增 `CallToolDetailedAsync`）→ 改 `McpClientManager.cs`（新增 `InvokeToolAsync`）→ 改 `McpExternalController.cs`（新增 invoke 动作）。
3. 后端：`dotnet build ForgeSelf.Api` 编译通过。
4. 后端测试：新增 `McpToolInvokeTests.cs`，跑 `dotnet test --filter McpCenter` 全绿（含既有 `McpClientSessionTests` 4 条与 `McpAdminAuthTests`）。
5. 前端：新增 `playground/schemaForm.ts` + `schemaForm.test.ts`，跑 vitest 通过。
6. 前端：`types/external.ts` / `api/external.ts` 改名与新增；新增 `ToolPlayground.vue`；`McpCenterView.vue` 两处最小接线；`package.json` 补 check/test。
7. 前端门禁：插件 web build + `pnpm run check`（如新增 tsconfig 参照 DesignSystem）+ 宿主 `pnpm run check && pnpm run test`。
8. `plugin.json` 版本 2.3.0；文档同步。
9. 插件层 e2e：按 `e2e-testing` 技能在隔离实例跑 `e2e/plugins/mcp-center/`；尝试真实 DeepWiki 调用（AC7/AC8）。若 e2e 环境无外网，按 02-spec「Unknown」条款处理并如实标注。
10. 出 05-evidence / 06-review / 07-final-report，交闸门2。

## Test Plan

1. **单测（后端）**：`McpToolInvokeTests` —— `CallToolDetailedAsync` 四种返回形态；invoke 端点成功/未连接/坏 JSON/工具不存在；`CallToolAsync` 既有 4 条断言回归。
2. **单测（前端）**：`schemaForm.test.ts` —— 各类型解析、`anyOf` 取首个带 type 分支（用 DeepWiki 真实 `repoName` schema 作输入）、`required`/`default`/`enum`、空 schema、非法 JSON、`serializeArguments` 丢弃空可选值。
3. **鉴权回归**：`McpAdminAuthTests` 仍绿（证明没为了调用方便削弱鉴权）。
4. **e2e**：`e2e/plugins/mcp-center/mcp-center.spec.ts`，新增/更新用例覆盖「选工具 → 表单出现 → 填参 → 调用 → 结果面板出现耗时与文本」。
5. **人工走查**：隔离实例上新增 DeepWiki 服务器 → 连接 → 工具清单 ≥3 → 调 `read_wiki_structure` → 截图留证。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
cd Plugins/McpCenter/web && pnpm run build
```

### Unit Test

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter"
cd Plugins/McpCenter/web && pnpm run test      # 新增：schemaForm 纯函数
```

### Integration Test

```bash
# N/A —— 本项目无独立 integration 测试工程；控制器级验证由
# ForgeSelf.Api.Tests/Plugins/McpCenterTests/ 承担（直连 Controller + 假传输层），见 Test Plan 1。
```

### E2E

```bash
cd ForgeSelf.Web
bash node_modules/.bin/playwright test e2e/plugins/mcp-center/
```

### Other Checks

```bash
cd Plugins/McpCenter/web && pnpm run check      # 新增（vue-tsc，参照 DesignSystem）
cd ForgeSelf.Web && pnpm run check && pnpm run test
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-04-mcp-tool-playground
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-04 15:30 | 02-spec「Unknown」#1：`e2e/plugins/mcp-center/mcp-center.spec.ts` 是否断言旧工具弹窗 DOM | 若断言则同步更新 | **实读确认未断言**（UI 用例只查标题/版本徽标/tab/外部区块空态；外部工具走 API 层断言）⇒ 无需改既有判据，只**新增**工具测试台用例 |
| 2026-10-04 15:31 | 02-spec「Unknown」#3：`McpCenterRuntime` 是否有心跳与会话竞争 | 实施时确认 | **实读确认**：该运行时只管网关配置与内置 MCP 服务器（Stop→Start 热重启），**不触碰外部会话** ⇒ 无并发竞争，不扩大改动 |
| 2026-10-04 23:47 | 闸门1 决策 G5 用户选「起本地假 MCP 服务器做 e2e」 | 可能需新建测试基础设施 | **零新增**：仓库已有 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js`（stdio/http/sse 三模式，暴露 `echo{text:string}` 与 `add{a:number,b:number}`，均带 `inputSchema`）⇒ e2e 与后端成功路径用例直接复用 |
| 2026-10-04 15:49 | 新增 `pnpm run check` 门禁暴露 3 处**存量**类型问题（非本批引入） | 只补脚本 | 最小修复：① `McpCenterView.vue:7` 未使用的 `testMcpServer` import 删除；② `:7` 同文件两处 `@update:model-value="v => …"` 补 `(v: boolean)` 显式类型。理由＝新门禁必须绿，且三处都是零风险的字面修复；未放宽任何编译选项 |
| 2026-10-04 15:47 | `ToolPlayground.vue` 模板属性里写 `[\"a\"]` 导致 Vue 编译器「Unterminated string constant」 | 模板内联三元拼提示语 | 抽成脚本函数 `placeholderFor(f)`，模板只调用（顺带可单测，模板里也不再嵌转义引号） |
