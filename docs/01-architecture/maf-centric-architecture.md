# 01-architecture — MAF 一等公民架构与命名体系

> 状态：提案生效中（2026-11-02。闸门1 已批两项：路由不改；插件名「智能体」。**P0 已落地（2026-11-02 走查批实证）**：审批链=PersistentApprovalService+ToolApproval 实体+ApprovalsController+ProjectController 拦截（浏览器走查全链通，截图 05-approval-approved-executed）；取消杀进程=RunTerminalCommandTool 超时杀子进程（新增专项测试）；执行上下文=执行记录面板。§4 表中「审批协议缺口 / 取消杀进程缺口」两行自此过时。**本批 05/06/07 工件与提交入库待收口**）
> 最后更新：2026-11-02（晚间走查批回写 P0 状态）
> 关联：[`../00-vision/01-vision.md`](../00-vision/01-vision.md) · [ADR-006](../07-decisions/006-maf-first-class-greenfield.md) · [ADR-005](../07-decisions/005-maf-local-agent-reuse.md) · [`dsh-runtime-architecture.md`](dsh-runtime-architecture.md)（历史 dsh 图，语义被本文件超集） · [`../02-features/021-ai-agent.md`](../02-features/021-ai-agent.md)

## 1. 目标

把铸己匣组织成 **本机个人 Agent 平台**。用户可见插件名是 **智能体**；执行中枢技术名是 **MAF**（架构层 L2 仍称 **MAF Runtime**）：

- 交互层提交 Task；
- MAF Runtime 编排循环；
- ToolRegistry 做 Permission → Approval → Execute → Verify → Result；
- 本机能力全部进 Cordis 插件。

**禁止**：另起 LocalBot / 平行 Orchestrator / 第三套 loop。

## 2. 四层（思想保留；L2 仍称 MAF Runtime，L1 对外称智能体）

```
┌─────────────────────────────────────────────────────────────┐
│ L1 交互工作台（智能体）                                       │
│  Chat / Composer / 审批卡 / Run 记录 / 任务表单               │
│  —— Chat 只是入口之一；驱动单元是 Task/Run                    │
│  —— 菜单/标题/功能清单用「智能体」，不把 MAF 当插件名          │
├─────────────────────────────────────────────────────────────┤
│ L2 MAF Runtime（执行中枢）                                    │
│  MafTaskLoopAgent（默认）· RunOrchestrator（计划任务）         │
│  SessionEvent 轨迹 · AgentRun（计划任务持久化）               │
│  —— 废弃旁路：ReactLoopAgent / ad-hoc 无日志循环（勿扩）       │
├─────────────────────────────────────────────────────────────┤
│ L3 执行安全内核（宿主）                                       │
│  ToolRegistry 六闸门 · IApprovalService · Cancel/Signal       │
│  IContext / EventBus / Fiber（Cordis）                        │
├─────────────────────────────────────────────────────────────┤
│ L4 本机能力插件（Cordis Plugins）                             │
│  文件 · 终端 · Skills · Scheduler/Routines · Browser · Memory │
│  MCP 中心 · 工作流 · … —— 只注册工具/服务，不自建 Runtime      │
└─────────────────────────────────────────────────────────────┘
```

| 层 | 职责 | 不职责 |
|----|------|--------|
| L1 | 入口、可视化、审批交互、Run 回放 | 不实现工具语义、不自建循环 |
| L2 | 任务循环、出口工具、计划逐步推进、轨迹 | 不实现文件系统/浏览器等能力本体 |
| L3 | 权限/审批/执行/结果冻结、取消传播 | 不承载业务插件逻辑 |
| L4 | 具体本机能力 + `IToolFunctionExtension` | 不另起 Agent loop / 平行状态机 |

## 3. 新命名体系（绿场）

> 原则：**产品文案与架构术语分开**。对外插件名是 **智能体**；执行中枢、Runtime、类名仍称 **MAF**。插件 Id 与路由已拍板不改（不改为 `/maf`）。DB / 枚举值重命名未授权。**不做迁移兼容层**（ADR-006）。

### 3.1 产品 / 对外

| 旧名 | 新名 | 说明 |
|------|------|------|
| AI Agent（功能品牌） | **智能体（执行中枢 MAF）** | 菜单/标题/README/功能清单用「智能体」；MAF 只作执行中枢技术名 |
| FreeLoop / 自由循环 | **开放任务**（Open Task） | Chat/会话入口驱动；默认 `MafTaskLoopAgent`（MAF Runtime） |
| PlanDriven / 计划驱动 | **计划任务**（Planned Task） | `POST .../runs` + `AgentRun` |
| ReactLoop / ReactLoopAgent | **Legacy Loop（废弃）** | 仅 `UseMafRuntime=false` 回退；禁止新依赖 |
| InMemory / ad-hoc 循环 | **旁路（废弃扩面）** | 无会话日志；不得承载新产品能力 |
| LocalBot / 本机 Runtime 方案 | **不做** | 能力并入 MAF Runtime；见 not-taken 050 |
| Agent Coordinator 启发式旁路 | **非主路径** | 未接 MAF；后续要么接入要么删除 |

### 3.2 代码标识（现状 → 目标态）

| 现状标识 | 目标态（后续批次） | 本回合 |
|----------|-------------------|--------|
| `MafTaskLoopAgent` | 保留（MAF Runtime 本体） | 注释标明「MAF Runtime Agent」 |
| `ReactLoopAgent` | 删除或迁 `Legacy/` 后删 | 文档标废弃；禁止新调用 |
| `UseMafRuntime` / `TaskLoopMode` | 收敛为默认常开；开关最终删除 | 不改契约 |
| `executionMode: free \| plan` | 对外文案 Open/Planned；值可后改 `open\|planned` | UI 文案先改 |
| `AgentRun` / `AgentStepRun` | 架构称 **MAF Run**；实体可后改名 | 文档用 MAF Run；界面说「计划任务」 |
| 插件 Id `ai-agent` | **保留** | 2026-11-02 用户已批：展示名「智能体」，Id 不改 |
| 路由 `/ai-agent`、`api/ai-agent/*` | **不改**（不改为 `/maf`） | 2026-11-02 用户已批；本批禁止改路径 |

### 3.3 工具执行叙事（统一）

所有工具（无论开放任务还是计划任务）：

```
Permission（pre-execute / Guard）
  → Approval（Ask / ApprovalRequiredAIFunction）
    → Execute（IToolFunctionExtension）
      → Verify（结果校验 / 出口工具语义）
        → Result（finalize 冻结 + 日志/Run 轨迹）
```

真源实现：`ForgeSelf.Api/Services/ToolRegistry.ExecuteAsync`；MAF 侧经 `RegistryToolFunction` 进入。

## 4. 与既有事实对齐（仓库核查 2026-11-02）

| 事实 | 依据 |
|------|------|
| 默认引擎已是 MAF | `AIAgentService.BuildAgentOptions`：`TaskLoopMode=true` + `UseMafRuntime=true` → `MafTaskLoopAgent` |
| 工具进六闸门 | `RegistryToolFunction.InvokeCoreAsync` → `IToolRegistry.ExecuteAsync` |
| 开放任务不写 AgentRun | 会话 `SessionEvent` + 投影；见 021 快照 |
| 计划任务写 AgentRun | `RunOrchestratorService` + `AgentRunsController` |
| 审批协议缺口 | ADR-005：包内有 `ToolApprovalRequestContent`，本仓未接；`NoopApprovalService` |
| 取消杀进程缺口 | 终端超时不杀子进程（宿主） |

## 5. 插件简化铁律

1. **新本机能力 = 新/既有插件**，经 ToolRegistry 暴露，由 MAF 调用。
2. **内核 / MAF 禁止**吸收「浏览器自动化引擎、第二套调度状态机、平行 PermissionBroker」等整套执行栈（AgentHub PermissionBroker 仍只服务外部 CLI 委派）。
3. **Scheduler / Routines** 的目标态是 **触发 MAF Run**，不是直接 `Process.Start` 绕过审批。
4. 契约跨边界进 `ForgeSelf.Abstractions`；插件间禁止隐式耦合（architecture-design 技能铁律）。

## 6. 路线图归属（能力 → 层）

| 阶段 | 能力 | 主要落层 |
|------|------|----------|
| P0 | 审批接线、取消杀进程、执行上下文 | L2+L3 |
| P1 | 文件/终端加固、Skills | L4 + L3 策略 |
| P2 | Scheduler → MAF Run | L4 触发 → L2 |
| P3 | Browser 插件 | L4 |
| P4 | Memory / 本地模型 | L4 + L2 提示注入 |

## 7. 废弃说明（文档标记）

以下名称与旁路**不再作为产品叙事**，后续批次可删代码：

- 「FreeLoop / PlanDriven」作为对外主名（改称开放任务 / 计划任务）
- `ReactLoopAgent` 作为默认或推荐引擎
- 「另起 Local Agent Runtime / LocalBot」类设计
- 新建 `specs/*` 规格目录（已弃用，用 `docs/ai/pilot/`）

历史图 [`dsh-runtime-architecture.md`](dsh-runtime-architecture.md) 仍保留作 dsh 对齐史料；**新设计以本文为准**。
