# Specification

> 阶段：Stage 2｜从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`。
> Task ID：2026-10-04-mcp-tool-playground

## Functional Requirements

### FR1 工具调用端点（后端）

在既有 `Controllers/McpExternalController.cs`（路由前缀 `api/mcp-center/servers`，类级 `[Authorize("ApiKeyPolicy")]`）内新增一个动作：

- `POST api/mcp-center/servers/{id}/tools/invoke`
- 请求体 `{ "tool": "<外部工具原生名>", "arguments": "<JSON 字符串或对象>" }`
- 行为：经 `McpClientManager` 取该服务器的会话 → 调用工具 → 返回结构化结果。

### FR2 工具调用结果需保留原始信息

现有 `McpClientSession.CallToolAsync` 只返回拼接文本（失败时还被包成 `{success:false,error}`），**丢掉了 `isError` 与原始 JSON**。测试台需要同时拿到「文本」「是否错误」「原始 JSON」。

- 在 `McpClientSession` 上新增 `CallToolDetailedAsync`，返回 `McpToolCallOutcome { IsError, Text, RawJson }`。
- `CallToolAsync` **保持现有返回值语义不变**（`McpClientSessionTests` 4 条断言钉住），改为委托到新的结果提取逻辑实现，不复制一份行为。

### FR3 工具清单补充可用信息

`McpExternalToolDto` 增加 `ServerId`（前端 `types/external.ts` 早已声明该字段却从未被赋值）。`InputSchemaJson` **保持字符串不变**（避免跨前后端破坏性契约变更），由前端解析。

### FR4 前端：按 inputSchema 动态渲染参数表单

在插件 `web/` 内新增：

- 纯函数模块（可单测）：`inputSchema` 字符串 → 字段描述符数组。
- 组件：工具列表 + 选中工具的参数表单 + 调用结果面板，嵌入现有「外部服务器工具」弹窗（替换当前只显示两行文本的 `v-for`）。

### FR5 前端：发起调用并展示结果

表单填参 → 调用 FR1 端点 → 展示：耗时、成功/失败、文本内容、原始 JSON（可折叠）。另提供 JSON 直编模式（高级用法，处理嵌套 object/array）。

### FR6 DeepWiki 预设

在「新增外部服务器」表单提供一个「填入 DeepWiki 预设」按钮，一键填入 `{ id: "deepwiki", name: "DeepWiki", transport: "streamable-http", url: "https://mcp.deepwiki.com/mcp" }`，降低联调门槛。

## Input

| 来源 | 内容 |
| --- | --- |
| 后端 | 已连接会话的 `tools/list` 快照：`McpExternalToolDto { FullName, Name, Description, InputSchemaJson, ServerId }` |
| 后端 invoke 请求体 | `{ tool: string, arguments: string \| object }`；`arguments` 为空时按 `{}` 处理 |
| 前端 | 用户在动态表单 / JSON 模式输入的参数 |
| 外部 | DeepWiki MCP（`https://mcp.deepwiki.com/mcp`），streamable-http，无鉴权，无状态 |

## Output

### 后端 `POST .../tools/invoke` 响应（`ApiResponse<McpToolInvokeResult>`）

```jsonc
{
  "serverId": "deepwiki",
  "tool": "read_wiki_structure",
  "ok": true,            // !isError
  "isError": false,      // 远端 MCP 声明的 isError
  "text": "Available pages for ...",
  "rawJson": "{...}",    // tools/call 返回的 result 原文
  "elapsedMs": 1234
}
```

`ok=false` 且带 `message` 描述失败原因（未连接 / 工具不存在 / 远端报错）。

### 前端

- 参数表单（由 schema 生成）、调用按钮、结果面板（耗时 / 状态 / 文本 / 原始 JSON）。

## Business Rules

- **BR1（鉴权）**：invoke 端点必须继承类级 `[Authorize("ApiKeyPolicy")]`；不得为了调用方便去掉或降级该策略。`McpAdminAuthTests` 的反射断言必须继续通过。
- **BR2（命名）**：`tool` 传**外部工具原生名**（`McpExternalToolDto.Name`），不是 `FullName`（`mcp.<id>.<name>`）。端点内部只做一次透传，不做前缀解析。
- **BR3（不改现有契约）**：`CallToolAsync` 的文本拼接 / `isError` 包装 / 结构化 content 透传 / 异常传播四条既有行为**一字不改**。
- **BR4（无持久化）**：调用历史不落盘、不新增实体、不新增表、不改 `external-servers.json` 结构。
- **BR5（超时）**：沿用 `StreamableHttpMcpTransport.RequestTimeoutMs = 30000`；不做自定义超时参数（避免扩大接口面）。
- **BR6（版本）**：`plugin.json` Version `2.2.0 → 2.3.0`（新功能，次版本升位）。

## Boundary Conditions

| 场景 | 期望 |
| --- | --- |
| 服务器未连接 | `ok=false`，消息含「未连接」，HTTP 400，不抛未处理异常 |
| `tool` 为空 / 工具不存在于清单 | `ok=false`，HTTP 400，消息说明工具名 |
| `arguments` 非合法 JSON | `ok=false`，HTTP 400，消息说明 JSON 解析失败 |
| `arguments` 为空 | 按 `{}` 调用 |
| 远端返回 `isError:true` | `ok=false`、`isError=true`，`text` 与 `rawJson` 照实返回（**不吞**） |
| `inputSchema` 为空 / 非 object | 表单降级为「无参数声明」，JSON 模式仍可用 |
| schema 属性为 `anyOf` | 取**第一个带 `type` 的分支**作为该字段类型（DeepWiki `repoName` 实测为 `anyOf[string, array<string>]` ⇒ 解析为 string） |
| schema 属性无 `type` | 字段类型标 `unknown`，JSON 模式下可手填 |
| 嵌套 object / array | 表单渲染一个多行文本域（JSON），并在字段说明里标明期望结构 |
| 调用耗时 > 30s | 端点返回错误，消息为现有传输层超时文案 |

## Error Handling

- 传输层异常（`McpClientException`）→ HTTP 400 + 明确消息（与现有 `{id}/test`、`{id}/connect` 一致）。
- 其余异常 → HTTP 500 + 消息（与该控制器既有 catch 结构一致）。
- 前端：调用失败展示消息 + 保留上一次成功结果不清空；不允许"点了没反应"。

## Compatibility

- 既有 `GET {id}/tools` 响应**增加**字段 `serverId`，不删字段 ⇒ 既有前端与 e2e 不受影响。
- `McpClientSession` **新增**方法，不改既有方法签名。
- 前端「外部服务器工具」弹窗改版，但工具清单仍来自同一端点；`e2e/plugins/mcp-center/mcp-center.spec.ts` 若断言了旧的两行文本 DOM，需同步更新（实施时先读该 spec 确认）。

## Non-functional Requirements

- 前端 schema→表单逻辑必须是**纯函数、零 element-plus 依赖**，以便 vitest 直接锁定。
- 插件前端不新增第三方依赖；新增组件仅依赖 `vue` + `element-plus`（均已 external）。
- 结果面板对超长文本限高滚动，不撑破弹窗。

## Acceptance Criteria

> 闸门1 用户确认的就是这里的清单。

- [ ] **AC1** `POST api/mcp-center/servers/{id}/tools/invoke` 存在；带令牌调用已连接服务器返回 `{ ok, isError, text, rawJson, elapsedMs }`。
- [ ] **AC2** 无宿主令牌调用该端点返回 **401**（`McpAdminAuthTests` 反射断言仍绿）。
- [ ] **AC3** 服务器未连接 / 工具名不存在 / 参数非 JSON 三种错误各有明确消息与 HTTP 400，无未处理异常。
- [ ] **AC4** `McpClientSession.CallToolAsync` 既有 4 条单测**原样通过**（契约零变更）；新增 `CallToolDetailedAsync` 的单测覆盖 text / isError / 结构化 content / 无 content 四种返回。
- [ ] **AC5** 前端 schema→表单纯函数单测覆盖：`string/number/integer/boolean/enum/array/object`、`required`、`default`、`anyOf` 取首个带 type 分支、空 schema、非法 JSON。
- [ ] **AC6** 「外部服务器工具」弹窗内可按 schema 渲染参数表单，必填有标记、enum 为下拉、default 预填。
- [ ] **AC7** 对 DeepWiki（`https://mcp.deepwiki.com/mcp`）完成一次真实调用（`read_wiki_structure`，`repoName=facebook/react`）并在界面展示结果文本与耗时。
- [ ] **AC8** 「填入 DeepWiki 预设」按钮可用，一键建服务器后连接成功且工具数 ≥ 3。
- [ ] **AC9** 插件 `plugin.json` Version = `2.3.0`，界面版本徽标同步显示。
- [ ] **AC10** 验证命令全绿：`dotnet build`、`dotnet test --filter McpCenter`、插件 web build、宿主 `pnpm run check && pnpm run test`、插件层 e2e（若本环境可跑）。
- [ ] **AC11** 零越界：`git status` 中改动文件全部落在 03-plan 的 Files To Change 名单内。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| `e2e/plugins/mcp-center/mcp-center.spec.ts` 是否断言了旧工具弹窗 DOM | 若断言则改版会红 | 实施前先读该 spec；如断言则同步更新，并在 Evidence 记录（不降低判据强度） |
| e2e 环境能否访问外网 `mcp.deepwiki.com` | AC7 若在网络受限环境跑不了 | 优先在隔离实例实跑；跑不通则 e2e 用本地假 MCP 服务器（或跳过并标注 `Unknown`），**真实 DeepWiki 调用改由人工走查确认并留截图** |
| `McpCenterRuntime` 是否有周期性心跳会与新增调用竞争会话 | 并发安全性 | 实施时读 `Services/McpCenterRuntime.cs` 确认；`McpClientSession` 无锁保护 `CallToolAsync`，若确有并发路径则记录为风险而非自行扩大改动 |
| 宿主 `exposeSharedDeps` 是否已暴露新增表单所需 EP 组件 | 组件可用性 | 只使用已在 `McpCenterView.vue` 中 import 过的组件（`ElInput/ElSelect/ElOption/ElButton/ElTag/ElSwitch/ElInputNumber/ElCheckbox/ElDialog` 等）；若确需新组件，回退为原生 HTML + `--el-*` 变量 |
