# 仓库现状理解（Repository Understanding）

> 本文记录 OpenForgeSelf 向 DeepSeek Harness（dsh）架构对齐改造**启动前**的仓库真实状态。
> 状态只报事实，基于 `git` 实读与既有架构文档，不凭推测。数据快照：2026-09-28 改造前。

## 1. 技术栈与分层

- 后端：.NET 10 / ASP.NET Core 10 / NewLife.XCode（唯一 ORM）/ Serilog
- 前端：Vue 3.5 + TS 5 + Vite 5 + Pinia + Naive UI；插件前端为 vite lib 出树构建
- 分层契约：`ForgeSelf.Abstractions`（跨插件契约）/ `ForgeSelf.Core`（EventBus/Context）/ `ForgeSelf.Api`（宿主服务）/ `Plugins/*`（插件，ALC 热卸载）
- AI 能力主体：`Plugins/AIAgent`（核心 agent 运行）

## 2. 会话与消息现状（改造前痛点）

- 写路径：Controller 直写 `ChatMessage`/`ChatTurn` 主表（`SaveMessageAsync`），与事件日志**双写**、易漂移；不变量「模型可见历史必须能从日志逐字重建」无强制。
- 事件：`SessionEvent` 为裸 `Type`/`Payload` 字符串字典，无联合类型、无 `SessionEventMap` 注册表。
- 事件总线：`ForgeSelf.Core/EventBus` 不跨 `Context` 父子冒泡（027 待办 5，原「待修」升级为「阻塞项」）。
- 运行时：无统一 Turn/Step 状态机；`IAgentLoop`/`InMemoryAgentLoop` 为旧契约，无 `IAgent`/`IAgentRegistry`/`TurnFrame`；`StepRunLoopService` 自有 `while(true)+45s` 循环。
- 工具管线：决策仅二态（Allow/Deny），无单调守卫、无 `Ask` 落点、无 waterfall 修正、`tools/*` 用广播、无 `model-ordered commit`、无 spill。
- 收件箱：`IInbox` 仅内存态，三通道（followup/steer/inject）语义未真正生效；取消/超时无原因区分。

## 3. 配置与环境

- 数据根：`DataLocationService` 读 `GetFolderPath(UserProfile)`（Known Folder 派生），不读 `FORGESELF_DATA_ROOT` 环境变量；WAF Testing 宿主与静态解析两路径分叉 → 沙箱 testhost 写工作区外必红（约 50 条环境族）。
- 配置：`ConfigUnifier` 无逐类降级，任一配置类重定向失败会炸启动。
- spill：无阈值约束，`ToolCallsJson` 大结果直接落实体。

## 4. 测试现状（基线，改造前）

- Backend 988/988 + Core 12/12 + Abstractions 13/13（main 基线口径，实施时以本地最新为准）。
- 无 XCode 隔离铁律、无「受控复现 0 残留证据法」。

## 5. 既有文档

- `docs/01-architecture/dsh-alignment-plan.md`（本案总纲，已迁本目录 `02-spec.md`）
- `docs/01-architecture/dsh-alignment-tasks-b2-b8.md`（本案逐批分解，已迁 `04-task.md`）
- `docs/01-architecture/dsh-alignment-施工总览.md`（批次表，已迁 `03-plan.md`）
- `specs/040-session-event-sourcing` / `041-turn-step-runtime` / `042-tool-pipeline`（详细设计，**已被 `.gitignore` 忽略、不入库**，仅本地参考）
- `docs/02-features/021-ai-agent.md` 等 feature 文档为项目事实标准（见 `01-intent.md` 规则1）

## 6. 本次会话要解决的核心问题

「一切进模型的东西必须先落日志，再从日志派生（Model-visible means logged）」——将三层心智（Intent/Orchestration/Capability）映射为 MAF 的 Agent/Workflow/Tool，通过八批次（B1–B9）改造让会话日志成为唯一真相源，并补文档审计发现的所有滞后，最终收敛全部上下文到 `docs/ai/pilot/dsh-alignment-b2-b9/`。
