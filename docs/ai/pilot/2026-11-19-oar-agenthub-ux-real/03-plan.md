# Plan

## Implementation Steps

1. file:`ForgeSelf.Abstractions/AgentRuntimeContracts.cs` — `InstallationKind`/`InstallationVia`；Available 语义注释。
2. file:`Plugins/AgentHub/Services/AgentHubRuntimeProvider.cs` — 过滤 scripted；installation 诚实探测。
3. file:`Plugins/AgentHub/Controllers/AgentHubRuntimesController.cs` — runtimes API 与诚实 installation 对齐。
4. file:`Plugins/AgentHub/RuntimeWorker/src/registry.ts` — Worker 注册表与产品面 runtime 清单一致。
5. file:`Plugins/AgentHub/web/src/assets/brands/*` + `runtimeBrand.ts` + `RuntimeBrandIcon.vue` — 图标。
6. file:`Plugins/AgentHub/web/src/AgentHubView.vue` + `AgentHubAgentCard.vue` — UX 分区 + 图标。
7. file:`docs/02-features/032-agent-hub.md` — 真源回写。
8. Tests：Provider 过滤断言；web build；Worker smoke；截图。

## expected_files（与 04-task 同步）

- `ForgeSelf.Abstractions/AgentRuntimeContracts.cs`
- `Plugins/AgentHub/Services/AgentHubRuntimeProvider.cs`
- `Plugins/AgentHub/Controllers/AgentHubRuntimesController.cs`
- `Plugins/AgentHub/RuntimeWorker/src/registry.ts`
- `Plugins/AgentHub/web/`
- `Plugins/AgentHub/docs/ui-design-brief.md`
- `ForgeSelf.Api.Tests/Plugins/AgentHub/`
- `docs/ai/pilot/2026-11-19-oar-agenthub-ux-real/`
- `docs/02-features/032-agent-hub.md`
- `scripts/dev-stack.ps1`

## Test Plan

- `dotnet test --filter FullyQualifiedName~AgentHubRuntimeProvider`
- `pnpm run smoke` in RuntimeWorker
- `pnpm run build` in AgentHub/web
- Host `pnpm run design:check`（若触及 token）
- e2e agent-hub 1 smoke（若环境可）
- Manual：dev-stack → Runtime 页截图 light/dark

## Verification

Build / Unit / E2E smoke / Screenshots under `/opt/cursor/artifacts/oar-ux-real/`.
