---
task_id: 2026-10-17-problem-class-closure
task_kind: infra            # defect | infra（本任务=流程基建；07 三节仍自愿齐备作示范）
feature: 流程基建·问题类闭环（无功能编号）
risk: { level: L2, reason: "动 AGENTS.md/规范/hook/测试工程，属公共流程契约；不改业务运行代码，可整体文件级回滚" }
rollback: 新增文件直接删除（未提交，git 工作树态）；修改文件 git checkout -- 还原；无 DB/发布动作
expected_files:
  - .agents/skills/problem-class-closure/**
  - docs/11-troubleshooting/**
  - docs/18-templates/postmortem/**
  - docs/18-templates/ai-pilot/07-final-report.tpl.md
  - docs/04-standards/ai-native-engineering-workflow.md
  - docs/README.md
  - AGENTS.md
  - scripts/check-problem-classes.ps1
  - scripts/hooks/pre-commit
  - ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs
  - ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs
  - docs/ai/pilot/2026-10-17-problem-class-closure/**
---

# Agent Task

> 阶段：Stage 4｜零自我决策空间：一切以 02-spec AC1~AC10 与 03-plan 文件清单为准。

## Objective

按闸门1 批准的「四层知识模型」方案，落地问题类闭环机制（阶段一~三）：技能、知识库、模板、流程接入、可执行守卫。

## Scope

### Allowed

- 创建/修改 `expected_files` 所列文件
- `ForgeSelf.Api.Tests` 新增两个测试类（纯源码扫描，不连库、不起宿主）
- 本机运行 `dotnet build/test`、`pwsh scripts/*.ps1`、`sh scripts/hooks/pre-commit`（假暂存模拟，不真提交）

### Forbidden

- 修改 `scripts/verify-pilot-artifacts.ps1`（L4 宪法层）与任何生产源码（Plugins/、ForgeSelf.Api/、ForgeSelf.Core/、ForgeSelf.Abstractions/、ForgeSelf.Web/）
- git commit / push / tag / 发布（等闸门2 后用户授权）
- 停/启/杀任何运行中宿主进程
- 顺手解决 TODO.md 既有 P3「plugin-development 技能补铁律」条目本体（PC-001 守卫已覆盖其可执行部分，技能正文改写属阶段四瘦身批）
- 在 AGENTS.md 增补三处之外的任何正文

## Acceptance Criteria

见 02-spec §8：AC1~AC10 逐条 checkbox，验证时回填 05-evidence。

- [ ] AC1 check-problem-classes.ps1 绿 + 每 PC 扫描计数非空转
- [ ] AC2 植入违规→红→移除→回绿（阳性对照）
- [ ] AC3 dotnet build 0 错 + dotnet test --filter ProblemClass 全绿
- [ ] AC4 PC-001/002/003 front-matter 完整、lens 非空、guards 齐
- [ ] AC5 case 引用闭合、两处索引双向一致
- [ ] AC6 AGENTS.md 恰好三处改动
- [ ] AC7 规范 v1.2.0 五处增补落笔
- [ ] AC8 07 模板三节替换且不破坏既有门禁关键节
- [ ] AC9 pre-commit 挂接 + 红绿两路模拟验证
- [ ] AC10 变更集 = 触点清单，零业务代码

## Verification Commands

```bash
dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~ProblemClass"
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-17-problem-class-closure -EnforceHeader -EnforceAcCoverage
```
