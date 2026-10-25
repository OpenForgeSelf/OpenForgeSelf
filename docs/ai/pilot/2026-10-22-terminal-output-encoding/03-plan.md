# Plan（PILOT-073 · 2026-10-22）

## Implementation Steps（顺序即执行序；先红后绿）

- **S1 红灯**（修复前必实测红）
  - 新建 `ForgeSelf.Api.Tests/Plugins/RunTerminalCommandEncodingTests.cs`
    reason: 报告实例的确定性复现——子进程强制写 GBK 字节（`pwsh -NoProfile -EncodedCommand <UTF-16LE base64>`，载荷 = `[Console]::OutputEncoding=[Text.Encoding]::GetEncoding(936); '中文编码测试✓'`），经 `RunTerminalCommandTool.ExecuteAsync`（Moq 假 IContext + IProjectWorkspaceService 指测试目录）断言 stdout 含原文且不含 U+FFFD；阳性对照一条 UTF-8 输出用例（防「永远 ANSI 解码」蒙混过关）。
- **S2 canonical 助手**
  - 新建 `ForgeSelf.Core/ProcessOutputText.cs`
    reason: 全仓唯一解码真源；纯 BCL + `GetACP`/`MultiByteToWideChar` P/Invoke（不新增包，见 00 的 CodePages 0 命中实测）；含 BOM 剥离、严格 UTF-8 → ANSI 回退、宽容兜底三级。
  - 新建 `ForgeSelf.Core.Tests/ProcessOutputTextTests.cs`
    reason: 五类入参（UTF-8/GBK/BOM/空/坏点）纯单测，GBK 期望字节用 `WideCharToMultiByte(ACP)` 现场生成，不依赖手写十六进制。
- **S3 站点迁移（8 处，同一 canonical fix）**
  - `Plugins/AIAgent/Services/ToolFunctions/RunTerminalCommandTool.cs`：`:139-140` 删固定编码；`:162-163` 改 `BaseStream` 字节捕获（保留双管道并发读 + 超时提前返回语义）；解码后走原 Truncate。
  - `Plugins/ToolBridge/Services/CommandExecutor.cs`：同形改造。
  - `Plugins/AgentHub/Services/ProcessRunner.cs` / `CliTransport.cs`：同形改造（保留 CancellationToken 语义，用 `Stream.ReadAllBytesAsync(ct)`）。
  - `Plugins/Scheduler/Services/TaskExecutor.cs`：同步 `ReadToEnd` → `BaseStream.ReadAllBytes()` + Decode。
  - `Plugins/ImGateway/Services/WeComScanAuthService.cs`、`ForgeSelf.Api/Services/ServiceManager.cs`、`ForgeSelf.Api/Services/RuntimeDetector.cs`：实现期逐文件核对形态后同形改造（02 Unknown-2）。
- **S4 守卫**
  - 新建 `ForgeSelf.Api.Tests/Architecture/ProcessOutputEncodingGuardTests.cs`
    reason: G2 类级枚举（FR-4），allowlist = `StdioMcpTransport.cs`（协议流，MCP 规范强制 UTF-8）/ `ScriptExecutor.cs`（事件流形态，TODO 逐字「PC-011」+expires）/ `RunnerService.cs`（不消费文本）；MUTATION 探针：临时摘一处站点引用 → 守卫红 → 复原。
- **S5 归档与文档**
  - `docs/11-troubleshooting/problem-classes/PC-011-*.md` + `cases/` case（脚手架 `new-problem-case.ps1`）；两处 README 索引 + `index.md` 速查行；
  - `TODO.md`：ScriptRunner 事件流迁移项（P2，逐字含 PC-011，expires 2026-12-31）；
  - 插件 `plugin.json` 版本 bump ×5 + 功能文档相应节（031 命令执行工具文档如存在则同步「输出解码策略」一句，真源指针入 case）。

## Test Plan（守卫覆盖分层，problem-class-closure P1/P4）

| 层 | 命令 | 判定 |
|---|---|---|
| P1 红灯 | `dotnet test ForgeSelf.Api.Tests --filter RunTerminalCommandEncodingTests`（S2 前） | GBK 用例 FAIL（U+FFFD 实证），S2/S3 后 PASS |
| Core 单测 | `dotnet test ForgeSelf.Core.Tests` | 全绿 |
| 门禁 | `dotnet build ForgeSelf.slnx` + `dotnet test ForgeSelf.Api.Tests --filter "AIAgent|ToolBridge|AgentHub|Scheduler|ImGateway|Process|Encoding|Guard"` | 0 error / 全绿（既有测试零回归） |
| G2 守卫 | `dotnet test --filter ProcessOutputEncodingGuardTests` + MUTATION 探针 | 绿 + 可红证据 |
| 问题类校验 | `pwsh scripts/check-problem-classes.ps1` | 绿 |
| 现场（③） | `pwsh scripts/dev-stack.ps1` → HTTP 调工具跑中文命令（GBK 子 + UTF-8 子各一） | JSON 中文完好，实据入 05 |
| 冒烟 e2e（②） | 既有 ai-agent 插件冒烟 e2e 复跑一条（不新增全流程） | 绿 |
| ⑤ 运行实例 | 用户升级后 fs 复跑 2026-10-22 原始命令 | 中文完好（用户拍板发布后） |

## Verification

- Build：`dotnet build`；Unit/Integration：上表过滤集；Other：dev-stack 现场 + 守卫 MUTATION；
- 回滚点：每步原子提交前记录 HEAD；助手为纯新增文件，站点改动可整体 revert 不回伤其他域。

## 风险与代价

- 中风险 = AgentHub ProcessRunner（委派主链路读输出）：行为等价改造 + 既有 AgentHub 测试过滤集兜底；
- 低风险 = 其余站点单点解码替换；
- 代价：8 站点 + 3 测试文件 + 1 助手 + 归档文档，约 15 文件；跨 5 插件 + 宿主 ⇒ 发布需宿主新版本（用户拍板）。

## 实施偏差（实现期实测，S1-S5 结构不变）

1. 原 S3「ProcessRunner/CliTransport 用 `Stream.ReadAllBytesAsync(ct)`」：本机 net10.0 引用集无该重载（CS1061 实测），改为助手内置 `ProcessOutputText.Capture/CaptureAsync`；且实测 **CliTransport 与 WeComScanAuthService 是逐行流式消费**（边读边 yield/回调），整段读取会破坏流式语义 ⇒ 增建 `ForgeSelf.Core/ProcessOutputLineBuffer.cs`（增量行解码器）收口这两处；ProcessRunner 实测为整段捕获，仍走 Capture。
2. 原 S4 allowlist 含 `RunnerService.cs（不消费文本）`：实测其不命中任何禁用 token，无需占位豁免；守卫与 PC-011 allowlist 均只登记 StdioMcpTransport（协议）/ ScriptExecutor（事件流挂账）两条。
3. ANSI 回退增加**往返校验**（探针实测 cp936 对任意字节几乎都能「成功解码」，无校验则 ansi-936 标签假确信、utf8-lossy 级成死代码）：见 `ProcessOutputText.RoundTripsToRaw`。
