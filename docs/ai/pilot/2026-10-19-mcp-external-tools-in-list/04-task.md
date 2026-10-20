---
feature: “外部 MCP 工具合并进 list_tools 枚举与 universal_tool 调用”
risk: { level: L2, rationale: “新增契约 + 插件间服务注册” }
expected_files:
  - ForgeSelf.Abstractions/
  - Plugins/McpCenter/Services/
  - Plugins/AIAgent/Services/
rollback: “git revert 本批 commit”
---

# Task

> 阶段：Stage 4 ｜ Task ID：PILOT-072 ｜ 日期：2026-10-19 ｜ 状态：待闸门1

## Task ID

PILOT-072 / mcp-external-tools-in-list（McpCenter v2.6.0 + AIAgent v1.9.0）

## Objective

让已连接的外部 MCP 工具①出现在 `list_tools` 枚举（`mcp.<id>.<工具名>` 全名 + 描述 + 可选 schema），②在内置 AIAgent `universal_tool` 可直接调用（与网关同语义）；外部工具仍不进宿主 `IToolRegistry`（D10 注册表隔离保持，合并发生在枚举出口）。

## Scope

**Allowed**：
- `ForgeSelf.Abstractions/IMcpExternalGateway.cs`（新建契约 + record）
- `ForgeSelf.Abstractions.Tests/McpExternalGatewayContractTests.cs`（新建）
- `Plugins/McpCenter/`：`Services/McpExternalGatewayAdapter.cs`（新建）、`McpCenterPlugin.cs`、`Services/ListToolsToolFunction.cs`、`Services/UniversalToolForwarder.cs`（仅描述文案）、`McpCenter.csproj`/`plugin.json`（版本 2.6.0）
- `Plugins/AIAgent/`：`Services/ToolFunctions/UniversalTool.cs`（mcp. 分支 + Description）、`plugin.json`（版本 1.9.0）
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/`（Adapter/ListTools 合并两组新建）、`ForgeSelf.Api.Tests/Plugins/AIAgentTests/`（外部路由新建）
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（v2.6.0 新用例）
- 文档：`docs/02-features/034-mcp-center.md`、`docs/07-decisions/`（D10 修订）、`TODO.md` 勾账

**Forbidden**：
- 把外部工具注册进 `IToolRegistry`（D10 隔离保持）
- 改三传输/会话/协议层（PC-008/010 领域，本批零触碰）
- 改 McpCenterView.vue / 任何前端 UI
- 改宿主 `HostProvidedServiceContracts` seed 清单
- 动 v2.5.2 超时批已收口的逻辑（只叠加不回调）
- 发布操作（release-local 仍按硬闸门等用户放行；两批合并一次发版）

## Acceptance Criteria

- [ ] AC-1 连接外部服务器后 `list_tools` 含全部 `mcp.<id>.<tool>` 条目（name=全名、description、pluginId=服务器id、source="mcp-external"，宿主条目 source="registry"）。
- [ ] AC-2 keyword 过滤命中外部条目（按全名或描述，忽略大小写）；includeSchema=true 时外部条目带 input schema，坏 JSON 置 null。
- [ ] AC-3 网关与 AIAgent 的 `universal_tool {tool:"list_tools"}` 返回同一合并结果（同一 ListToolsToolFunction 有单测证据）。
- [ ] AC-4 AIAgent `universal_tool` 调 `mcp.<id>.<tool>`：成功透传文本；异常→`{success:false,error}`；契约缺失→「请确认 MCP 中心插件已启用」中文错误，不落 unknown。
- [ ] AC-5 无外部连接时 `list_tools` 与现状等价（宿主条目逐字段不变）；既有 McpCenter/AIAgent/Abstractions 单测全绿。
- [ ] AC-6 e2e v2.6.0 用例绿：mock 连接 → 网关 list_tools 断言含 `mcp.<id>.echo` → 实调回显成功 → 清理。
- [ ] AC-7 `dotnet build ForgeSelf.Api` 0 错；版本 McpCenter 2.6.0 / AIAgent 1.9.0；034 与 D10 修订文档回写。

## Expected Files

见 03-plan A~F 清单（新增 7 文件 + 修改 8 文件）。

## Verification Commands

```pwsh
dotnet build ForgeSelf.Api
dotnet test --filter "FullyQualifiedName~McpCenter|FullyQualifiedName~AIAgent|FullyQualifiedName~Abstractions"
cd ForgeSelf.Web; pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts -g "v2.6.0"
# 现场：dev-stack(:7301) 真连 deepwiki → universal_tool list_tools 含 mcp.deepwiki.* → ask_wiki_question(microsoft/vscode) 成功
```
