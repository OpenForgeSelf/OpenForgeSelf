# Plan

## Implementation Steps

1. file:`Plugins/AgentHub/Controllers/AgentHubAgentsController.cs` + `AgentHubTasksController.cs` — 加类级 Authorize  
2. file:`ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubAuthTests.cs` — 程序集控制器 ApiKeyPolicy 守卫  
3. file:`Plugins/AgentHub/RuntimeWorker/src/handlers.ts` — `runtime.install`  
4. file:`Plugins/AgentHub/Services/OarWorkerClient.cs` — InstallPlanAsync / InstallAsync  
5. file:`Plugins/AgentHub/Controllers/AgentHubRuntimesController.cs` — install-plan / install  
6. file:`Plugins/AgentHub/web/src/http.ts` — session/runtime 客户端方法  
7. file:`Plugins/AgentHub/web/src/AgentHubView.vue` — Session/Runtime/Install/Steer UI  
8. file:`ForgeSelf.Api.Tests/Plugins/AgentHub/*` — Provider Watch/SendInput/Close + EventStore 路径补测（可行）  
9. 回写 `docs/02-features/032-agent-hub.md`、架构 §8/§9、gaps  
10. e2e 冒烟扩展或复跑 Session tab

## Test Plan

- `dotnet test --filter FullyQualifiedName~AgentHub`  
- AgentHub web `pnpm run build`  
- RuntimeWorker `pnpm run build`（若改 handlers）  
- HTTP 探针脚本 → artifacts  
- `pnpm exec playwright test e2e/plugins/agent-hub`（1 冒烟，环境允许时）  
- `pwsh scripts/dev-stack.ps1` 走查截图

## Verification

Build + Unit + 1 e2e + 手工走查截图。
