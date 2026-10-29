# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。

## Task

PILOT-075（2026-10-29-terminal-pipe-chain-guard）

## Changed Files

- Plugins/McpCenter/Services/PublicTools/TerminalCommandGuard.cs（分段器 + 逐段校验重构）
- Plugins/McpCenter/Services/PublicTools/RunTerminalCommandTool.cs（双路径执行 + Description/Schema 文案）
- Plugins/ToolBridge/Services/CommandGuard.cs（同构镜像）
- Plugins/ToolBridge/Services/CommandExecutor.cs（同构镜像）
- Plugins/ToolBridge/Services/ToolSpec.cs（run_command 描述 + schema 文案）
- Plugins/ToolBridge/Services/PromptBuilder.cs（约定行改「每段首 token」口径，白名单句仍由 DefaultAllowlist 派生）
- Plugins/McpCenter/plugin.json（2.8.0 → 2.9.0）
- Plugins/ToolBridge/plugin.json（1.0.4 → 1.1.0）
- ForgeSelf.Api.Tests/Plugins/TerminalCommandGuardTests.cs（第 2 道门重构 + TrySplitSegments 组）
- ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ToolBridgeGuardParityTests.cs（金样表 35→51 行 + 阳性对照）
- ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ExecutorTests.cs（+3 链式用例）
- ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/PromptBuilderTests.cs（口径同步）
- ForgeSelf.Api.Tests/Plugins/McpCenterTests/RunTerminalCommandChainExecutionTests.cs（新增，6 用例）
- docs/02-features/021-ai-agent.md、docs/02-features/039-tool-bridge.md
- ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts（+PILOT-075 冒烟用例）

## Build

Command:

```bash
dotnet build ForgeSelf.Api --nologo -nr:false -m:1
dotnet build Plugins/McpCenter --nologo -nr:false -m:1
dotnet build Plugins/ToolBridge --nologo -nr:false -m:1
dotnet build ForgeSelf.Api.Tests --nologo -nr:false -m:1
```

Result: PASS（来源等级：Verified）

```text
四工程 exit=0，0 error（仅既有基线警告 CS8618/NU1510 等）。
过程记录：中间两轮编译红已修复——CS1628（本地函数捕获 out 参数，FlushSegment 改返回消息）
与 CS0023（残留一处 24 空格缩进调用点漏改）。
```

## Unit Test

Command:

```bash
# 前置（§5.0）：$env:TEMP=$env:TMP='<repo>\.temp\tmp'
dotnet test ForgeSelf.Api.Tests --nologo --filter "FullyQualifiedName~TerminalCommandGuard|FullyQualifiedName~ToolBridge|FullyQualifiedName~RunTerminalCommand|FullyQualifiedName~McpCenterTests|FullyQualifiedName~Parity"
```

Result: PASS（来源等级：Verified）

```text
最终轮：已通过! - 失败: 0，通过: 431，已跳过: 0，总计: 431（43s）——.temp/test-p075-r3.log
第一轮 35 红（我引入的自伤 bug：重写 FlushSegment 时丢失 result.Add(seg) → 分段恒空 →
逐段循环零次 → 一切放行）；修复后 432/434；余 2 红为测试期望与实际门序/消息不符（见偏差），修正后全绿。
```

## Integration Test

Result: N/A（快档不含集成层；守卫/执行器行为由工具级真实执行测试覆盖——真实起 pwsh/node/git 子进程）

## E2E

Result: PASS（来源等级：Verified）

```text
npx playwright test e2e/plugins/mcp-center/mcp-center.spec.ts -g "PILOT-075" --workers=1
→ 1 passed (55.6s)，.temp/e2e-p075.log
真实证据（globalSetup 起 publish 宿主，零 mock）：
- chain allow: {"success":true,"command":"git --version && node --version","exitCode":0,
  "stdout":"git version 2.53.0.windows.2\nv24.16.0"} —— 多段经 pwsh 真执行
- chain segment-reject: {"success":false,"error":"Error: segment 2/2: Error: command not
  allowed by terminal allowlist: python..."} —— 段级拒绝带前缀
```

## Static Analysis

Result: N/A（本批无 ForgeSelf.Web/src 改动，不触发 pnpm check/test 门禁；e2e TS 由 playwright 编译验证）

## Screenshots

- ForgeSelf.Web/screenshots/dev-walkthrough/2026-10-29-p075/01-mcpcenter-v290-new-description.png（MCP 中心页：v2.9.0 徽标 + run_terminal_command 新描述 + 服务运行中）
- 同目录 02-tool-row-new-description.png（工具行完整新文案）
- 同目录 walkthrough-evidence.log（dev-stack 走查证据原文：UI + 本实例网关 18895 真实 JSON-RPC 双链输出）

## Acceptance Criteria 对照（04-task AC1-AC11）

- [x] AC1 链式/管道真实可执行——Verified：RunTerminalCommandChainExecutionTests（&&链/换行两段/真管道数据穿透）+ ExecutorTests（写读文件链）+ e2e 全绿
- [x] AC2 `git status && rm -rf x` 拒绝 + segment 2/2 + 零子进程——Verified；**偏差：原因词是 `allowlist` 非 `destructive`**（rm 本就不在白名单；段内门序=白名单先于红线，与单命令门序一致；白名单外壳下的破坏词由 `pwsh -Command Remove-Item` 系用例钉死，实测 segment 2/2 + destructive）
- [x] AC3 `git status | select-string foo` 拒绝（allowlist + segment 2/2）——Verified（金样表行）
- [x] AC4 引号保护放行——Verified：`git log --format="%h | %s"`、`git commit -m "..."`、`pwsh -Command 'Write-Host "a ; b"'` 均单段放行
- [x] AC5 E2/E3/E4 整串仍拒，消息与 02-spec 一致——Verified：`git log > out.txt`/`echo \`id\``/`git status &`/空段/`git "status`（E3 为独立消息 `Error: unbalanced quotes in command`，**偏差：AC5 原文把 E3 归入 chained/piped 字样，实际按 spec E3 独立消息**，测试按 spec 钉）
- [x] AC6 既有用例回归全绿（仅旧策略断言按偏差清单更新）——Verified：431/431 含全部既有 guard/executor/encoding 用例
- [x] AC7 金样表扩展 ≥12 条且两实现逐条同判——Verified：35→51 行（+16），parity 全绿；阳性对照新增 `git status && git diff` 两侧放行
- [x] AC8 文案与版本同步——Verified：UI 徽标 v2.9.0 + 新描述截图；两 plugin.json 已改；PromptBuilderTests 钉口径
- [x] AC9 build 0 error + 快档过滤集全绿——Verified（见上）
- [x] AC10 e2e 冒烟 1 条——Verified（见上）
- [x] AC11 dev-stack 走查——Verified，**偏差：AC11 原文「测试台发链式」不可行**——工具测试台（ToolPlayground，McpCenterView.vue:1142-1157）仅服务外部 MCP 服务器工具，内置工具「测试」按钮为连通性 ping（实测弹「测试通过 73ms」）；等效证据链 = UI 截图（版本徽标+新文案）+ 本实例网关 18895 真实 JSON-RPC 双链（链式放行 exit 0 / 破坏链 segment 2/2 destructive）+ 快档执行测试；走查后 dev-stack 已 -Stop，无测试数据残留（链式命令只读）

## Known Limitations

- 重定向（`>` `<`）、反引号、后台单 `&` 本批明确未放开（用户范围=管道与链式）。
- 多段命令超时为整串墙钟（pwsh 进程整体），非逐段；退出码取 pwsh 返回值（链式=最后一段）。
- 段数无上限；`|&` 形态落到「下一段以 & 开头」→ E2 拒绝；`2>&1` 含 `>` → E2 拒绝。
- McpCenter 网关 initialize 的 serverInfo.version 常量仍是 2.7.0（插件版本徽标/plugin.json 已 2.9.0）——既有陈旧常量，与本批无关，已记 TODO。

## Unresolved Issues

无（全程无 BLOCKED）。
