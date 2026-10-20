---
id: PC-010
title: stdio 外部进程启动与消息帧协商脱节：Windows 批处理 shim（.cmd/.bat）不走 CreateProcess、单一帧型连不上官方 SDK 生态、字面 null params 致服务器沉默
tags: [mcp, stdio, process-launch, framing, windows-shim]
invariant: 用用户配置的 command 启动外部 stdio 进程必须按操作系统解析语义探测实际形态（Windows PATHEXT：exe>cmd>bat，批处理 shim 经 ComSpec /d /s /c 包装拉起）；stdio 消息帧型必须自适应对方实况（NDJSON/LSP 双帧读侧嗅探 + auto 协商，不得只按规范 SHOULD 形态单说一种）；JSON-RPC 可选字段（params/arguments）不携带时省略键本身，禁止发字面 null
status: guarded
first_seen: 2026-10-18
recurrences: []
cases: [2026-10-18-mcp-stdio-npx-windows-shim]
signature:
  - {id: S1, kind: grep, glob: "Plugins/McpCenter/Services/McpClient/*.cs", regex: "@params\\s*=\\s*prms", expect: no-match}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStdioWindowsShimTests.cs}
  - {id: S3, kind: test, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStdioFramingTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStdioWindowsShimTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStdioFramingTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientProtocolGuardTests.cs, status: active}
  - {level: G3, path: scripts/check-problem-classes.ps1, status: active}
lens:
  - 用用户配置的 command 拉起外部进程时，是否在**目标操作系统**核过解析语义（Windows 下 npx/npm/uvx/poetry 等入口是 .cmd 批处理 shim，UseShellExecute=false 的 CreateProcess 不按 PATHEXT 解析）？
  - stdio 协议客户端的帧型是对着**对方真实实现**（官方 SDK 生态实况）定的，还是只照规范 SHOULD 形态写死一种？项目自带 mock 的帧型与官方 SDK 一致吗（宽松/同构靶子掩盖）？
  - JSON-RPC 可选字段序列化时省略键还是发字面 null？严格服务器（FastMCP 系）收到 null 的行为核实过吗（沉默 ≠ 慢）？
  - 同一逻辑散布在多条传输/多个文件时，是否收敛为 canonical 单点并做同类扫查（发现一处必查三处）？
---
# PC-010 stdio 外部进程启动与消息帧协商脱节

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

用户可配置的「启动外部进程 + stdio 协议通信」链路，兼容性由三层事实共同决定，任何一层按"我这边能跑"写死都会断：

1. **进程启动层**：Windows 的 `npx`/`npm`/`uvx` 等命令实体是 `npx.cmd` 批处理 shim；.NET `Process` 在 `UseShellExecute=false` 下走 CreateProcess，**不按 PATHEXT 解析批处理** → `ERROR_FILE_NOT_FOUND`（报「系统找不到指定的文件」）。Linux/macOS 的 execvp 走 PATH+shebang 自洽，所以该缺陷是 Windows-only，跨平台开发环境最容易漏。
2. **帧协商层**：MCP stdio 规范 SHOULD 形态是 LSP 式 `Content-Length` 帧，但**官方 TS/Python SDK 实况只说 NDJSON**（换行分隔 JSON），且对 Content-Length 请求**完全沉默**（探针实测 got-reply=False，不报错不应答）——单帧型客户端连不上生态多数实现。
3. **载荷序列化层**：JSON-RPC 的 `params` 可选；发 `"params":null` 时 FastMCP 系服务器完全沉默（A/B 探针坐实：`params:{}` 应答、`params:null` 沉默），客户端只能干等超时。

认知缺口（为什么 Agent/人会犯）：
- **同构靶子诱导**：项目自带 mock MCP 服务器与客户端帧型一致（LSP）、且宽松（不挑 null params），单测/e2e 全绿给出虚假安全感——与 PC-008 同一课，这次连"换真实服务器"都不够，还要换**帧型与序列化细节**都真实的服务器（npx 官方包）；
- **规范形态 ≠ 生态实况**：只读 spec 的 SHOULD 段落，未对照官方 SDK 源码/行为；
- **旧 e2e 宽松断言掩盖**：应用层旧用例给「stdio npx 连不上」写了优雅降级分支，三层全灭仍能绿（宽松断言掩盖教训第三次命中，前两次见 PC-008）。

## 2 特征签名与扫描方法

- **S1（grep，G3）**：三传输源文件出现手写请求载荷 `@params = prms`（null 会被序列化成字面量 null）即违规——请求序列化唯一合法入口是 `McpClientProtocol.SerializeRequest`；由 `scripts/check-problem-classes.ps1`（pre-commit 挂接）执行。
- **S2（test）**：Windows shim 红灯锚——真实 `npx @modelcontextprotocol/server-everything` 集成测 + `ResolveLaunch` 纯函数单测（注入 pathDirs/isWindows/fileExists 跨平台确定性，含包装器参数逐字形状断言）。
- **S3（test）**：帧协商矩阵——auto 首发 NDJSON 命中（<探测超时）、auto 对 LSP-only 服务器回退重发、explicit ndjson/lsp 强制零探测、`ParseFraming` 非法值回落 auto。
- 已知盲区：进程启动层无法在 Linux CI 上真跑 Windows 分支（集成测 `OperatingSystem.IsWindows()` 门 + CI skip），由 ResolveLaunch 注入式单测兜形状；服务器"沉默"类缺陷无法靠超时区分"慢"与"死"，探测预算（FramingProbeTimeoutMs=5000）是经验值——lens 第 3 问在 Spec 阶段兜住新接入实现时的 A/B 探针义务。

## 3 Canonical fix

**三层各一个单点**，参考实现：

- 启动层 `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs` `ResolveLaunch`：PATH 目录逐个数 `exe > cmd > bat`（PATHEXT 优先级）；命中 shim → `(ComSpec, /d /s /c "<带引号的命令+参数>")`（`/s` 保首尾引号、含空格 token 内层加引号）；命中 exe / 非 Windows → 直启原行为；全不见 → 可行动报错（指名 PATH / env.PATH 覆盖 / 「快捷方式启动不继承终端 PATH」）；
- 帧层 同文件 `ReadMessageAsync`：行首 `{` → NDJSON 整行；否则 LSP 头 + 空行 + 定长 UTF-8 字节体；首命中锁定 `_observedFraming`，写帧跟随（`_configuredFraming ?? _observedFraming ?? Ndjson`）；auto 下 NDJSON 首发、5s 无响应 catch TimeoutException 换 LSP 帧**重发同 id 一次**（`_pending.TryRemove` 天然去重）；配置字段 `Framing`（""/ndjson/lsp）贯穿 config→Dto→controller merge→store 透传（**注意 store 逐字段拷贝会吞新字段**）；
- 序列化层 `Plugins/McpCenter/Services/McpClient/McpClientProtocol.cs` `SerializeRequest`：prms 为 null 时**省略 params 键**；`tools/call` 的 `arguments` 空给 `{}` 同族封堵；三传输一律引用（S1 封死回退）。

被否方案：① 只把 `FileName` 改成 `npx.cmd` 硬编码——治标且漏掉 npm/uvx/poetry 同族与用户自定义命令；② 默认帧型改 LSP 首发——官方 SDK 系（生态多数）要多等一次 5s 探测，NDJSON 首发对官方实现零成本；③ 前端加"帧型"下拉强制项——UI 暂不加（auto 覆盖 99% 场景），config 字段已预留，出现 auto 判不了的现场再加。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStdioWindowsShimTests.cs` | dotnet test | npx 真实集成（Windows-only）+ ResolveLaunch 6 组纯函数（报错文案/包装形状/PATH 顺序/env.PATH 覆盖） |
| G2 | `.../McpStdioFramingTests.cs` | dotnet test | auto 命中/回退、explicit 强制、ParseFraming 回落矩阵（mock 双帧型 stdio-ndjson/stdio） |
| G2 | `.../McpClientProtocolGuardTests.cs`（PC-010 段） | dotnet test | SerializeRequest null 省略键直测 + 三传输源码扫描 |
| G2 | `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`「stdio npx 真实世界」 | 插件 e2e | 零容忍 connected（禁降级分支）+ /test success + UI 截图取证 |
| G3 | `scripts/check-problem-classes.ps1` S1（pre-commit） | git commit | 手写 `@params = prms` 载荷回退 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| [2026-10-18-mcp-stdio-npx-windows-shim](../cases/2026-10-18-mcp-stdio-npx-windows-shim.md) | 2026-10-18 | 用户配 `npx @playwright/mcp@latest` 报 400「系统找不到指定的文件」；修复启动层后又先后暴露帧型沉默与 null params 沉默 | N/A 首次（本类建类案；PC-008 是同插件异根因） |
