---
task_id: PILOT-079
feature: host-defects-agent-dev-loop
risk: { level: L2, triggers: ["7 缺陷批修复，插件行为变更，零 UI/DB/鉴权改动，可回滚"], raised_by_agent: false }
gate1: 用户直接指令（输入8 2026-11-07 23:28「全部修复，再走一遍这个流程…」+ 输入7 缺陷清单确认）——本工件 02-04 落盘即视为闸门1 材料齐备
gate2: Evidence（05）+ Review（06）齐备后交用户验收，通过前不提交
gate3: 用户验收后提交归档（本批不 git 提交，等用户）
expected_files: [Plugins/SmartTerminal/Services/SmartTerminalToolFunctions.cs, ForgeSelf.Api.Tests/Architecture/PluginRawCreateScopeGuardTests.cs, Plugins/McpCenter/Services/PublicTools/TerminalCommandGuard.cs, Plugins/McpCenter/Services/PublicTools/RunTerminalCommandTool.cs, ForgeSelf.Api.Tests/Plugins/McpCenterTests/*, Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs, ForgeSelf.Api.Tests/Plugins/AIAgent/*, Plugins/AIAgent/Controllers/ApprovalsController.cs, Plugins/Scheduler/Services/*, Plugins/Scheduler/SchedulerPlugin.cs, ForgeSelf.Api.Tests/Plugins/Scheduler/*, docs/11-troubleshooting/*]
rollback: 全部为源码级改动，git checkout应文件即回滚；无 DB/配置/发布产物变更
writeback: docs/11-troubleshooting/problem-classes/{PC-005,PC-013} 归档更新 + 新 PC 定级 + skills/README 回写 + TODO/日记
---

# Task

## Objective
7 项缺陷全部红灯→绿灯修复并按问题类闭环收口；随后本地实例浏览器驱动验证 Agent 自主开发插件闭环（SC4）。

## Scope — Allowed
- `Plugins/SmartTerminal/**`（仅 Services/SmartTerminalToolFunctions.cs + Plugin 文件头 using）
- `Plugins/McpCenter/Services/PublicTools/**` + McpCenter 测试
- `Plugins/AIAgent/**`（MafLoop/Controllers/Services + 测试）
- `Plugins/Scheduler/**`（Services/SchedulerPlugin + 测试）
- `ForgeSelf.Api.Tests/**`（守卫 + 各插件测试）
- `docs/11-troubleshooting/**`、pilot 工件 00-07、TODO/日记

## Scope — Forbidden
- `ForgeSelf.Web/**`、任何插件 `web/**`、`:51888` 实例及其进程（停/启/杀均禁）
- git 提交/发布/打 tag；DB 结构与鉴权核心；新增 NuGet 依赖
- 非缺陷位顺手重构（同类扫查同根因命中除外——problem-class-closure 铁律3）

## Acceptance Criteria
1. 每项缺陷有红灯→绿灯证据（05-evidence 记 Verified，测试名可追溯）。
2. B1-B7 各批次 filter 绿；全量 `dotnet test` 对基线新增红=0；`check-problem-classes.ps1` 绿。
3. P1①/P2① 合入后宿主 Agent 能经 run_terminal_command 自跑 `dotnet build`（>30s 不假失败）。
4. PC-005/PC-013 逃逸分析归档；P2②/P2③/P3① 新 PC 定级或记入 07 悬空教训清单（必须为空）。
5. 本地 dev-stack 实例上 Agent 自主插件开发闭环（含自跑构建），截图存档。

## Commands
```
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
cd ForgeSelf.Api.Tests; dotnet test --filter "PluginRawCreateScopeGuardTests|SmartTerminal"
dotnet test --filter "RunTerminalCommand|ChainExecution|TerminalCommandGuard|McpCenter"
dotnet test --filter "Maf|AIAgent|Approval|Scheduler|ChatModel|Failover|Context"
# 中档全量（B7 后）：dotnet test -c Debug（NO_PROXY/TEMP 按 §5.0）
```

## 集成验证（代码批次后）
1. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1`（端口认 `.temp/dev-stack.json`）。
2. 本地实例设置审批自动放行；新开浏览器 tab（隔离上下文）注入 token。
3. 驱动 Agent 开发新插件（提需求→Agent 自查→自建→自测→自跑构建），全程截图；失败即记录→修复→重测（测完改改完再测）。
