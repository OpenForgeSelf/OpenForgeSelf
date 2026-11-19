# OAR × AgentHub 并发实现计划（组件团队）

> 归属：design pilot `2026-11-19-oar-agenthub-runtime` 附录  
> 真源：`docs/01-architecture/agenthub-oar-runtime.md` · ADR-007 · design `02-spec` / `03-plan`  
> 闸门1：已批准｜P0：✅ `2026-11-19-oar-runtime-worker-p0`

## 1. 原则

1. **阶段串行，阶段内可并行**：P0→P1→P2→P3→P4 有依赖；同阶段内按组件团队拆轨，**合并必须串行**（同一 PR 分支线性提交）。  
2. **不破坏 Cli / `IAgentDelegation`**：任何阶段 verdant 后须能跑既有 AgentHub Cli 过滤测试。  
3. **禁止**自动 install 第三方 Agent；**禁止**停/杀用户宿主。  
4. 每阶段独立 pilot `00–07`（或允许的 mini-task），commit trailer `Pilot: <dir>`。

## 2. 组件团队（角色）

| 团队 ID | 职责 | 主要路径 |
| --- | --- | --- |
| **T-Contract** | Abstractionseds 契约与 DTO；无 OAR 类型泄漏 | `ForgeSelf.Abstractions/AgentRuntimeContracts.cs` |
| **T-WorkerClient** | Node Worker 进程生命周期 + stdio JSON-RPC 客户端 | `Plugins/AgentHub/Services/OarWorkerClient.cs`（及 options/协议 POCO） |
| **T-Transport** | `AgentTransportRegistry` / `OarTransport` / AccessPoint.RuntimeId | `Plugins/AgentHub/Services/*Transport*`、实体/Model.xml（RuntimeId） |
| **T-Session** | `AgentHubSession` XCode、事件双层序号、SessionKey 映射 | `Plugins/AgentHub/Data/**`、EventStore |
| **T-Delegation** | DelegationRuntime 按 Transport 选路；Cli 回归 | `DelegationRuntime.cs`、既有测试 |
| **T-Provider** | `AgentHubRuntimeProvider` 实现 `IAgentRuntime`；`ctx.Register` | `AgentHubPlugin.cs`、Provider 类 |
| **T-Surface** | API / UI / `hub_ask_agent` SessionKey | Controllers、`Plugins/AgentHub/web/**`、Tools |
| **T-ConsumerQA** | 消费者试点、过滤/中档测试、Node≥24 打包说明 | 消费插件 pilot、docs、CI notes |

## 3. 阶段依赖与并行轨

```text
P0 ✅ Worker
    │
    ▼
P1 ──┬── T-Contract（Abstractionseds IAgentRuntime）     ⎫ 可并行开工
     └── T-WorkerClient（OarWorkerClient + 集成测）   ⎭ 合并：Contract 先于/同批 Client 测试引用
    │
    ▼
P2 ──┬── T-Transport（Registry + OarTransport + RuntimeId）⎫ 可并行
     ├── T-Session（XCode Session + 事件序号）            ⎬ 合并：Schema 先合，再接 Transport/Delegation
     └── T-Delegation（选路接线 + Cli 回归）              ⎭ 依赖 SessionKey/Transport 字段就绪
    │
    ▼
P3 ──┬── T-Provider（Register + Effect 清理）  ⎫ Provider 依赖 P1 契约 + P2 Session/Transport
     └── T-Surface（API/UI/工具）              ⎭ Surface 依赖 Provider 行为稳定；可错峰
    │
    ▼
P4 ── T-ConsumerQA（试点 + 测试体系 + Node 打包）  依赖 P3 可 Get
```

| 阶段 | 可并行 | 必须串行（相对前序） | 完成标准（摘要） |
| --- | --- | --- | --- |
| **P1** | Contract ∥ WorkerClient | 依赖 P0 Worker | 契约可编译；Client `hello`+`runtime.list`+scripted open/prompt/dispose；Abstractions 无 OAR 类型；不改 Cli |
| **P2** | Transport ∥ Session；Delegation 尾随 | 依赖 P1 Client | Registry 选路；Session 持久化；Cli 过滤测试绿；不注册完整 Provider 亦可 |
| **P3** | Provider 先稳，Surface 跟 | 依赖 P2 | `ctx.Get<IAgentRuntime>()`；工具 SessionKey；API/UI 最小面 |
| **P4** | 单轨 | 依赖 P3 | 消费者试点 + 中档测试 + Node≥24 说明 |

## 4. 本轮（Cloud）执行裁剪

- **立即**：写本计划 + 开 P1 pilot → 实现 P1（Contract + Client；单人可串行写，逻辑上仍两轨）。  
- **P1 绿后**：同 run 仅当范围仍 coherent 才开 P2；否则干净检查点交接 T-Transport / T-Session。  
- 不在本轮做 P3 UI / P4 打包正文（可预登 TODO）。

## 5. 验证分档（对齐 AGENTS §5.6）

| 阶段 | 档 | 命令焦点 |
| --- | --- | --- |
| P1 | 快+相关过滤 | `dotnet build`；`dotnet test --filter` OarWorker/AgentRuntime；可选 Worker `pnpm run smoke` |
| P2 | 快 + AgentHub Cli 过滤；碰 Model.xml 则相关集成 | CliTransport / DelegationRuntime 过滤集 |
| P3 | 快 + 插件冒烟 e2e 1 条 | `e2e/plugins/agent-hub` 一条主链路 |
| P4 | 中（碰宿主/脚本时） | 全量 `dotnet test` 子集 + 打包说明自检 |

## 6. 交接清单（阶段结束）

- [ ] 本阶段 pilot 05/06/07 齐，AC 勾选  
- [ ] 032 §6 进度句更新  
- [ ] ADR/架构真源阶段表勾选  
- [ ] 下一团队：Allowed/Forbidden 文件表 + 阻塞项  
