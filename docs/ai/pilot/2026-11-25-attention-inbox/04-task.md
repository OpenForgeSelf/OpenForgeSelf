---
task_id: 2026-11-25-attention-inbox
feature: 102
risk: { level: L2, triggers: ["ForgeSelf.Web/src/views/TaskCenterView.vue", "ForgeSelf.Abstractions/IApprovalService.cs"], raised_by_agent: false }
gate1: { mode: user, at: 2026-11-26, ref: "implement-the-plan" }
task_kind: defect
gate2: { mode: reviewer, at: 2026-11-26, decision: changes_required }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Web/src/services/runtimeBoard.ts
  - ForgeSelf.Web/src/services/runtimeBoardApi.ts
  - ForgeSelf.Web/src/views/TaskCenterView.vue
  - ForgeSelf.Web/src/views/SchedulerStatusView.vue
  - ForgeSelf.Web/src/views/BrowserSessionView.vue
  - docs/02-features/102-human-attention.md
rollback: "git revert 本任务提交"
writeback: { feature_doc: docs/02-features/102-human-attention.md, reason: "运行实况已落地" }
---

# Agent Task

## Objective

运行实况页列出全部要处理和正在跑的事项，点击能打开对应详情。

## Scope

### Allowed

分类纯函数、分路取数、运行实况页、标签数字、只读列表接口、来源页查询参数、定时任务与浏览器只读页、本功能文档。

### Forbidden

不改 IM 进展推送。不新建插件。不建第二套审批库。不把系统监控全机进程表搬进这一页。不在打开页面或定时器里调用进程扫描。不新增依赖。不改各来源决定接口的语义。

## Acceptance Criteria

- [x] AC1 至 AC10、AC13 至 AC18、AC20、AC21、AC23 至 AC26：有截图，见 102
- [ ] AC11、AC12、AC19、AC22：本环境没有造出对应行，05 标 Unknown

## Expected Files

见头部 `expected_files`。深链还改到 AgentHub、Sems、DbAdmin、FileTools、TodoTracker、SmartTerminal 的页面。

## Verification Commands

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~PersistentApprovalServiceTests|FullyQualifiedName~FolderScanServiceTests|FullyQualifiedName~GetExecutions_ValidRequest"
cd ForgeSelf.Web && pnpm run check && pnpm exec vitest run --maxWorkers=2 && pnpm run design:check
```
