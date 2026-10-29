---
task_id: 2026-10-29-terminal-pipe-chain-guard
feature: 021-ai-agent            # run_terminal_command 功能文档所在；tool-bridge 侧同步 039-tool-bridge.md
risk: { level: L2, triggers: ["Plugins/McpCenter/Services/PublicTools/TerminalCommandGuard.cs", "Plugins/ToolBridge/Services/CommandGuard.cs"], raised_by_agent: false }
gate1: { mode: user, at: 2026-10-29T16:12, decision: "批准，按方案开工（选项：批准，按方案开工 Recommended）" }
gate2: { mode: user, at: null, decision: null }
gate3: { mode: agent, at: 2026-10-29T18:25, decision: "APPROVED（06-review.md；快档 431/431 + e2e 1 passed + 走查截图）" }
expected_files:
  - Plugins/McpCenter/Services/PublicTools/TerminalCommandGuard.cs
  - Plugins/McpCenter/Services/PublicTools/RunTerminalCommandTool.cs
  - Plugins/ToolBridge/Services/CommandGuard.cs
  - Plugins/ToolBridge/Services/CommandExecutor.cs
  - Plugins/ToolBridge/Services/ToolSpec.cs
  - Plugins/ToolBridge/Services/PromptBuilder.cs
  - Plugins/McpCenter/plugin.json
  - Plugins/ToolBridge/plugin.json
  - ForgeSelf.Api.Tests/Plugins/TerminalCommandGuardTests.cs
  - ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ToolBridgeGuardParityTests.cs
  - ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ExecutorTests.cs
  - ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/PromptBuilderTests.cs
  - ForgeSelf.Api.Tests/Plugins/McpCenterTests/RunTerminalCommandChainExecutionTests.cs
  - docs/02-features/021-ai-agent.md
  - docs/02-features/039-tool-bridge.md
  - ForgeSelf.Web/e2e/plugins/mcp-center/
rollback: "git checkout -- <上述文件>（本批未提交前）；提交后 git revert <commit>"
writeback: { feature_doc: 021-ai-agent.md + 039-tool-bridge.md, reason: "工具能力语义变化，文档必须同步（plugin-development 铁律 6）" }
---

# Agent Task

> 阶段：Stage 4｜前序工件：00-03 齐备，待闸门1 用户批准后才进入 Implement。

## Task ID

PILOT-075

## Objective

执行命令工具（run_terminal_command / tool-bridge run_command）从「整串禁止管道与链式」升级为
「引号感知分段，每段过与单命令完全相同的拦截校验」，多段命令经 pwsh 真实执行；
两份守卫实现逐条同判，描述文案与版本号同步，全部验证真实跑绿。

## Scope

### Allowed

- 上表 16 个文件/目录（03-plan Files To Change 全集）及其测试。
- 新增 `SplitSegments` 公共方法与 `RunTerminalCommandChainExecutionTests` 测试类。
- 文档行级更新（021/039）与 e2e 冒烟 1 条。

### Forbidden

- 改白名单 `DefaultAllowlist` / 破坏词表 / 超时与输出上限常量（一个字不动）。
- 改宿主源码 `ForgeSelf.Api/**`、前端 `ForgeSelf.Web/src/**`、任何 csproj/依赖。
- 改第 3 道 cwd 门、PC-011 的 `ProcessOutputText` 裁决逻辑。
- 触碰运行实例（:51888）、git 提交/发布（闸门2 通过且用户同意后另行执行）。
- 跳过对账金样表直接改单侧守卫。

## Acceptance Criteria

- [x] AC1 链式/管道真实可执行（`;`、`&&`、跨段管道 ANSWER:42、换行两段）——工具级真实执行测试绿
- [x] AC2 `git status && rm -rf x` 拒绝：原因含 `destructive` + `segment 2/2`，零子进程（**偏差：原因词实为 `allowlist`**——rm 本不在白名单，段内门序=白名单先于红线，与单命令一致；白名单外壳内嵌破坏词由 `pwsh -Command Remove-Item` 用例钉死=destructive+segment 2/2，见 05-evidence）
- [x] AC3 `git log | select-string foo` 拒绝：原因含 `allowlist` + `segment 2/2`
- [x] AC4 `git commit -m "a && b"` 放行（引号保护）
- [x] AC5 E2/E3/E4 整串仍拒（`>` `<` 反引号 单`&` / 引号不配对 / 空段），消息按 02-spec Error Handling 表（E3 为独立消息，测试按 spec 钉）
- [x] AC6 既有用例回归全绿（仅第 2 道门旧策略断言按偏差清单更新）
- [x] AC7 对账金样表扩展 ≥12 条，两实现判定与原因逐条相同（35→51 行，+16）
- [x] AC8 文案与版本同步（Description/Schema/PromptBuilder/两 plugin.json 2.9.0 / 1.1.0）
- [x] AC9 `dotnet build` 0 error + 快档过滤集全绿（431/431）
- [x] AC10 e2e/plugins/mcp-center 冒烟 1 条（universal_tool → 链式命令）实跑通过（1 passed 55.6s）
- [x] AC11 dev-stack 起源码走查：测试台真实发链式命令 + 破坏链各 1 条，截图存档，清测试数据（**偏差**：工具测试台仅服务外部 MCP 工具，内置工具「测试」为连通性 ping；等效证据=UI 截图+本实例网关 18895 真实 JSON-RPC 双链，见 05-evidence）

## Expected Files

见 front-matter expected_files（16 项）。

## Verification Commands

```bash
# 1. 构建
cd ForgeSelf.Api && dotnet build

# 2. 快档过滤集（先设 $env:TEMP=$env:TMP='<repo>\.temp\tmp'；§5.0）
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TerminalCommandGuard|FullyQualifiedName~ToolBridge|FullyQualifiedName~RunTerminalCommand|FullyQualifiedName~McpCenterTests|FullyQualifiedName~Parity"

# 3. e2e 冒烟（先设 NO_PROXY=localhost,127.0.0.1）
cd ForgeSelf.Web && bash node_modules/.bin/playwright test e2e/plugins/mcp-center --grep 冒烟

# 4. 走查（起源码本地前后端，用完 -Stop；只停自己起的）
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1
```
