# Evidence（stdio 外部 MCP npx 三层修复批，2026-10-18）

## Task

用户报障：MCP 中心配 `npx @playwright/mcp@latest`（stdio 外部 MCP）点「测试」报 400「无法启动外部 MCP 进程 'npx'…系统找不到指定的文件」。要求：复现 → 测试复现 → 修复 → 回归。

## Changed Files

- 修复：`Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs`（S1+S2 主体）、`McpClientProtocol.cs`（S3 SerializeRequest）、`StreamableHttpMcpTransport.cs` / `LegacySseMcpTransport.cs`（S3 同改）、`Models/McpExternalServerConfig.cs` + `Controllers/McpExternalController.cs` + `Services/ExternalServersStore.cs`（framing 贯穿链）
- 测试：`McpStdioWindowsShimTests.cs`（新，红灯锚+6 组纯函数）、`McpStdioFramingTests.cs`（新，帧协商矩阵）、`McpClientProtocolGuardTests.cs`（PC-010 守卫段）、`McpClientIntegrationTests.cs`（lsp 快路径锁）、`Fixtures/mock-mcp-server.js`（stdio-ndjson 模式）、`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（npx 零容忍用例 + 既有 stdio 用例锁 framing）
- 版本/文档：`plugin.json` + `McpCenter.csproj`（2.5.1）、`docs/02-features/034-mcp-center.md`、PC-010/case/三索引、本 pilot 目录

## Build

- **Verified**：`dotnet build`（插件+宿主）0 错；dll 被并行 dev-stack 宿主锁定时用 `-p:BaseOutputPath=.temp\altbuild\` 绕行（未停他批进程）。

## Unit Test

- **Verified**：红灯（修复前）：`StdioTransport_WindowsNpxShim_LaunchesAndInitializes` 失败输出 = 用户报错**逐字同款**；
- 定向 32/32 绿（ShimTests 7 + FramingTests 4 组 + GuardTests PC-010 段 + 既有 Integration/Frame 回归）；
- 全量 `dotnet test`：3043/3103，60 红全部位于 Chat/Multimodal/Workflow/RealLLM 域——`git status` 证实并行会话正在改 20+ AI/Chat 后端文件，与本批改动零重叠 ⇒ 归因他批基线，登记不修（日记 2026-10-17/18 已记）。

## Integration Test

- npx 真实集成（server-everything）：修复①后推进到 tools/list 30s 超时（暴露缺陷②）；修复②后仍超时（探针 A/B 定位缺陷③）；三修全绿。

## E2E

- **Verified**：插件套 `mcp-center.spec.ts` **6/6 绿**（1.8m）：新用例「stdio npx 真实世界」零容忍——connected=True、toolCount=13、/test success（握手 3661→3689ms、协议 2025-11-25）；
- 复跑（+scrollIntoViewIfNeeded + 版本 2.5.1）单用例绿：toast 原文「e2e npx shim 握手成功（3589ms，协议 2025-11-25）」。

## Static Analysis

- `pwsh scripts/check-problem-classes.ps1`：10 PC 全绿（含 PC-010 S1 grep 签名）；
- `ProblemClassRegistryTests` 5/5 绿（PC/case 互指、路径存在性）；
- 前端 `pnpm run check`/e2e lint 随 playwright 运行无错。

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/mcp-center/npx-shim-list.png` —— 读图核验：版本徽标 **v2.5.1**、「e2e npx shim」条目 已连接/stdio/13 个工具/协议 2025-11-25 mcp-servers/everything ✓
- `ForgeSelf.Web/screenshots/e2e/mcp-center/npx-shim-test-toast.png` —— 读图核验：绿色 toast「e2e npx shim 握手成功（3589ms，协议 2025-11-25）」+ 同条目 ✓
- （首版 list 图无条目——Playwright 断言不滚动，截图前补 scrollIntoViewIfNeeded 后复跑核图，经验入 case §9）

## 门禁③ 现场实测（dev-stack 源码栈）

- 用户原文配置（command=npx, args=[@playwright/mcp@latest]，**无 -y**）：`connected=True toolCount=25`（Playwright 真服务器）、`/test success=True 3851ms`；
- Browser 子代理 UI 走查功能全 PASS（列表已连接 25 工具、绿 toast、编辑回显 npx + @playwright/mcp@latest 完整）；截图受窗口 hidden 阻塞 ⇒ 视觉取证已固化进 e2e UI 段（合规路径，未手写截图脚本）；
- 测试数据清理：`live-npx-playwright` 已从 `.temp/dev-stack/data/plugins/mcp-center/external-servers.json` 移除；探针 4 件（probe-stdio-framing*.ps1、g3-live-npx.ps1）已删。

## Known Limitations

- auto 帧协商对 LSP-only 服务器多一次 5s 首帧等待（显式 framing 可消除）；
- Windows 启动分支在 Linux CI 不可真跑（集成测 OS 门 + CI skip），由注入式纯函数单测兜形状；
- 前端未暴露 framing 选择 UI（config/API 已预留，出现 auto 判不了的现场再加）。

## Unresolved Issues

- 门禁⑤（运行实例 :51888 只读复验）待用户把宿主更新到含 2.5.1 的新版本后发起；
- R2 提案（三传输公共 JsonRpcChannelBase 骨架）未做，理由见 case §6，登记 TODO；
- 全量套 60 红属并行会话域，由其自行收口（本批零重叠证据在案）。

## 门禁④ 发布

- 发布决策：发现 updates 内已有并行会话 13:03 发出的 2.4.10（解包核实其 McpCenter.dll=2.5.0，**不含本批修复**）→ AskUserQuestion，用户拍板「现在就发 2.4.11」；
- 命令：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\release\release-local.ps1 -Version 2.4.11 -Sign` → exit 0 + 日志 `release-local: ALL DONE in 139s`（`.forgeself/tmp-release-2411-20261018.log`）；
- 签名段（看正文不看 exit code）：签成 2 / 跳过 1（createdump.exe）/ 失败 0，**校验通过 3/3**，签名者 CN=OpenForgeSelf 本地自签，`Authenticode signing ok (8.9s)`；
- 产物：`D:\src\my-proj\OpenForgeSelf\updates\OpenForgeSelf-2.4.11.2610181333-win-x64.zip`（158.2 MB）+ RELEASE-NOTES + SHA256SUMS；
- **解包核验（plugin-publish-verify 规则④）**：zip 内 `versions/2.4.11.2610181333/plugins/McpCenter/McpCenter.dll` `FileVersion=2.5.1.0`、`ProductVersion=2.5.1+8ea581c…` ✓ 含本批修复；SHA256 实测 = SHA256SUMS.txt 行（`0E3FB2…551809` MATCH）✓；解包临时 dll 已删；
- 风险交代（已拍板确认）：包为工作树快照，含并行会话未验证改动；本地更新源不自动推，更新决策在用户。
