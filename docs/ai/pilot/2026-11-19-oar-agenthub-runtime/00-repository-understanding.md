# Repository Understanding

> 阶段：Stage 0｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2  
> Task ID：`2026-11-19-oar-agenthub-runtime`｜日期：2026-11-19  
> 原则：条目均来自真实仓库内容。

## 项目结构

- 解决方案：`ForgeSelf.slnx`（Core / Abstractions / Api / Tests + 多插件）
- 宿主前端：`ForgeSelf.Web/`（Vue 3 + Vite + pnpm）
- 宿主后端：`ForgeSelf.Api/`（.NET 10 + SQLite + NewLife.XCode + 插件加载）
- 外部 Agent 中枢插件：`Plugins/AgentHub/`（id=`agent-hub`，`plugin.json` 版本 1.0.11）
- 契约层：`ForgeSelf.Abstractions/`
- 开发工件：`docs/ai/pilot/`；功能档案：`docs/02-features/`；架构：`docs/01-architecture/`

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 宿主前端 | Vue 3.5 + Vite + TS + Element Plus + pnpm | `ForgeSelf.Web/package.json`；AGENTS.md §2.1 |
| 宿主后端 | .NET 10 + XCode + SQLite | `ForgeSelf.Api/*.csproj`；AGENTS.md §2.1 |
| AgentHub 插件 | C# 插件 + 自带 `web/` | `Plugins/AgentHub/AgentHub.csproj`、`plugin.json` |
| 跨插件服务 | `IContext.Register/Get` | `docs/01-architecture/host-capability-seams.md`、`cordis-kernel.md` |
| OAR（拟引入） | `@botiverse/oar` TypeScript ESM，需 Node.js 24+ | 外部调研；本机当前 `node -v` = v22.23.4（云环境实测） |

## 架构特点（与本任务相关）

1. **外部 Agent 委派已落地**：`Plugins/AgentHub` 提供登记、CLI 探测、`CliTransport`、`DelegationRuntime`、任务/事件持久化、SSE、权限审批、工具 `hub_*`。
2. **跨插件薄接缝已落地**：`IAgentDelegation`（`ForgeSelf.Abstractions/AgentDelegationContracts.cs`），提供方 AgentHub，消费方 todo-tracker；**仅「入队 + 读回」**，不含 Session/Runtime 管理。
3. **`IAgentRuntime` 未落地**：`docs/01-architecture/cordis-kernel.md` §3.5 将其列为「未落地接缝」；`ForgeSelf.Abstractions` 无该类型（Grep 0 命中）。
4. **本机 Agent 循环 ≠ 外部 Runtime**：Abstractions `IAgent`/`IAgentRegistry`（`Agents.cs`）驱动 AIAgent/MAF 侧 turn；旧 `IAgentLoop` **已删除**（dsh B5/B9）。ADR-005 禁止另起 **LocalBot / 平行本机 Runtime**——与「外部 Coding Agent 经 OAR 适配」不是同一命题。
5. **Transport 现状**：接口注释与 `Model.xml` 声明 `Cli|Acp|Http`；实现仅 `CliTransport`；DI 单注册 `services.AddSingleton<IAgentTransport, CliTransport>()`（`AgentHubPlugin.cs`）。无 `OarTransport` / `RuntimeWorker` / 独立 Session 表。
6. **会话现状**：内存 `AgentSession` + `DelegationTask.SessionRef`；无 `AgentHubSession` 实体。

## 测试方式

| 层 | 入口 |
| --- | --- |
| 后端 | `dotnet build` / `dotnet test`（`ForgeSelf.Api.Tests` 等） |
| 宿主前端 | `cd ForgeSelf.Web && pnpm run check && pnpm run test` |
| 插件前端 | `cd Plugins/AgentHub/web && pnpm run build` |
| 插件 e2e | `ForgeSelf.Web/e2e/plugins/agent-hub/`（Playwright，技能 `e2e-testing`） |

## 构建命令

```bash
dotnet build ForgeSelf.slnx
cd Plugins/AgentHub/web && pnpm install && pnpm run build
cd ForgeSelf.Web && pnpm run check
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/AgentHub/Services/` | Registry / Transport / DelegationRuntime / PermissionBroker / Probe |
| `Plugins/AgentHub/Data/` | XCode `Model.xml` + `AgentDefinition`/`AgentAccessPoint`/`DelegationTask`/`DelegationEvent` |
| `Plugins/AgentHub/Tools/` | `hub_list_agents` / `hub_delegate_task` / `hub_task_result` / `hub_cancel_task` / `hub_ask_agent` |
| `ForgeSelf.Abstractions/` | 跨插件契约（含 `IAgentDelegation`，尚无 `IAgentRuntime`） |
| `docs/02-features/032-agent-hub.md` | AgentHub 功能真源（探测/设置；未含 OAR） |

## 代码组织方式

- AgentHub：Controller → Service → Transport → 进程/事件；跨插件经 `ctx.Register<IAgentDelegation>`。
- 命名碰撞：Abstractions `IAgentRegistry`（会话运行时）≠ AgentHub `Services.IAgentRegistry`（外部 Agent 目录）；插件内已 alias。

## 现有工程规范

- 开发流程唯一：`docs/04-standards/ai-native-engineering-workflow.md`（闸门1 后才允许业务代码 Implement）
- 架构变更：技能 `architecture-design`；契约必须进 Abstractions；消费者 `ctx.Get<T>()` 不缓存
- ADR-005 / not-taken 050：不另起本机 Local Agent Runtime（MAF 主路径）
- 插件任务 Spec 必含 Interaction Design；数据层继续 XCode，禁止另引 EF
- 安全：cwd 白名单、禁止默认无限权限、控制器 ApiKeyPolicy（agents/tasks 鉴权仍待核对）

## 适用问题类

查 `docs/11-troubleshooting/problem-classes/README.md`：本批为**架构决策/文档落盘**，非缺陷修复。相邻 lens：跨插件服务实例分叉（host-capability-seams §7 坑1 / §8 坑1）——Spec/Plan 必须要求 `IAgentRuntime` 与 MS DI **同实例**注册。未命中既有 PC 复发条目 → 写「无」。

## 候选低风险任务

| 候选 | 风险 | 说明 |
| --- | --- | --- |
| A. 仅写调研/ADR/架构真源 + pilot 00–04（本批） | 低 | 不改业务代码；为后续 P0–P4 定界 |
| B. 直接落地 Node Worker + `IAgentRuntime` | 高 | Node 依赖、DB Session 表、公共契约、Transport 选路 |
| C. 先改 UI Session 页 | 中高 | 无运行时则页面空转 |

## 选择该任务的原因

选 **A**：用户要求「梳理方案、确定最终实现、更新文档、产出工件」；符合闸门1 前先定契约与边界；避免在 Node 24+/OAR API 未验证前改 `DelegationRuntime`。
