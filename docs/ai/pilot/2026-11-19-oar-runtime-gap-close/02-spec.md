# Specification

> Task ID：`2026-11-19-oar-runtime-gap-close`

## Functional Requirements

| ID | 要求 |
| --- | --- |
| FR1 | `AgentHubSession` 增加 `OarSequence`；`AgentSessionManager.Touch` 可回写 |
| FR2 | 新建表 `AgentRuntimeRecord`（最小列：SessionKey、TaskId、OarSequence、AgentHubSequence、Generation、Type、PayloadJson≤4000、Truncated、Timestamp）；登记 `AgentHubTables` |
| FR3 | `AgentRuntimeEventStore`：追加 Record 时分配全局单调 `AgentHubSequence`，更新 Session 水位；ListBySession(afterSeq) |
| FR4 | `OarTransport`（及可用路径）在收到 Worker 事件/prompt 结果时写入 Record（脱敏截断；默认不含 reasoning 正文） |
| FR5 | `OarWorkerClient` 封装 `runtime.installation|auth|models`、`session.records` |
| FR6 | API：`POST runtimes/{id}/probe`、`GET runtimes/{id}/models`、`POST sessions/{key}/abort`、`GET sessions/{key}/records`、`GET sessions/{key}/stream`（SSE 轮询增量，对齐 tasks/stream 最小面；**不**重写统一 Watch SSE） |
| FR7 | `AgentHubRuntimesController` / `AgentHubSessionsController` 类级 `[Authorize("ApiKeyPolicy")]` |
| FR8 | e2e：一条 Session 页签冒烟（切 tab → 见 Runtime/空态文案 → GET runtimes/sessions 200） |
| FR9 | 真源回写：架构 §7.2 注明最小 Record 路径；§9 端点落地；032 §6；design AC-R2..R5 勾选；audit 刷新 |

## Business Rules

1. 对外 API/DTO **禁止**回传 `OarSessionId`。  
2. Record 载荷脱敏 + 4000 截断；reasoning 正文默认不入。  
3. `AgentHubSequence` 全局（表内 max+1）单调；`OarSequence` 来自 Worker 或本地递增水位，不得替代全局序。  
4. Worker 不可用时 probe/models 返回可解释降级（Accepted=false / Available=false），不崩宿主。

## Boundary Conditions

| 边界 | 行为 |
| --- | --- |
| 未知 sessionKey | abort/records/stream → 404 |
| Worker 未启动 | probe/models → 503 或 body 标明 unavailable |
| scripted models | Worker 可返回 `not_supported`；API 原样可解释 |
| 空 Session 列表 | UI 空态文案；e2e 断言可见 |

## Interaction Design

| 通道 | 点什么 | 出现什么 |
| --- | --- | --- |
| UI Session 页签 | 点 Session | Runtime 列表或空态；Session 列表或「还没有持久化会话」 |
| API probe | POST | installation+auth 快照 |
| API abort | POST | AgentControlResult |
| API records | GET | 脱敏 Record 列表（含 AgentHubSequence/OarSequence） |
| API stream | GET SSE | 按 AgentHubSequence 增量；心跳；会话 Closed 可 done |

五态：加载/空/出错/无权限(401)/正常 — e2e 覆盖空+正常 API。

## Acceptance Criteria

- [x] AC1：Record 追加后 Session.`AgentHubSequence`/`OarSequence` 单调递增（单测）
- [x] AC2：五端点可达（过滤测或控制器测）；无 token → 401
- [x] AC3：既有 AgentHub 过滤集不因本批新增红
- [x] AC4：AgentHub web build 绿；Session e2e 冒烟绿（或 Evidence 记环境阻塞）
- [x] AC5：032 / 架构 / design AC / audit 回写；05–07 齐
