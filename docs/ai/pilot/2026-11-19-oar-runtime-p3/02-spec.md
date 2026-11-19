# Specification

> Task ID：`2026-11-19-oar-runtime-p3`

## Functional Requirements

| ID | 要求 |
| --- | --- |
| FR1 | AgentHubRuntimeProvider 实现 IAgentRuntime |
| FR2 | 同实例 services.AddSingleton + ctx.Register；Effect Dispose OarWorkerClient |
| FR3 | OarWorkerClient 进程级单例，供 OarTransport 与 Provider 共享 |
| FR4 | Tools：SessionKey（sessionRef 别名）；禁止回传 OarSessionId |
| FR5 | API：GET runtimes；GET/POST sessions… input/close |
| FR6 | UI：Session 页签 + 委派 SessionKey 字段 + 任务详情展示 |

## Input / Output

- In：P2 Session/Transport + P1 Client  
- Out：Provider、Controllers、Tools/UI、pilot、032 回写

## Business Rules

1. 消费者每次 Get，禁止字段缓存。  
2. 缺席 → null → 降级。  
3. SessionKey 对外唯一；OarSessionId 仅库内。

## Boundary Conditions

| 边界 | 行为 |
| --- | --- |
| Worker 不可用 | List 仍返回 Cli Agent；Oar 路径 Available=false |
| 空 Prompt | Submit 返回 Failed 句柄 |
| 未知 taskKey Cancel | Rejected not_found |

## Error Handling

控制类 AgentControlResult；Submit 失败不静默成功。

## Compatibility

IAgentDelegation / Cli 路径保持；sessionRef 别名保留。

## Interaction Design

| 通道 | 点什么 | 出现什么 |
| --- | --- | --- |
| UI Session 页签 | 点 Session | Runtime 列表 + SessionKey 列表 |
| UI 委派 | 填 SessionKey 发起 | 任务入队；详情显示 SessionKey |
| hub_ask_agent | 传 sessionKey | 续接追问；出参含 sessionKey |
| API sessions/{key}/input | POST text | ControlResult |

五态：加载/空/出错/无权限/正常 — Session 空态有文案。

## Acceptance Criteria

- [x] AC1 Provider 同实例测绿  
- [x] AC2 List/Submit/Cancel 测绿  
- [x] AC3 既有 Delegation/Transport 过滤不因本批新增红  
- [x] AC4 032/架构 P3 ✅；05–07 齐  
