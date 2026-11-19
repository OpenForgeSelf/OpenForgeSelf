# Plan

> Task ID：`2026-11-19-oar-runtime-gap-close`

## Steps

1. **Model + Entity**：`Model.xml` 加 `OarSequence`、`AgentRuntimeRecord`；手改 `AgentHubSession.cs`；新建 Record 实体+Biz；`AgentHubTables` 登记。  
2. **EventStore**：`AgentRuntimeEventStore` + `AgentSessionManager.Touch` 扩展；单测序列单调。  
3. **Worker Client**：installation/auth/models/records RPC。  
4. **Transport 接线**：OarTransport 事件 → EventStore.Append。  
5. **Controllers**：probe/models/abort/records/stream + ApiKeyPolicy；DTO 扩展 Sequence 字段。  
6. **Tests**：EventStore + 可选 Provider/API 过滤。  
7. **e2e**：`agent-hub.spec.ts` 增 Session 冒烟一条。  
8. **Writeback + Evidence/Review/Report**；commit/push。

## 改动文件（≤预期）

- `Plugins/AgentHub/Data/Model.xml`
- `Plugins/AgentHub/Data/Entities/AgentHubSession.cs`（+Biz 注释）
- `Plugins/AgentHub/Data/Entities/AgentRuntimeRecord.cs` / `.Biz.cs`
- `Plugins/AgentHub/Data/AgentHubTables.cs`
- `Plugins/AgentHub/Services/AgentRuntimeEventStore.cs`（新）
- `Plugins/AgentHub/Services/AgentSessionManager.cs`
- `Plugins/AgentHub/Services/OarWorkerClient.cs`
- `Plugins/AgentHub/Services/OarTransport.cs`
- `Plugins/AgentHub/Controllers/AgentHubRuntimesController.cs`
- `Plugins/AgentHub/Controllers/AgentHubSessionsController.cs`
- `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubRuntimeEventStoreTests.cs`（新）
- `ForgeSelf.Web/e2e/plugins/agent-hub/agent-hub.spec.ts`
- docs：pilot、032、architecture、design 02-spec AC、audit

## 风险

- XCode 实体手增列：既有库 Migration 依赖 InitConnection；测试夹具清缓存。  
- e2e 环境（SQLite provider / 插件 dist / NO_PROXY）可能假红 — 按 §5.0 排环境。  
- 回滚：`git revert` 本 pilot commits。
