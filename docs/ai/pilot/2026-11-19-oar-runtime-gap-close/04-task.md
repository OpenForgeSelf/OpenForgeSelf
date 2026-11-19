---
task_id: 2026-11-19-oar-runtime-gap-close
feature: 032
risk: { level: L1, triggers: ["Plugins/AgentHub/Data/", "Plugins/AgentHub/Controllers/", "Plugins/AgentHub/Services/"], raised_by_agent: true }
gate1: { mode: authorized-by-task, at: "2026-11-19T12:30Z", ref: "user:把应该做却没做的补上 + audit PARTIAL in-scope" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AgentHub/Data/
  - Plugins/AgentHub/Services/
  - Plugins/AgentHub/Controllers/
  - ForgeSelf.Api.Tests/Plugins/AgentHub/
  - ForgeSelf.Web/e2e/plugins/agent-hub/
  - docs/ai/pilot/2026-11-19-oar-runtime-gap-close/
  - docs/02-features/032-agent-hub.md
  - docs/01-architecture/agenthub-oar-runtime.md
  - docs/ai/pilot/2026-11-19-oar-agenthub-runtime/02-spec.md
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR gap-close: dual-layer events + §9 API + Session e2e" }
---

# Agent Task

## Objective

关闭 OAR 第一波未完项：事件双层最小耐久路径、架构 §9 API、Session e2e 冒烟、真源回写。

## Scope

### Allowed

expected_files 所列。

### Forbidden

P5；Skills/MCP/Subagents；真实 vendor 登录；Watch SSE 全量重写；PR#7 WindowStyle；停宿主；OAR 类型进 Abstractionseds。

## Acceptance Criteria

- [x] AC1 EventStore 序列测绿
- [x] AC2 五端点 + ApiKeyPolicy
- [x] AC3 既有过滤不新增红
- [x] AC4 web build + Session e2e
- [x] AC5 文档/audit/05–07

## Verification Commands

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -p:EnableWindowsTargeting=true \
  --filter "FullyQualifiedName~AgentHub|FullyQualifiedName~OarWorker|FullyQualifiedName~AgentRuntime|FullyQualifiedName~TransportRouting"
cd Plugins/AgentHub/web && pnpm run build
# e2e：见 03-plan；证据落 /opt/cursor/artifacts/
```
