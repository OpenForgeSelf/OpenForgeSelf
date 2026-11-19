# AgentHub × Open Agent Runtime（OAR）架构真源

> 状态：**已定案（ADR-007 Accepted）｜实现进行中**｜2026-11-19  
> ADR：[007-oar-agenthub-runtime.md](../07-decisions/007-oar-agenthub-runtime.md)  
> 调研：[007-oar-agenthub-runtime.md](../06-research/007-oar-agenthub-runtime.md)  
> 功能档案：[032-agent-hub.md](../02-features/032-agent-hub.md)  
> design pilot：`docs/ai/pilot/2026-11-19-oar-agenthub-runtime/`  
> 并发计划：同目录 [`concurrency-plan.md`](../ai/pilot/2026-11-19-oar-agenthub-runtime/concurrency-plan.md)  
> 阶段 pilots：P0 `2026-11-19-oar-runtime-worker-p0`（✅）· P1 `2026-11-19-oar-runtime-p1`

本文是「外部 Coding Agent 如何经铸己匣统一运行」的**唯一架构真源**。实现与本文冲突时，先改本文或提 ADR，禁止静默分叉。

---

## 1. 一句话

AgentHub 升级为外部 Agent **Runtime Provider**：内部用 **OAR Node Worker** 适配多种 Harness；对外只暴露 **`IAgentRuntime`（及既有 `IAgentDelegation`）**；本机 MAF/`IAgent` 循环保持独立。

## 2. 目标架构

```text
其他插件 (Workflow / AIAgent / SEMS / …)
        │  ctx.Get<IAgentRuntime>()
        │  ctx.Get<IAgentDelegation>()   ← 保留薄委派
        ▼
ForgeSelf.Abstractions  (契约 + DTO，无 OAR 类型)
        │
        ▼
AgentHub Plugin
  ├── AgentRegistry / AccessPoint (Transport + RuntimeId)
  ├── AgentTransportRegistry
  │     ├── CliTransport          (保留)
  │     └── OarTransport          (新增)
  ├── DelegationRuntime           (任务状态机 / 权限 / cwd)
  ├── AgentSessionManager         (SessionKey ↔ OarSessionId)
  ├── EventStore (双层序号)
  └── AgentHubRuntimeProvider     (实现 IAgentRuntime；同实例 Register)
        │  JSON-RPC/JSONL over stdio
        ▼
RuntimeWorker (Node ≥24, @botiverse/oar 精确锁版本)
        │
        ├── Codex / Claude Code / OpenCode / …
```

## 3. 职责边界

| 组件 | 负责 | 不负责 |
| --- | --- | --- |
| OAR Worker | Harness 适配、原生 Session、原始 Record | 任务调度、权限策略、铸己匣持久化、跨插件契约 |
| AgentHub | 注册、选路、权限、Session/Task 持久化、事件归一、审计、API/UI、Provider | 实现本机 MAF 循环 |
| `IAgentRuntime` | 跨插件 Runtime 能力 | OAR/CLI 细节 |
| `IAgentDelegation` | 入队 + 读回（兼容既有消费者） | Session 控制全集 |
| `IAgent` / MAF | 本机智能体 turn | 外部 Claude/Codex 进程 |

## 4. 关键决策摘要

| 决策项 | 定案 |
| --- | --- |
| 是否引入 OAR | 是 |
| OAR 位置 | AgentHub 内部 Provider |
| 调用方式 | Node≥24 长驻 Worker + stdio JSON-RPC/JSONL |
| OAR 版本 | 验证基线 `0.109.0` 精确锁定 |
| AgentHub | 升级不重写 |
| Transport | 新增 `Oar`；保留 `Cli`；Acp/Http 预留 |
| Session | 独立表；对外 `SessionKey` |
| Event | 存原始 Record + 上层事件；自增 `AgentHubSequence` |
| 控制结果 | `AgentControlResult`，禁止纯 bool |
| 公共接口 | `IAgentRuntime` ∈ Abstractions |
| 注册 | 同实例 → MS DI + `ctx.Register`；清理走 `ctx.Effect` |
| 数据层 | NewLife.XCode（AgentHub 库） |
| 第一批 Runtime | Codex、Claude Code、OpenCode |
| 降级 | 无 Node/Worker 失败 → OAR unavailable，Cli 仍可用 |

## 5. 契约草图（实现以 Abstractions 落码为准）

```csharp
// ForgeSelf.Abstractions — 示意，非已编译源码
public interface IAgentRuntime
{
    Task<IReadOnlyList<AgentDescriptor>> ListAgentsAsync(CancellationToken ct = default);
    Task<AgentTaskHandle> SubmitAsync(AgentTaskRequest request, CancellationToken ct = default);
    Task<AgentTaskSnapshot?> GetTaskAsync(string taskKey, CancellationToken ct = default);
    IAsyncEnumerable<AgentTaskEvent> WatchTaskAsync(string taskKey, long afterSequence = 0, CancellationToken ct = default);
    Task<AgentControlResult> SendInputAsync(string sessionKey, AgentInput input, CancellationToken ct = default);
    Task<AgentControlResult> CancelAsync(string taskKey, CancellationToken ct = default);
    Task CloseSessionAsync(string sessionKey, CancellationToken ct = default);
}
```

规则：

- 不暴露 OAR `SessionOptions` / Record 原始 TS 类型。  
- 权限字段只能在 AgentPolicy 允许范围内生效。  
- 消费者每次 `ctx.Get<IAgentRuntime>()`，禁止字段缓存。  
- 缺席 → null → 消费方降级（503/可解释文案），禁止静默当成功。

`IAgentDelegation`：**保留**；可由同一 Provider 适配层转发到 DelegationRuntime，契约方法不强制破坏。

## 6. Transport 与配置

`AgentAccessPoint.Transport`：`Cli` | `Oar` |（预留 `Acp`/`Http`）。  
新增 `RuntimeId`（对应 OAR registry id），**禁止**塞进 `Vendor`（Vendor 仍服务 profile/厂商配置）。

`DelegationRuntime` 经 `IAgentTransportRegistry` 按 Transport 选路，禁止再 DI 单一 `IAgentTransport` 导致多实现无法选路。

## 7. Session 与事件

### 7.1 `AgentHubSession`（P2）

关键字段：`SessionKey`（对外 GUID）、`AgentId`、`AccessPointId`、`Transport`、`RuntimeId`、`OarSessionId`、`Cwd`、`Model`、`Effort`、`OptionsJson`（无密钥）、`Status`、`Generation`、`LastTaskId`、时间戳。

关系：一个 Session 多轮 Task；Task 结束 ≠ Session 结束；恢复 Session ≠ 恢复旧进程。

### 7.2 双层事件

- 底层 `AgentRuntimeRecord`：完整审计（含脱敏后的原始载荷）。  
- 上层 `AgentRuntimeEvent` / 任务事件：供 UI 与插件消费。  
- 序号：`AgentHubSequence`（全局单调）+ `OarSequence` + `Generation`（每次 open/resume +1）。

默认不向插件/前端暴露模型内部 reasoning 正文。

## 8. Worker 协议与生命周期

- 方法面：`runtime.list|installation|auth|models|usage`，`session.open|resume|prompt|deliver|control|records|dispose`。  
- stdout=协议，stderr=日志；多路并发 + 请求 id；超时与崩溃监控；进程树终止。  
- 延迟启动；插件卸载 `ctx.Effect` 释放。  
- **禁止**启动时自动 `install` 第三方 Agent；只读 `installPlan` 可先做，执行须用户确认。  
- 模型探测等工作目录必须落在受信任/白名单路径。

## 9. API / UI（P3，保留现页扩展）

新增（示意）：

| 方法 | 路径 | 功能 |
| --- | --- | --- |
| GET | `api/agent-hub/runtimes` | Runtime 列表 |
| GET | `api/agent-hub/runtimes/{id}` | 详情 |
| POST | `api/agent-hub/runtimes/{id}/probe` | 安装+认证探测 |
| GET | `api/agent-hub/runtimes/{id}/models` | 模型 |
| POST/GET | `api/agent-hub/sessions`… | 会话 CRUD/输入/abort/records/stream |

现有 `api/agent-hub/tasks` 保留。鉴权：类级 `ApiKeyPolicy`（与 032 待核对项一并闭合）。

## 10. 安全约束（不可因 OAR 省略）

工作目录白名单；权限 ⊆ Policy；禁止默认无限权限；Worker 不拼 shell；不落第三方 Token 到事件；载荷脱敏与长度限制；取消必清子进程；多插件不得绕过 AgentHub 直控 Worker。

## 11. 实施阶段

| 阶段 | 内容 | 另开 pilot | 状态 |
| --- | --- | --- | --- |
| P0 | Worker 骨架 + OAR 探针（12 步） | `2026-11-19-oar-runtime-worker-p0` | ✅ Worker + smoke 12/12 |
| P1 | `IAgentRuntime` 契约 + `OarWorkerClient` stdio JSON-RPC / 生命周期 | `2026-11-19-oar-runtime-p1` | ✅ |
| P2 | Transport 注册表、RuntimeId、Session、事件、Delegation 选路 | 另开 | ⬜ |
| P3 | Provider 注册、工具 SessionKey、管理面/API | 另开 | ⬜ |
| P4 | 消费者试点、测试体系、打包/Node 检查文档 | 另开 | ⬜ |

详细完成标准见 pilot `02-spec.md` / 用户调研方案第九章（已采纳）。

## 12. 审慎不做

见 `docs/07-decisions/not-taken-decisions.md` 条目「OAR 第一期缓做能力」。
