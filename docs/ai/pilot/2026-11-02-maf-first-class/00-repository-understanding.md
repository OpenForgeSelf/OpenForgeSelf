# Repository Understanding

> 阶段：Stage 0｜Task：`2026-11-02-maf-first-class`｜日期：2026-11-02
> 原则：条目来自真实仓库内容（CodeGraph + 文档只读核查）。

## 项目结构

- 解决方案：`ForgeSelf.slnx`；宿主 `ForgeSelf.Api/`；前端 `ForgeSelf.Web/`；插件 `Plugins/*`；契约 `ForgeSelf.Abstractions/`；内核 `ForgeSelf.Core/`；测试 `ForgeSelf.Api.Tests/`
- 文档：`docs/00-vision/`（愿景）· `docs/01-architecture/` · `docs/02-features/` · `docs/07-decisions/` · `docs/ai/pilot/`
- AI 执行插件：`Plugins/AIAgent/`（含 `MafLoop/`、`Services/ReactLoopAgent.cs`、`web/`）

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 前端 | Vue 3.5 + Vite + TS + Element Plus + pnpm | `ForgeSelf.Web/package.json` |
| 后端 | .NET 10 + SQLite + XCode | `ForgeSelf.Api/*.csproj` |
| Agent Runtime | Microsoft Agents / Extensions.AI + 本仓 `MafTaskLoopAgent` | `Plugins/AIAgent/MafLoop/`；`plugin.json` v1.9.7+ 描述 |
| 插件内核 | Cordis 风格 IContext/EventBus | `docs/01-architecture/cordis-kernel.md`；ADR-001 |

## 架构特点

- 默认聊天循环：`BuildAgentOptions` → `TaskLoopMode=true` + `UseMafRuntime=true` → `MafTaskLoopAgent`（`AIAgentService.cs`）
- 工具：`RegistryToolFunction` → `ToolRegistry.ExecuteAsync` 六闸门（含 Approval 钩子，默认 Noop）
- 双入口：开放任务 `POST /api/ai-agent/chat`；计划任务 `POST /api/ai-agent/runs` + `AgentRun`
- 插件简化既有方向：能力经 ToolRegistry；ADR-002/004 工具网关与命名空间

## 测试方式

- 后端：`dotnet test`（`ForgeSelf.Api.Tests`）；MAF 相关如 `MafTaskLoopAgentTests`
- 前端：`cd ForgeSelf.Web && pnpm run check && pnpm run test`
- 插件 e2e：`e2e/plugins/ai-agent/`（技能 `e2e-testing`）

## 构建命令

```bash
dotnet build
cd ForgeSelf.Web && pnpm run check
cd Plugins/AIAgent/web && pnpm run build
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/AIAgent/MafLoop/` | MAF Runtime（`MafTaskLoopAgent`、`RegistryToolFunction`） |
| `ForgeSelf.Api/Services/ToolRegistry.cs` | 执行安全内核六闸门 |
| `docs/00-vision/` | 产品愿景/目标/原则 |
| `docs/07-decisions/` | ADR |

## 现有工程规范

- 开发唯一流程：`docs/04-standards/ai-native-engineering-workflow.md` + `docs/ai/pilot/`
- 架构变更走 `architecture-design` 技能；禁止平行 Runtime（ADR-005/006）
- 闸门1 未批禁止大规模业务代码

## 候选低风险任务（本回合）

- 更新 vision / goals / principles / direction
- 新架构文 `maf-centric-architecture.md` + ADR-006
- 登记 `docs/README.md` §一
- 对外文案：plugin 展示名、features.ts、AgentEditDialog 模式名、页面标题
- 开本 pilot 00–04；**禁止**重写 MafLoop / Browser / DB 迁移

## 选择该任务的原因

用户明确要求先定产品与架构真源，再落地可验证骨架；符合绿场命名、MAF 一等、插件简化，且风险可控（文档 + 展示层）。
