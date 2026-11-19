# Plan

> 阶段：Stage 3｜本批仅文档/决策；实现步骤按 P0–P4 预登，供后续 pilot 引用。  
> Task ID：`2026-11-19-oar-agenthub-runtime`

## Files To Change（本批）

- file: `docs/06-research/007-oar-agenthub-runtime.md`  
  reason: 调研归档 + 与仓库事实纠偏
- file: `docs/01-architecture/agenthub-oar-runtime.md`  
  reason: OAR/AgentHub 目标架构唯一真源
- file: `docs/07-decisions/007-oar-agenthub-runtime.md`  
  reason: ADR 固化最终决策
- file: `docs/07-decisions/not-taken-decisions.md`  
  reason: 登记第一期缓做项
- file: `docs/01-architecture/cordis-kernel.md`  
  reason: `IAgentRuntime` 从「未落地」改为「已定案待实现」并指向真源
- file: `docs/01-architecture/host-capability-seams.md`  
  reason: 登记 `IAgentRuntime` L1 接缝与同实例规则
- file: `docs/02-features/032-agent-hub.md`  
  reason: 功能档案增加 OAR 演进节
- file: `docs/README.md`  
  reason: §一 30 秒速查加行
- file: `docs/ai/pilot/2026-11-19-oar-agenthub-runtime/*`  
  reason: 00–04 + gate1-proposal（及本批完成后的 05–07）

## Files Forbidden（本批）

- `Plugins/AgentHub/**/*.cs`、`Plugins/AgentHub/web/src/**`（业务实现）
- `ForgeSelf.Abstractions/**/*.cs`（契约代码）
- `Plugins/AgentHub/RuntimeWorker/**`（尚未创建；P0 另批）
- 任意发布/停宿主进程

## Implementation Steps（本批）

1. 落盘调研 007、架构真源、ADR-007、缓做条目。
2. 回写 cordis-kernel / host-capability-seams / 032 / README。
3. 写齐 pilot 00–04 + `gate1-proposal.md`（六段）。
4. 自检交叉链接与「无业务代码 diff」。
5. 补 05-evidence / 06-review / 07-final-report；提交文档 PR。

## 后续实现预登（批准后另开 pilot，不在本批改代码）

| 阶段 | 主要文件（预期） | 完成标准 |
| --- | --- | --- |
| P0 | `Plugins/AgentHub/RuntimeWorker/**` | Node24+、锁 OAR 0.109.0、探针 12 步通过 |
| P1 | `ForgeSelf.Abstractions/IAgentRuntime*.cs`；`Plugins/AgentHub/Runtime/OarWorkerClient.cs` 等 | 契约无 OAR 类型泄漏；stdio JSON-RPC 稳定 |
| P2 | `AgentTransportRegistry`/`OarTransport`；`Data/Model.xml` Session；`DelegationRuntime` 选路 | Cli 回归 + Oar Session/事件序 |
| P3 | `AgentHubRuntimeProvider`；`ctx.Register`；工具 SessionKey；API/UI | `Get<IAgentRuntime>()` 可用 |
| P4 | 消费者试点 + 测试 + 打包说明 | 干净环境可降级/可运行 |

## Test Plan（本批）

1. `git diff --name-only`：仅 `docs/**`（及必要索引）。
2. 人工打开 README 速查行 → 能跳到架构/ADR。
3. Grep：`IAgentRuntime` 在文档中有定义与边界；业务 `.cs` 仍无该符号（本批）。

## Verification

### Build

```bash
# 本批无代码编译要求；可选确认仓库仍可 build（不作为本批必过门）
```

### Unit Test

N/A（无代码变更）

### Other

```bash
test -f docs/01-architecture/agenthub-oar-runtime.md
test -f docs/07-decisions/007-oar-agenthub-runtime.md
rg -n "agenthub-oar-runtime|007-oar-agenthub" docs/README.md
```

## Risks

| 风险 | 缓解 |
| --- | --- |
| 用户误以为本批已含实现 | 04-task Forbidden + 汇报标明 NOT_COMPLETED 实现、COMPLETED 文档 |
| Node 22 环境无法做 P0 | Spec Unknown + 前置条件写入架构真源 |
| Session 表被当成「已批准即可直接改库」 | 闸门1 待拍板显式问「是否批准 P2 XCode 实体扩展」 |
