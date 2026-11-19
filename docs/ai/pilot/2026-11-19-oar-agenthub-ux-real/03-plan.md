# Plan

## Implementation Steps

1. file:`ForgeSelf.Abstractions/AgentRuntimeContracts.cs` — `InstallationKind`/`InstallationVia`；Available 语义注释。
2. file:`Plugins/AgentHub/Services/AgentHubRuntimeProvider.cs` — 过滤 scripted；installation 诚实探测。
3. file:`Plugins/AgentHub/web/src/assets/brands/*` + `runtimeBrand.ts` + `RuntimeBrandIcon.vue` — 图标。
4. file:`Plugins/AgentHub/web/src/AgentHubView.vue` + `AgentHubAgentCard.vue` — UX 分区 + 图标。
5. file:`docs/02-features/032-agent-hub.md` — 真源回写。
6. Tests：Provider 过滤断言；web build；Worker smoke；截图。

## Test Plan

- `dotnet test --filter FullyQualifiedName~AgentHubRuntimeProvider`
- `pnpm run smoke` in RuntimeWorker
- `pnpm run build` in AgentHub/web
- Host `pnpm run design:check`（若触及 token）
- e2e agent-hub 1 smoke（若环境可）
- Manual：dev-stack → Runtime 页截图 light/dark

## Verification

Build / Unit / E2E smoke / Screenshots under `/opt/cursor/artifacts/oar-ux-real/`.
