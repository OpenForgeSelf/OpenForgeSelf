# Plan

> Task ID：`2026-11-19-oar-runtime-p2`

## Files To Change

- `Plugins/AgentHub/Data/Model.xml` + Entities（xcode）
- `Plugins/AgentHub/Services/{IAgentTransportRegistry,AgentTransportRegistry,OarTransport,AgentSessionManager}.cs`
- `Plugins/AgentHub/Services/DelegationRuntime.cs` / `AgentHubPlugin.cs` / `AgentRegistry.cs` / DTOs
- Tests：Registry / Session / Routing + Delegation MakeRuntime 适配
- `docs/02-features/032-agent-hub.md` + 本 pilot + 架构进度

## Forbidden

- Provider Register / Controllers / web / 停宿主 / 自动 install

## Verification

见 04-task。
