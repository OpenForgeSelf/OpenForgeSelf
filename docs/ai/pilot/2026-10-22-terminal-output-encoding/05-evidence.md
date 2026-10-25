# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> Task：PILOT-073（子进程输出固定码页解码中文乱码 · PC-011 首见闭环）

## Task

PILOT-073 —— 修复 run_terminal_command 等 8 站点子进程输出固定 UTF-8 解码导致 GBK 中文 U+FFFD 乱码；新增 canonical 助手 `ProcessOutputText`/`ProcessOutputLineBuffer`、G2 源码守卫、PC-011 问题类归档。

## AC 覆盖对照（02-spec Acceptance Criteria 逐项）

| AC | 结果 | 来源等级 | 证据位置 |
|----|------|----------|----------|
| AC1 红灯先行（GBK 修复前实测红、修复后绿；UTF-8 阳性对照恒绿） | 达成 | Verified | 本文件 Unit Test 节「红灯（修复前）」+ 会话 transcript task-e6f.jsonl |
| AC2 ProcessOutputTextTests 五类入参全绿 | 达成 | Verified | Core.Tests 38/38（本文件 Unit Test 节） |
| AC3 G2 守卫 + MUTATION 探针实测可红 | 达成 | Verified | 本文件 Static Analysis 节（首绿/MUTATION红/还原绿三份日志） |
| AC4 8 站点迁移 + build 0 error + 过滤集全绿 | 达成 | Verified | 本文件 Build + Unit Test 节 |
| AC5 dev-stack 真实宿主中文完好 | 达成 | Verified | 本文件 E2E/现场验收节（GBK+UTF-8 双通过 + FR-3 留痕日志） |
| AC6 PC-011 归档 + 三索引 + check-problem-classes 绿 | 达成 | Verified | 本文件问题类台账节 |
| AC7 发布 + 运行实例复验 | **未执行——待用户拍板（闸门1 待批项②）** | Unknown | agent 不得自行发布/停启用户实例（AGENTS.md §0 铁律1） |
| AC8 ScriptRunner 迁移项挂账 TODO.md | 达成 | Verified | TODO.md「PC-011」行（P2，expires 2026-12-31） |

## Changed Files

git status --porcelain 实测 28 项（2026-10-22）：

- 生产码（8 站点 + 2 助手 + 1 守卫 + 2 测试，M/??）：
  - `M ForgeSelf.Api/Services/RuntimeDetector.cs`、`M ForgeSelf.Api/Services/ServiceManager.cs`（站点 1-2）
  - `M Plugins/AIAgent/Services/ToolFunctions/RunTerminalCommandTool.cs`（站点 3）
  - `M Plugins/ToolBridge/Services/CommandExecutor.cs`（站点 4）
  - `M Plugins/AgentHub/Services/ProcessRunner.cs`、`M Plugins/AgentHub/Services/CliTransport.cs`（站点 5-6）
  - `M Plugins/Scheduler/Services/TaskExecutor.cs`（站点 7）
  - `M Plugins/ImGateway/Services/WeComScanAuthService.cs`（站点 8，流式→ProcessOutputLineBuffer）
  - `?? ForgeSelf.Core/ProcessOutputText.cs`、`?? ForgeSelf.Core/ProcessOutputLineBuffer.cs`
  - `?? ForgeSelf.Api.Tests/Plugins/RunTerminalCommandEncodingTests.cs`、`?? ForgeSelf.Core.Tests/ProcessOutputTextTests.cs`、`?? ForgeSelf.Api.Tests/Architecture/ProcessOutputEncodingGuardTests.cs`
- 插件版本（5 × plugin.json bump）：`M Plugins/{AIAgent 1.9.4, ToolBridge 1.0.4, AgentHub 1.0.11, Scheduler 1.0.1, ImGateway 2.0.1}/plugin.json`
- 文档/台账（M）：`TODO.md`、`docs/02-features/021-ai-agent.md`、`docs/11-troubleshooting/{index.md, cases/README.md, problem-classes/README.md}`
- 归档（??）：`docs/11-troubleshooting/problem-classes/PC-011-process-output-fixed-codepage-decode.md`、`docs/11-troubleshooting/cases/2026-10-22-process-output-fixed-codepage-decode.md`、本 pilot 目录

## Build

Command:

```powershell
dotnet build ForgeSelf.slnx
```

Result: PASS（来源等级：Verified）

```text
0 个错误（日志全量：.forgeself/tmp-build-pc011-sites8.log）
```

## Unit Test

### 红灯（修复前，AC1 证据）

Command：`dotnet test --filter "RunTerminalCommandEncodingTests"`（迁移 RunTerminalCommandTool 之前，站点仍旧码）

Result: FAIL —— 预期红（来源等级：Verified）

```text
[FAIL] GBK 子用例：Expected stdout "????" to contain "中文编码测试"
[PASS] UTF-8 阳性对照（恒绿，证明修复不伤害合法 UTF-8 流）
```

（首跑曾因 WorkingDirectory 不存在产生**无效红**，已纠为真实缺陷红；过程见会话 transcript `task-e6f.jsonl`。）

### 绿灯（修复后）

Command:

```powershell
dotnet test ForgeSelf.Api.Tests --filter "RunTerminalCommandEncodingTests|TerminalCommandGuardTests"
dotnet test ForgeSelf.Api.Tests --filter "AIAgent|ToolBridge|AgentHub|Scheduler|ImGateway"
dotnet test ForgeSelf.Core.Tests
```

Result: PASS（来源等级：Verified）

```text
RunTerminalCommandEncodingTests + TerminalCommandGuardTests：57/57 通过
五插件过滤集（AIAgent|ToolBridge|AgentHub|Scheduler|ImGateway）：355/355 通过
ForgeSelf.Core.Tests（含 ProcessOutputTextTests 五类入参：UTF-8/GBK/BOM/空/混合坏点）：38/38 通过
```

## Integration Test

Result: N/A（依据：本任务无数据库/外部服务契约变更；工具 JSON 封套字段与守卫语义零改动，集成面由 355 项插件过滤集覆盖）

## E2E（插件层）

Result: N/A（依据：无 UI 改动；插件层冒烟 e2e 属发布批（AC7）范围，随拍板后按 plugin-development §四 六步执行）

## 现场验收（AC5，dev-stack 真实宿主）

Command:

```powershell
pwsh -NoProfile -File scripts/dev-stack.ps1        # backend :7301 / MCP 网关 :18899
# 探针（一次性，用完已删，不进版本控制）：POST api/project/directory 设根 → 网关 tools/call run_terminal_command
pwsh -NoProfile -File scripts/dev-stack.ps1 -Stop  # STOP_EXIT=0
```

Result: PASS（来源等级：Verified）

```text
set-dir(Bearer): success=True root=D:\src\my-proj\OpenForgeSelf\OpenForgeSelf
[GBK] success=True exit=0
[GBK] stdout=[中文编码测试-GBK]
[GBK] 中文完好=True 含U+FFFD=False
[UTF8] success=True exit=0
[UTF8] stdout=[中文编码测试-UTF8]
[UTF8] 中文完好=True 含U+FFFD=False
```

FR-3 非 UTF-8 留痕日志（宿主日志实测原文）：

```text
18:42:41 [AIAgentPlugin] run_terminal_command …输出按非 UTF-8 还原… stdout=ansi-936 stderr=utf8
```

## Static Analysis（G2 守卫 + MUTATION 探针，AC3）

Command:

```powershell
dotnet test ForgeSelf.Api.Tests --filter "ProcessOutputEncodingGuardTests"   # 首绿
# MUTATION：向 RunTerminalCommandTool.cs 临时注入 StandardOutputEncoding = Encoding.UTF8 → 复跑
# 还原：撤销注入 → 复跑
```

Result: PASS（来源等级：Verified）

```text
首绿：    .forgeself/tmp-guard-pc011-first.log    （通过:1 失败:0）
MUTATION红：.forgeself/tmp-guard-pc011-mutation.log（[xUnit.net] ProcessOutputEncodingGuardTests.生产源码_子进程输出_禁止固定码页解码 [FAIL]，失败:1）——实证守卫可红，非哑守卫
还原绿：  .forgeself/tmp-guard-pc011-revert.log   （通过:1 失败:0，注入已彻底撤销、净变更为零）
```

## 问题类台账（AC6）

Command:

```powershell
pwsh -NoProfile -File scripts/check-problem-classes.ps1
dotnet test ForgeSelf.Api.Tests --filter "ProblemClassRegistryTests"
```

Result: PASS（来源等级：Verified）

```text
check-problem-classes.ps1：11 个 PC 全部 OK，CHECK_EXIT=0（含新建 PC-011 的 S1-S5 signature 校验）
ProblemClassRegistryTests：5/5 通过（.forgeself/tmp-pc011-registry.log）
索引三处已更新：docs/11-troubleshooting/index.md 速查表、problem-classes/README.md（tags 表 + 总表）、cases/README.md
```

## Screenshots

N/A（本批无用户可见 UI 改动；现场验收为 HTTP JSON 层证据，原文已录上节）

## Known Limitations

- ScriptRunner `ScriptExecutor` 事件流形态未迁移（allowlist 挂账，expires 2026-12-31，TODO.md 逐字「PC-011」可检索）；
- StdioMcpTransport 按 MCP 规范保留强制 UTF-8（异根因，裁决记录在 PC-011 正文，防误改）；
- ANSI 回退依赖 `GetACP()`：若子进程码页与系统 ACP 不一致（如 chcp 65001 下输出 GBK），由严格 UTF-8 首选路径 + 往返校验兜底，极端混合流仍可能损失为替换符（如实返回 `utf8-lossy` 标签）。

## Unresolved Issues

- AC7 发布与 :18890 fs 实例复验：**待用户拍板**（本地目录更新源 `release-local.ps1 -UpdateDir -Sign` 是否执行、宿主版本是否同步 bump）。非阻塞缺陷，属流程闸门，不列入 BLOCKED。

## 提交前复核（2026-10-25）

提交批次前按验收清单逐项重跑（全部 Verified）：

| 检查 | 命令 | 结果 |
|---|---|---|
| AC4 构建 | `dotnet build ForgeSelf.slnx -c Debug` | **0 错误**（448 警告为既有 xUnit 分析器告警；日志 `.temp/accept-build.log`） |
| AC2 解码器单测 | `dotnet test ForgeSelf.Core.Tests --filter FullyQualifiedName~ProcessOutputText` | **6/6 通过**，Core.Tests 全量 **38/38** |
| AC3/AC4 守卫与迁移站点 | `dotnet test ForgeSelf.Api.Tests --filter "ProcessOutputEncoding\|RunTerminalCommandEncoding\|..."` | 定向过滤集 **182/182 通过**（含 `ProcessOutputEncodingGuardTests`、`RunTerminalCommandEncodingTests`） |
| AC6 问题类台账 | `pwsh scripts/check-problem-classes.ps1` | exit 0，**13 个 PC** 签名/allowlist/guards 全过 |

AC5（真实宿主中文输出）与 AC1（红灯先行）沿用本文件既有 Verified 现场记录，本轮未重复执行。
