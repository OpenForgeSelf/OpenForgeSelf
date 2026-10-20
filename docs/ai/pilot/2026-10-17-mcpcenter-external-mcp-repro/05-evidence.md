# 05-evidence：MCP 中心·外部 MCP 三缺陷复现（2026-10-17 复现批）

> 级别：轻量复现批。结论证据分级：**Verified**（真跑拿到输出）/ **Inferred**（代码推断）/ **Unknown**。

## Changed Files
| 文件 | 动作 |
|---|---|
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs` | 新增（红灯复现测试，1 个 Fact） |
| `docs/ai/pilot/2026-10-17-mcpcenter-external-mcp-repro/mini-task.md` | 新增（本批工件） |

业务代码 **0 改动**（Scope Forbidden 遵守）。

## V1 运行实例现场复现「测试失败」（Verified）
- Command：`curl.exe -X POST http://localhost:51888/api/mcp-center/servers/deepwiki/test`（Bearer=scripts/get-forge-token.cjs；/test 端点设计为不建会话不落盘，无副作用）
- Result：**HTTP 400**，body 逐字：
  `{"data":null,"code":400,"message":"外部 MCP 返回错误 [-32602]（initialize）: Invalid request parameters","success":false}`
- 与用户观察「测试也失败」一致；「连接」按钮同走 `McpClientManager.TryConnectAsync → McpClientSession.ConnectAsync → InitializeAsync`（同一请求体），失败同源（Verified by 代码路径 + Inferred 同因）。

## V2 运行实例状态视图（Verified）
- Command：`curl.exe http://localhost:51888/api/mcp-center/servers`（Bearer 同上）
- Result：`deepwiki` 条目 `enabled:true, connected:false, toolCount:0`，且响应体**含明文 `url`（https://mcp.deepwiki.com/mcp）/`command`/`args`** —— 证明编辑回显缺陷③的数据源在后端是齐全的，丢在前端（见 V4）。
- 用户配置真源 `C:\Users\12504\.forgeself\plugins\mcp-center\external-servers.json` 核对：仅 deepwiki 一条，`Headers:{}`、`Env:{}`（本次失败与密钥掩码回写无关，掩码覆盖缺陷为同根因衍生项）。

## V3 根因 A/B 现场对照（Verified，探索期探针，已删不留存）
对 `https://mcp.deepwiki.com/mcp` 直连 POST：
| 请求体 | 结果 |
|---|---|
| initialize **不带** clientInfo（= 本插件现状） | `{"error":{"code":-32602,"message":"Invalid request parameters"}}` |
| initialize **带** `clientInfo:{name:"openforgeself",version:"1.0"}` | 200，`serverInfo:{name:"DeepWiki",version:"2.14.3"}` |
| 带 clientInfo 后独立 POST tools/list（无会话头、未发 initialized 通知） | 200，正常返回工具数组（deepwiki 无状态，响应无 mcp-session-id） |

结论：①②的唯一当前失败因 = **客户端 initialize 缺规范必填 `clientInfo`**；「每请求独立会话」策略对 deepwiki 无碍（对有状态外部服务器是次级缺口，登记待修批裁决）。

## V4 红灯复现测试（Verified）
- Command：`dotnet test ForgeSelf.Api.Tests -c Release --filter "FullyQualifiedName~McpExternalInitializeClientInfoTests"`
- Result：**FAIL（预期红灯=复现成功）**
  `失败 ...StreamableHttp_Initialize_RequestMustCarryMandatoryClientInfo [33 ms]`
  错误消息：`initialize 缺少规范必填的 clientInfo（DeepWiki 实测不带即 -32602 Invalid request parameters，连接/测试全灭）`
- 编译通过（既有 xUnit1031 warning 为他文件存量，与本文件无关）。
- 修复批语义：补 clientInfo 后此测试**转绿**（先红后绿锚点）。

## V5 编辑不回显（Verified=API 响应体 + Inferred=前端行为，代码行号钉死）
- `Plugins/McpCenter/web/src/McpCenterView.vue:408-423` `openEditExternal`：`url: ''`、`command: ''`、`argsText: ''` 硬编码空值（注释无、非待填提示——就是没回填）。
- `Plugins/McpCenter/web/src/types/external.ts:15-27`：前端 `McpExternalServerStateDto` 未声明 `url/command/args`（后端 V2 实测在发，类型层丢弃）。
- `headersMasked/envMasked` 回填进表单且保存链路 `parseKv` 原样提交（McpCenterView.vue:417/420/491/494）→ 编辑后保存会把 `****尾4` 掩码写回真实 Headers/Env（后端 `PUT` 合并 `dto.Headers ?? baseConfig.Headers` 不做掩码识别）——**衍生缺陷，同根因（脱敏视图被当编辑真值往返）**。

## 同类扫查命中表（P3）
| 位置 | 症状 | 定性 |
|---|---|---|
| `StreamableHttpMcpTransport.cs:54-55` | initialize 缺 clientInfo | 同根因·命中（V4 红灯锚定） |
| `StdioMcpTransport.cs:115-116` | 同缺 | 同根因·命中（修复批一并补） |
| `LegacySseMcpTransport.cs:55-56` | 同缺 | 同根因·命中（修复批一并补） |
| 三传输 | 无 `notifications/initialized`；HTTP 不回传 `Mcp-Session-Id` | 同族次级缺口（对**有状态**外部服务器必炸；deepwiki 不阻；修复批裁决范围） |
| `McpCenterView.vue:408-423` + `types/external.ts` | 编辑硬编码空值/类型丢字段 | 缺陷③本体（异根因于①②） |
| `McpCenterView.vue:417/420` 掩码回填 + PUT 合并 | 掩码覆盖真实密钥 | 缺陷③衍生（同根因往返问题，修复批应一并处理） |
| `forge-self 宿主侧 MCP 网关服务端`（McpJsonRpcHandler） | 不校验 clientInfo（宽容） | 不适用（服务端宽容是 2026-10-01-mcp2 设计裁决，非缺陷） |

## Known Limitations
- 「连接」按钮的 UI 级失败截图未拍（:51888 只读约束下未点写状态端点 /connect；文案与 /test 同源同请求体，V1+V3 已构成充分证据链）。
- stdio/http-sse 两传输未各建红灯（发真实子进程/双端点成本高且同文案同根因，以行号扫查命中表覆盖，修复批补类级守卫时再评估 G2 枚举）。

## Unresolved Issues（→ 修复批待拍板）
- 修复方案（clientInfo 常量值取什么名/版本、是否补 initialized 通知与 Session-Id 回传、编辑回显改法与掩码往返守卫）见 mini-task 根因节，待闸门1 批准后开工。
