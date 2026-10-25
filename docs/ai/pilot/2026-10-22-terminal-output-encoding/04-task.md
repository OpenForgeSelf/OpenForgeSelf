---
task_id: 2026-10-22-terminal-output-encoding
task_kind: defect
feature: 021-ai-agent          # 主影响面 run_terminal_command；另涉宿主 ServiceManager/RuntimeDetector 与四插件
risk: { level: L1, triggers: ["ForgeSelf.Core 解码链路被 8 站点消费", "5 插件版本 bump"], raised_by_agent: false }
gate1: { mode: reviewer, at: 2026-10-22, ref: "用户「开工」批准 AC1-AC8 清单；发布 AC7 另待拍板" }
gate2: { mode: reviewer, at: 2026-10-22, decision: APPROVED }
gate3: { mode: agent, at: 2026-10-22 }
expected_files:
  - ForgeSelf.Core/ProcessOutputText.cs
  - ForgeSelf.Core/ProcessOutputLineBuffer.cs
  - Plugins/AIAgent/Services/ToolFunctions/RunTerminalCommandTool.cs
  - Plugins/ToolBridge/Services/CommandExecutor.cs
  - Plugins/AgentHub/Services/ProcessRunner.cs
  - Plugins/AgentHub/Services/CliTransport.cs
  - Plugins/Scheduler/Services/TaskExecutor.cs
  - Plugins/ImGateway/Services/WeComScanAuthService.cs
  - ForgeSelf.Api/Services/ServiceManager.cs
  - ForgeSelf.Api/Services/RuntimeDetector.cs
  - ForgeSelf.Api.Tests/Architecture/ProcessOutputEncodingGuardTests.cs
  - ForgeSelf.Api.Tests/Plugins/RunTerminalCommandEncodingTests.cs
  - ForgeSelf.Core.Tests/ProcessOutputTextTests.cs
rollback: "git checkout -- 各站点 + 删新增助手/测试/归档文件（本批未提交，回滚点 = 工作区基线）"
writeback: { feature_doc: done, reason: "021-ai-agent.md run_terminal_command 行已补输出解码说明与真源指针" }
---

# Task（PILOT-073 · 2026-10-22）

## Task ID

PILOT-073-terminal-output-encoding（工件目录 `docs/ai/pilot/2026-10-22-terminal-output-encoding/`）

## Objective

落地 PC-011 canonical fix（字节捕获 + UTF-8 严格/系统 ANSI 回退单点助手），迁移 8 处同根因站点，红灯→绿灯 + G2 守卫 + 问题类归档；发布与运行实例复验在闸门2 后按用户拍板执行。

## Scope

**Allowed**：
- 新建 `ForgeSelf.Core/ProcessOutputText.cs`、`ForgeSelf.Core.Tests/ProcessOutputTextTests.cs`、`ForgeSelf.Api.Tests/Plugins/RunTerminalCommandEncodingTests.cs`、`ForgeSelf.Api.Tests/Architecture/ProcessOutputEncodingGuardTests.cs`；
- 03-plan S3 列名 8 站点的输出读取段改造 + 各插件 `plugin.json` 版本 bump；
- PC-011/case 建档与三处索引、`TODO.md`（ScriptRunner 迁移项）、本 pilot 工件 05/06/07。

**Forbidden**：
- 改 `TerminalCommandGuard` 安全语义（白名单/拒绝规则）；
- 改 `StdioMcpTransport`（异根因）与 `ScriptExecutor` 事件流重构（入 TODO）；
- 停/启/杀 :51888、:18890 等用户运行实例；未获拍板跑发布链；
- 无关顺手重构、新增 NuGet 依赖。

## Acceptance Criteria

- [ ] AC1 红灯实测（修复前 FAIL 日志 + 修复后 PASS 日志入 05，均 Verified）
- [ ] AC2 ProcessOutputTextTests 五类入参全绿
- [ ] AC3 G2 守卫绿 + MUTATION 探针可红证据
- [ ] AC4 `dotnet build` 0 error；AIAgent/ToolBridge/AgentHub/Scheduler/ImGateway/Core/宿主过滤集全绿
- [ ] AC5 dev-stack 现场 HTTP 调用中文命令输出完好（GBK 子 + UTF-8 子）
- [ ] AC6 PC-011 + case + 三处索引 + `check-problem-classes.ps1` 绿
- [ ] AC7 发布（用户拍板后）+ ⑤ fs 实例复跑原始命令复验
- [ ] AC8 TODO 含「PC-011」逐字可检索的 ScriptRunner 迁移项（expires 2026-12-31）

## Expected Files

见 03-plan S1-S5（约 15 文件：新增 5、迁移 8、清单/文档/台账若干）。

## Verification Commands

```bash
dotnet build ForgeSelf.slnx
dotnet test ForgeSelf.Core.Tests
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~RunTerminalCommandEncodingTests"
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~ProcessOutputEncodingGuardTests"
dotnet test ForgeSelf.Api.Tests --filter "AIAgent|ToolBridge|AgentHub|Scheduler|ImGateway"
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
pwsh -NoProfile -File scripts/dev-stack.ps1   # 现场验收后 -Stop
```
