# Specification

> Task ID：`2026-11-19-oar-runtime-p2`

## Functional Requirements

| ID | 要求 |
| --- | --- |
| FR1 | `IAgentTransportRegistry` + 实现；Cli/Oar 登记 |
| FR2 | `OarTransport` Kind=Oar，缺 RuntimeId/Worker 时错误事件可解释 |
| FR3 | Model.xml：AccessPoint.RuntimeId；新表 AgentHubSession（最小字段集） |
| FR4 | DelegationRuntime 按 `ap.Transport` Resolve；取消同理 |
| FR5 | AgentSessionManager：Open/Find/Close/InterruptOpenSessions |
| FR6 | 不改 `IAgentDelegation` 契约；默认 profile 仍 Cli |

## Input / Output

- In：P1 `OarWorkerClient` + P0 Worker；concurrency-plan P2 轨  
- Out：Registry/OarTransport/Session 实体与选路；过滤测试；032/架构回写

## Business Rules

1. Transport 选路大小写不敏感；空/未知回落 Cli（若已登记）。  
2. Oar AccessPoint 校验 RuntimeId；Executable 对 Oar 可选。  
3. 对外只暴露 SessionKey；OarSessionId 仅存库。  
4. 本批 SessionKey 可与 Worker sessionKey 对齐（P3 可换独立 GUID 映射）。

## Boundary Conditions

| 边界 | 行为 |
| --- | --- |
| Worker 入口缺失 | OarTransport 吐可解释 Error 事件；Cli 不受影响 |
| Session 落库失败 | Warn 后继续执行本轮 Stream |
| 未知 Transport 且无 Cli | KeyNotFoundException |

## Error Handling

- Registry：未登记且无 Cli 回落 → `KeyNotFoundException`  
- OarTransport：Open/Prompt 失败 → `AgentEvent` Error + Exit  
- Biz Valid：Oar 缺 RuntimeId → ArgumentNullException

## Compatibility

- 不修改 `IAgentDelegation` 签名。  
- DI 仍暴露 `IAgentTransport`→CliTransport（兼容旧解析）。  
- 既有 DelegationRuntime 测试改用 Registry + Fake Kind=`Cli`。

## Non-functional

- 不停宿主；不自动 install；OAR 类型不进 Abstractionseds。

## Failure Modes

| 模式 | 应对 |
| --- | --- |
| 单一 IAgentTransport 无法选路 | 改 Registry |
| Oar 无 Executable 被 Biz 拒 | Biz 对 Oar 改校验 RuntimeId |
| Session 表未登记 | AgentHubTables.EntityTypes 加入 |
| async iterator try/catch+yield | 缓冲 helper（CS1626/CS1631） |

## Interaction Design

**无界面交互**。契约/传输：

| 通道 | 入参 | 成功 | 错误 |
| --- | --- | --- | --- |
| Registry.Resolve | Transport=Oar | OarTransport | 未知且无 Cli → KeyNotFound |
| OarTransport.StreamAsync | RuntimeId+Prompt | 事件流+Session 落库 | Worker 不可用/缺 RuntimeId |
| SessionManager.Find | SessionKey | 实体 | null |

## Acceptance Criteria

- [x] AC1：Registry 选路单测绿
- [x] AC2：Session Open/Find/Close 单测绿
- [x] AC3：Transport=Oar 走 Oar 替身路由测绿
- [x] AC4：既有 DelegationRuntime 过滤集不因本批变红（环境例外除外）
- [x] AC5：032 / 架构阶段表写 P2；05–07 齐
