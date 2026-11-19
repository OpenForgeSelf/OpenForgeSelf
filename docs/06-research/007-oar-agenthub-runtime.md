# 007 · Open Agent Runtime（OAR）调研与 AgentHub 落点核对

> 日期：2026-11-19  
> 状态：已定案（实现前文档；裁决见 [ADR-007](../07-decisions/007-oar-agenthub-runtime.md)）  
> 输入：用户提供的 OAR 调研与改造方案全文  
> 架构真源：[agenthub-oar-runtime.md](../01-architecture/agenthub-oar-runtime.md)  
> pilot：`docs/ai/pilot/2026-11-19-oar-agenthub-runtime/`

## 1. OAR 是什么（调研转述）

OAR（[botiverse/oar](https://github.com/botiverse/oar)）是面向 Coding Agent 的**统一运行时接口库**（npm `@botiverse/oar`，Apache-2.0）。它屏蔽 Claude Code、Codex、OpenCode 等 Harness 差异，统一：启动、会话、输入投递、事件、控制、安装/认证/模型查询等。

它**不是**：独立 Agent、模型 API 网关、完整编排平台。

验证基线版本：**`0.109.0`（精确锁定，禁止 `^`）**。Cursor 等部分 Runtime 需额外 SDK，不能假定全部开箱即用。Node 引擎要求：**≥24**。

核心设计：**统一接口，但不强行统一能力**——Runtime 声明能力；不支持则明确拒绝；不静默忽略选项。

事件模型：Record Stream = Frame / Request / Response；含 seq、sessionId、agentPath、requestId 等；支持续读游标。Session 恢复会开启**新记录流（seq 从 0）**。

## 2. 与铸己匣仓库事实的差距（2026-11-19 实测）

| 调研稿表述 | 仓库事实 | 定案纠偏 |
| --- | --- | --- |
| 存在可对照的 `IAgentLoop` | `IAgentLoop` 已于 dsh B5/B9 **删除**；本机驱动为 `IAgent`/`IAgentRegistry`（`Agents.cs`） | `IAgentRuntime` 对位的是**外部 Coding Agent Runtime**，对照物写 `IAgent`（本机）+ `IAgentDelegation`（外部薄委派），不要再写 `IAgentLoop` |
| `IAgentRuntime` 已规划待落地 | `cordis-kernel.md` 列为未落地；Abstractions **无**该类型 | 本定案落地该接缝 |
| 插件间尚无能力 | 已有 `IAgentDelegation`（入队+读回），todo-tracker 在用 | **保留** Delegation；新增更完整的 `IAgentRuntime`，二者共存 |
| Transport 可扩展 Acp/Http | 注释/Model 有 Cli\|Acp\|Http；**实现仅 CliTransport** | 新增 `Oar`；Acp/Http 仍为预留 |
| 应用有独立 Session 表 | 仅 `DelegationTask.SessionRef` + 内存 `AgentSession` | 新增 `AgentHubSession` + `SessionKey` |
| 可直接 .NET 引 OAR | OAR 为 TS ESM + `node:child_process` | **Node 长驻 Worker + stdio JSON-RPC/JSONL** |

AgentHub 现状能力（保留并升级，不推翻）：Agent 注册、CLI 探测、委派执行、任务持久化、事件回放、SSE、取消、PermissionBroker、`hub_*` 工具。

## 3. 推荐方案（与 ADR-007 一致）

**AgentHub + OAR Node Worker + `ForgeSelf.Abstractions.IAgentRuntime`**

原则：

1. OAR 只做 AgentHub 内部运行时实现。  
2. AgentHub 继续持有注册、调度、权限、持久化、审计、对外服务。  
3. 其他插件只依赖 Abstractions 契约。  
4. OAR 类型不泄漏出 AgentHub。

通信：长期 Worker + stdio；延迟启动；`ctx.Effect` 释放；不用插件级 `IHostedService` 另搞一套。

## 4. 与 ADR-005 的关系

ADR-005 禁止另起 **本机 LocalBot / 平行 MAF Agent Runtime**。  
本方案引入的是 **外部 CLI/Harness 的统一适配**，不替代 `MafTaskLoopAgent` / `IAgent`。二者正交，不冲突。

## 5. 第一期范围 / 缓做

**第一期**：Runtime 探测、安装/认证状态、模型查询、Session 开/恢复、Prompt、实时事件、取消、`IAgentRuntime` 跨插件发布、Cli 并存。

**缓做**（见 not-taken）：Skills/Commands/MCP 管理与挂载、Account Usage、自动 Install/Upgrade 流水线、高级 Steering、Subagents、Agent 间消息。

## 6. 参考链接

- https://github.com/botiverse/oar  
- https://github.com/botiverse/oar/blob/main/docs/spec/README.md  
- https://www.npmjs.com/package/@botiverse/oar  
- 仓内既有外部委派调研：`docs/06-research/002-cli-agent-detection-delegation.md`
