# Specification（PILOT-073 · 2026-10-22）

> 由 00 真实读码 + 01 Intent 推导。Task ID：PILOT-073

## Functional Requirements

- **FR-1 单点解码助手**：`ForgeSelf.Core` 新增 `ProcessOutputText`（纯 BCL + kernel32 P/Invoke，零新依赖）：
  - `Decode(byte[] raw) -> (string Text, string EncodingUsed)`：① 剥 UTF-8 BOM（若存在）；② `UTF8Encoding(throwOnInvalidBytes: true)` 严格解码；③ 失败 → `MultiByteToWideChar(GetACP(), …)` 按系统 ANSI 码页解码；④ P/Invoke 异常（非 Windows）→ 回退宽容 UTF-8（替换符）并如实返回 EncodingUsed 供日志。
  - `ReadAllDecoded(Stream)` 便捷入口（同步/异步各一），供各站点从 `StandardOutput.BaseStream` 捕获字节后调用。
- **FR-2 站点迁移（同根因同形态 8 处）**：`RunTerminalCommandTool`、ToolBridge `CommandExecutor`、AgentHub `ProcessRunner`/`CliTransport`、Scheduler `TaskExecutor`、ImGateway `WeComScanAuthService`、宿主 `ServiceManager`/`RuntimeDetector` —— 一律改为**读 BaseStream 字节 → ProcessOutputText.Decode**，删除各站点 `StandardOutputEncoding/StandardErrorEncoding = UTF8` 固定设置（保留读取超时/取消语义不变）。
- **FR-3 诊断**：解码 EncodingUsed ≠ utf-8 时，站点既有日志通道记一条 Info/Warn（如 `[AIAgentPlugin] run_terminal_command 输出按 {codepage} 回退解码`），不许静默。
- **FR-4 类级守卫（G2）**：`ForgeSelf.Api.Tests` 新增源码扫描测试，锁定不变量「生产代码起子进程消费文本输出，不得固定单一码页解码」；形态参考 `PluginToolConstructorDiGuardTests`/`ProblemClassEnumerationTests`（剥注释后扫 `RedirectStandardOutput = true`，命中文件必含 `ProcessOutputText` 或在带理由 allowlist）。

## Input / Output

- Input：子进程 stdout/stderr **原始字节**（不再经 StreamReader 编码层）；
- Output：与现契约**完全一致**的字符串（工具 JSON 封套字段名、截断规则 `TerminalCommandGuard.Truncate`、`exitCode/timedOut` 语义均不变）。

## Business Rules

- UTF-8 优先：合法 UTF-8 字节流**必须**按 UTF-8 解（node/git/MCP 子进程恒 UTF-8，不得被 ANSI 回退伤害——GBK 字节恰好构成合法 UTF-8 的概率对真实中文文本极低，且此时两种解码必有一种对，取协议主流）；
- 回退只作解码纠错，**不改命令守卫**：白名单/管道拒绝/CWD 越界三道门语义零变化。

## Boundary Conditions

- 空输出 → 空串（不解码）；纯 ASCII → 两种解码同结果；
- 超时路径：现状返回空串，维持不变（字节捕获不得阻塞超时的提前返回）；
- 输出含混合编码坏点（ANSI 回退后仍有非法字节）→ `MultiByteToWideChar` 用默认字符替换，如实返回；
- UTF-8 BOM 开头 → 剥 BOM 后按 UTF-8。

## Error Handling

- P/Invoke 失败（非 Windows/异常码页）→ 宽容 UTF-8 兜底，绝不抛断调用方（工具契约：失败也回传观察数据）；
- 站点不得吞 Decode 返回的 EncodingUsed（FR-3 日志义务）。

## Failure Modes（缺陷类必写，按 PC lens 逐条）

| PC.lens 问题 | 本任务如何满足 |
|---|---|
| PC-011 候选不变量：固定码页解码必坏其一（UTF8 固定坏 GBK 子；跟随码页坏 UTF-8 子） | FR-1 双相解码消除两坏；FR-2 全量同形态站点执行同一 canonical fix |
| PC-006 lens：中文 Windows 码页陷阱会藏进交付物 | 红灯测试用子进程**强制写 GBK**（`-EncodedCommand` 设 `[Console]::OutputEncoding=GetEncoding(936)`），与测试环境码页无关，确定性复现 |
| PC-010 lens：stdio 外部进程启动与帧协商脱节 | StdioMcpTransport 判异根因不动（协议面强制 UTF-8 是 MCP 规范），扫查表留裁决记录防误改 |
| 守卫缺失即复发（PC-009 教训：G2 缺位则新增点无门禁） | FR-4 G2 类级枚举守卫 + allowlist 带理由 |
| 回退解码把协议流掰坏（新引入风险） | 守卫 allowlist 显式排除 StdioMcpTransport；测试含 UTF-8 阳性对照 |

## 交互设计（Interaction Design）

**不适用** —— 本批无用户可见 UI/交互改动（纯后端输出解码链路；工具返回 JSON 契约字段不变）。页面 Agent / MCP 客户端侧「点什么出现什么」无新增，仅既有字段内容从乱码变正确。

## Compatibility

- 工具 JSON 出参字段、守卫行为、超时/截断语义不变 ⇒ 外部调用方（fs/Qoder/MCP 网关/e2e）零适配；
- 插件版本各 bump 一位（AIAgent/ToolBridge/AgentHub/Scheduler/ImGateway plugin.json）；宿主改动（Core/Api）随宿主版本发布；
- 非 Windows 运行时（CI linux runner 若存在）：P/Invoke 分支有兜底，行为不劣于现状。

## Non-functional Requirements

- 每命令额外开销 = 一次字节缓冲 + 两次尝试解码（首试通常即中），对 30s 超时域可忽略；
- 大输出仍先经字节捕获再截断（截断按解码后字符数，与现状一致，禁止截断字节流导致多字节字符拦腰断）。

## Acceptance Criteria（闸门1 待批清单）

- [ ] AC1 红灯先行：`RunTerminalCommandEncodingTests`（GBK 子用例）在修复前**实测红**、修复后绿；UTF-8 阳性对照恒绿（05 记录红/绿两份输出）
- [ ] AC2 `ProcessOutputTextTests`（Core 单测）：UTF-8 字节/GBK 字节/BOM/空/混合坏点 五类入参判据全绿
- [ ] AC3 G2 守卫 `ProcessOutputEncodingGuardTests`：扫生产码 `RedirectStandardOutput = true` 站点必引用 `ProcessOutputText` 或入 allowlist（StdioMcpTransport=协议流 / ScriptExecutor=事件流 TODO / RunnerService=不消费文本）；MUTATION 探针实测可红
- [ ] AC4 8 站点迁移完成，`dotnet build` 0 error，AIAgent/ToolBridge/AgentHub/Scheduler/ImGateway/Core 相关测试过滤集全绿
- [ ] AC5 dev-stack 真实宿主：HTTP 调 run_terminal_command 执行中文输出命令，返回 JSON 中文完好（实据入 05）
- [ ] AC6 PC-011 + case 归档、三处索引更新、`check-problem-classes.ps1` 绿
- [ ] AC7 发布（用户拍板后：本地目录更新源 -Sign）+ 用户升级 fs 实例后复跑原始乱码命令复验（⑤）
- [ ] AC8 ScriptRunner 事件流站点迁移项已记 `TODO.md`（逐字含 PC-011，带 expires）

## Unknown

| 不确定点 | 影响 | 处置 |
|---|---|---|
| :18890 fs 实例当前跑的宿主/插件版本与升级路径 | ⑤ 复验时点 | 实现后本地发布 + 用户在实例侧更新时自然揭示；复验前先探 version 端点 |
| ImGateway/ServiceManager/RuntimeDetector 三处读取形态细节（同步/异步、是否逐行） | FR-2 迁移写法 | 实现期逐文件核对，偏差回写本工件（规范 Stage 5.7） |
| TerminalCommandGuard 对测试命令 `-EncodedCommand` 载荷的解码扫描是否放行本用例串 | AC1 红灯可写性 | 已见守卫有 base64 解码扫描先例；若被误拦，调整用例命令形态（不改守卫安全语义） |
