---
id: 2026-10-22-process-output-fixed-codepage-decode
title: 子进程输出固定码页解码：按系统ANSI(GBK)写出的中文烧成U+FFFD，协议性UTF-8又被宿主码页毁掉
pc: PC-011
severity: P1
evidence: Verified
---
# Case：2026-10-22 子进程输出固定码页解码

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC-011，不复述。

## 1 现象

用户经外部 agent 走 fs 工具集 MCP（HTTP `127.0.0.1:18890/mcp`）调 `run_terminal_command` 执行 `pwsh -NoProfile -Command "Get-Content -LiteralPath TODO.md -Raw"`：exitCode 0，但返回 stdout 里所有中文均为 U+FFFD（`????`式乱码）；同一文件经同插件 `read_file` 读取中文完好 ⇒ 文件未损，断点锁定在 run_terminal_command 子进程输出解码链路（会话记录：`.forgeself/memory/2026-10-22.md` 输入2；任务 PILOT-073）。

## 2 复现红灯

`dotnet test ForgeSelf.Api.Tests --filter RunTerminalCommandEncodingTests`（修复前，S2 canonical 助手落地前）：
- GBK 用例 **FAIL**：`Expected stdout "????" to contain "中文编码测试"`（载荷经 `pwsh -EncodedCommand` 直写 GBK 字节 `d6 d0 ce c4 b1 e0 c2 eb b2 e2 ca d4`，坐定"固定 UTF-8 解码毁 ANSI 侧"）；
- UTF-8 对照用例 PASS（防"永远 ANSI 解码"蒙混过关）。
- 首跑曾为**无效红**（WorkingDirectory 指向不存在目录，进程起不来 success=false 两例皆红）——修正为真实临时目录后才得到"GBK 红 + UTF-8 绿"的有效红。红灯原始输出未单独落盘（教训见 §9），修复后同 filter 2/2 PASS 留存于会话记录与 05-evidence。

## 3 根因（5 Whys + 第 6 问）

1. 为什么中文乱码？→ `RunTerminalCommandTool` 读 stdout 得到 U+FFFD 替换符。
2. 为什么是替换符？→ psi 设了 `StandardOutputEncoding = Encoding.UTF8`，pwsh 子进程继承控制台 GBK 码页写出的字节按严格 UTF-8 判非法，fallback 烧字符。
3. 为什么会设固定 UTF-8？→ 写站点时手上只有 dotnet/node 这类 UTF-8 子进程，"设了立刻验证通过"；API 存在本身暗示设了就对（PC-011 §1 认知缺口）。
4. 为什么不设就没事？→ 不行：.NET 默认跟随宿主码页，dotnet/pnpm 的协议 UTF-8 在非 936 宿主又被毁——**固定哪侧都坏另一侧**（不变量破坏点：把"多码页字节流"当"单一已知码页"解码）。
5. 为什么 8 个站点都这样？→ 同一范式被复制粘贴扩散（CommandExecutor/ProcessRunner/CliTransport/TaskExecutor/WeComScanAuthService/ServiceManager/RuntimeDetector 同根因）。
6. **第 6 问：为什么现有流程/守卫没让我一开始就写对？** 全仓无任何“子进程输出解码”规范条文/守卫（找不到索引）；U+FFFD 不抛异常不红既有测试（无守卫强制）；PC 台账 tags 表没有 encoding×child-process 这一行（索引缺失）。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| `Plugins/AIAgent/Services/ToolFunctions/RunTerminalCommandTool.cs` | 是（报告实例） | 已修（字节捕获+ProcessOutputText） |
| `Plugins/ToolBridge/Services/CommandExecutor.cs` | 是 | 已修 |
| `Plugins/AgentHub/Services/ProcessRunner.cs` | 是 | 已修（SafeAwait→SafeAwaitBytes） |
| `Plugins/AgentHub/Services/CliTransport.cs` | 是（流式形态） | 已修（ProcessOutputLineBuffer） |
| `Plugins/Scheduler/Services/TaskExecutor.cs` | 是（未设编码=默认宿主码页形态） | 已修 |
| `Plugins/ImGateway/Services/WeComScanAuthService.cs` | 是（流式形态） | 已修（ProcessOutputLineBuffer） |
| `ForgeSelf.Api/Services/ServiceManager.cs` | 是 | 已修 |
| `ForgeSelf.Api/Services/RuntimeDetector.cs` | 是 | 已修 |
| `Plugins/ScriptRunner/Services/ScriptExecutor.cs` | 同根因·异形态（BeginOutputReadLine 事件流，回调入参已是被毁的解码行，无法内联修） | TODO「PC-011」expires 2026-12-31 + allowlist 登记 |
| `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs` | 否（协议流，MCP 规范强制 UTF-8，固定 UTF-8 是正确姿势） | allowlist 登记（expires 2027-12-31） |
| 其余起进程点（KillProcessTree/taskkill 等不消费文本输出） | 非命中 | 无需处置 |

## 5 修复与被否方案

按 PC-011 §3 canonical fix 执行：新建 `ForgeSelf.Core/ProcessOutputText.cs`（整段四级裁决）+ `ProcessOutputLineBuffer.cs`（流式逐行），8 站点统一迁移；被否方案四条见 PC §3。相对 03-plan 的偏差（回写于 05-evidence）：① `Stream.ReadAllBytesAsync(ct)` 在本机 net10.0 引用集不存在（CS1061 实测），助手内置 `Capture/CaptureAsync` 替代；② CliTransport/WeComScanAuthService 实测为逐行流式，"整段 ReadAllBytes"会破坏流式语义 → 增建 ProcessOutputLineBuffer 收口；③ ANSI 回退加往返校验（探针实测 cp936 对垃圾字节 `{0x81}` 也能"成功解码"，无校验则 ansi-936 标签假确信、utf8-lossy 成死代码）。

## 6 重构提案与结论

R1（本批已做）：8 站点解码逻辑收敛到 Core 两个单点，消灭复制粘贴扩散面。R2 提案：`ScriptExecutor` 事件流整体换字节流形态——挂账 TODO（expires 2026-12-31），不在本批扩范围。无 R3。

## 7 守卫落地

- G1 `ForgeSelf.Api.Tests/Plugins/RunTerminalCommandEncodingTests.cs`（active，红灯锚）；
- G1 `ForgeSelf.Core.Tests/ProcessOutputTextTests.cs`（active，裁决器六类入参）；
- G2 `ForgeSelf.Api.Tests/Architecture/ProcessOutputEncodingGuardTests.cs`（active，剥注释四类 token 源码扫描 + 阳性对照；MUTATION 探针实证可红：注入 `StandardOutputEncoding =` 到 RunTerminalCommandTool → FAIL（`.forgeself/tmp-guard-pc011-mutation.log`）→ 还原 → PASS（`tmp-guard-pc011-revert.log`））；
- G3 `scripts/check-problem-classes.ps1` S1/S2/S3（active，pre-commit）。
首见建类，无逃逸。

## 8 逃逸分析

N/A 首次（本类建类案）。

## 9 基建改进

- 全仓新增"子进程输出解码"唯一真源（此前无任何规范条文/守卫）；
- problem-classes 检索表新增 encoding×child-process 区域行，后续 Spec 触碰 tags 必答 lens 五问；
- 教训固化：红灯原始输出必须落盘（本次首红未单独存 log，靠会话记录背书）——05-evidence 起所有门禁命令输出落 `.forgeself/`。

## 10 复用赋能

凡新功能起子进程并消费 stdout/stderr（新插件工具、宿主服务、CLI 集成）：Spec 交互节必答 PC-011 lens 五问；插件评审时核对是否绕过 BaseStream 姿势。对应 lens 已入 PC-011 front-matter。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| 修复前 GBK 用例红、UTF-8 绿 | Verified | 会话 transcript `task-e6f.jsonl`（S1 红灯运行输出）；`.forgeself/memory/2026-10-22.md` 输入2/3 |
| 修复后编码测试 2/2 绿 | Verified | `dotnet test --filter RunTerminalCommandEncodingTests`（05-evidence 留档） |
| 裁决器单测 38/38 绿 | Verified | `dotnet test ForgeSelf.Core.Tests`（05-evidence） |
| G2 守卫绿且可红 | Verified | `.forgeself/tmp-guard-pc011-first.log`、`tmp-guard-pc011-mutation.log`、`tmp-guard-pc011-revert.log` |
| 全解决方案零回归 | Verified | `dotnet build ForgeSelf.slnx` 0 error（`.forgeself/tmp-build-pc011-sites8.log`）+ Api.Tests 过滤集 355/355 |
| 现场中文完好 | Verified | dev-stack 走查记录（05-evidence §现场） |
