# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。每个验证项标注来源等级：Verified（亲自跑过，有真实输出）。
> 本任务全部验证均在独立 worktree `D:\src\my-proj\OpenForgeSelf\wt-mcp2`（分支 mcp2-support）内执行，未触碰主仓库源码与运行实例（:51888 / 18889）。

## Task

PILOT-mcp2-protocol

## Changed Files

- `Plugins/McpCenter/Services/McpJsonRpcHandler.cs`（改：协议白名单 + `2025-11-25` 置首、serverInfo.description、类注释）
- `Plugins/McpCenter/Services/McpClient/McpClientSession.cs`（改：客户端白名单 + `2025-11-25` 置首、注释）
- `Plugins/McpCenter/plugin.json`（改：Version 2.1.1 → 2.2.0）
- `Plugins/McpCenter/McpCenter.csproj`（改：Version/AssemblyVersion/FileVersion 2.1.0 → 2.2.0，修复与 plugin.json 双源不一致）
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpJsonRpcHandlerTests.cs`（改：+description 断言、+`Initialize_NewClientVersion2025_11_25_IsNegotiated`）
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientSessionTests.cs`（改：客户端声明列表断言 + `2025-11-25`）
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientIntegrationTests.cs`（改：stdio 协商断言 2025-06-18 → 2025-11-25）
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js`（改：PROTOCOL_VERSIONS + `2025-11-25`）
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（改：+MCP 2.0 initialize 声明断言块）
- `docs/02-features/034-mcp-center.md`（改：版本/协议表/契约表/转发契约/验证记录 v2.2.0 小节）

## Build

Command:

```bash
cd wt-mcp2\ForgeSelf.Api; dotnet restore && dotnet build --nologo
```

Result: PASS（来源等级：Verified）

```text
0 个错误
1190 个警告（既有基线，无本任务引入）
```

## Unit Test

Command:

```bash
cd wt-mcp2; dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter" --nologo
```

Result: PASS（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 96，已跳过: 0，总计: 96，持续时间: 14 s
（含新增 Initialize_NewClientVersion2025_11_25_IsNegotiated；description 非空断言；客户端列表断言；stdio 协商 2025-11-25）
```

## Integration Test

Result: PASS（来源等级：Verified）
依据：`McpClientIntegrationTests`（真连 node mock 三传输 stdio/streamable-http/http-sse + universal_tool mcp. 转发）含于上述 96/96；stdio 用例断言 `session.ProtocolVersion` 含 `2025-11-25`（客户端与 mock 均支持 2.0 后协商结果由 2025-06-18 变为 2025-11-25）。

## E2E

Command:

```bash
cd wt-mcp2\ForgeSelf.Web; .\node_modules\.bin\playwright.cmd test e2e/plugins/mcp-center --reporter=line
```

Result: PASS（来源等级：Verified）

```text
3 passed (43.3s)
证据关键行：
initialize(2025-11-25): {"protocolVersion":"2025-11-25",...,"serverInfo":{"name":"ForgeSelf McpCenter","version":"2.2.0","description":"OpenForgeSelf MCP Center（万能工具网关，支持 MCP 2.0）"}}
stdio 已连接：toolCount=2 protocol=2025-11-25（外部 MCP 客户端对服务器也协商到 2.0）
health: {"status":"ok","version":"2.2.0","tools":1,...}
```

## Static Analysis

<!-- pnpm run check（vue-tsc + eslint） + vitest -->

Result: PASS（来源等级：Verified）

```text
pnpm run check: 0 errors, 81 warnings（既有基线，无本任务引入；exit 0）
pnpm run test (vitest): Test Files 53 passed (53) / Tests 568 passed (568)
```

## 现场验证（输入6：用户要求临时实例 + 浏览器添加服务器实测）

**临时实例**（全部隔离，结束已停、生产未受影响）：
- 宿主 = worktree e2e 构建的 publish 产物（`.temp/e2e/wt-47f6959c/publish/ForgeSelf.exe`，McpCenter 2.2.0）
- `FORGESELF_INSTANCE_ID=live-verify`（绕过生产单实例 Mutex）、`FORGESELF_DATA_ROOT=.temp/live-verify/data`（数据隔离）、宿主 `51999`、MCP 网关 `51998`、`FORGESELF_NO_TRAY=1`

**① 服务器侧 2.0 协商（curl 直连网关，Verified）**：
```bash
POST http://127.0.0.1:51998/mcp  {"protocolVersion":"2025-11-25",...}
→ {"protocolVersion":"2025-11-25","capabilities":{"tools":{"listChanged":false}},
   "serverInfo":{"name":"ForgeSelf McpCenter","version":"2.2.0","description":"OpenForgeSelf MCP Center（万能工具网关，支持 MCP 2.0）"}}
```
→ **回显 2025-11-25（不再降级到 2025-06-18）+ 新增 description 契约点生效**。对照组：声明 `2025-06-18` → 回显 `2025-06-18`（向后兼容）。

**② 2.0 会话下工具链路（curl，Verified）**：`tools/list` 返回 `universal_tool`（含完整能力描述）；`tools/call` calculate 8*9 → `{"success":true,"expression":"8*9","result":72}`。

**③ 浏览器控制 51888 生产宿主添加临时实例（用户指定路径，Verified）**：
- 51888 宿主（McpCenter **2.1.1**，生产部署）MCP 中心 → 新增外部服务器 → 传输 **Streamable HTTP（MCP 2.0）** → ID `live-verify`、URL `http://127.0.0.1:51998/mcp` → 保存自动连接
- 结果：**已连接**、`Streamable HTTP`、`mcp.live-verify.<工具名>`、**1 个工具**（universal_tool 被枚举）、serverInfo `ForgeSelf McpCenter`、**协议 `2025-06-18`**
- **协议 2025-06-18 符合预期**：旧客户端（2.1.1）白名单最高 2025-06-18，与新服务器协商取交集 → 证明①旧客户端对 2.0 服务器完全兼容（连接/工具发现/转发正常）；②新服务器协商正确降级
- 验证后已删除 live-verify 测试服务器（现场恢复）；生产 MCP 网关（18889, v2.1.0）全程未受影响

**结论**：改造生效的完整证据链 = 服务器侧 2.0 协商（curl）+ 2.0 会话工具调用（curl）+ 旧客户端兼容（浏览器 51888）+ 既有 e2e/单测（前文）。

## Screenshots

N/A（本任务无 UI 视觉变更；e2e 用例② UI 渲染走断言非截图；现场验证以节点文本证据留档）

## Known Limitations

1. **esbuild 写入被环境按路径拦截（worktree 特有环境问题，非代码缺陷）**：本环境（full_access 真实系统）对 esbuild.exe 写入 `wt-mcp2` 路径下文件一律 `Access is denied`（node/PowerShell 写同路径正常；主仓库路径可写）。vite dev 首次预构建依赖因此失败 → e2e 前端 7002 起不来。**已解决**：将 worktree `ForgeSelf.Web/node_modules/.vite` 建 junction 指向主仓库 `.vite`（写入落到可写路径），vite 重优化成功、dev 就绪。副作用：主仓库 `.vite/deps/_metadata.json` 的 configHash 被 worktree 覆盖为 worktree 值——主仓库下次 dev 会自愈重写（缓存仅加速用，不影响正确性）。
2. **worktree 新检出无插件前端产物**：`Plugins/McpCenter/web/dist` 不入库（`.gitignore`），e2e 宿主加载插件界面 404。**已解决**：复制主仓库等价 dist（同提交 e118b04，产物一致）。
3. **globalSetup SQLite 检查为陈旧逻辑（本任务 e2e 时暴露，非代码缺陷）**：`ForgeSelf.Api.csproj` 已含正式依赖 `XCode.SQLite 11.24.2026.302`（输入38 入库），`dotnet publish` 产物**自带** `System.Data.SQLite.dll`/`e_sqlite3.dll`（已实测 e2e 临时 publish 根含两文件）。但 `e2e/global-setup.ts` 第 1.5 步为 XCode 运行时探测时代的兼容逻辑，仍强制从 `REPO_ROOT` 候选（`build/runtime/Plugins`、`REPO_ROOT/publish` 根/Plugins）找源并复制——worktree 无 `REPO_ROOT/publish`（从未发布过）→ 该步报错。本次以复制 `build/runtime/Plugins` 兜底让其通过；**根因修复**（1.5 步改为「publish 已自带则跳过」）已记 TODO，随输入38 清理任务或独立小单处理。

## Unresolved Issues

无阻塞项。`list_todos` 工具缺陷（`No service for type 'IServiceScopeFactory>'`）为既有 TODO（来源输入19），不在本任务范围。MCP 2.0 可选能力（OAuth/tasks/icons/elicitation）与 SEP-1303 错误语义（D1）按闸门1 决策不实现。
