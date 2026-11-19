---
task_id: 2026-11-19-oar-remaining-close
feature: 032
risk: { level: L1, triggers: ["Plugins/AgentHub/Controllers/", "Plugins/AgentHub/web/", "Plugins/AgentHub/RuntimeWorker/"], raised_by_agent: true }
gate1: { mode: authorized-by-task, at: "2026-11-19T13:10Z", ref: "user:把上面没做的三部分…做完 + 走查截图" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AgentHub/Controllers/
  - Plugins/AgentHub/Services/
  - Plugins/AgentHub/RuntimeWorker/
  - Plugins/AgentHub/web/
  - ForgeSelf.Api.Tests/Plugins/AgentHub/
  - docs/ai/pilot/2026-11-19-oar-remaining-close/
  - docs/02-features/032-agent-hub.md
  - docs/01-architecture/agenthub-oar-runtime.md
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR remaining-close: F1–F5 management surface + evidence" }
---

# Agent Task

## Objective

闭合 OAR remaining F1–F5，落盘 V* HTTP/测试证据与 W* 走查截图；缓做标 ⏸。

## Scope

### Allowed

expected_files 所列。

### Forbidden

缓做 D1–D12；停杀宿主；Watch SSE 全量重写；发明 P5；改 csproj 版本行。

## Acceptance Criteria

- [x] AC1：Auth 守卫测绿；无 token agents/tasks 401
- [x] AC2：install-plan + install(confirmed) API；未确认 400
- [x] AC3：Session UI 操作接线；http 客户端齐
- [x] AC4：Runtime probe/models UI 有截图
- [x] AC5：能力禁用 Steer 有 UI/逻辑证据
- [x] AC6：V1–V8/V11–V12 证据文件存在（可行项）
- [x] AC7：W1–W8 尽可能截图；W9–W10 ⏸
- [x] AC8：032/架构/gaps/TODO 一致；00–07 齐

## Verification Commands

```bash
dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~AgentHubAuth
cd Plugins/AgentHub/web && pnpm run build
# HTTP → /opt/cursor/artifacts/oar-todo-verify/
# walkthrough → /opt/cursor/artifacts/oar-todo-walkthrough/
```
