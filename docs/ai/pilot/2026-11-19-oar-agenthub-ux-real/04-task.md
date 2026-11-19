---
task_id: 2026-11-19-oar-agenthub-ux-real
feature: 032
risk: { level: L1, triggers: ["Plugins/AgentHub/web/", "Plugins/AgentHub/Services/", "ForgeSelf.Abstractions/"], raised_by_agent: true }
gate1: { mode: authorized-by-task, at: "2026-11-19T14:47Z", ref: "user:去掉所有模拟数据…品牌图标…重新设计调整" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Abstractions/AgentRuntimeContracts.cs
  - Plugins/AgentHub/Services/AgentHubRuntimeProvider.cs
  - Plugins/AgentHub/web/
  - Plugins/AgentHub/docs/ui-design-brief.md
  - ForgeSelf.Api.Tests/Plugins/AgentHub/
  - docs/ai/pilot/2026-11-19-oar-agenthub-ux-real/
  - docs/02-features/032-agent-hub.md
  - scripts/dev-stack.ps1
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "honest OAR runtimes + brand icons + UX layout" }
---

# Agent Task

## Objective

产品面去掉 scripted/假绿；安装态走真实 OAR snapshot；品牌图标区分；Runtime/Session UX 分区。

## Scope

### Allowed

expected_files 所列。

### Forbidden

停杀 :51888；自动 install 缺失 vendor；把 scripted 标为产品 Agent；改 csproj 版本行。

## Acceptance Criteria

- [x] AC1：审计文档 Grok-only
- [x] AC2：API/UI 无 scripted
- [x] AC3：installation 诚实（executable / not_found / bundled）
- [x] AC4：品牌图标
- [x] AC5：UX 分区 + 截图
- [x] AC6：web build + Worker smoke + Provider 测试

## 验证命令

```bash
cd Plugins/AgentHub/RuntimeWorker && pnpm run smoke
cd Plugins/AgentHub/web && pnpm run build
dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~AgentHubRuntimeProvider
pnpm run design:check
```

## 闸门1

用户任务书目标 1–7 → authorized-by-task（§8.5）。
