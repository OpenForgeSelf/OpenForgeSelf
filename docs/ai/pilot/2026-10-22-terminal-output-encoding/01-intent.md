# Intent（PILOT-073 · 2026-10-22）

## Problem

宿主/AIAgent 的 `run_terminal_command` 工具把子进程输出**固定按 UTF-8 解码**。中文 Windows 上子进程（pwsh、dotnet CLI 等控制台程序）继承宿主 GBK(936) 控制台码页、按 GBK 写字节时，父端解码出成片的 U+FFFD —— 中文观察数据**不可逆销毁**。2026-10-22 经 fs MCP 实例实测坐实（exitCode 0 但整段乱码），同文件走 `read_file` 正常，排除文件损坏。全仓扫查另有 7 处同根因站点（固定 UTF8 或跟随码页，两种固定姿势必坏其一）。

## Why

- 该工具是外部 agent（fs/Qoder/MCP 客户端）与页面 Agent 读终端输出的**唯一通道**，乱码 = 拿到的所有中文命令输出（构建错误、git log、文件内容）全部作废；
- 属问题类闭环定义的结构性缺陷（编码错配类，PC-006/PC-010 同族新类 PC-011），无守卫，复发面在 8+ 个起进程点。

## Expected Outcome

- 终端型子进程输出**无论子进程按 UTF-8 还是系统 ANSI 码页写，都正确还原为文本**（UTF-8 严格优先、失败回退系统 ANSI）；
- 报告实例（run_terminal_command）+ 全部同根因同形态站点一次性修掉（canonical fix 单点助手）；
- 红灯集成测试（子进程强制写 GBK）先红后绿；G2 类级枚举守卫落地（新增起进程点乱用固定码页即 CI 红）；PC-011 + case 归档。

## Constraints

- 不新增 NuGet 依赖（全仓无 CodePages 包，GBK 解码走 kernel32 `MultiByteToWideChar` P/Invoke，纯 BCL 入 ForgeSelf.Core）；
- 不动 `StdioMcpTransport`（MCP 协议面强制 UTF-8，异根因）；不改 `TerminalCommandGuard` 安全语义；
- 禁止 agent 停/启/杀 :18890 等用户运行实例；发布走本地目录更新源（用户拍板后）；
- 事件流形态站点（ScriptRunner ScriptExecutor）不随批重构 → TODO 带 PC-011 id。

## Success Criteria

1. `dotnet test` 红灯→绿灯证据（05 Verified）：GBK 子进程输出经 run_terminal_command 返回正确中文、无 U+FFFD；UTF-8 阳性对照同样正确；
2. G2 守卫用例存在且 MUTATION 探针实测可红（去掉一处助手调用即红）；
3. dev-stack 真实宿主 HTTP 调用工具：中文命令输出正确（截图/JSON 实据）；
4. 用户升级后 fs 实例复跑 2026-10-22 原始乱码命令：中文完好（⑤运行实例只读复验）；
5. `scripts/check-problem-classes.ps1` 绿 + PC-011/case/三处索引齐。
