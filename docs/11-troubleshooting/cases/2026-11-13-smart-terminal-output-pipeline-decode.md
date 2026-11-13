---
id: 2026-11-13-smart-terminal-output-pipeline-decode
title: 智能终端插件新写输出管道重蹈固定码页解码（PC-011 二次复发）
pc: PC-011
severity: P2
evidence: Verified
---
# Case：2026-11-13 智能终端输出管道固定码页解码（PC-011 二次复发）

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC-011，不复述。

## 1 现象

2026-11-13 门禁跑（输入6，09:55）`pwsh scripts/check-problem-classes.ps1` 命中 PC-011 签名 3 处，全部落在 smart-terminal 插件 2026-11-12 合入的终端会话管道代码（commit `fb48fe5`，2026-11-12 20:16 +0800「feat(smart-terminal): 后端对齐 v2 契约（commands / 凭据 / 长寿命壳）」最后触及两文件）：S1 ×1 `TerminalSessionService.cs`（`StandardOutputEncoding = Encoding.UTF8` 固定码页赋值）+ S2 ×2 `SessionShell.cs` / `TerminalSessionService.cs`（`BeginOutputReadLine` 事件流默认解码）。同款红并非首次被看见：2026-11-05 全量基线对账已在 TODO 挂账（SessionShell.cs 四处命中，「待指派修复会话」），挂账 8 天无 owner 无到期未修。这是 PC-011 的**二次复发**（首见 2026-10-22 建类案）——守卫在，红也被拦住了，逃的是「红→修复」回流。

## 2 复现红灯

- **G3 签名红灯**：`pwsh scripts/check-problem-classes.ps1`（`gate-logs/pc.log`，2026-11-13 09:55：L37-39 S1、L76-78 S2×2）——`Plugins/**/*.cs` 面上 3 处命中，见 §4。
- **G2 守卫红灯（历史）**：`ProcessOutputEncodingGuardTests` 在 2026-11-05 基线对账已红并挂账 TODO；2026-11-13 全量 `dotnet test`（`gate-logs/be-test-full.log`，3774/3746/28 红）未复现该红——因修复（§5）先于全量落地，属时序而非守卫失效。
- **修复后绿灯**：`gate-logs/pc-check2.log` PC-011 行「完成」（S1/S2/S3 零命中，仅剩存量 allowlist 与在途批归因项）。
- **归档终局绿灯**：pc-check3（case + PC 台账更新后重跑，结果追记 §11）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么插件里出现固定码页解码？→ smart-terminal 新写终端会话管道直接采用了 .NET 常见范式（psi 设 UTF-8 / `BeginOutputReadLine`）。
2. 为什么会用这个范式？→ 写码参照通用 .NET 资料与直觉；PC-011 的 canonical 参考实现（`RunTerminalCommandTool`/`CliTransport`）与 lens 五问不在其阅读面，没有任何检查点强制新代码先过 PC-011。
3. 为什么 11-05 守卫红之后 8 天没有修？→ 红灯只进 TODO 挂账（「待 lead 指派修复会话」），无 owner、无到期、无强制回流——红→修复回路断裂。
4. 为什么插件合入批没被拦在合入前？→ 插件批验证面 = 插件前端 build + 过滤集测试 + 冒烟 e2e；G2/G3 宿主架构守卫不在插件合入门禁里。PC-011 这类「守卫住在宿主测试/仓库脚本」的问题类对插件改动静默，直到基线对账/门禁跑才暴露（PC-020 本批同例）。
5. 为什么直到本次才确认为二次复发？→ 11-05 sighting 未归档（无 case、PC `recurrences` 未登记），台账复发计数停在 0，趋势不可见。
6. **第 6 问：为什么流程没让新代码一开始就写对？** 三个缺口：① **事前指引缺位**——插件开发技能链没有「新写子进程输出消费 → 先答 PC-011 lens」的检查点；② **挂账红灯无 owner/无到期**——TODO 条目可无限期漂移；③ **合入门禁缺宿主守卫过滤集**——插件批不跑任何宿主架构守卫。

## 4 同类扫查命中表

| 位置（修复前，文件:行） | 签名 | 同根因？ | 处置 |
|--------------------------|------|----------|------|
| `Plugins/SmartTerminal/Services/TerminalSessionService.cs:773-774` | S1 | 是（本批新写） | 已修（删固定编码赋值，BaseStream 原始字节 → `ProcessOutputLineBuffer` 逐行裁决） |
| `Plugins/SmartTerminal/Services/TerminalSessionService.cs:807-808` | S2 | 是（本批新写） | 已修（`PumpDecodedLinesAsync` 接管） |
| `Plugins/SmartTerminal/Services/SessionShell.cs:34-35` | S2 | 是（本批新写） | 已修（`Pump` + `_stdoutLines`/`_stderrLines` 缓冲） |
| `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs`、`Plugins/ScriptRunner/Services/ScriptExecutor.cs` | — | 否 / 同根因异形态（存量） | 维持 allowlist 与 TODO 挂账不变（PC-011 front-matter） |
| `ForgeSelf.Api/**/*.cs` | S3 | 无命中 | 无需处置 |
| 并行在途批 8 个 untracked 文件（tool_choice / openai-responsive） | — | 异根因（PC-033，所有权在途） | 仅记录所有权，不属本批修复面 |

## 5 修复与被否方案

按 PC-011 §3 canonical fix 迁移（本批 C 批）：`SessionShell.cs` 删事件流，建 `_stdoutLines`/`_stderrLines`（`ProcessOutputLineBuffer`）+ `Pump(Stream, ProcessOutputLineBuffer, StringBuilder)` 读 BaseStream 原始字节逐行裁决；`TerminalSessionService.cs` 删固定编码赋值与事件流，`PumpDecodedLinesAsync` 同姿势接管，`LogNonUtf8Decoding`（XTrace 留痕）承担「EncodingUsed 非 UTF-8 时留日志」职责（lens 第 5 问）。helper（`ProcessOutputText`/`ProcessOutputLineBuffer`）为 PILOT-073 既有单点，本批零新增基础设施。被否方案与决策见 PC-011 §3，本批无新增偏离。

## 6 重构提案与结论

无新增提案。R2（`ScriptExecutor` 事件流整体换字节流形态）维持挂账 TODO（expires 2026-12-31）。二次复发按规则核对守卫等级：G2+G3 自首见建类即 active，满足「二次复发强制 ≥G2」，无需升级。

## 7 守卫落地

全部维持 active、本批零改动：G1 `RunTerminalCommandEncodingTests`、G1 `ProcessOutputTextTests`、G2 `ProcessOutputEncodingGuardTests`、G3 `check-problem-classes.ps1` S1-S3。守卫未逃逸——G3 门禁跑（11-13）与 G2 基线对账（11-05）都拦住了；逃的是红→修复回流与合入覆盖面（§8）。

## 8 逃逸分析（二次复发，强制）

守卫面未逃逸（两道都红），流程面三缺口，按 05-escape-analysis 失败模式归因：

| 缺口 | 失败模式 | 基建动作 |
|------|----------|----------|
| 事前指引缺位（新写代码无检查点） | 指引未触达写码现场 | plugin-development 技能回流挂账 TODO（P3）：插件任务预检指向 problem-classes tags 表 PC-011/PC-020 行 |
| 挂账红灯无 owner/无到期（漂 8 天） | 发现后无闭环 | 按 problem-class-closure 铁律「教训不得悬空 → 落成 TODO id 带 expires」执行；本批把该挂账转为修复并勾销（§9） |
| 插件合入门禁缺宿主守卫过滤集 门禁面 ≠ 改动面 | 新增 TODO（P2，来源输入6）：插件合入前跑宿主架构守卫过滤集 = `ProcessOutputEncodingGuardTests` + `ToolNamespaceGuardTests` + `PluginLoadDependencyGuardTests` |

## 9 基建改进

- 2026-11-05 挂账条目逐条落成：S1/S2（PC-011）→ 本批修复；PluginLoadDependencyGuard（PC-020，同批暴露的姊妹面）→ 本批 A1b 修复（隔离复跑 6/6 绿）；`ToolNamespaceGuardTests` 红仍开放 → 归 smart-terminal 批。
- 新增 TODO：插件合入门禁宿主守卫过滤集（P2）。
- 新增 TODO：plugin-development 技能回流（P3）。
- PC 台账：`recurrences` 登记本案（复发计数 0→1），status 保持 `guarded`。

## 10 复用赋能

- 凡插件新写子进程输出消费（长寿命 shell、CLI 封装、进程探活）：先答 PC-011 lens 五问（front-matter 已有）再落码。
- 本案例补充复发视角：发现同款红先查 PC `recurrences`，再定「首见建类 / 复发走逃逸分析」处置，不要当首见处理。
- 台账检索行已存在（problem-classes README：起子进程消费 stdout/stderr / 中文乱码 U+FFFD → PC-011），插件 Spec 触碰该区域必查。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出） |
|------|------|----------------------------|
| 修复前 G3 命中 3 处 | Verified | `gate-logs/pc.log` L37-39（S1）、L76-78（S2×2），2026-11-13 09:55 |
| 2026-11-05 已见同款红并挂账 | Verified | `TODO.md` SmartTerminal PC-011 挂账条目（202-11-05 记；行号随队列漂移，按内容定位） |
| 合入 commit 归属 | Verified | git log：`fb48fe5` 2026-11-12 20:16 +0800 最后触及两文件 |
| 修复后签名零命中 | Verified | `gate-logs/pc-check2.log` PC-011 行「完成」 |
| 修复姿势实码 | Verified | `SessionShell.cs` L17-18/36-37/43；`TerminalSessionService.cs` L795-798/909/927（grep 实测） |
| 全量28 红不含本类守卫 | Verified | `gate-logs/be-test-full.log`（3774/3746/28；修复先于全量落地） |
| 归档终局门禁 | Verified | `gate-logs/pc-check3.log`（见下追记） |

> 追记（2026-11-13 18:14）：pc-check3 重跑（gate-logs/pc-check3.log）：33 类扫描全部「完成」，PC-011 status=guarded——二次复发归档闭环通过（recurrences/cases/§5 行/台账/双索引均已落）。唯一 FAIL = PC-033 S3 未知 kind 'source'，系 tool_choice/openai-responsive 在途批 8 个未跟踪文件的归属问题（记录不修，该批收口时自清），与本类无关。
