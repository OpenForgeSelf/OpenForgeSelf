# ADR-007：引入 OAR 作为 AgentHub 外部 Runtime Provider，并落地 `IAgentRuntime`

- 状态：**Accepted**（2026-11-19；闸门1 = 用户 seq6「搞定上面的问题后提交一把，然后开始实现」+ 续作消息「方案落实了…并发实现」）
- 关联：ADR-001（Cordis）· ADR-005（不另起本机 Runtime）· ADR-006（MAF 一等）· `IAgentDelegation`（PILOT-054）· design pilot `2026-11-19-oar-agenthub-runtime` · P0 `2026-11-19-oar-runtime-worker-p0` · P1 `2026-11-19-oar-runtime-p1`
- 真源：[`docs/01-architecture/agenthub-oar-runtime.md`](../01-architecture/agenthub-oar-runtime.md)
- 调研：[`docs/06-research/007-oar-agenthub-runtime.md`](../06-research/007-oar-agenthub-runtime.md)

## 背景

外部 Coding Agent（Claude Code / Codex / OpenCode 等）能力与协议碎片化。AgentHub 已具备 CLI 委派与 `IAgentDelegation`，但缺少统一长会话、能力声明、控制拒绝与跨插件 Runtime 级 API。OAR 提供统一 Runtime 接口，且为 TypeScript/Node 库，不能直接嵌入 .NET。

## 决策

1. **引入 OAR**，位置限定为 **AgentHub 内部运行时 Provider**，不是宿主全局基础依赖。  
2. **调用形态**：Node.js ≥24 **长驻 Worker** + **JSON-RPC/JSONL over stdio**；依赖 `@botiverse/oar` **精确锁版本**（验证基线 `0.109.0`）。  
3. **不重写 AgentHub**：保留 Registry、DelegationRuntime、PermissionBroker、任务/事件基础；新增 `OarTransport` + `AgentTransportRegistry`；保留 `CliTransport`。  
4. **落地 `ForgeSelf.Abstractions.IAgentRuntime`**：其他插件经 `ctx.Get<IAgentRuntime>()` 使用；OAR 类型不泄漏。提供方同实例注册到 MS DI 与 `ctx.Register`。  
5. **保留 `IAgentDelegation`**：兼容 todo-tracker 等既有消费者；可作薄适配，不强制一次性迁移。  
6. **独立 Session 持久化**：对外 `SessionKey`；对内映射 OAR session；事件用铸己匣单调序号 + OAR seq + Generation。  
7. **控制结果**使用 `Accepted/Code/Reason`，能力以 Runtime 声明为准。  
8. **与 ADR-005 正交**：本决策不引入平行本机 LocalBot/MAF Runtime；本机循环仍走 `IAgent`/MAF。  
9. **分阶段 P0→P4**；第一期不做 Skills/MCP/Subagents/自动安装等（见 not-taken）。  
10. **降级**：无合格 Node/Worker 时 OAR 不可用，CLI 路径继续工作。

## 备选方案与否决理由

| 方案 | 结论 |
| --- | --- |
| .NET 直接引用 OAR | 技术栈不匹配 |
| 每任务拉起 Node 进程 | 会话与开销差 |
| Node 独立 HTTP 服务 | 端口/鉴权/生命周期更重，单机插件形态不优先 |
| 推翻 AgentHub 重写 | 丢弃已有权限/任务/探测资产，风险过高 |
| 只扩展 `IAgentDelegation` | 无法干净表达 Session/事件/控制语义，硬撑会破坏薄委派边界 |
| 各插件自接 OAR | 违反「契约进 Abstractions / 禁止隐式耦合」 |

## 后果

- 正面：统一外部 Agent 接入；跨插件可测的 Runtime 能力；Cli 与 Oar 并存可回退。  
- 负面：增加 Node≥24 运维面；OAR 版本演进快需钉版本与回归；Session 表增加 AgentHub 库结构变更。  
- 后续：P0–P4 已按 concurrency-plan 落地（pilots `…-worker-p0` / `…-p1`–`…-p4`）；当前计划终段，不另开 P5。

## 撤销 / 修订触发条件

- OAR 许可或安全模型与本机策略冲突。  
- P0 证明三大 Runtime 均无法稳定会话。  
- 出现官方 .NET 绑定且能力对等（可再评估去掉 Worker）。
