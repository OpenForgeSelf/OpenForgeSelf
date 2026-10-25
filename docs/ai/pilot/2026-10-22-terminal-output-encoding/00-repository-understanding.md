# 00-repository-understanding（终端子进程输出编码乱码修复批，2026-10-22，PILOT-073）

## 涉及模块与文件（已读码确认）

- `Plugins/AIAgent/Services/ToolFunctions/RunTerminalCommandTool.cs` —— 报告实例。`:139-140` 固定 `StandardOutputEncoding=UTF8` 解码子进程管道，`:162-163` `StandardOutput.ReadToEndAsync()`。**缺陷面**：子进程（pwsh 等控制台程序）继承宿主 GBK(936) 控制台码页时按 GBK 写字节 → 父端按 UTF-8 解码 → U+FFFD（信息不可逆丢失）。
- 同类扫查（全仓 `RedirectStandardOutput = true`，生产代码 11 处，2026-10-22 grep 实测）：
  | # | 位置 | 现状解码 | 读取形态 | 归类 |
  |---|------|---------|---------|------|
  | 1 | AIAgent `RunTerminalCommandTool.cs:137` | 固定 UTF8 | ReadToEndAsync | **同根因·本批修** |
  | 2 | ToolBridge `Services/CommandExecutor.cs:47` | 固定 UTF8 | ReadToEndAsync | **同根因·本批修** |
  | 3 | AgentHub `Services/ProcessRunner.cs:78` | 固定 UTF8 | ReadToEndAsync(ct) | **同根因·本批修** |
  | 4 | AgentHub `Services/CliTransport.cs:76` | 固定 UTF8 | stderr ReadToEndAsync | **同根因·本批修** |
  | 5 | Scheduler `Services/TaskExecutor.cs:136` | 未设（=跟随控制台码页） | 同步 ReadToEnd | **同根因·本批修** |
  | 6 | ImGateway `Services/WeComScanAuthService.cs:120` | 固定 UTF8 | 待实现期核对 | **同根因·本批修** |
  | 7 | 宿主 `ForgeSelf.Api/Services/ServiceManager.cs:266` | 未设 | 待核对 | **同根因·本批修** |
  | 8 | 宿主 `ForgeSelf.Api/Services/RuntimeDetector.cs:166` | 未设 | 待核对 | **同根因·本批修** |
  | 9 | McpCenter `Services/McpClient/StdioMcpTransport.cs:98` | 固定 UTF8 | LSP 帧协议流 | **异根因**（MCP 协议规定 UTF-8，改回退解码会污染协议面；PC-010 域） |
  | 10 | ScriptRunner `Services/ScriptExecutor.cs:219` | 未设 | OutputDataReceived 事件流 | **同根因·异形态**（事件流无字节层入口，改造=重构流式读取 → TODO 带 PC-011 id） |
  | 11 | Sems `Services/RunnerService.cs:217` | 未设 | 只取退出码不消费文本 | **非命中**（不解码即不乱码） |
- `ForgeSelf.Core` —— 新助手落点候选：插件 csproj 一律只引用 Core+Abstractions（plugin-development 铁律 12b 实测 19/19），Core 零 PackageReference，纯 BCL + kernel32 P/Invoke 可放入。
- `ForgeSelf.Api.Tests/Plugins/TerminalCommandGuardTests.cs` / `UniversalToolTests.cs` —— 既有 AIAgent 工具测试范式（Moq 假 IContext，零副作用）；本批新增**进程实跑**集成测试（EncodedCommand 绕过守卫的 `;` 拒绝，守卫对 -EncodedCommand 有 base64 解码扫描先例）。
- 全仓无任何 `System.Text.Encoding.CodePages` 包引用（grep 实测 0 命中）⇒ GBK 解码不可用 `Encoding.GetEncoding(936)`，须走 `MultiByteToWideChar(GetACP())` P/Invoke。

## 现场证据（Verified，2026-10-22）

- 经 fs（:18890，MCP over HTTP）`universal_tool → run_terminal_command` 执行 `pwsh -NoProfile -Command "Get-Content -LiteralPath TODO.md,… -Raw"`：exitCode 0，stdout 中文全量 U+FFFD；
- 同文件同网关走 `read_file`：中文完好 ⇒ 文件未损，断点锁定终端输出解码层；
- fs 登记根 = `D:\src\os-proj\X\DesignSystem`（usedRoot 字段实证），该仓 TODO.md 已自记 TASK-20261022-ENCODING-01「PowerShell 中文显示异常，Node 显式 UTF-8 回读正常」——与本批同一事实链。

## 适用问题类（Stage 0 必查，tags 检索表）

- **PC-006**（PowerShell 5.1 编码/BOM/码页）：相邻区域 lens——同为码页错配，但那是脚本文件/日志侧，本批是**子进程管道解码侧**，判**新类**；
- **PC-010**（stdio 拉起外部进程）：命中「stdio 拉起外部进程」区域；本批不动其协议面，StdioMcpTransport 列异根因；
- **新类 PC-011（本批建档）**：终端型子进程输出编码与解码端错配（固定码页解码必坏其一）→ canonical fix = 字节捕获 + UTF-8 严格优先 + 系统 ANSI 回退。

## 构建/验证入口（复核自仓库现状）

- 后端 `dotnet build`（ForgeSelf.slnx）+ `dotnet test ForgeSelf.Api.Tests --filter …`；`ForgeSelf.Core.Tests` 独立工程存在；
- 本地起前后端唯一入口 `pwsh scripts/dev-stack.ps1`；插件维护闭环六步（plugin-development §四）。

## 既有约束

- 禁止 agent 停/启/杀用户运行中宿主（含 :18890 fs 实例）；发布默认本地目录更新源 + `-Sign`，宿主升级由 update-agent 自更新；
- 闸门1 批准后才许 Implement；缺陷类按 problem-class-closure 出口清单收口（红灯→扫查→守卫→case/PC 归档→07 三节）。
