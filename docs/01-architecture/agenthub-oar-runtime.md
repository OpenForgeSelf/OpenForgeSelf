# AgentHub × Open Agent Runtime（OAR）架构真源

> 状态：**已定案（ADR-007 Accepted）｜P0–P4 + gap-close 主路径已落地**｜2026-11-19  
> ADR：[007-oar-agenthub-runtime.md](../07-decisions/007-oar-agenthub-runtime.md)  
> 调研：[007-oar-agenthub-runtime.md](../06-research/007-oar-agenthub-runtime.md)  
> 功能档案：[032-agent-hub.md](../02-features/032-agent-hub.md)  
> design pilot：`docs/ai/pilot/2026-11-19-oar-agenthub-runtime/`  
> 并发计划：同目录 [`concurrency-plan.md`](../ai/pilot/2026-11-19-oar-agenthub-runtime/concurrency-plan.md)  
> 阶段 pilots：P0 `…-worker-p0`（✅）· P1 `…-p1`（✅）· P2 `…-p2`（✅）· P3 `…-p3`（✅）· P4 `…-p4`（✅）· gap-close `2026-11-19-oar-runtime-gap-close`（✅）

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

**SessionKey ≠ Worker 侧 key（映射边界）**：库内允许 `SessionKey` 与 `OarSessionId` 取不同值；对外 API/Tools/UI **只暴露 SessionKey**，禁止回传 `OarSessionId`（守卫见 P4 消费者试点测）。当前 `OarTransport` 常把 Worker 返回的 sessionKey **对齐**为对外 SessionKey（便于 cancel）；若未来需要稳定公共 GUID 与 Worker 内 id 解耦，只扩展 `AgentSessionManager` 映射，**不**改对外契约字段名。P4 不落地独立映射表、不重写 Watch SSE。

关系：一个 Session 多轮 Task；Task 结束 ≠ Session 结束；恢复 Session ≠ 恢复旧进程。

### 7.2 双层事件

- 底层 `AgentRuntimeRecord`：完整审计（含脱敏后的原始载荷）。**最小耐久路径（gap-close）已落地**：XCode 表 `AgentRuntimeRecord`（SessionKey / OarSequence / AgentHubSequence / Generation / Type / PayloadJson≤4000 / Truncated）；经 `AgentRuntimeEventStore.Append` 写入并回写 Session 水位。**未做**：长期留存策略、跨会话全局归档压缩、reasoning 全文可选开关（默认仍省略）。  
- 上层任务事件：继续由 `DelegationEvent`（任务 SSE / Watch 轮询）承担；不把 OAR seq 当全局序。  
- 序号：`AgentHubSequence`（表内全局单调）+ Session.`OarSequence` 水位 + `Generation`（每次 open/resume +1）。

默认不向插件/前端暴露模型内部 reasoning 正文。

## 8. Worker 协议与生命周期

- 方法面：`runtime.list|installation|installPlan|install|auth|models|usage`，`session.open|resume|prompt|deliver|control|records|dispose`。  
- stdout=协议，stderr=日志；多路并发 + 请求 id；超时与崩溃监控；进程树终止。  
- 延迟启动；插件卸载 `ctx.Effect` 释放。  
- **禁止**启动时自动 `install` 第三方 Agent；只读 `installPlan` 可先做，执行须用户确认。  
- 模型探测等工作目录必须落在受信任/白名单路径。

## 9. API / UI（P3 + gap-close）

| 方法 | 路径 | 功能 | 状态 |
| --- | --- | --- | --- |
| GET | `api/agent-hub/runtimes` | Runtime 列表 | ✅ P3 |
| GET | `api/agent-hub/runtimes/{id}` | 详情 | ✅ P3 |
| POST | `api/agent-hub/runtimes/{id}/probe` | 安装+认证只读探测 | ✅ gap-close |
| GET | `api/agent-hub/runtimes/{id}/models` | 模型 | ✅ gap-close |
| GET | `api/agent-hub/runtimes/{id}/install-plan` | 只读安装计划 | ✅ remaining-close |
| POST | `api/agent-hub/runtimes/{id}/install` | 执行安装（须 confirmed=true） | ✅ remaining-close |
| GET | `api/agent-hub/sessions` | 会话列表 | ✅ P3 |
| GET | `api/agent-hub/sessions/{key}` | 详情 | ✅ P3 |
| POST | `api/agent-hub/sessions/{key}/input` | 投递输入 | ✅ P3 |
| POST | `api/agent-hub/sessions/{key}/abort` | abort | ✅ gap-close |
| GET | `api/agent-hub/sessions/{key}/records` | 脱敏 Record | ✅ gap-close |
| GET | `api/agent-hub/sessions/{key}/stream` | Record SSE（轮询最小面） | ✅ gap-close |
| POST | `api/agent-hub/sessions/{key}/close` | 关闭 | ✅ P3 |

现有 `api/agent-hub/tasks` 保留。鉴权：`runtimes`/`sessions`/`agents`/`tasks`/`settings` 均类级 `ApiKeyPolicy`（remaining-close 闭合 agents/tasks）。管理面 Session/Runtime UI 已接线 probe/models/install 确认与 input/abort/close/records/stream。

## 10. 安全约束（不可因 OAR 省略）

工作目录白名单；权限 ⊆ Policy；禁止默认无限权限；Worker 不拼 shell；不落第三方 Token 到事件；载荷脱敏与长度限制；取消必清子进程；多插件不得绕过 AgentHub 直控 Worker。

## 11. 实施阶段

| 阶段 | 内容 | 另开 pilot | 状态 |
| --- | --- | --- | --- |
| P0 | Worker 骨架 + OAR 探针（12 步） | `2026-11-19-oar-runtime-worker-p0` | ✅ Worker + smoke 12/12 |
| P1 | `IAgentRuntime` 契约 + `OarWorkerClient` stdio JSON-RPC / 生命周期 | `2026-11-19-oar-runtime-p1` | ✅ |
| P2 | Transport 注册表、RuntimeId、Session、Delegation 选路 | `2026-11-19-oar-runtime-p2` | ✅ |
| P3 | Provider 注册、工具 SessionKey、管理面/API | `2026-11-19-oar-runtime-p3` | ✅ |
| P4 | 消费者试点、测试体系、打包/Node 检查文档 | `2026-11-19-oar-runtime-p4` | ✅ |
| gap-close | 双层事件最小耐久 + §9 端点 + Session e2e | `2026-11-19-oar-runtime-gap-close` | ✅ |

详细完成标准见 pilot `02-spec.md` / 用户调研方案第九章（已采纳）。

## 12. 审慎不做

见 `docs/07-decisions/not-taken-decisions.md` 条目「OAR 第一期缓做能力」。

## 13. 运维与打包（Node ≥24 · RuntimeWorker）

> 操作细则真源：`Plugins/AgentHub/RuntimeWorker/README.md`。本节只固化跨文档契约，禁止复制绝对路径。

| 项 | 约定 |
| --- | --- |
| 运行时 | **Node.js ≥ 24**（`package.json` `engines.node`；OAR 0.109 要求） |
| 包管理 | 在 `Plugins/AgentHub/RuntimeWorker` 内用 **pnpm**（与仓根一致） |
| 锁版本 | `@botiverse/oar@0.109.0` 精确锁定；升级须另开 pilot + ADR 备注 |
| 构建 | `pnpm install` → `pnpm run build`；产物 `dist/`（gitignore，发布随插件包或开发机现编） |
| 探针 | `pnpm run smoke`（12 步 scripted，零第三方 Agent 二进制） |
| 启动 | 宿主经 `OarWorkerClient` 拉起 `node <WorkerEntry>`；**禁止**启动时自动 `install` 第三方 Agent |
| CI / Cloud | 默认镜像若仅 Node 22，须另备 Node ≥24 工具链再跑 Worker smoke/build；**不要**为此把仓根 Node 引擎整体抬到 24（宿主前端仍按仓根约定） |
| Linux 测宿主 | 过滤/单测若碰 WindowsDesktop 宿主 `runtimeconfig`，沿用既有 strip 夹具；与 Node 版本无关 |
| 降级 | Worker 不可用或未配置 `WorkerEntryPath` 时，`Transport=Cli` AccessPoint 仍须可 Submit（P4 消费者试点覆盖） |

插件 zip / 宿主 release 链路仍走 `packaging-upgrade-backup.md`；RuntimeWorker 作为 AgentHub 插件内嵌目录随插件版本走，不单独成宿主 versions 层。
