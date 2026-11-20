---
task_id: 2026-11-20-oar-a-bucket-honesty
feature: 032
risk: { level: L1, triggers: ["docs/02-features/032-agent-hub.md", "ForgeSelf.Api.Tests/Plugins/AgentHub/", "Plugins/AgentHub/web/", "ForgeSelf.Web/e2e/plugins/agent-hub/"], raised_by_agent: true }
gate1: { mode: authorized-by-user, at: "2026-11-20T03:53Z", ref: "parent-agent:A-bucket #22/#28/#25/#78" }
gate2: { mode: reviewer, at: "2026-11-20T04:05Z", decision: APPROVED }
gate3: { mode: agent, at: null }
expected_files:
  - docs/02-features/032-agent-hub.md
  - docs/ai/pilot/2026-11-19-oar-agenthub-runtime/02-spec.md
  - ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubRuntimeProviderTests.cs
  - ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubInterruptFixtureTests.cs
  - Plugins/AgentHub/web/src/probeAllRuntimes.ts
  - Plugins/AgentHub/web/src/AgentHubView.vue
  - ForgeSelf.Web/e2e/plugins/agent-hub/agent-hub.spec.ts
  - docs/ai/pilot/2026-11-20-oar-a-bucket-honesty/
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "Watch poll honesty + interrupt fixtures + probe-all e2e" }
---

# Agent Task（轻量 · 细节见 mini-task.md）

## Objective

A-bucket honesty：Watch=轮询最小面文档与测试；interrupt fixture；一键探测正式 e2e。

## Scope

### Allowed

`expected_files` 所列。

### Forbidden

实现 #63 unified SSE；改 OarTransport early-Exit / handlers bundled（sibling）；停杀宿主；改 csproj 版本行；scripted 充当 vendor 验收。

## Acceptance Criteria

- [x] 032 §6.2 写明 Watch/stream = poll；AC-R3 诚实
- [x] Submit/Get/Watch/Cancel/续接测绿；Interrupted ≠ Succeeded
- [x] e2e「一键探测」触发 probe 且刷新不触发；vitest 并发池绿
- [x] 不实现 #63；不混入 sibling transport PR

## Verification Commands

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AgentHubInterruptFixtureTests|FullyQualifiedName~AgentHubRuntimeProviderTests"
cd Plugins/AgentHub/web && pnpm run test && pnpm run build
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/agent-hub/agent-hub.spec.ts -g "一键探测"
```
