---
id: PC-011
title: 子进程输出固定码页解码：按系统ANSI(GBK)写出的中文烧成U+FFFD，协议性UTF-8又被宿主码页毁掉
tags: [process-output,encoding,gbk,utf8,child-process]
invariant: 子进程 stdout/stderr 是多码页字节流（sc.exe/原生 CLI 按系统 ANSI 写、dotnet/pnpm/node 按协议写 UTF-8），任何固定码页解码必坏一侧——必须读 BaseStream 原始字节，交唯一裁决器（ProcessOutputText 整段 / ProcessOutputLineBuffer 流式）按「剥BOM→严格UTF-8→ANSI回退+往返校验→宽容兜底」还原
status: guarded
first_seen: 2026-10-22
recurrences: []
cases: [2026-10-22-process-output-fixed-codepage-decode]
signature:
  - {id: S1, kind: grep, glob: "Plugins/**/*.cs", regex: "Standard(?:Output|Error)Encoding\s*=\s*Encoding\.", expect: no-match}
  - {id: S2, kind: grep, glob: "Plugins/**/*.cs", regex: "Begin(?:Output|Error)ReadLine\(", expect: no-match}
  - {id: S3, kind: grep, glob: "ForgeSelf.Api/**/*.cs", regex: "Standard(?:Output|Error)Encoding\s*=\s*Encoding\.", expect: no-match}
  - {id: S4, kind: test, path: "ForgeSelf.Api.Tests/Architecture/ProcessOutputEncodingGuardTests.cs"}
  - {id: S5, kind: test, path: "ForgeSelf.Api.Tests/Plugins/RunTerminalCommandEncodingTests.cs"}
allowlist:
  - {path: "Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs", reason: "协议流：MCP 规范强制 JSON-RPC over stdio 用 UTF-8，固定 UTF-8 是正确姿势（PC-010 §3 同款裁决）", expires: "2027-12-31"}
  - {path: "Plugins/ScriptRunner/Services/ScriptExecutor.cs", reason: "事件流形态待迁移（BeginOutputReadLine 按宿主码页解码=本类变体B），已挂账 TODO.md「PC-011」，迁移收口后必须移出本表", expires: "2026-12-31"}
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/RunTerminalCommandEncodingTests.cs, status: active}
  - {level: G1, path: ForgeSelf.Core.Tests/ProcessOutputTextTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/Architecture/ProcessOutputEncodingGuardTests.cs, status: active}
  - {level: G3, path: scripts/check-problem-classes.ps1, status: active}
lens:
  - 起子进程读输出时，读的是 BaseStream 原始字节，还是设了 StandardOutputEncoding / 什么都没设（.NET 默认跟随宿主码页）/ 用了 BeginOutputReadLine 事件流？三者都是本类命中形态。
  - 流式逐行消费（边读边 yield/回调）时，是否用 ProcessOutputLineBuffer 处理跨读边界的多字节序列，而不是对半截字节流解码？
  - 确需固定 UTF-8 的协议流（JSON-RPC over stdio 等），有规范依据并已登记 PC allowlist 带 expires 吗？
  - ANSI 回退是否带往返校验（WideCharToMultiByte 编回必须逐字节等于原始输入）？cp936 对任意字节几乎都能解码，无往返校验的 ansi-936 标签是假确信。
  - 解码结果是否携带 EncodingUsed 并在非 UTF-8 生效时留日志（现场可回溯，不用重跑）？
---
# PC-011 子进程输出固定码页解码

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

Windows 中文环境下，子进程写 stdout/stderr 的码页**不由调用方决定**：`sc.exe`、原生 CLI、`cmd /c` 包装的批处理按**系统 ANSI(GBK/936)** 写本地化文本；`dotnet`、`pnpm`、`node`、各家 agent CLI 按**协议约定 UTF-8** 写 JSON。于是任何"固定码页解码"必坏一侧：

- `psi.StandardOutputEncoding = Encoding.UTF8`（本项目曾 8 处采用）→ 子进程按 GBK 写的中文逐字节按 UTF-8 判非法 → `DecoderFallback` 烧成 U+FFFD，`run_terminal_command` 返回 `"????"` 式乱码（用户 2026-10-22 实测报障实例）；
- 什么都不设（.NET 默认跟随宿主码页）或 `BeginOutputReadLine` 事件流（同走默认解码）→ dotnet/pnpm/node 的协议 UTF-8 在非 936 宿主或含多字节跨行时被毁——**"不设"不是中立，是换了坏的一侧**。

认知缺口（为什么 Agent/人会犯）：① `ProcessStartInfo.StandardOutputEncoding` 这个 API 的存在暗示"设了就对了"，官方文档不告诉你两侧都会坏；② 写代码时手上只有一个测试子进程（往往恰好是 UTF-8 的 dotnet/node），设 UTF-8 立刻"验证通过"；③ U+FFFD 不抛异常、不红测试，坏输出静默流入报告——与 PC-007「静默陷阱」同族的表现形式。

## 2 特征签名与扫描方法

- **S1/S3（grep，G3）**：生产源码（Plugins / ForgeSelf.Api）出现 `Standard(Output|Error)Encoding = Encoding.` 赋值即违规；由 `scripts/check-problem-classes.ps1`（pre-commit 挂接）执行，allowlist 两条各带理由与到期日。
- **S2（grep，G3）**：`Begin(Output|Error)ReadLine(` 事件流 = 默认码页解码变体B；现存唯一命中 ScriptExecutor.cs 已挂账 TODO「PC-011」expires 2026-12-31。
- **G2 守卫**（`ProcessOutputEncodingGuardTests`）比 S1-S3 更严：剥注释后扫全部生产目录（含 Core/Abstractions/Bootstrapper）四类 token，阳性对照防扫描失效；MUTATION 探针已实证可红（2026-10-22，注入违规→FAIL→还原→PASS，日志 `.forgeself/tmp-guard-pc011-mutation.log`）。
- 已知盲区：G3 签名不做注释剥离（pre-commit 求快），注释里写 `StandardOutputEncoding = Encoding.` 原文会假红——写迁移说明时避开赋值句式；测试工程不在扫描面（桩进程属测试基建）；动态 Emit/Reflection 拼出的赋值扫不到，由 G2 + code review 兜。

## 3 Canonical fix

**全仓唯一解码真源两个单点**（均在 `ForgeSelf.Core`，纯 BCL + kernel32 P/Invoke，不引 CodePages 包）：

- 整段捕获 `ForgeSelf.Core/ProcessOutputText.cs`：`Capture/CaptureAsync(Stream)` 取原始字节 → `Decode(byte[])` 四级裁决：剥 UTF-8 BOM → `UTF8Encoding(false,true)` 严格解码（`DecoderFallbackException` 判否）→ `MultiByteToWideChar(GetACP())` ANSI 回退 **+ `WideCharToMultiByte` 往返校验**（编回必须逐字节等于原始输入，防 cp936 "垃圾也能解"假确信）→ 宽容 UTF-8 兜底。返回 `Decoded(Text, EncodingUsed)`，标签 `utf8|utf8-bom|ansi-<cp>|utf8-lossy`。
- 流式逐行 `ForgeSelf.Core/ProcessOutputLineBuffer.cs`：按 0x0A 切行（GBK trail 字节 0x40–0xFE 不含 0x0A，切分安全）、行尾 CR 剥除、跨读边界多字节先入缓冲、逐行交上裁决器。
- 站点统一姿势：psi **删除**固定编码两行；`proc.StandardOutput.BaseStream` 进上述单点；保留各站点原有语义（双管道并发读、超时提前返回、Truncate、CancellationToken）；`EncodingUsed` 非 `utf8*` 时 `XTrace.Log.Info` 留痕。参考实现：`Plugins/AIAgent/Services/ToolFunctions/RunTerminalCommandTool.cs`（整段）、`Plugins/AgentHub/Services/CliTransport.cs`（流式）。

被否方案：① 全局 `Encoding.RegisterProvider(CodePagesEncodingProvider)` + `GetEncoding(936)`——全仓 0 依赖该包，新增运行时依赖只为一个解码点不值，P/Invoke `GetACP` 等价；② 只把固定 UTF-8 改成固定 GBK——同样坏另一侧（dotnet/pnpm 协议 UTF-8）；③ 设 `psi.StandardOutputEncoding = Console.OutputEncoding`——把宿主码页的不确定性转嫁给启动环境，e2e/服务/托盘拉起时三个样；④ ScriptExecutor 事件流内联修复——`OutputDataReceived` 回调给的已是解码后的行，信息已毁，只能整体换字节流形态（挂账迁移，见 TODO）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ForgeSelf.Api.Tests/Plugins/RunTerminalCommandEncodingTests.cs` | dotnet test | 报告实例红灯锚：`pwsh -EncodedCommand` 直写 GBK/UTF-8 原始字节，断言 stdout 含中文且无 U+FFFD（期望串 `\u` 转义固化，与测试环境码页无关） |
| G1 | `ForgeSelf.Core.Tests/ProcessOutputTextTests.cs` | dotnet test | 裁决器六类入参（UTF-8 纯/带BOM/纯ASCII/空null/系统ANSI/双坏垃圾），ANSI 期望字节由 `WideCharToMultiByte(ACP)` 现场生成 |
| G2 | `ForgeSelf.Api.Tests/Architecture/ProcessOutputEncodingGuardTests.cs` | dotnet test | 生产源码四类 token 扫描 + 剥注释 + allowlist 带理由 + 阳性对照；MUTATION 实证可红 |
| G3 | `scripts/check-problem-classes.ps1` S1/S2/S3（pre-commit） | git commit | 固定编码赋值与事件流两个正则的存量回退 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| [2026-10-22-process-output-fixed-codepage-decode](../cases/2026-10-22-process-output-fixed-codepage-decode.md) | 2026-10-22 | 外部 agent 经 fs 工具集 MCP 调 `run_terminal_command` 执行 pwsh 读中文文件，返回 stdout 全 `?`/U+FFFD；同类扫查命中 8 站点 | N/A 首次（本类建类案） |
