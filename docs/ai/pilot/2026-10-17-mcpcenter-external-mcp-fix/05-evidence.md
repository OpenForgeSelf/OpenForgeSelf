# Evidence

## Task

PC-008 修复批：外部 MCP 客户端侧规范合规（clientInfo / initialized 通知 / 会话头回传）+ 编辑全回显与掩码不往返 + `/test` 端点 data 契约（门禁② e2e 暴露的第 4 缺陷）。闸门1 = 用户「开工」（2026-10-17，登记于 00）。

## Changed Files

| 文件 | 动作 |
|------|------|
| `Plugins/McpCenter/Services/McpClient/McpClientProtocol.cs` | 新建（canonical：BuildInitializeParams / InitializedNotificationMethod / 会话头常量） |
| `Plugins/McpCenter/Services/McpClient/StreamableHttpMcpTransport.cs` | clientInfo + NotifyAsync + CaptureSessionId/ApplyProtocolHeaders（请求/通知/SSE GET 全覆盖） |
| `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs` | clientInfo + initialized 帧（走 _writeLock） |
| `Plugins/McpCenter/Services/McpClient/LegacySseMcpTransport.cs` | clientInfo + endpoint POST 无 id 通知 |
| `Plugins/McpCenter/Services/ExternalServersStore.cs` | RejectMaskedValue（`****` 前缀且 ≤8 字符拒绝落盘） |
| `Plugins/McpCenter/Controllers/McpExternalController.cs` | /test 成功 data 补 `{success,message,durationMs}`（信封 message 到不了前端） |
| `Plugins/McpCenter/Services/McpClientManager.cs` | TestAsync 返回 (Ok, Message, DurationMs) |
| `Plugins/McpCenter/web/src/McpCenterView.vue` | openEditExternal 真值回显 + externalSecretBaseline 掩码基线 + saveExternal 基线相同不提交 + 脱敏 hint（独立行 margin-left 88px） |
| `Plugins/McpCenter/web/src/types/external.ts` | StateDto 补 url?/command?/args? |
| `Plugins/McpCenter/web/src/types/dsh.ts` | DshMcpConfigDto 补 entryId/url（异根因既有 check 错，最小解封，见 TODO 同族条目） |
| `Plugins/McpCenter/plugin.json` + `McpCenter.csproj` | 版本 2.4.0 → 2.5.0（三处 + Description） |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs` | 红灯转 G1（捕获全部请求体：首发 clientInfo + 后续无 id 通知） |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientProtocolGuardTests.cs` | 新建 G2/G3（形状直测 + 三传输源码扫描 + 掩码拒绝枚举） |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStreamableSessionHeaderTests.cs` | 新建 G2（会话头回传 / 无状态不携带） |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientIntegrationTests.cs` | 新增 TestEndpoint 两测（/test 成功/400 data 形状锁） |
| `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` | 新增「外部 MCP UI 主链路（v2.5.0）」用例（预设→建连→测试→编辑回显→PUT→删除，7 证据） |
| `.agents/skills/problem-class-closure/scripts/new-problem-case.ps1` | 修 Double→`{0:D3}` 格式崩（归档过程实证） |
| `docs/11-troubleshooting/`（PC-008 + case + 三索引）、`docs/02-features/034-mcp-center.md` | 问题类归档 + 契约回写 |

## Build

- **Verified**：`dotnet build ForgeSelf.slnx`（经 dotnet test 隐含）：**0 错误**（McpCenter 2.5.0 装配版本生效）。
- **Verified**：插件前端 `pnpm run build`：**绿**（vite 构建通过）。

## Unit Test

- **Verified**：`dotnet test --filter FullyQualifiedName~McpCenterTests`：**121/121 通过**（含 G1 转绿、G2 守卫 3 文件、TestEndpoint 两测；node mock 三传输集成回归不破）。
- `dotnet test --filter ProblemClass`：**8/8 通过**（PC-008/case 三索引一致性）。
- `pwsh scripts/check-problem-classes.ps1`：**OK，8 个 PC 签名/allowlist/guards 全过**（含 PC-008 S1 grep no-match）。
- 插件前端 `pnpm run test`：**23/23 通过**。

## Integration Test

（外部 MCP 集成即上列 McpCenterTests 内 node mock 用例，无独立集成套件。）

## E2E

- **Verified**：`pnpm run test:e2e -- e2e/plugins/mcp-center/mcp-center.spec.ts`：**5/5 通过**（真实宿主 publish + 零 mock + node mock MCP）。
- 新用例（v2.5.0）7 行证据：预设按钮生效 → 提交自动建连 → 列表已连接+2 工具 → **测试按钮真实握手（initialize+ping）通过** → 编辑 URL 回显真值+脱敏说明可见 → PUT 原样保存 url 未丢 → UI 删除移除。日志 `.pw-out`/OUT_DIR `external-mcp-ui.log`。

## Static Analysis

- 插件前端 `pnpm run check`：**0 error**（曾报 3 个 DshMcpConfigDto 既有错，git stash 验证为既有后以 types/dsh.ts 最小补字段解封；视图旧链路迁移留 TODO）。

## Screenshots

- dev-stack（源码前后端，动态端口 7301/7399）走查 5 步全 PASS，截图读图核验：`screenshots/g3-devstack-20261017/g3-devstack-01…05.png`（02=绿色「G3 走查 握手成功（3ms，协议 2025-11-25）」toast；03=编辑弹窗 ID 禁用/URL 真值/脱敏 hint 独立行不挤压）。
- API 层实测：`/test` 响应 data 含 `success:true, durationMs:4`（dev-stack 宿主 = 新代码，构建新鲜度双证）。
- 走查后测试数据已清（列表空态截图 05），dev-stack 已 `-Stop`，mock 进程已回收。

## Known Limitations

- initialized 通知按「尽力而为」处理（失败仅 Warn）：规范 MUST 已满足（正常服务器必收到），宽松服务器对通知回错误不影响会话——A/B 证据为 mock 对通知回 `-32601` 仍握手成功。
- G3 grep 签名覆盖面 = `Services/McpClient/*.cs`；他处新写 MCP 客户端由 PC-008 lens 在 Spec 阶段兜（已写入 case §2 盲区）。

## Unresolved Issues

无悬空：走查发现的 `v-loading` 未注册、窄视口卡片布局劣化 → TODO「McpCenter 插件前端视觉/指令缺陷」条目③④；dsh 视图旧链路迁移 → TODO 既有条目。
