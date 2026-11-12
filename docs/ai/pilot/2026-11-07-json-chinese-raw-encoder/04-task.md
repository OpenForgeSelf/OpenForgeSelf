---
task_id: 2026-11-07-json-chinese-raw-encoder
feature: none（全仓 JSON 序列化中文保持原文，PC-027，用户输入6；对应 docs/11-troubleshooting 问题类）
risk: { level: L2, triggers: ["全仓 JSON 写路径转义行为变更（宿主+22 插件 ~40 文件，语义等价）", "Mcp mock 夹具按字节切帧伴生修复"], raised_by_agent: false, notes: "面广但每处仅改转义行为；类级守卫 + 全量后端测试兜底；无 DB/鉴权/生产环境触碰" }
gate1: { mode: user, at: 2026-11-07, ref: "用户输入6 直接指令即批准" }
gate2: { mode: reviewer, at: 2026-11-07, decision: APPROVED }
gate3: { mode: user, at: null }
expected_files: [ForgeSelf.Abstractions/ForgeJson.cs, ForgeSelf.Api/AppBuilder.cs, ForgeSelf.Api/**/*.cs(~25), ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs, Plugins/**/**/*.cs(除 Tests/web), docs/ai/pilot/2026-11-07-json-chinese-raw-encoder/**, docs/11-troubleshooting/**]
rollback: 本批不提交 git（闸门3 等用户）；回滚 = 工作区 `git checkout -- <本批文件>`，无数据/配置迁移
---

# 04 Task — 2026-11-07-json-chinese-raw-encoder

## Task ID
PILOT-2026-11-07-json-chinese-raw-encoder（问题类 PC-027）

## Objective
生产代码全部 JSON 写路径中文保持（不义 `\u`），G2 守卫先红后绿，全量后端测试零新增红。

## Scope
### Allowed
- `ForgeSelf.Abstractions/ForgeJson.cs`（新建）
- `ForgeSelf.Api/**/*.cs`（仅序列化写出点 + AppBuilder AddJsonOptions）
- `Plugins/**/**/*.cs`（仅序列化写出点；含 DbAdmin——机械单行改动，与 db-multi-database 批冲突面极小，已在 TODO 协作区声明）
- `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs`（新建）
- `docs/ai/pilot/2026-11-07-json-chinese-raw-encoder/**`、`docs/11-troubleshooting/**`、`TODO.md`、日记

### Forbidden
- `ForgeSelf.Web/**`、`e2e/**`、`plugin`（版本号）、`Data/Model.xml`、`Data/Entities/*` 生成件、任何 `Tests` 目录的存量测试、`openwiki/**`、反序列化路径行为、命名策略/缩进变更、无关重构、git 提交/推送。

## Acceptance Criteria
- [ ] AC1 ForgeJson 单测绿（中文原文 + 无 `\uXXXX` 转义 + Indented 缩进）
- [ ] AC2 G2 守卫先红后绿（RED 488 → GREEN 0，失败明细见 05-evidence）
- [ ] AC3 dotnet build 0 error
- [ ] AC4 全量 dotnet test 新增红 = 0
- [ ] AC5 05-evidence 全条目标 Verified（Inferred/Unknown 显式标注）
- [ ] AC6 PC-027 + case + 三处索引 + check-problem-classes.ps1 绿

## Expected Files
见头部 expected_files。

## Verification Commands
```
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~JsonNonAsciiEscapingGuard"
cd ForgeSelf.Api.Tests && dotnet test            # 全量（中档）
pwsh scripts/check-problem-classes.ps1
pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-07-json-chinese-raw-encoder -EnforceHeader -EnforceAcCoverage -EnforceInteraction
```
