# 闸门1 六段方案 · OAR × AgentHub Runtime

> 任务：`2026-11-19-oar-agenthub-runtime`  
> 依据：用户输入1 调研方案 + 仓库事实核对  
> 本文件即对外拍板件；聊天只报路径。

## 1. 目标

把 AgentHub 从「CLI 委派中枢」升级为「外部 Coding Agent Runtime Provider」：**引入 OAR（Node Worker）+ 落地 `IAgentRuntime`**，保留 Cli 与现有权限/任务体系。  
成功判据（文档批）：ADR-007/架构真源/调研/README 速查/pilot 00–04 齐备；实现批另按 P0 探针 12 步与 Cli 回归绿判定。

## 2. 改动（最终实现定案）

1. `ForgeSelf.Abstractions` → 新增 `IAgentRuntime` + DTO（不泄漏 OAR 类型）。  
2. `Plugins/AgentHub` → `RuntimeWorker`（`@botiverse/oar@0.109.0` 精确锁）+ `OarWorkerClient`（JSON-RPC/JSONL stdio）+ `OarTransport` + `AgentTransportRegistry`。  
3. 数据 → XCode 新增 `AgentHubSession`；事件双层序号（AgentHubSequence/OarSequence/Generation）。  
4. 插件互通 → 同实例 `ctx.Register<IAgentRuntime>`；**保留** `IAgentDelegation`。  
5. 管理面/API（P3）→ Runtime/Session 视图与会话 API；`hub_ask_agent` 改 SessionKey。

## 3. 不改

- 不重写 AgentHub；不删 CliTransport。  
- 不另起本机 LocalBot/平行 MAF Runtime（ADR-005 仍有效）。  
- 第一期不做：Skills/Commands/MCP 挂载、Usage 账单、自动 Install/Upgrade、高级 Steer、Subagents、Agent 间投递。  
- 本闸门1 文档批不改业务 `.cs`。

## 4. 验证

- 本批：文档交叉链接 + `git diff` 仅 docs。  
- P0 起：Worker 探针；Scripted Runtime 单测；Cli 回归；中档含 Abstractions/宿主相关 `dotnet test`；插件 1 条冒烟 e2e（P3）。

## 5. 代价与风险

- 新增 Node≥24 运行时依赖；环境不足时 OAR 不可用但 CLI 降级。  
- Session 表与公共契约为中高风险；回滚点=未合并 P2 前保留 Cli 唯一路径 / git revert。

## 6. 待你拍板

1. **是否批准本定案作为实现依据？** 默认推荐：**批准**（含 P0 先行，P1 契约与 Worker 通信，再 P2 Session/Transport）。  
2. **是否批准后续 P2 在 AgentHub 库新增 `AgentHubSession`（及 Record 表）XCode 实体？** 默认推荐：**批准最小字段集**（Spec FR-R4）；字段膨胀另批。
