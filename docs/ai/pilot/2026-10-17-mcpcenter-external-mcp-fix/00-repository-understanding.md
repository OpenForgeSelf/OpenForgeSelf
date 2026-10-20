# 00-repository-understanding：外部 MCP 三缺陷修复批（2026-10-17）

> 上游复现批：`docs/ai/pilot/2026-10-17-mcpcenter-external-mcp-repro/`（三缺陷根因均已坐实，Review=APPROVED）。
> 闸门1 批准依据：用户 2026-10-17 回复「开工」（批准复现批六段方案 + 默认项：initialized 通知 + Mcp-Session-Id 回传随批补）。

## 涉事模块与文件（已读码确认）
| 文件 | 角色 | 现状锚点 |
|------|------|----------|
| `Plugins/McpCenter/Services/McpClient/StreamableHttpMcpTransport.cs` | 传输① | :54-55 initialize 缺 clientInfo；:12 注释「每次请求独立会话」；无 initialized 通知、无 Mcp-Session-Id 回传 |
| `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs` | 传输② | :115-116 同缺 clientInfo |
| `Plugins/McpCenter/Services/McpClient/LegacySseMcpTransport.cs` | 传输③ | :55-56 同缺 clientInfo |
| `Plugins/McpCenter/Services/McpClient/IMcpClientTransport.cs` | 接缝 | 接口签名不动（initialized 通知内化在各传输 InitializeAsync 里） |
| `Plugins/McpCenter/Services/McpClient/McpClientSession.cs` | 会话 | :14 SupportedProtocolVersions；ConnectAsync 调 InitializeAsync |
| `Plugins/McpCenter/Services/McpClientManager.cs` | 状态视图 | :131-135 Url/Command/Args 明文返回、Headers/Env 走 Mask（`****尾4`，:197-204） |
| `Plugins/McpCenter/Services/ExternalServersStore.cs` | 校验 | :62 ValidateAndNormalize（新增/更新共用，M2 掩码防护落点） |
| `Plugins/McpCenter/web/src/McpCenterView.vue` | 前端表单 | :408-423 openEditExternal 硬编码空 url/command/args + 掩码回填；:473-510 saveExternal 无条件提交 parseKv |
| `Plugins/McpCenter/web/src/types/external.ts` | 类型 | :15-27 StateDto 未声明 url/command/args（后端在发、类型丢弃） |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs` | 红灯锚点 | 复现批产出，当前预期 FAIL，本批转绿 |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientIntegrationTests.cs` | 集成回归 | node mock 三传输往返（新增通知/会话头不得破坏） |

## 既有约束
- plugin-development §四 六步闭环（门禁①-⑥）；problem-class-closure 修复批出口（守卫 + PC/case 归档）。
- 运行实例 :51888 由用户持有，禁止停/启/杀；第⑤步待用户页面更新后触发。
- 发布主路径 = 本地目录更新源 `release-local.ps1 -UpdateDir`（带 `-Sign`）。
