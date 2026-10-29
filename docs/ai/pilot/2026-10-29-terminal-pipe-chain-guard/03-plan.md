# Plan

> 阶段：Stage 3｜Task ID：PILOT-075｜必须具体到真实文件路径。

## Files To Change

| file | reason |
| --- | --- |
| `Plugins/McpCenter/Services/PublicTools/TerminalCommandGuard.cs` | FR-1/FR-2：新增 `SplitSegments`（引号感知分段，public 供对账测试）；`CheckCore` 重构为「整串级检查（E1~E4）→ 逐段单命令核心（E5~E7，多段加 `segment i/n:` 前缀）」 |
| `Plugins/McpCenter/Services/PublicTools/RunTerminalCommandTool.cs` | FR-3：段数 >1 走 `pwsh -NoProfile -NonInteractive -Command`（ArgumentList）；FR-4：Description + Schema 文本 |
| `Plugins/ToolBridge/Services/CommandGuard.cs` | FR-1/FR-2 镜像（与 mcp-center 逐行同构，消息逐字一致） |
| `Plugins/ToolBridge/Services/CommandExecutor.cs` | FR-3 镜像（多段 pwsh 路径；ToolResult 形状不变） |
| `Plugins/ToolBridge/Services/ToolSpec.cs` | FR-4：run_command 条目描述 + command 字段描述 |
| `Plugins/ToolBridge/Services/PromptBuilder.cs` | FR-4：命令约定行（管道/链式支持 + 仍拒项；白名单句保持常量派生） |
| `Plugins/McpCenter/plugin.json` | FR-5：Version 2.8.0→2.9.0 + Description 注记 |
| `Plugins/ToolBridge/plugin.json` | FR-5：Version 1.0.4→1.1.0 + Description 注记 |
| `ForgeSelf.Api.Tests/Plugins/TerminalCommandGuardTests.cs` | AC1~AC6：第 2 道门用例组重构（仍拒/新放行/逐段拦截/引号保护），SplitSegments 直测 |
| `ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ToolBridgeGuardParityTests.cs` | AC7：金样表扩展新形态 ≥12 条（链式放行/逐段拒/E2~E4/引号保护） |
| `ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ExecutorTests.cs` | AC1/AC2/AC3 镜像：run_command 链式真实执行 + 逐段拒绝用例 |
| `ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/PromptBuilderTests.cs` | AC8：提示词新文案断言更新 |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/RunTerminalCommandChainExecutionTests.cs`（新建） | AC1/AC2：工具级真实链式执行（`;`/`&&`/管道/换行/逐段拒绝/引号保护） |
| `docs/02-features/021-ai-agent.md` | FR-5：run_terminal_command 行更新（含 cwd 旧描述一并更正） |
| `docs/02-features/039-tool-bridge.md` | FR-5：命令守卫描述行更新 |
| `ForgeSelf.Web/e2e/plugins/mcp-center/`（现有 spec 内增 1 条冒烟） | AC10：万能工具转发链式命令，断言逐段校验 + 真实输出 |

不触碰：宿主源码（`ForgeSelf.Api/**`）、前端 `ForgeSelf.Web/src/**`、白名单/破坏词表常量、csproj、其他插件。

## Implementation Steps

1. mcp-center 守卫：实现 `SplitSegments`（单趟扫描：引号态 + 引号外整串级字符检查 E2 + 切分）→ `CheckCore` 改为逐段循环；
   单段原因逐字节不变、多段加前缀；`Check` 外层日志不动。
2. mcp-center 工具：`ExecuteAsync` 中段数判定（经守卫公开方法），>1 走 pwsh ArgumentList 路径；执行后段（超时/解码/截断/日志）复用现有代码，不复制逻辑；Description/Schema 文案。
3. tool-bridge 守卫/执行器/文案：与步骤 1/2 逐行镜像（消息逐字一致）。
4. 测试落地（TDD 顺序：先改红既有第 2 道门用例 → 实现转绿 → 补新形态用例 → 对账表扩展）。
5. 版本号 + 两篇功能文档 + e2e 冒烟用例。
6. 门禁与走查（见 Verification）；证据落 05/06/07。

## Test Plan

1. **守卫纯逻辑**（零子进程）：仍拒组（E2 三例 + E3 + E4）／新放行组（`;`、`&&`、`||`、`|`、换行，段全白名单）／
   逐段拦截组（段 2 破坏性 / 段 2 白名单外，断言 `segment 2/2` 前缀）／引号保护组／单段原因逐字节不变组。
2. **对账**：金样表新增 ≥12 条（正反两侧），两实现判定 + 原因原文逐条相等；阳性对照放行事实保留。
3. **真实执行**（有既有真子进程测试先例）：工具级（mcp-center）`;` 链、`&&` 链、跨段管道（ANSWER:42）、
   换行两段、破坏段拒绝零进程、引号保护；tool-bridge Executor 同型 2-3 条。
4. **文案互锁**：PromptBuilderTests（新约定句 + 白名单句仍由常量派生）、ToolSpec schema 文本断言。
5. **e2e 冒烟 1 条**：universal_tool → run_terminal_command 链式（零 mock，不依赖 LLM）。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
```

### Unit Test（快档过滤集）

```bash
# 先 $env:TEMP/$env:TMP 指向 <repo>\.temp\tmp（§5.0 环境前置）
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TerminalCommandGuard|FullyQualifiedName~ToolBridge|FullyQualifiedName~RunTerminalCommand|FullyQualifiedName~McpCenterTests|FullyQualifiedName~Parity"
```

### Integration Test

```bash
# 真实子进程用例包含在上面过滤集（ExecutorTests / RunTerminalCommandChainExecutionTests），无独立集成套件
```

### E2E（冒烟 1 条，铁律 24）

```bash
cd ForgeSelf.Web && bash node_modules/.bin/playwright test e2e/plugins/mcp-center --grep 冒烟
# 环境前置：NO_PROXY=localhost,127.0.0.1 / TEMP=<repo>\.temp\tmp（§5.0）
```

### Other Checks

- 快档说明：触 `Plugins/**` 不触宿主源码/scripts/共享夹具 → 按 §5.6 跑快档；不触前端与 UI → `pnpm run check/test`、`design:check` N/A。
- 走查（铁律 25，无 UI 形态）：dev-stack 起源码本地前后端 → mcp-center 测试台真实发送链式命令与破坏链命令各 1 条 → 截图存 `ForgeSelf.Web/screenshots/dev/mcp-center/` → 清测试数据。
- 发布：本批不含；待用户拍板（本地目录更新源走 release-local，另行征得同意）。

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| （实现期填写） | | | |

### 既有断言更新清单（旧策略 → 新语义，规格内动作）

| 文件 | 旧断言 | 新断言 |
| --- | --- | --- |
| TerminalCommandGuardTests `Check_ChainedOrPiped_Rejected` | `git status \| head` 拒（chained） | 拒但原因 = `allowlist`（head 不在白名单，段级） |
| 同上 | `git status; rm -rf /` 拒（chained） | 拒但原因 = `destructive`（段 2 破坏性） |
| 同上 | `dotnet build && pwsh -c x` 拒 | **放行**（两段均过门） |
| 同上 | `dotnet build\ngit push` 拒 | **放行**（换行=分隔） |
| GuardTests `Check_DestructiveOrChainedInjection_Rejected` 的 `git status; del *` 期望 `chained` | chained | `destructive` |
| Parity 金样表第 2 道门 4 条 | 全拒 | 按新语义重标（`git status && rm -rf x` 拒-destructive；`git status\ngit log` 放行；`echo a > out.txt` 仍拒；`dotnet build; dotnet test` 放行） |
| PromptBuilderTests `text.Should().Contain("管道")` | 旧句 | 新句（仍含「管道」字样，语义反转） |
