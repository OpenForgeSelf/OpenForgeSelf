# Spec

> Task ID：`2026-11-19-oar-runtime-p4`

## 1. Context

- In：P3 Provider + SessionKey；concurrency-plan T-ConsumerQA  
- Out：生产消费插件业务页、SSE 统一、独立 SessionKey 映射表实现  

## 2. Functional Requirements

| ID | 要求 |
| --- | --- |
| FR1 | 样例消费者每次 `ctx.Get<IAgentRuntime>()`；同 Register 实例 |
| FR2 | 未 Register → null → 可解释降级文案 |
| FR3 | `WorkerEntryPath` 空时 Cli AccessPoint 仍可经 Runtime Submit 至终态 |
| FR4 | `SessionDto` 无 `OarSessionId`；库内可 SessionKey≠OarSessionId |
| FR5 | Node≥24 运维写入架构 §13 + RuntimeWorker README（无绝对路径） |
| FR6 | P0–P4 阶段状态全部 ✅；不另开 P5 |

## 3. Non-Functional

- 测试零真实第三方 Agent；Fake Cli transport  
- 文档卫生：无盘符/用户主目录绝对路径  

## 4. Interaction Design

无界面交互。消费面 = `ctx.Get` / HTTP 已有 runtimes|sessions；本批不改 UI。

## 5. Data / API

不改 Schema；不改对外 HTTP 契约字段。

## 6. Risks

| 风险 | 缓解 |
| --- | --- |
| 测试夹具未 seed `IServiceProvider` | 对齐 Delegation 解析测姿势 |
| 文档写入绝对路径 | hygiene + 相对路径约定 |

## 7. Acceptance Criteria

- [x] AC1 Consumer Get/降级/Cli fallback 测绿  
- [x] AC2 Session DTO / SessionKey 解耦守卫绿  
- [x] AC3 既有 AgentHub Runtime 过滤集不因本批新增红  
- [x] AC4 Worker smoke 12/12  
- [x] AC5 032 / 架构 / concurrency / seams 勾 P4；05–07 齐  

## 8. Unknowns

无（范围由用户 checkpoint 钉死）。

## 9. Not-to-do

Watch SSE 大改；独立 GUID 映射表落地；P5；自动 install；停宿主。
