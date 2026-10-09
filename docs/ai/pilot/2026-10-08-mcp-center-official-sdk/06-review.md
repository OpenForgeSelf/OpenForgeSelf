# 06 Review — McpCenter 接入官方 MCP SDK（有状态会话）

## Requirement Check

| AC | 结论 | 依据 |
| --- | --- | --- |
| AC1 插件编译 0 错误 | **FAIL（外部阻断）** | 我侧 0 错；18 错全在他人 `DshMcpConfigWriter.cs` |
| AC2 `initialize` 下发 `Mcp-Session-Id` | **Verified（尖峰）/ Unknown（真实插件）** | 尖峰实测 `5o-msTR0pWLEzPPxgFRGPA`；真实插件未跑 |
| AC3 `tools/list` 恒 1 | **Verified（尖峰）/ Unknown（真实插件）** | 尖峰 `['universal_tool']` |
| AC4 `call_tool(universal_tool)` 真实转发 | **Inferred** | 尖峰用 Fake 转发器验证接线；真实 `UniversalToolForwarder` 未跑 |
| AC5 不支持版本显式报错 | **Verified（尖峰）** | `-32022` |
| AC6 `/health` 免鉴权、`/mcp` 鉴权 | **Unknown** | 未跑（中间件逻辑凭代码 Inferred） |
| AC7 `McpCenterTests` 全绿 | **Unknown** | 未跑 |
| AC8 未越界改文件 | **PASS** | 仅 McpCenter 插件 + 其测试 + 工件；未动 Controllers/前端/宿主 |

## Scope Check

- **PASS**：改动限于 `Plugins/McpCenter/**` 与 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/**`，符合 `04-task.md` 的 Allowed/Forbidden。
- **PASS**：未改 `Controllers/`、插件前端 `web/`、e2e、宿主、数据库结构。
- **PASS**：提交范围已剔除他人两处改动（csproj 版本号、`DshMcpConfigWriter` 注册行）。

## Test Check

- **FAIL**：T006 用例未写、T007 未跑、单测未跑 ⇒ AC2/AC3/AC6/AC7 在**真实插件**上无证据。
- **PASS**：尖峰 2 覆盖了三个「计划中标记为未验证」的技术风险点，且跑完即删（未留存 `temp/`）。

## Architecture Check

- **PASS**：`universal_tool` facade 与官方 SDK 兼容（尖峰证实），未复制转发逻辑，`UniversalToolForwarder` 零改动。
- **关注**：新增 NuGet 依赖 1 个（官方、Apache-2.0）；保留 `McpJsonRpcHandler` 作回退路径，决策已记录（D1）。
- **关注**：SSE 帧格式变化影响 DSH 补丁层，已登记待复测。

## Risk

| 级别 | 项 |
| --- | --- |
| L2 | 外部阻断未解除 ⇒ 无法证明真实插件可用，存在「接线正确但运行时异常」的残余风险 |
| L2 | SSE 帧格式变化可能致 DSH 补丁层解析失败（未复测） |
| L3 | 无 `Content-Length` 的分块请求不再受 1 MB 上限保护 |

## Findings

- **Critical**：无。
- **Major**：验证不完整—— blocked by 外部文件，AC2~AC7 在真实插件上均为 Unknown，**不得宣称完成**。
- **Minor**：`McpJsonRpcHandler` 保留为死代码（有注释说明，可接受待下线）。

## Final Decision

**BLOCKED** —— 代码改动本身符合 Spec 与 Scope，且关键技术风险已由尖峰证实；但受并行会话文件编译失败阻断，**测试与集成验证无法执行**，不具备闸门2 验收条件。

解除条件：`DshMcpConfigWriter.cs` 与 `DshMcpConfigDto.cs` 对齐后，补跑 T006/T007 与 `dotnet test --filter McpCenterTests`，再回到本阶段重审。
